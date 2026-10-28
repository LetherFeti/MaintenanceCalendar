namespace MaintenanceCalendar;

using System.Windows.Data;

public sealed class DueWindow : ToolWindow
{
    private readonly DataGrid grid = Ui.Grid(("所持品", "Name"), ("最終お手入れ", "LastCareText"), ("基準日", "BaseText"), ("お手入れ期限", "DueText"));
    private readonly StackPanel content = new() { Margin = new Thickness(0, 0, 0, 8) };
    private readonly System.Windows.Threading.DispatcherTimer dateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private DateTime loadedDate = DateTime.Today;
    /// <summary>
    /// 手入れ期限へ達した所持品の一覧と履歴表示ボタンを構成します。
    /// </summary>
    public DueWindow() : base("お手入れ通知", 800, 450)
    {
        Root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        AddButton("HistoryButton", "選択した所持品の履歴", Button_Click);
        Refresh();
        dateTimer.Tick += (_, _) => { if (DateTime.Today != loadedDate) Refresh(); };
        dateTimer.Start();
        Closed += (_, _) => dateTimer.Stop();
    }

    private void Refresh()
    {
        loadedDate = DateTime.Today; content.Children.Clear();
        var due = Services.Db.Due(loadedDate);
        var events = Services.Db.Events(loadedDate, loadedDate.AddDays(1), Services.Settings.MissingRecurrenceDateBehavior);
        content.Children.Add(Ui.Text($"今日の予定  {events.Count}件", 20));
        if (events.Count == 0) content.Children.Add(Ui.Text("今日の予定はありません。"));
        else
        {
            var eventList = new ItemsControl { ItemsSource = events, Margin = new Thickness(0, 0, 0, 10) };
            var template = new DataTemplate(typeof(CalendarEvent));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7)); border.SetValue(Border.MarginProperty, new Thickness(2));
            border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush"); border.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var row = new FrameworkElementFactory(typeof(DockPanel));
            var time = new FrameworkElementFactory(typeof(TextBlock)); time.SetBinding(TextBlock.TextProperty, new Binding(nameof(CalendarEvent.Start)) { StringFormat = "HH:mm" });
            time.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); time.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 12, 0)); time.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            row.AppendChild(time);
            var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new Binding(nameof(CalendarEvent.Title))); title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); row.AppendChild(title);
            var location = new FrameworkElementFactory(typeof(TextBlock)); location.SetBinding(TextBlock.TextProperty, new Binding(nameof(CalendarEvent.Location))); location.SetValue(TextBlock.MarginProperty, new Thickness(12, 0, 0, 0)); location.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); location.SetValue(DockPanel.DockProperty, Dock.Right); row.AppendChild(location);
            border.AppendChild(row); template.VisualTree = border; eventList.ItemTemplate = template; content.Children.Add(eventList);
        }
        content.Children.Add(Ui.Text($"お手入れが必要な所持品  {due.Count}件", 20));
        content.Children.Add(Ui.Text("未手入れの場合は購入日、購入日がなければ登録日を基準にしています。"));
        grid.ItemsSource = due; grid.MinHeight = 160; grid.MaxHeight = 280; content.Children.Add(grid);
    }
    /// <summary>
    /// 選択した期限対象品の履歴画面を開きます。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        if (((Button)sender).Name == "HistoryButton" && grid.SelectedItem is DueItem row) Ui.Show(this, new HistoryWindow(row.Item.Id));
    });
}

