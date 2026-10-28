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

        var card = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(20), Margin = new Thickness(12),
            Background = (Brush)Application.Current.FindResource("CreamBrush"), BorderBrush = (Brush)Application.Current.FindResource("AccentBrush"), BorderThickness = new Thickness(2) };
        var content = new StackPanel(); card.Child = content;
        var title = Ui.Text(heading, 13); title.Foreground = (Brush)FindResource("AccentBrush"); title.FontWeight = FontWeights.SemiBold; content.Children.Add(title);
        var eventTitle = Ui.Text(entry.Title, 20); eventTitle.FontWeight = FontWeights.Bold; content.Children.Add(eventTitle);
        content.Children.Add(Ui.Text($"{entry.Start:yyyy年M月d日（ddd）}　{entry.Start:HH:mm}～{entry.End:HH:mm}", 14));
        if (!string.IsNullOrWhiteSpace(entry.Location)) content.Children.Add(Ui.Text(entry.Location, 13));
        if (!string.IsNullOrWhiteSpace(entry.Description)) content.Children.Add(Ui.Text(entry.Description, 13));
        if (!string.IsNullOrWhiteSpace(timing)) content.Children.Add(Ui.Text(timing, 12));
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var acknowledge = Ui.Button("AcknowledgeEventButton", "OK", (_, _) => Complete(false)); acknowledge.Style = (Style)FindResource("PrimaryButton"); actions.Children.Add(acknowledge);
        actions.Children.Add(Ui.Button("SuppressEventNotificationsButton", "以降、この予定を通知しない", (_, _) => Complete(true)));
        content.Children.Add(actions); Content = card;
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
