using System.Text.Json;

namespace MaintenanceCalendar;

/// <summary>
/// 表示色・今日の図形・祝日取得日時の保存と画面への反映を担います。
/// </summary>
public sealed class Settings
{
    public string ThemeColor { get; set; } = "#795548";
    public string BackgroundColor { get; set; } = "#FFF8E7";
    public string TodayColor { get; set; } = "#B78A65";
    public string UseColor { get; set; } = "#C9E2CD";
    public string CareColor { get; set; } = "#B7D5ED";
    public string TodayShape { get; set; } = "●";
    public DateTime? HolidayFetchedAt { get; set; }
    public string SidebarPosition { get; set; } = "Left";
    public bool SidebarShowLabels { get; set; }
    public bool ShowInTaskbar { get; set; } = false;
    public string MissingRecurrenceDateBehavior { get; set; } = "LastDay";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }

    /// <summary>
    /// 設定JSONを読み込み、ファイルがない場合は既定設定を返します。
    /// </summary>
    public static Settings Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new InvalidDataException("設定が空です。") : new();

    /// <summary>
    /// 各色がRGB形式であり、今日の図形が対応する選択肢であることを検証します。
    /// </summary>
    public void Validate()
    {
        foreach (string value in new[] { ThemeColor, BackgroundColor, TodayColor, UseColor, CareColor })
        {
            if (value.Length != 7 || value[0] != '#' || !value.Skip(1).All(Uri.IsHexDigit))
                throw new InvalidDataException("色は #RRGGBB 形式で指定してください。");
        }
        if (!new[] { "●", "■", "▼" }.Contains(TodayShape)) throw new InvalidDataException("今日の図形が不正です。");
        if (!new[] { "Left", "Right", "Top", "Bottom" }.Contains(SidebarPosition)) throw new InvalidDataException("サイドバーの位置が不正です。");
        if (!new[] { "LastDay", "Skip" }.Contains(MissingRecurrenceDateBehavior)) throw new InvalidDataException("定期予定の基準日処理が不正です。");
        foreach (var value in new[] { WindowLeft, WindowTop, WindowWidth, WindowHeight })
            if (value.HasValue && !double.IsFinite(value.Value)) throw new InvalidDataException("ウィンドウ位置が不正です。");
    }

    /// <summary>
    /// 設定を検証し、一時ファイルからの置換でJSONを保存します。
    /// </summary>
    public void Save(string path)
    {
        Validate();
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }

    /// <summary>
    /// 設定を検証してテーマ色と背景色の共有リソースを更新します。
    /// </summary>
    public void Apply()
    {
        Validate();
        Application.Current.Resources["AccentBrush"] = Brush(ThemeColor);
        Application.Current.Resources["CreamBrush"] = Brush(BackgroundColor);
        UiPalette.Apply(ThemeColor, BackgroundColor);
    }

    /// <summary>
    /// 色文字列をWPFの単色ブラシに変換します。
    /// </summary>
    public static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));

}
