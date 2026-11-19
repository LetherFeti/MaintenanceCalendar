using System.Windows.Controls.Primitives;

namespace MaintenanceCalendar;

/// <summary>保存済みの予定シリーズを一覧し、追加・編集・削除します。</summary>
public sealed class CalendarEventsWindow : ToolWindow
{
    private readonly CalendarEventsWindowView view;
    private readonly EventsViewModel vm = new();
    private DataGrid grid => view.Grid;

    public CalendarEventsWindow() : base("予定一覧", 900, 560)
    {
        view = new CalendarEventsWindowView(); view.DataContext = vm; Root.Children.Add(view);
        AddButton("AddEventButton", "追加", Button_Click);
        AddButton("EditEventButton", "編集", Button_Click);
        AddButton("DeleteEventButton", "削除", Button_Click);
        Refresh();
    }

    private void Refresh() { vm.SelectedItem = null; vm.Items = Services.Db.AllEvents(); }

    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        switch (((Button)sender).Name)
        {
            case "AddEventButton":
                var created = new CalendarEvent { Start = DateTime.Today.AddHours(9), End = DateTime.Today.AddHours(10) };
                var add = new CalendarEventEditWindow(created) { Owner = this };
                if (add.ShowDialog() == true) { Services.Db.SaveEvent(add.Result!); Refresh(); }
                break;
            case "EditEventButton":
                if (vm.SelectedItem is not CalendarEvent selected) { MessageBox.Show("編集する予定を選択してください。"); return; }
                var edit = new CalendarEventEditWindow(selected) { Owner = this };
                if (edit.ShowDialog() == true) { Services.Db.SaveEvent(edit.Result!); Refresh(); }
                break;
            case "DeleteEventButton":
                if (vm.SelectedItem is CalendarEvent entry && Ui.Confirm($"「{entry.Title}」の予定シリーズを削除しますか？"))
                { Services.Db.DeleteEvent(entry.Id); Refresh(); }
                break;
        }
    });
}

/// <summary>予定シリーズを編集する共通ダイアログです。</summary>
public sealed class CalendarEventEditWindow : ToolWindow
{
    private readonly CalendarEventEditWindowView view;
    private readonly EventEditViewModel vm = new();
    private TextBox title => view.Title;
    private DatePicker date => view.Date;
    private TextBox startTime => view.StartTime;
    private TextBox endTime => view.EndTime;
    private TextBox location => view.Location;
    private TextBox description => view.Description;
    private ComboBox recurrence => view.Recurrence;
    private DatePicker recurrenceEnd => view.RecurrenceEnd;
    private CheckBox previous => view.Previous;
    private CheckBox fifteen => view.Fifteen;
    private CheckBox five => view.Five;
    public CalendarEvent? Result { get; private set; }

    public CalendarEventEditWindow(CalendarEvent source) : base(source.Id == 0 ? "予定を追加" : "予定を編集", 560, 660)
    {
        Tag = source;
        view = new CalendarEventEditWindowView(); view.DataContext = vm; Root.Children.Add(view);
        vm.Options = new Dictionary<string, string> { ["None"] = "繰り返しなし", ["Weekly"] = "毎週", ["Biweekly"] = "隔週", ["Monthly"] = "毎月", ["Yearly"] = "毎年" };
        title.Text = source.Title; date.SelectedDate = source.Start.Date; startTime.Text = source.Start.ToString("HH:mm"); endTime.Text = source.End.ToString("HH:mm");
        location.Text = source.Location; description.Text = source.Description; vm.SelectedValue = source.Recurrence;
        recurrenceEnd.SelectedDate = source.RecurrenceEnd?.Date; previous.IsChecked = source.NotifyPreviousDay; fifteen.IsChecked = source.Notify15Minutes; five.IsChecked = source.Notify5Minutes;
        date.Loaded += (_, _) =>
        {
            if (date.Template.FindName("PART_TextBox", date) is DatePickerTextBox textBox) textBox.TextAlignment = TextAlignment.Center;
        };
        recurrence.SelectionChanged += (_, _) => UpdateRecurrence(); UpdateRecurrence();
        AddButton("SaveEventDetailsButton", "保存", Button_Click); AddButton("CancelEventDetailsButton", "キャンセル", Button_Click);
    }

    private void UpdateRecurrence() => view.RecurrenceEndField.Visibility = (string?)vm.SelectedValue == "None" ? Visibility.Collapsed : Visibility.Visible;

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Name == "CancelEventDetailsButton") { DialogResult = false; return; }
        Ui.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || !date.SelectedDate.HasValue || !TimeSpan.TryParse(startTime.Text, out var start) || !TimeSpan.TryParse(endTime.Text, out var end))
                throw new InvalidDataException("タイトル・日付・時刻を確認してください。");
            var starts = date.SelectedDate.Value.Date + start; var ends = date.SelectedDate.Value.Date + end;
            if (ends <= starts) ends = ends.AddDays(1);
            Result = new CalendarEvent
            {
                Id = ((CalendarEvent)Tag!).Id,
                Title = title.Text,
                Start = starts,
                End = ends,
                Location = location.Text,
                Description = description.Text,
                Recurrence = vm.SelectedValue as string ?? "None",
                RecurrenceEnd = recurrenceEnd.SelectedDate?.Date,
                NotifyPreviousDay = previous.IsChecked == true,
                Notify15Minutes = fifteen.IsChecked == true,
                Notify5Minutes = five.IsChecked == true,
                Notified = ((CalendarEvent)Tag!).Notified
            };
            DialogResult = true;
        });
    }
}
