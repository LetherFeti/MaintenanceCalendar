using System.ComponentModel;
using System.Windows.Data;

namespace MaintenanceCalendar;

public sealed class DayWindow : ToolWindow
{
    private readonly DateTime date;
    private readonly DataGrid grid = Ui.Grid(("メーカー", "Manufacturer"), ("モデル", "Model"), ("色", "Color"), ("使用", "UseStatus"), ("お手入れ", "CareStatus"));
    private readonly List<DayItem> rows;
    private readonly ICollectionView visibleRows;
    private readonly TextBox search = new() { MinWidth = 220 };
    private readonly CheckBox showUsed = new() { Content = "使用済を表示", IsChecked = true };
    private readonly CheckBox showCared = new() { Content = "お手入れ済みを表示", IsChecked = false };
    private readonly Button use, care, cancelCare, batchUse, batchCare;
    private readonly TextBlock status = Ui.Text("");
    private readonly TextBlock batchStatus = Ui.Text("");
    private readonly TextBox memo = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 85 };
    private List<HistoryRow> records = [];

    /// <summary>当日の登録状況と一括登録の対象を一覧表示します。</summary>
    public DayWindow(DateTime date, IReadOnlyList<Item> items, long? selected) : base(date.ToString("yyyy年M月d日（ddd）"), 740, 610)
    {
        this.date = date;
        MinWidth = 660; MinHeight = 800;
        rows = items.Select(x => new DayItem(x)).ToList();
        visibleRows = CollectionViewSource.GetDefaultView(rows);
        visibleRows.Filter = MatchesFilter;
        AddTop(Ui.Text("使用・お手入れを記録", 17));

        AddTop(Ui.Text("メーカー・モデル・色で検索"));
        var filters = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        filters.Children.Add(search); filters.Children.Add(showUsed); filters.Children.Add(showCared);
        AddTop(filters);

        var batchActions = new WrapPanel();
        batchActions.Children.Add(Ui.Button("CheckAllButton", "全チェック / 解除", Button_Click));
        batchUse = Ui.Button("BatchUseButton", "チェック分を使用登録", Button_Click); batchActions.Children.Add(batchUse);
        batchCare = Ui.Button("BatchCareButton", "チェック分をお手入れ登録", Button_Click); batchActions.Children.Add(batchCare);
        AddTop(batchActions); AddTop(batchStatus);


        search.TextChanged += (_, _) => RefreshFilter();
        showUsed.Checked += (_, _) => RefreshFilter(); showUsed.Unchecked += (_, _) => RefreshFilter();
        showCared.Checked += (_, _) => RefreshFilter(); showCared.Unchecked += (_, _) => RefreshFilter();

        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "一括登録の対象");
        check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(DayItem.IsChecked)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        grid.Columns.Insert(0, new DataGridTemplateColumn { Header = "対象", CellTemplate = new DataTemplate { VisualTree = check }, Width = 54 });
        grid.Columns[4].Width = 100; grid.Columns[5].Width = 120;
        grid.ItemsSource = visibleRows;
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "写真"));
        rowStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
        rowStyle.Setters.Add(new EventSetter(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(Row_ToolTipOpening)));
        grid.RowStyle = rowStyle;
        foreach (var row in rows) row.PropertyChanged += (_, _) => UpdateBatchStatus();

        use = Ui.Button("UseButton", "使用", Button_Click); Actions.Children.Add(use);
        care = Ui.Button("CareButton", "お手入れ", Button_Click); Actions.Children.Add(care);
        cancelCare = Ui.Button("CancelCareButton", "お手入れ取消", Button_Click); Actions.Children.Add(cancelCare);
        var detail = new StackPanel(); detail.Children.Add(status); detail.Children.Add(Ui.Text("選択行のお手入れメモ")); detail.Children.Add(memo);
        DockPanel.SetDock(detail, Dock.Bottom); Root.Children.Add(detail);
        Root.Children.Add(grid);
        grid.SelectionChanged += (_, _) => UpdateSelection();
        grid.SelectedItem = rows.FirstOrDefault(x => x.Id == selected);
        Refresh();
    }

    private bool MatchesFilter(object value)
    {
        if (value is not DayItem row) return false;
        if (showUsed.IsChecked != true && row.Used) return false;
        if (showCared.IsChecked != true && row.Cared) return false;
        string query = search.Text.Trim();
        return query.Length == 0 || row.Manufacturer.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || row.Model.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || row.Color.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshFilter()
    {
        visibleRows.Refresh();
        UpdateSelection();
        UpdateBatchStatus();
    }

    private void Row_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        try
        {
            if (sender is not DataGridRow gridRow || gridRow.Item is not DayItem item || string.IsNullOrWhiteSpace(item.Item.Photo))
            { e.Handled = true; return; }
            var image = Services.LoadPhoto(item.Item.Photo);
            if (image is null) { e.Handled = true; return; }
            gridRow.ToolTip = new Image { Source = image, MaxWidth = 240, MaxHeight = 180, Stretch = Stretch.Uniform };
        }
        catch (Exception ex) { LogManager.GetCurrentClassLogger().Warn(ex, "Item photo preview failed"); e.Handled = true; }
    }

    /// <summary>記録を読み直し、チェック状態を保って全行の登録状況を更新します。</summary>
    private void Refresh()
    {
        records = Services.Db.Day(date);
        var registered = records.Select(x => (x.ItemId, x.Work)).ToHashSet();
        foreach (var row in rows)
        {
            row.Used = registered.Contains((row.Id, 1));
            row.Cared = registered.Contains((row.Id, 2));
        }
        visibleRows.Refresh(); grid.Items.Refresh(); UpdateSelection(); UpdateBatchStatus();
    }

    private void UpdateBatchStatus()
    {
        var targets = visibleRows.Cast<DayItem>().Where(x => x.IsChecked).ToList();
        int uses = targets.Count(x => !x.Used), cares = targets.Count(x => !x.Cared);
        batchUse.IsEnabled = uses > 0; batchCare.IsEnabled = cares > 0;
        batchStatus.Text = $"チェック：{targets.Count}件　未登録：使用 {uses}件 ／ お手入れ {cares}件";
    }

    private void UpdateSelection()
    {
        var row = grid.SelectedItem as DayItem;
        var entry = records.FirstOrDefault(x => x.ItemId == row?.Id && x.Work == 2);
        use.IsEnabled = care.IsEnabled = row is not null;
        use.Content = row?.Used == true ? "使用取消" : "使用";
        care.Content = entry is null ? "お手入れ" : "手入れメモ編集";
        cancelCare.IsEnabled = entry is not null; memo.Text = entry?.Memo ?? "";
        status.Text = row is null ? "個別操作：一覧から行を選択してください。" : $"個別操作：{row.Item.DisplayName}";
    }

    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        string name = ((Button)sender).Name;
        if (name == "CheckAllButton")
        {
            var visible = visibleRows.Cast<DayItem>().ToList();
            bool check = visible.Any(x => !x.IsChecked);
            foreach (var row in visible) row.IsChecked = check;
            grid.Items.Refresh(); return;
        }
        if (name is "BatchUseButton" or "BatchCareButton")
        {
            int work = name == "BatchUseButton" ? 1 : 2;
            var targets = visibleRows.Cast<DayItem>().Where(x => x.IsChecked && (work == 1 ? !x.Used : !x.Cared)).Select(x => x.Id).ToArray();
            if (targets.Length == 0) return;
            string commonMemo = "";
            if (work == 2)
            {
                var editor = new CareMemoWindow("") { Owner = this, Title = $"未登録の{targets.Length}件に共通のお手入れメモ" };
                if (editor.ShowDialog() != true) return;
                commonMemo = editor.Memo;
            }
            Services.Db.SaveMissingHistory(date, targets, work, commonMemo);
            Refresh(); return;
        }
        if (grid.SelectedItem is not DayItem selected) return;
        switch (name)
        {
            case "UseButton":
                if (selected.Used) Services.Db.DeleteHistory(date, selected.Id, 1);
                else Services.Db.SaveHistory(date, selected.Id, 1);
                break;
            case "CareButton":
                var editor = new CareMemoWindow(records.FirstOrDefault(x => x.ItemId == selected.Id && x.Work == 2)?.Memo ?? "") { Owner = this };
                if (editor.ShowDialog() == true) Services.Db.SaveHistory(date, selected.Id, 2, editor.Memo);
                break;
            case "CancelCareButton":
                if (Ui.Confirm("この日のお手入れ記録とメモを取り消しますか？")) Services.Db.DeleteHistory(date, selected.Id, 2);
                break;
        }
        Refresh();
    });

    private sealed class DayItem(Item item) : INotifyPropertyChanged
    {
        public Item Item { get; } = item;
        public long Id => Item.Id;
        public string Manufacturer => Item.Manufacturer;
        public string Model => Item.Model;
        public string Color => Item.Color;
        public bool Used { get; set; }
        public bool Cared { get; set; }
        public string UseStatus => Used ? "✓ 登録済" : "未登録";
        public string CareStatus => Cared ? "✓ 登録済" : "未登録";
        private bool isChecked;
        public bool IsChecked
        {
            get => isChecked;
            set { if (isChecked == value) return; isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
