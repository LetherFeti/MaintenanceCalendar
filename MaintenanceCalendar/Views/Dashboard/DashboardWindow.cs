using System.Windows.Automation;

namespace MaintenanceCalendar;

public sealed class DashboardWindow : ToolWindow
{
    /// <summary>従来と同じ有効品・使用回数・購入日・期限の集計をカードと円グラフで表示します。</summary>
    public DashboardWindow() : base("ダッシュボード", 660, 510)
    {
        var panel = new StackPanel();
        Root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        panel.Children.Add(Ui.Text("所持品のダッシュボード", 23));
        panel.Children.Add(Muted("お気に入りと過ごした日々を、ひと目で。削除済みの所持品は含みません。"));
        var items = Services.Db.GetItems().ToDictionary(x => x.Id);
        var usage = Services.Db.UsageCounts();
        int due = Services.Db.Due(DateTime.Today).Count;
        long total = usage.Sum(x => x.Count);
        var metrics = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 6, 0, 8) };
        metrics.Children.Add(Metric("登録中", $"{items.Count:N0}", "点"));
        metrics.Children.Add(Metric("使用記録", $"{total:N0}", "回"));
        metrics.Children.Add(Metric("期限到来", $"{due:N0}", "点"));
        panel.Children.Add(metrics);
        var cards = new Grid(); panel.Children.Add(cards);
        var duePanel = new StackPanel();
        duePanel.Children.Add(Ui.Text("お手入れのタイミング", 16));
        double rate = items.Count == 0 ? 0 : (double)due / items.Count;
        var theme = (Brush)FindResource("AccentBrush");
        var care = Settings.Brush(Services.Settings.CareColor);
        duePanel.Children.Add(Chart([(due, theme), (items.Count - due, care)], items.Count == 0 ? "—" : $"{rate:P0}", "期限到来", $"登録中{items.Count}点のうち期限到来{due}点"));
        duePanel.Children.Add(Legend(theme, $"期限到来  {due:N0}点"));
        duePanel.Children.Add(Legend(care, $"期限前  {items.Count - due:N0}点"));
        duePanel.Children.Add(Muted(items.Count == 0 ? "所持品を登録すると表示されます。" : due == 0 ? "今は期限を迎えた所持品はありません。" : "通知から対象の所持品を確認できます。"));
        var usagePanel = new StackPanel();
        usagePanel.Children.Add(Ui.Text("使用回数の内訳", 16));
        var slices = new List<(double Value, Brush Brush)>();
        var labels = new List<(Brush Brush, string Text)>();
        var color = Settings.Brush(Services.Settings.ThemeColor).Color;
        for (int n = 0; n < Math.Min(3, usage.Count); n++)
        {
            var brush = new SolidColorBrush(Color.FromRgb(
                (byte)(color.R + (210 - color.R) * n * .26),
                (byte)(color.G + (190 - color.G) * n * .26),
                (byte)(color.B + (235 - color.B) * n * .26)));
            var row = usage[n]; slices.Add((row.Count, brush));
            labels.Add((brush, $"{items[row.Id].DisplayName}  {row.Count:N0}回"));
        }
        long rest = usage.Skip(3).Sum(x => x.Count);
        if (rest > 0) { slices.Add((rest, care)); labels.Add((care, $"その他  {rest:N0}回")); }
        usagePanel.Children.Add(Chart(slices, total.ToString("N0"), "使用回数", $"使用記録合計{total}回"));
        if (total == 0) usagePanel.Children.Add(Muted("使用記録がありません。日付から記録してみましょう。"));
        foreach (var label in labels) usagePanel.Children.Add(Legend(label.Brush, label.Text));
        var most = new StackPanel(); most.Children.Add(Ui.Text("一番使用した所持品", 16));
        if (usage.Count == 0) most.Children.Add(Muted("使用記録がありません。"));
        else foreach (var row in usage.Where(x => x.Count == usage[0].Count))
            most.Children.Add(Ui.Text($"{items[row.Id].DisplayName}  /  {row.Count:N0}回", 14));
        var oldest = new StackPanel(); oldest.Children.Add(Ui.Text("一番古くから使っている所持品", 16));
        var purchased = items.Values.Where(x => x.PurchaseDate.HasValue).OrderBy(x => x.PurchaseDate).ToList();
        if (purchased.Count == 0) oldest.Children.Add(Muted("購入日が登録されていません。"));
        else foreach (var item in purchased.Where(x => x.PurchaseDate == purchased[0].PurchaseDate))
        {
            int days = (DateTime.Today - item.PurchaseDate!.Value.Date).Days;
            oldest.Children.Add(Ui.Text(item.DisplayName, 14));
            oldest.Children.Add(Muted($"購入日 {item.PurchaseDate:yyyy/MM/dd}  /  {(days >= 0 ? $"{days:N0}日経過" : $"購入予定まで{-days:N0}日")}"));
        }
        foreach (var child in new[] { duePanel, usagePanel, most, oldest }) cards.Children.Add(Card(child));
        // 狭い画面では縦一列へ並べ替え、固定幅による欠けを避けます。
        void ArrangeCards()
        {
            int columns = cards.ActualWidth >= 540 ? 2 : 1;
            if (cards.ColumnDefinitions.Count == columns) return;
            cards.ColumnDefinitions.Clear(); cards.RowDefinitions.Clear();
            for (int n = 0; n < columns; n++) cards.ColumnDefinitions.Add(new ColumnDefinition());
            for (int n = 0; n < (cards.Children.Count + columns - 1) / columns; n++) cards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int n = 0; n < cards.Children.Count; n++) { Grid.SetColumn(cards.Children[n], n % columns); Grid.SetRow(cards.Children[n], n / columns); }
        }
        cards.SizeChanged += (_, _) => ArrangeCards(); ArrangeCards();
    }
    /// <summary>テーマに追従するカード枠を作ります。</summary>
    private static Border Card(UIElement child)
    {
        var card = new Border { Child = child, CornerRadius = new CornerRadius(16), Padding = new Thickness(16), Margin = new Thickness(4), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush"); card.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); return card;
    }
    /// <summary>数値と単位を読みやすい順に並べます。</summary>
    private static Border Metric(string title, string value, string unit)
    {
        var panel = new StackPanel(); panel.Children.Add(Muted(title));
        var number = Ui.Text(value, 28); number.FontWeight = FontWeights.SemiBold;
        number.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); panel.Children.Add(number); panel.Children.Add(Muted(unit));
        var card = Card(panel); card.Padding = new Thickness(12, 8, 12, 8); return card;
    }
    /// <summary>説明文に共通の控えめな文字色を適用します。</summary>
    private static TextBlock Muted(string text)
    {
        var block = Ui.Text(text, 11); block.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); return block;
    }
    /// <summary>グラフと同じ色の凡例に、折り返し可能な品名と値を表示します。</summary>
    private static UIElement Legend(Brush brush, string text)
    {
        var row = new DockPanel();
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = brush, Margin = new Thickness(2, 8, 8, 0), VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(dot, Dock.Left); row.Children.Add(dot); row.Children.Add(Ui.Text(text, 11)); return row;
    }
    /// <summary>中央に実数と説明を添えた、拡縮可能な円グラフを生成します。</summary>
    private static UIElement Chart(IReadOnlyList<(double Value, Brush Brush)> segments, string value, string caption, string accessible)
    {
        var drawing = new Grid { Width = 180, Height = 180, Margin = new Thickness(0, 6, 0, 10) };
        drawing.Children.Add(new DonutChart { Segments = segments });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var number = Ui.Text(value, 30); number.HorizontalAlignment = HorizontalAlignment.Center; number.FontWeight = FontWeights.SemiBold;
        text.Children.Add(number); var label = Muted(caption); label.HorizontalAlignment = HorizontalAlignment.Center; text.Children.Add(label); drawing.Children.Add(text);
        AutomationProperties.SetName(drawing, accessible);
        return new Viewbox { Child = drawing, Stretch = Stretch.Uniform, MaxHeight = 140, HorizontalAlignment = HorizontalAlignment.Stretch };
    }
}
