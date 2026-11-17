using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace MaintenanceCalendar;

public sealed class DayWindow : ToolWindow
{
    private readonly DateTime date;
    private readonly DayWindowView view;
    private DataGrid grid => view.Grid;
    private readonly List<DayItem> rows;
    private readonly ICollectionView visibleRows;
    private TextBox search => view.Search;
    private CheckBox showUsed => view.ShowUsed;
    private CheckBox showCared => view.ShowCared;
    private readonly Button use, care, cancelCare, batchUse, batchCare;
    private TextBlock status => view.Status;
    private TextBlock batchStatus => view.BatchStatus;
    private TextBox memo => view.Memo;
    private List<HistoryRow> records = [];
    private ListBox eventList => view.EventList;
    private TextBox eventTitle => view.EventTitle;
    private TextBox eventStart => view.EventStart;
    private TextBox eventEnd => view.EventEnd;
    private TextBox eventLocation => view.EventLocation;
    private TextBox eventDescription => view.EventDescription;
    private CheckBox notifyPrevious => view.NotifyPrevious;
    private CheckBox notify15 => view.Notify15;
    private CheckBox notify5 => view.Notify5;
    private DatePicker eventDate => view.EventDate;
    private ComboBox recurrence => view.Recurrence;
    private DatePicker recurrenceEnd => view.RecurrenceEnd;
    private ComboBox eventScope => view.EventScope;
    private FrameworkElement recurrenceEndField => view.RecurrenceEndField;
    private FrameworkElement eventScopeField => view.EventScopeField;
    private TextBlock eventFormHeading => view.EventFormHeading;
    private Button saveEventButton => view.SaveEventButton;
    private Button deleteEventButton => view.DeleteEventButton;
    private TabControl tabs => view.Tabs;
    private CalendarEvent? selectedEvent;

    /// <summary>当日の登録状況と一括登録の対象を一覧表示します。</summary>
    public DayWindow(DateTime date, IReadOnlyList<Item> items, long? selected, TimeSpan? initialEventTime = null) : base(date.ToString("yyyy年M月d日（ddd）"), 740, 610)
    {
        this.date = date;
        view = new DayWindowView(); Root.Children.Add(view);
        MinWidth = 660; MinHeight = 800;
        Cursor = Cursors.Arrow;
        use = view.UseButton; care = view.CareButton; cancelCare = view.CancelCareButton;
        batchUse = view.BatchUseButton; batchCare = view.BatchCareButton;
        recurrence.ItemsSource = new Dictionary<string, string> { ["None"] = "繰り返しなし", ["Weekly"] = "毎週", ["Biweekly"] = "隔週", ["Monthly"] = "毎月", ["Yearly"] = "毎年" };
        eventScope.ItemsSource = new Dictionary<string, string> { ["Occurrence"] = "この回だけ", ["Series"] = "シリーズ全体" };
        search.Cursor = eventTitle.Cursor = eventStart.Cursor = eventEnd.Cursor = eventLocation.Cursor = eventDescription.Cursor = Cursors.IBeam;
        memo.Cursor = Cursors.Arrow;
        eventDate.Cursor = notifyPrevious.Cursor = notify15.Cursor = notify5.Cursor = Cursors.Arrow;
        rows = items.Select(x => new DayItem(x)).ToList();
        visibleRows = CollectionViewSource.GetDefaultView(rows);
        visibleRows.Filter = MatchesFilter;
        showUsed.Cursor = showCared.Cursor = Cursors.Arrow;
        batchUse.Cursor = batchCare.Cursor = view.CheckAllButton.Cursor = Cursors.Arrow;
        foreach (var button in new[] { view.CheckAllButton, batchUse, batchCare, use, care, cancelCare }) button.Click += Button_Click;
        saveEventButton.Click += EventButton_Click; deleteEventButton.Click += EventButton_Click;

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

        BuildEventTab();
        tabs.SelectionChanged += (_, _) => UpdateActionMode();
        grid.SelectionChanged += (_, _) => UpdateSelection();
        grid.SelectedItem = rows.FirstOrDefault(x => x.Id == selected);
        Refresh();
        RefreshEvents();
        if (initialEventTime is { } startAt)
        {
            tabs.SelectedIndex = 1;
            eventStart.Text = startAt.ToString(@"hh\:mm");
            eventEnd.Text = startAt.Add(TimeSpan.FromHours(1)).ToString(@"hh\:mm");
        }
        UpdateActionMode();
    }

    private void BuildEventTab()
    {
        eventList.SelectionChanged += (_, _) => LoadSelectedEvent();
        eventList.Cursor = Cursors.Arrow;
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
        var edited = new CalendarEvent
        {
            Id = selectedEvent?.Id ?? 0,
            Title = eventTitle.Text,
            Start = start,
            End = end,
            Location = eventLocation.Text,
            Description = eventDescription.Text,
            NotifyPreviousDay = notifyPrevious.IsChecked == true,
            Notify15Minutes = notify15.IsChecked == true,
            Notify5Minutes = notify5.IsChecked == true,
            Notified = selectedEvent?.Notified ?? "",
            Recurrence = recurrence.SelectedValue as string ?? "None",
            RecurrenceEnd = recurrenceEnd.SelectedDate?.Date
        };
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
