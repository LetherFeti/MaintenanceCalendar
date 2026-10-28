using Microsoft.Win32;

namespace MaintenanceCalendar;

public sealed class SettingsWindow : ToolWindow
{
    private readonly Dictionary<string, TextBox> colors = [];
    private readonly ComboBox shape = new() { ItemsSource = new[] { "●", "■", "▼" } };
    private readonly ComboBox missingRecurrenceDate = new() { DisplayMemberPath = "Value", SelectedValuePath = "Key",
        ItemsSource = new Dictionary<string, string> { ["LastDay"] = "月末日にする", ["Skip"] = "その回は作らない" } };
    private readonly CheckBox showInTaskbar = new() { Content = "Alt＋Tab に表示する" };
    private readonly ComboBox sidebarPosition = new() { DisplayMemberPath = "Value", SelectedValuePath = "Key",
        ItemsSource = new Dictionary<string, string> { ["Left"] = "左", ["Right"] = "右", ["Top"] = "上", ["Bottom"] = "下" } };
    private readonly TextBlock holidayStatus = Ui.Text("");
    private readonly TextBlock startupStatus = Ui.Text("");
    private readonly TextBlock operationStatus = Ui.Text("");
    private bool busy;
    private bool closingAfterImport;
    /// <summary>
    /// 現在の設定値・祝日取得状況・データ管理・スタートアップの操作画面を構成します。
    /// </summary>
    public SettingsWindow() : base("設定", 590, 730)
    {
        var panel = new StackPanel(); Root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        panel.Children.Add(Ui.Text("見た目とデータ管理", 21));

        panel.Children.Add(Ui.Text("色は #RRGGBB 形式で入力してください。曜日・祝日の文字色は固定です。"));

        // 色設定の入力欄を登録し、ラベルとともに設定画面へ追加します。
        void ColorField(string key, string label, string value)
        {
            var text = new TextBox { Text = value, MaxLength = 7 }; colors[key] = text; Ui.Field(panel, label, text);
        }
        var settings = Services.Settings;

        ColorField("Theme", "テーマカラー", settings.ThemeColor); ColorField("Background", "背景色", settings.BackgroundColor);

        ColorField("Today", "今日", settings.TodayColor); ColorField("Use", "使用", settings.UseColor); ColorField("Care", "お手入れ", settings.CareColor);

        shape.SelectedItem = settings.TodayShape; Ui.Field(panel, "今日の図形", shape);
        sidebarPosition.SelectedValue = settings.SidebarPosition;
        Ui.Field(panel, "サイドバーの表示位置", sidebarPosition);
        showInTaskbar.IsChecked = settings.ShowInTaskbar;
        Ui.Field(panel, "ウィンドウ", showInTaskbar);
        missingRecurrenceDate.SelectedValue = settings.MissingRecurrenceDateBehavior;
        panel.Children.Add(Ui.Text("毎月・毎年の予定で基準日がない場合", 14));
        Ui.Field(panel, "例：毎月31日、2月29日", missingRecurrenceDate);

        Actions.Children.Add(Ui.Button("SaveButton", "設定を保存", Button_Click));
        Actions.Children.Add(Ui.Button("HolidayButton", "祝日取得", Button_Click));
        Actions.Children.Add(Ui.Button("CloseButton", "閉じる", Button_Click));
        Actions.Children.Add(holidayStatus);

        panel.Children.Add(Ui.Text("バックアップ / インポート", 18));

        panel.Children.Add(Ui.Text("DB・写真・設定をZIP保存します。停止中にZIPのDataフォルダをアプリフォルダへ展開しても復元できます。インポート直前のデータは別フォルダへ自動退避します。"));

        var backup = new WrapPanel(); backup.Children.Add(Ui.Button("BackupButton", "バックアップ", Button_Click)); backup.Children.Add(Ui.Button("ImportButton", "インポート", Button_Click)); panel.Children.Add(backup);

        panel.Children.Add(Ui.Text("スタートアップ", 18));

        var startup = new WrapPanel(); startup.Children.Add(Ui.Button("StartupOnButton", "登録", Button_Click)); startup.Children.Add(Ui.Button("StartupOffButton", "解除", Button_Click)); panel.Children.Add(startup); panel.Children.Add(startupStatus);

        panel.Children.Add(Ui.Text("保存先：" + Services.Data)); panel.Children.Add(operationStatus);

        AddDirtyTracking(); UpdateStatus();

        Closing += (_, e) =>
        {
            if (busy) { e.Cancel = true; return; }
            if (!closingAfterImport && HasUnsavedChanges() && !Ui.Confirm("保存されていない設定があります。保存せずに終了しますか？")) e.Cancel = true;
        };
    }

    private void AddDirtyTracking()
    {
        foreach (var field in colors.Values) field.TextChanged += (_, _) => UpdateDirtyStatus();
        shape.SelectionChanged += (_, _) => UpdateDirtyStatus();
        sidebarPosition.SelectionChanged += (_, _) => UpdateDirtyStatus();
        missingRecurrenceDate.SelectionChanged += (_, _) => UpdateDirtyStatus();
        showInTaskbar.Checked += (_, _) => UpdateDirtyStatus();
        showInTaskbar.Unchecked += (_, _) => UpdateDirtyStatus();
    }

