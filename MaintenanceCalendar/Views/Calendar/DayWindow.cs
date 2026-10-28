using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
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
    private readonly TextBox memo = new() { IsReadOnly = true, IsTabStop = false, Focusable = false, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 85 };
    private List<HistoryRow> records = [];
    private readonly ListBox eventList = new() { MinHeight = 90 };
    private readonly TextBox eventTitle = new() { MinWidth = 260 };
    private readonly TextBox eventStart = new() { Width = 100, Text = "09:00" };
    private readonly TextBox eventEnd = new() { Width = 100, Text = "10:00" };
    private readonly TextBox eventLocation = new() { MinWidth = 220 };
    private readonly TextBox eventDescription = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 65, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly CheckBox notifyPrevious = new() { Content = "前日", IsChecked = true };
    private readonly CheckBox notify15 = new() { Content = "15分前", IsChecked = true };
    private readonly CheckBox notify5 = new() { Content = "5分前", IsChecked = true };
    private readonly DatePicker eventDate = new();
    private readonly ComboBox recurrence = new() { DisplayMemberPath = "Value", SelectedValuePath = "Key", ItemsSource = new Dictionary<string, string> { ["None"] = "繰り返しなし", ["Weekly"] = "毎週", ["Biweekly"] = "隔週", ["Monthly"] = "毎月", ["Yearly"] = "毎年" } };
    private readonly DatePicker recurrenceEnd = new();
    private readonly ComboBox eventScope = new() { DisplayMemberPath = "Value", SelectedValuePath = "Key", ItemsSource = new Dictionary<string, string> { ["Occurrence"] = "この回だけ", ["Series"] = "シリーズ全体" }, Visibility = Visibility.Collapsed };
    private FrameworkElement? recurrenceEndField, eventScopeField;
    private readonly TextBlock eventFormHeading = Ui.Text("新しい予定", 16);
    private Button? saveEventButton;
    private Button? deleteEventButton;
    private TabControl? tabs;
    private CalendarEvent? selectedEvent;

    /// <summary>当日の登録状況と一括登録の対象を一覧表示します。</summary>
    public DayWindow(DateTime date, IReadOnlyList<Item> items, long? selected) : base(date.ToString("yyyy年M月d日（ddd）"), 740, 610)
    {
        this.date = date;
        MinWidth = 660; MinHeight = 800;
        Cursor = Cursors.Arrow;
        search.Cursor = eventTitle.Cursor = eventStart.Cursor = eventEnd.Cursor = eventLocation.Cursor = eventDescription.Cursor = Cursors.IBeam;
        memo.Cursor = Cursors.Arrow;
        eventDate.Cursor = notifyPrevious.Cursor = notify15.Cursor = notify5.Cursor = Cursors.Arrow;
        rows = items.Select(x => new DayItem(x)).ToList();
        visibleRows = CollectionViewSource.GetDefaultView(rows);
        visibleRows.Filter = MatchesFilter;
        AddTop(Ui.Text("使用・お手入れを記録", 17));

        var filters = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        filters.Children.Add(search); filters.Children.Add(showUsed); filters.Children.Add(showCared);
        showUsed.Cursor = showCared.Cursor = Cursors.Arrow;

        var batchActions = new WrapPanel();
        var checkAll = Ui.Button("CheckAllButton", "全チェック / 解除", Button_Click); checkAll.Cursor = Cursors.Arrow; batchActions.Children.Add(checkAll);
        batchUse = Ui.Button("BatchUseButton", "チェック分を使用登録", Button_Click); batchActions.Children.Add(batchUse);
        batchCare = Ui.Button("BatchCareButton", "チェック分をお手入れ登録", Button_Click); batchActions.Children.Add(batchCare);
        batchUse.Cursor = batchCare.Cursor = Cursors.Arrow;


        search.TextChanged += (_, _) => RefreshFilter();
        showUsed.Checked += (_, _) => RefreshFilter(); showUsed.Unchecked += (_, _) => RefreshFilter();
        showCared.Checked += (_, _) => RefreshFilter(); showCared.Unchecked += (_, _) => RefreshFilter();

        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "一括登録の対象");
        check.SetValue(FrameworkElement.CursorProperty, Cursors.Arrow);
        check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(DayItem.IsChecked)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        grid.Columns.Insert(0, new DataGridTemplateColumn { Header = "対象", CellTemplate = new DataTemplate { VisualTree = check }, Width = 54 });
        grid.Columns[4].Width = 100; grid.Columns[5].Width = 120;
        grid.ItemsSource = visibleRows;
        grid.Cursor = Cursors.Arrow;
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "写真"));
        rowStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
        rowStyle.Setters.Add(new EventSetter(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler(Row_ToolTipOpening)));
        grid.RowStyle = rowStyle;
        foreach (var row in rows) row.PropertyChanged += (_, _) => UpdateBatchStatus();

        use = Ui.Button("UseButton", "使用", Button_Click); Actions.Children.Add(use);
        care = Ui.Button("CareButton", "お手入れ", Button_Click); Actions.Children.Add(care);
        cancelCare = Ui.Button("CancelCareButton", "お手入れ取消", Button_Click); Actions.Children.Add(cancelCare);
        saveEventButton = Ui.Button("SaveCalendarEventButton", "予定を保存", EventButton_Click); saveEventButton.Style = (Style)FindResource("PrimaryButton"); saveEventButton.Visibility = Visibility.Collapsed; Actions.Children.Add(saveEventButton);
        deleteEventButton = Ui.Button("DeleteCalendarEventButton", "予定を削除", EventButton_Click); deleteEventButton.Visibility = Visibility.Collapsed; deleteEventButton.IsEnabled = false; Actions.Children.Add(deleteEventButton);
        var detail = new StackPanel { Margin = new Thickness(4, 8, 4, 0) };
        var memoHeading = Ui.Text("お手入れメモ（表示のみ。編集は下部の「お手入れ」ボタン）", 12); memoHeading.FontWeight = FontWeights.SemiBold; memoHeading.Foreground = (Brush)FindResource("MutedBrush");
        detail.Children.Add(status); detail.Children.Add(memoHeading); detail.Children.Add(memo);
        tabs = new TabControl();
        var recordTab = new TabItem { Header = "使用・お手入れ" };
        var eventTab = new TabItem { Header = "予定" };
        // Move the existing record controls into the original tab without changing their behavior.
        var recordContent = new DockPanel { Cursor = Cursors.Arrow };
        var recordTop = new StackPanel();
        recordTop.Children.Add(Ui.Text("メーカー・モデル・色で検索")); recordTop.Children.Add(filters);
        recordTop.Children.Add(batchActions); recordTop.Children.Add(batchStatus);
        DockPanel.SetDock(recordTop, Dock.Top); recordContent.Children.Add(recordTop);
        DockPanel.SetDock(detail, Dock.Bottom); recordContent.Children.Add(detail); recordContent.Children.Add(grid);
        recordTab.Content = recordContent;
        tabs.Items.Add(recordTab); tabs.Items.Add(eventTab);
        BuildEventTab(eventTab);
        tabs.SelectionChanged += (_, _) => UpdateActionMode();
        AddTop(tabs);
        grid.SelectionChanged += (_, _) => UpdateSelection();
        grid.SelectedItem = rows.FirstOrDefault(x => x.Id == selected);
        Refresh();
        RefreshEvents();
        UpdateActionMode();
    }

    private void BuildEventTab(TabItem tab)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        var heading = new DockPanel { Margin = new Thickness(2, 0, 2, 8) };
        heading.Children.Add(Ui.Text("この日の予定", 16));
        var count = Ui.Text("一覧から選ぶと編集できます", 12); count.Foreground = (Brush)FindResource("MutedBrush"); count.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(count, Dock.Right); heading.Children.Add(count); panel.Children.Add(heading);
        eventList.MaxHeight = 120; eventList.Margin = new Thickness(0, 0, 0, 14);
        var dataTemplate = new DataTemplate(typeof(CalendarEvent));
        var template = new FrameworkElementFactory(typeof(Border));
        template.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); template.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));
        template.SetValue(Border.MarginProperty, new Thickness(2)); template.SetResourceReference(Border.BackgroundProperty, "CreamBrush");
        var eventContent = new FrameworkElementFactory(typeof(DockPanel));
        var time = new FrameworkElementFactory(typeof(TextBlock)); time.SetBinding(TextBlock.TextProperty, new Binding(nameof(CalendarEvent.Start)) { StringFormat = "HH:mm" });
        time.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); time.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("AccentBrush")); time.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 14, 0));
        eventContent.AppendChild(time);
        var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new Binding(nameof(CalendarEvent.Title))); title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); title.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        eventContent.AppendChild(title); template.AppendChild(eventContent); dataTemplate.VisualTree = template; eventList.ItemTemplate = dataTemplate;
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        var rowTemplate = new ControlTemplate(typeof(ListBoxItem));
        var rowFrame = new FrameworkElementFactory(typeof(Border)); rowFrame.Name = "Frame";
        rowFrame.SetValue(Border.CornerRadiusProperty, new CornerRadius(9)); rowFrame.SetValue(Border.PaddingProperty, new Thickness(2));
        var rowPresenter = new FrameworkElementFactory(typeof(ContentPresenter)); rowPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        rowFrame.AppendChild(rowPresenter); rowTemplate.VisualTree = rowFrame;
        var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentSoftBrush"), "Frame"));
        var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentSoftBrush"), "Frame"));
        rowTemplate.Triggers.Add(selectedTrigger); rowTemplate.Triggers.Add(hoverTrigger);
        itemStyle.Setters.Add(new Setter(Control.TemplateProperty, rowTemplate));
        eventList.ItemContainerStyle = itemStyle;
        panel.Children.Add(eventList);
        eventList.SelectionChanged += (_, _) => LoadSelectedEvent();
        var card = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Background = (Brush)FindResource("SurfaceBrush"), BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1) };
        var form = new StackPanel(); card.Child = form;
        eventFormHeading.Margin = new Thickness(0, 0, 0, 8); form.Children.Add(eventFormHeading);
        AddEventField(form, "タイトル", eventTitle);
        var dateTimes = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        dateTimes.Children.Add(EventField("日付", eventDate, 155));
        dateTimes.Children.Add(EventField("開始", eventStart, 108));
        dateTimes.Children.Add(EventField("終了", eventEnd, 108)); form.Children.Add(dateTimes);
        AddEventField(form, "繰り返し", recurrence);
        recurrenceEndField = EventField("終了日（空欄は終了なし）", recurrenceEnd, 190); form.Children.Add(recurrenceEndField);
        eventScopeField = EventField("適用範囲", eventScope); eventScopeField.Visibility = Visibility.Collapsed; form.Children.Add(eventScopeField);
        AddEventField(form, "場所", eventLocation); AddEventField(form, "説明", eventDescription);
        var notifyHeader = Ui.Text("通知タイミング", 12); notifyHeader.FontWeight = FontWeights.SemiBold; notifyHeader.Foreground = (Brush)FindResource("MutedBrush"); form.Children.Add(notifyHeader);
        var notify = new WrapPanel { Margin = new Thickness(-3, 0, 0, 8) }; notify.Children.Add(notifyPrevious); notify.Children.Add(notify15); notify.Children.Add(notify5); form.Children.Add(notify);
        panel.Children.Add(card);
        tab.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Cursor = Cursors.Arrow };
        eventDate.SelectedDate = date;
        eventDate.Loaded += (_, _) =>
        {
            if (eventDate.Template.FindName("PART_TextBox", eventDate) is DatePickerTextBox dateTextBox)
            {
                dateTextBox.TextAlignment = TextAlignment.Center;
                dateTextBox.VerticalContentAlignment = VerticalAlignment.Center;
            }
        };
        eventTitle.TextChanged += (_, _) => UpdateEventActionState();
        eventStart.TextChanged += (_, _) => UpdateEventActionState(); eventEnd.TextChanged += (_, _) => UpdateEventActionState();
        eventDate.SelectedDateChanged += (_, _) => UpdateEventActionState();
        recurrence.SelectionChanged += (_, _) => UpdateEventRecurrenceState();
        eventScope.SelectionChanged += (_, _) => UpdateEventScopeState();
    }

    private static void AddEventField(Panel panel, string label, Control control) => panel.Children.Add(EventField(label, control));
    private static FrameworkElement EventField(string label, Control control, double? width = null)
    {
        if (width is { } value) control.Width = value;
        var stack = new StackPanel { Margin = new Thickness(0, 0, 18, 5) };
        var caption = Ui.Text(label, 12); caption.FontWeight = FontWeights.SemiBold; caption.Foreground = (Brush)Application.Current.FindResource("MutedBrush");
        stack.Children.Add(caption); stack.Children.Add(control); return stack;
    }

    private void RefreshEvents()
    {
        eventList.ItemsSource = Services.Db.Events(date.Date, date.Date.AddDays(1), Services.Settings.MissingRecurrenceDateBehavior);
        eventList.SelectedItem = selectedEvent is null ? null : ((IEnumerable<CalendarEvent>)eventList.ItemsSource).FirstOrDefault(x => x.Id == selectedEvent.Id);
        if (selectedEvent is null) NewEventForm();
    }

    private void LoadSelectedEvent()
    {
        if (eventList.SelectedItem is not CalendarEvent entry) return;
        selectedEvent = entry; eventTitle.Text = entry.Title; eventDate.SelectedDate = entry.Start.Date;
        eventStart.Text = entry.Start.ToString("HH:mm"); eventEnd.Text = entry.End.ToString("HH:mm");
        eventLocation.Text = entry.Location; eventDescription.Text = entry.Description;
        notifyPrevious.IsChecked = entry.NotifyPreviousDay; notify15.IsChecked = entry.Notify15Minutes; notify5.IsChecked = entry.Notify5Minutes;
        recurrence.SelectedValue = entry.Recurrence; recurrenceEnd.SelectedDate = entry.RecurrenceEnd?.Date;
        eventScope.SelectedValue = "Occurrence";
        eventFormHeading.Text = "予定を編集"; UpdateEventRecurrenceState(); UpdateEventScopeState();
    }

    private void NewEventForm()
    {
        selectedEvent = null; eventList.SelectedItem = null; eventTitle.Clear(); eventDate.SelectedDate = date;
        eventStart.Text = "09:00"; eventEnd.Text = "10:00"; eventLocation.Clear(); eventDescription.Clear();
        notifyPrevious.IsChecked = notify15.IsChecked = notify5.IsChecked = true;
        recurrence.SelectedValue = "None"; recurrenceEnd.SelectedDate = null; eventScope.SelectedValue = "Occurrence";
        eventFormHeading.Text = "新しい予定"; UpdateEventRecurrenceState(); UpdateEventScopeState();
    }

    private void UpdateEventRecurrenceState()
    {
        bool recurring = recurrence.SelectedValue is string value && value != "None";
        if (recurrenceEndField is not null) recurrenceEndField.Visibility = recurring ? Visibility.Visible : Visibility.Collapsed;
        if (eventScopeField is not null) eventScopeField.Visibility = selectedEvent?.IsRecurringOccurrence == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateEventActionState();
    }

    private void UpdateEventScopeState()
    {
        bool recurringOccurrence = selectedEvent?.IsRecurringOccurrence == true;
        bool onlyThis = recurringOccurrence && (string?)eventScope.SelectedValue == "Occurrence";
        recurrence.IsEnabled = !onlyThis; recurrenceEnd.IsEnabled = !onlyThis;
        if (recurringOccurrence && !onlyThis)
        {
            eventDate.SelectedDate = selectedEvent!.SeriesStart.Date;
            eventStart.Text = selectedEvent.SeriesStart.ToString("HH:mm");
            eventEnd.Text = selectedEvent.SeriesStart.Add(selectedEvent.End - selectedEvent.Start).ToString("HH:mm");
        }
        else if (onlyThis)
        {
            eventDate.SelectedDate = selectedEvent!.Start.Date;
            eventStart.Text = selectedEvent.Start.ToString("HH:mm"); eventEnd.Text = selectedEvent.End.ToString("HH:mm");
        }
        if (deleteEventButton is not null) deleteEventButton.Content = onlyThis ? "この回を削除" : "予定を削除";
        if (saveEventButton is not null) saveEventButton.Content = onlyThis ? "この回を保存" : recurringOccurrence ? "シリーズを保存" : "予定を保存";
        UpdateEventActionState();
    }

    private void UpdateActionMode()
    {
        bool eventMode = tabs?.SelectedIndex == 1;
        foreach (var button in new[] { use, care, cancelCare }) button.Visibility = eventMode ? Visibility.Collapsed : Visibility.Visible;
        if (saveEventButton is not null) saveEventButton.Visibility = eventMode ? Visibility.Visible : Visibility.Collapsed;
        if (deleteEventButton is not null) deleteEventButton.Visibility = eventMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateEventActionState(); UpdateSelection();
    }

    private void UpdateEventActionState()
    {
        if (saveEventButton is not null)
            saveEventButton.IsEnabled = !string.IsNullOrWhiteSpace(eventTitle.Text) && eventDate.SelectedDate.HasValue
                && TimeSpan.TryParse(eventStart.Text, out _) && TimeSpan.TryParse(eventEnd.Text, out _);
        if (deleteEventButton is not null) deleteEventButton.IsEnabled = selectedEvent is not null;
    }

    private void EventButton_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        switch (((Button)sender).Name)
        {
            case "DeleteCalendarEventButton":
                if (selectedEvent is not null && Ui.Confirm((selectedEvent.IsRecurringOccurrence && (string?)eventScope.SelectedValue == "Occurrence" ? "この回だけ" : "予定") + "を削除しますか？"))
                {
                    if (selectedEvent.IsRecurringOccurrence && (string?)eventScope.SelectedValue == "Occurrence") Services.Db.DeleteEventOccurrence(selectedEvent);
                    else Services.Db.DeleteEvent(selectedEvent.Id);
                }
                NewEventForm(); RefreshEvents(); return;
        }
        if (!eventDate.SelectedDate.HasValue || !TimeSpan.TryParse(eventStart.Text, out var startTime) || !TimeSpan.TryParse(eventEnd.Text, out var endTime))
            throw new InvalidDataException("日付と時刻を確認してください。");
        var start = eventDate.SelectedDate.Value.Date + startTime; var end = eventDate.SelectedDate.Value.Date + endTime;
        if (end <= start) end = end.AddDays(1);
        var edited = new CalendarEvent { Id = selectedEvent?.Id ?? 0, Title = eventTitle.Text, Start = start, End = end, Location = eventLocation.Text, Description = eventDescription.Text,
            NotifyPreviousDay = notifyPrevious.IsChecked == true, Notify15Minutes = notify15.IsChecked == true, Notify5Minutes = notify5.IsChecked == true,
            Notified = selectedEvent?.Notified ?? "", Recurrence = recurrence.SelectedValue as string ?? "None", RecurrenceEnd = recurrenceEnd.SelectedDate?.Date };
        if (selectedEvent?.IsRecurringOccurrence == true && (string?)eventScope.SelectedValue == "Occurrence") Services.Db.SaveEventOccurrence(selectedEvent, edited);
        else Services.Db.SaveEvent(edited);
        NewEventForm(); RefreshEvents();
    });

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
        if (name is "SaveCalendarEventButton" or "DeleteCalendarEventButton") { EventButton_Click(sender, e); return; }
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
