using System.Windows.Automation;

namespace MaintenanceCalendar;

public sealed class DashboardWindow : ToolWindow
{
    /// <summary>従来と同じ有効品・使用回数・購入日・期限の集計をカードと円グラフで表示します。</summary>
    public DashboardWindow() : base("ダッシュボード", 660, 510)
    {
        var view = new DashboardWindowView(); Root.Children.Add(view);
        var items = Services.Db.GetItems().ToDictionary(x => x.Id);
        var usage = Services.Db.UsageCounts();
        int due = Services.Db.Due(DateTime.Today).Count;
        var recentCare = Services.Db.RecentCare();
        var monthRecords = Services.Db.Month(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), null);
        var lastUse = Services.Db.LastUseDates();
        long total = usage.Sum(x => x.Count);
        var metrics = view.Metrics;
        metrics.Children.Add(Metric("登録中", $"{items.Count:N0}", "点"));
        metrics.Children.Add(Metric("使用記録", $"{total:N0}", "回"));
        metrics.Children.Add(Metric("期限到来", $"{due:N0}", "点"));
        var cards = view.Cards;
        var recentPanel = new StackPanel(); recentPanel.Children.Add(Ui.Text("最近のお手入れ", 16));
        if (recentCare.Count == 0) recentPanel.Children.Add(Muted("お手入れ記録がありません。"));
        foreach (var row in recentCare)
            recentPanel.Children.Add(Ui.Text($"{row.Date:yyyy/MM/dd}  {items[row.ItemId].DisplayName}", 13));
        var monthPanel = new StackPanel(); monthPanel.Children.Add(Ui.Text("今月の記録", 16));
        int monthUses = monthRecords.Count(x => x.Work == 1), monthCares = monthRecords.Count(x => x.Work == 2);
        var monthTotal = monthUses + monthCares;
        var monthNumber = Ui.Text($"{monthTotal:N0}", 30); monthNumber.FontWeight = FontWeights.SemiBold;
        monthNumber.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush"); monthPanel.Children.Add(monthNumber);
        monthPanel.Children.Add(Muted($"使用 {monthUses:N0}回  /  お手入れ {monthCares:N0}回"));
        monthPanel.Children.Add(Muted(monthTotal == 0 ? "今月の記録はありません。" : $"{DateTime.Today:yyyy年M月}の記録"));
        var idlePanel = new StackPanel(); idlePanel.Children.Add(Ui.Text("しばらく使っていない所持品", 16));
        var idleItems = items.Values.OrderBy(x => lastUse.TryGetValue(x.Id, out var date) ? date : DateTime.MinValue).ThenBy(x => x.DisplayName).Take(5).ToList();
        if (idleItems.Count == 0) idlePanel.Children.Add(Muted("所持品が登録されていません。"));
        foreach (var item in idleItems)
            idlePanel.Children.Add(Ui.Text(lastUse.TryGetValue(item.Id, out var date)
                ? $"{item.DisplayName}  /  最終使用 {date:yyyy/MM/dd}（{Math.Max(0, (DateTime.Today - date.Date).Days):N0}日前）"
                : $"{item.DisplayName}  /  使用記録なし", 13));
        if (items.Count > idleItems.Count) idlePanel.Children.Add(Muted($"ほか {items.Count - idleItems.Count}点"));
        var usagePanel = new StackPanel();
        usagePanel.Children.Add(Ui.Text("使用回数の内訳", 16));
        var slices = new List<(double Value, Brush Brush)>();
        var labels = new List<(Brush Brush, string Text)>();
        var color = Settings.Brush(Services.Settings.ThemeColor).Color;
        var care = Settings.Brush(Services.Settings.CareColor);
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
        foreach (var child in new[] { recentPanel, monthPanel, idlePanel, usagePanel, most, oldest }) cards.Children.Add(Card(child));
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
