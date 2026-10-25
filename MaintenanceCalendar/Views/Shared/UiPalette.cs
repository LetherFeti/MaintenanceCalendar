namespace MaintenanceCalendar;

/// <summary>保存済みのテーマ色・背景色から表示専用の配色を作ります。</summary>
internal static class UiPalette
{
    /// <summary>背景の明暗に合わせたカード・文字・テーマ上の文字色を共有リソースへ反映します。</summary>
    public static void Apply(string theme, string background)
    {
        var accent = Settings.Brush(theme).Color;
        var canvas = Settings.Brush(background).Color;
        bool dark = Brightness(canvas) < 0.38;
        var resources = Application.Current.Resources;
        resources["TextBrush"] = Settings.Brush(dark ? "#EDF0F7" : "#293245");
        resources["MutedBrush"] = Settings.Brush(dark ? "#B6BDCD" : "#687286");
        resources["AccentTextBrush"] = OnColor(accent);
        resources["SurfaceBrush"] = new SolidColorBrush(Blend(canvas, dark ? Color.FromRgb(70, 76, 96) : Colors.White, dark ? .42 : .78));
        resources["LineBrush"] = new SolidColorBrush(dark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(22, 41, 50, 69));
        resources["AccentSoftBrush"] = new SolidColorBrush(Blend(canvas, accent, dark ? .30 : .12));
    }

    /// <summary>背景色に対して白または濃色の文字を選びます。</summary>
    internal static SolidColorBrush OnColor(Color color) => Settings.Brush(Brightness(color) > 0.35 ? "#172032" : "#FFFFFF");

    /// <summary>sRGBの相対輝度を求め、テーマ背景上の文字色を選びます。</summary>
    private static double Brightness(Color c)
    {
        double Linear(byte value) { double v = value / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
    }

    /// <summary>二つの色を混ぜてカードや薄いテーマ背景を生成します。</summary>
    private static Color Blend(Color a, Color b, double amount) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount));
}
