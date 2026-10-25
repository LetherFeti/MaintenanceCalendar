namespace MaintenanceCalendar;

public sealed class HistoryWindow : ToolWindow
{
    private readonly ComboBox combo;
    private readonly TextBlock last = Ui.Text("");
    private readonly TextBlock pageText = Ui.Text("");
    private readonly TextBox memo = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly DataGrid grid = Ui.Grid(("日付", "Date"), ("作業", "WorkLabel"), ("お手入れメモ", "Memo"));
    private int page;
    private const int PageSize = 200;
    private readonly Button previous, next;

    /// <summary>
    /// 所持品の選択欄・履歴一覧・メモ表示・ページ移動を構成します。
    /// </summary>
    public HistoryWindow(long? selected = null) : base("使用・手入れ履歴", 820, 560)
    {
        AddTop(Ui.Text("使用・手入れ履歴", 21));

        var items = Services.Db.GetItems(true);

        combo = Ui.ItemCombo(items, selected);

        // Deleted items remain readable without reappearing in the main calendar.
        foreach (var item in items.Where(x => x.IsDeleted)) item.Model += "（削除済み）";

        AddTop(combo); AddTop(last); AddTop(pageText);

        DockPanel.SetDock(memo, Dock.Bottom); Root.Children.Add(memo); Root.Children.Add(grid);

        ((DataGridTextColumn)grid.Columns[0]).Binding.StringFormat = "yyyy/MM/dd";

        previous = Ui.Button("PreviousButton", "前へ", Button_Click); Actions.Children.Add(previous);

        next = Ui.Button("NextButton", "次へ", Button_Click); Actions.Children.Add(next);

        grid.SelectionChanged += (_, _) => memo.Text = (grid.SelectedItem as HistoryRow)?.Memo ?? "";

        combo.SelectionChanged += (_, _) => Ui.Run(() => { page = 0; Refresh(); }); Refresh();
    }
    /// <summary>
    /// 選択品の最終使用・手入れ日と現在ページの記録を取得し、ページ操作の可否を更新します。
    /// </summary>
    private void Refresh()
    {
        if (combo.SelectedItem is not Item { Id: > 0 } item) { last.Text = "所持品を選択してください。"; grid.ItemsSource = null; previous.IsEnabled = next.IsEnabled = false; return; }

        // 日付を表示形式へ変換し、値がなければ未登録の文言を返します。
        string TextDate(DateTime? d) => d?.ToString("yyyy/MM/dd") ?? "未登録";

        last.Text = $"最終使用：{TextDate(Services.Db.Last(item.Id, 1))}    最終手入れ：{TextDate(Services.Db.Last(item.Id, 2))}";

        var records = Services.Db.HistoryPage(item.Id, page * PageSize, PageSize + 1);

        next.IsEnabled = records.Count > PageSize; previous.IsEnabled = page > 0;

        grid.ItemsSource = records.Take(PageSize).ToList(); pageText.Text = $"{page + 1}ページ（1ページ{PageSize}件・日付降順）";
    }
    /// <summary>
    /// 前後のページへ移動し、選択品の履歴を読み直します。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        switch (((Button)sender).Name) { case "PreviousButton": page = Math.Max(0, page - 1); break; case "NextButton": page++; break; }
        Refresh();
    });
}