    private bool HasUnsavedChanges()
    {
        var current = Services.Settings;
        return colors["Theme"].Text.Trim() != current.ThemeColor || colors["Background"].Text.Trim() != current.BackgroundColor
            || colors["Today"].Text.Trim() != current.TodayColor || colors["Use"].Text.Trim() != current.UseColor
            || colors["Care"].Text.Trim() != current.CareColor || (string?)shape.SelectedItem != current.TodayShape
            || (string?)sidebarPosition.SelectedValue != current.SidebarPosition || showInTaskbar.IsChecked != current.ShowInTaskbar
            || (string?)missingRecurrenceDate.SelectedValue != current.MissingRecurrenceDateBehavior;
    }

    private void UpdateDirtyStatus() => operationStatus.Text = HasUnsavedChanges() ? "未保存の設定があります。" : string.Empty;
    /// <summary>
    /// 保存済みの祝日取得日時・件数とスタートアップ登録状態を表示します。
    /// </summary>
    private void UpdateStatus()
    {
        holidayStatus.Text = "最終取得：" + (Services.Settings.HolidayFetchedAt?.ToString("yyyy/MM/dd HH:mm") ?? "未取得") + $" / {Services.Db.Holidays().Count}件";

        startupStatus.Text = StartupService.Registered ? "登録済み（ユーザーのスタートアップフォルダ）" : "未登録";
    }
    /// <summary>
    /// 設定保存・祝日取得・ZIP保存と復元・スタートアップ変更を実行し、処理中の操作を制御します。
    /// </summary>
    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;

        string name = ((Button)sender).Name;

        try
        {
            switch (name)
            {
                case "SaveButton":
                    var s = new Settings { ThemeColor = colors["Theme"].Text.Trim(), BackgroundColor = colors["Background"].Text.Trim(), TodayColor = colors["Today"].Text.Trim(), UseColor = colors["Use"].Text.Trim(), CareColor = colors["Care"].Text.Trim(), TodayShape = (string)shape.SelectedItem,
                        SidebarPosition = (string)sidebarPosition.SelectedValue, SidebarShowLabels = Services.Settings.SidebarShowLabels,
                        ShowInTaskbar = showInTaskbar.IsChecked == true,
                        MissingRecurrenceDateBehavior = (string)missingRecurrenceDate.SelectedValue,
                        WindowLeft = Services.Settings.WindowLeft, WindowTop = Services.Settings.WindowTop,
                        WindowWidth = Services.Settings.WindowWidth, WindowHeight = Services.Settings.WindowHeight,
                        HolidayFetchedAt = Services.Settings.HolidayFetchedAt };
                    s.Save(Services.SettingsPath); Services.Settings = s; s.Apply();
                    foreach (Window window in Application.Current.Windows)
                        if (window is MainWindow main) main.ApplyTaskbarSetting();
                    operationStatus.Text = "設定を保存しました。";
                    LogManager.GetCurrentClassLogger().Info("Appearance settings saved"); break;
                case "HolidayButton":
                    busy = true; Root.IsEnabled = false; operationStatus.Text = "内閣府から祝日を取得しています…";
                    var holidays = await HolidayService.Fetch(); Services.Db.ReplaceHolidays(holidays);
                    Services.Settings.HolidayFetchedAt = DateTime.Now; Services.Settings.Save(Services.SettingsPath);
                    operationStatus.Text = $"祝日を{holidays.Count}件保存しました。"; UpdateStatus(); break;
                case "BackupButton":
                    var save = new SaveFileDialog { Filter = "ZIPバックアップ|*.zip", FileName = "MaintenanceCalendar-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip" };
                    if (save.ShowDialog(this) == true)
                    {
                        busy = true; Root.IsEnabled = false; operationStatus.Text = "バックアップを作成しています…";
                        await System.Threading.Tasks.Task.Run(() => new BackupService(Services).Export(save.FileName));
                        operationStatus.Text = "バックアップを保存しました。";
                    }
                    break;
                case "ImportButton":
                    var open = new OpenFileDialog { Filter = "ZIPバックアップ|*.zip" };
                    if (open.ShowDialog(this) == true && Ui.Confirm("選択したバックアップからDB・写真・設定を復元しますか？\n現在のデータは自動退避されます。"))
                    {
                        busy = true; Root.IsEnabled = false; operationStatus.Text = "バックアップを検証・復元しています…";
                        await System.Threading.Tasks.Task.Run(() => new BackupService(Services).Import(open.FileName));
                        Services.Settings.Apply(); MessageBox.Show("復元しました。設定画面を閉じて再読込します。");
                        busy = false; closingAfterImport = true; Close();
                    }
                    break;
                case "StartupOnButton": StartupService.Set(true); UpdateStatus(); break;
                case "StartupOffButton": StartupService.Set(false); UpdateStatus(); break;
                case "CloseButton": Close(); break;
            }
        }
        catch (Exception ex) { operationStatus.Text = "処理に失敗しました。"; Ui.Error(ex); }
        finally { busy = false; Root.IsEnabled = true; }
    }
}
