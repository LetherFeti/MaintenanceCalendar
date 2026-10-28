using System.Windows.Controls.Primitives;

namespace MaintenanceCalendar;

/// <summary>保存済みの予定シリーズを一覧し、追加・編集・削除します。</summary>
public sealed class CalendarEventsWindow : ToolWindow
{
    private readonly DataGrid grid = Ui.Grid(("予定", "Title"), ("開始", "Start"), ("繰り返し", "RecurrenceLabel"), ("終了日", "RecurrenceEnd"), ("場所", "Location"));

    public CalendarEventsWindow() : base("予定一覧", 900, 560)
    {
        AddTop(Ui.Text("予定一覧", 21));
        AddTop(Ui.Text("定期予定はシリーズ単位で管理します。カレンダーから開催回ごとの編集・取り消しもできます。"));
        ((DataGridTextColumn)grid.Columns[1]).Binding.StringFormat = "yyyy/MM/dd HH:mm";
        ((DataGridTextColumn)grid.Columns[3]).Binding.StringFormat = "yyyy/MM/dd";
        Root.Children.Add(grid);
        AddButton("AddEventButton", "追加", Button_Click);
        AddButton("EditEventButton", "編集", Button_Click);
        AddButton("DeleteEventButton", "削除", Button_Click);
        Refresh();
    }

    private void Refresh() => grid.ItemsSource = Services.Db.AllEvents();

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
                if (grid.SelectedItem is not CalendarEvent selected) { MessageBox.Show("編集する予定を選択してください。"); return; }
                var edit = new CalendarEventEditWindow(selected) { Owner = this };
                if (edit.ShowDialog() == true) { Services.Db.SaveEvent(edit.Result!); Refresh(); }
                break;
            case "DeleteEventButton":
                if (grid.SelectedItem is CalendarEvent entry && Ui.Confirm($"「{entry.Title}」の予定シリーズを削除しますか？"))
                { Services.Db.DeleteEvent(entry.Id); Refresh(); }
                break;
        }
    });
}

/// <summary>予定シリーズを編集する共通ダイアログです。</summary>
public sealed class CalendarEventEditWindow : ToolWindow
{
    private readonly TextBox title = new() { MinWidth = 320 };
    private readonly DatePicker date = new();
    private readonly TextBox startTime = new() { Width = 105, Text = "09:00" };
    private readonly TextBox endTime = new() { Width = 105, Text = "10:00" };
    private readonly TextBox location = new() { MinWidth = 240 };
    private readonly TextBox description = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ComboBox recurrence = new() { DisplayMemberPath = "Value", SelectedValuePath = "Key", ItemsSource = new Dictionary<string, string> { ["None"] = "繰り返しなし", ["Weekly"] = "毎週", ["Biweekly"] = "隔週", ["Monthly"] = "毎月", ["Yearly"] = "毎年" } };
    private readonly DatePicker recurrenceEnd = new();
    private readonly CheckBox previous = new() { Content = "前日", IsChecked = true };
    private readonly CheckBox fifteen = new() { Content = "15分前", IsChecked = true };
    private readonly CheckBox five = new() { Content = "5分前", IsChecked = true };
    private FrameworkElement? recurrenceEndField;
    public CalendarEvent? Result { get; private set; }

    public CalendarEventEditWindow(CalendarEvent source) : base(source.Id == 0 ? "予定を追加" : "予定を編集", 560, 660)
    {
        Tag = source;
        var panel = new StackPanel { Margin = new Thickness(4) };
        Root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Ui.Field(panel, "タイトル", title);
        var dateTimes = new WrapPanel(); dateTimes.Children.Add(Field("日付", date, 170)); dateTimes.Children.Add(Field("開始", startTime, 115)); dateTimes.Children.Add(Field("終了", endTime, 115)); panel.Children.Add(dateTimes);
        Ui.Field(panel, "場所", location); Ui.Field(panel, "説明", description); Ui.Field(panel, "繰り返し", recurrence);
        recurrenceEndField = Field("終了日（空欄は終了なし）", recurrenceEnd, 190); panel.Children.Add(recurrenceEndField);
        panel.Children.Add(Ui.Text("通知タイミング", 13)); var checks = new WrapPanel(); checks.Children.Add(previous); checks.Children.Add(fifteen); checks.Children.Add(five); panel.Children.Add(checks);
        title.Text = source.Title; date.SelectedDate = source.Start.Date; startTime.Text = source.Start.ToString("HH:mm"); endTime.Text = source.End.ToString("HH:mm");
        location.Text = source.Location; description.Text = source.Description; recurrence.SelectedValue = source.Recurrence;
        recurrenceEnd.SelectedDate = source.RecurrenceEnd?.Date; previous.IsChecked = source.NotifyPreviousDay; fifteen.IsChecked = source.Notify15Minutes; five.IsChecked = source.Notify5Minutes;
        date.Loaded += (_, _) =>
        {
            if (date.Template.FindName("PART_TextBox", date) is DatePickerTextBox textBox) textBox.TextAlignment = TextAlignment.Center;
        };
        recurrence.SelectionChanged += (_, _) => UpdateRecurrence(); UpdateRecurrence();
        AddButton("SaveEventDetailsButton", "保存", Button_Click); AddButton("CancelEventDetailsButton", "キャンセル", Button_Click);
    }

    private static FrameworkElement Field(string label, Control control, double width)
    {
        control.Width = width; var panel = new StackPanel { Margin = new Thickness(0, 0, 14, 4) };
        panel.Children.Add(Ui.Text(label, 12)); panel.Children.Add(control); return panel;
    }

    private void UpdateRecurrence() => recurrenceEndField!.Visibility = (string?)recurrence.SelectedValue == "None" ? Visibility.Collapsed : Visibility.Visible;

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Name == "CancelEventDetailsButton") { DialogResult = false; return; }
        Ui.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || !date.SelectedDate.HasValue || !TimeSpan.TryParse(startTime.Text, out var start) || !TimeSpan.TryParse(endTime.Text, out var end))
                throw new InvalidDataException("タイトル・日付・時刻を確認してください。");
            var starts = date.SelectedDate.Value.Date + start; var ends = date.SelectedDate.Value.Date + end;
            if (ends <= starts) ends = ends.AddDays(1);
            Result = new CalendarEvent { Id = ((CalendarEvent)Tag!).Id, Title = title.Text, Start = starts, End = ends, Location = location.Text, Description = description.Text,
                Recurrence = recurrence.SelectedValue as string ?? "None", RecurrenceEnd = recurrenceEnd.SelectedDate?.Date,
                NotifyPreviousDay = previous.IsChecked == true, Notify15Minutes = fifteen.IsChecked == true, Notify5Minutes = five.IsChecked == true,
                Notified = ((CalendarEvent)Tag!).Notified };
            DialogResult = true;
        });
    }

    private FrameworkElement Field(string label, Control control) => Field(label, control, control.Width > 0 ? control.Width : 250);
}
