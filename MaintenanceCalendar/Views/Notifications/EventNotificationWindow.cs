namespace MaintenanceCalendar;

/// <summary>予定の通知を予定一覧に干渉しない独立ウィンドウで表示します。</summary>
public sealed class EventNotificationWindow : Window
{
    private readonly Action<bool> completed;
    private bool completedOnce;

    public EventNotificationWindow(CalendarEvent entry, string heading, string timing, Action<bool> completed)
    {
        this.completed = completed;
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "予定のお知らせ"; Width = 370; SizeToContent = SizeToContent.Height; MinHeight = 220; MaxHeight = 520;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = true;
        ShowActivated = false; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = System.Windows.Input.Cursors.Arrow;

        var view = new EventNotificationWindowView();
        view.Heading.Text = heading;
        view.EventTitle.Text = entry.Title;
        view.WhenText.Text = $"{entry.Start:yyyy年M月d日（ddd）}　{entry.Start:HH:mm}～{entry.End:HH:mm}";
        view.LocationText.Text = entry.Location;
        view.LocationText.Visibility = string.IsNullOrWhiteSpace(entry.Location) ? Visibility.Collapsed : Visibility.Visible;
        view.DescriptionText.Text = entry.Description;
        view.DescriptionText.Visibility = string.IsNullOrWhiteSpace(entry.Description) ? Visibility.Collapsed : Visibility.Visible;
        view.TimingText.Text = timing;
        view.TimingText.Visibility = string.IsNullOrWhiteSpace(timing) ? Visibility.Collapsed : Visibility.Visible;
        view.AcknowledgeButton.Click += (_, _) => Complete(false);
        view.SuppressButton.Click += (_, _) => Complete(true);
        Content = view;
        Loaded += (_, _) => PositionAtBottomRight();
        Closing += (_, _) => Complete(false);
    }

    private void PositionAtBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - ActualWidth - 12);
        Top = Math.Max(area.Top, area.Bottom - ActualHeight - 12);
    }

    private void Complete(bool suppress)
    {
        if (completedOnce) return;
        completedOnce = true;
        completed(suppress);
        if (IsVisible) Close();
    }
}
