namespace MaintenanceCalendar;


public sealed class DueWindow : ToolWindow
{
    private readonly DueWindowView view;
    private readonly DueItemsViewModel vm = new();
    private DataGrid grid => view.Grid;
    private readonly System.Windows.Threading.DispatcherTimer dateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private DateTime loadedDate = DateTime.Today;
    /// <summary>
    /// 手入れ期限へ達した所持品の一覧と履歴表示ボタンを構成します。
    /// </summary>
    public DueWindow() : base("お手入れ通知", 800, 450)
    {
        view = new DueWindowView(); view.DataContext = vm; Root.Children.Add(view);
        AddButton("HistoryButton", "選択した所持品の履歴", Button_Click);
        Refresh();
        dateTimer.Tick += (_, _) => { if (DateTime.Today != loadedDate) Refresh(); };
        dateTimer.Start();
        Closed += (_, _) => dateTimer.Stop();
    }

    private void Refresh()
    {
        loadedDate = DateTime.Today;
        var due = Services.Db.Due(loadedDate);
        var events = Services.Db.Events(loadedDate, loadedDate.AddDays(1), Services.Settings.MissingRecurrenceDateBehavior);
        vm.SelectedItem = null;
        view.EventsHeading.Text = $"今日の予定  {events.Count}件";
        vm.Events = events;
        view.EmptyEvents.Visibility = events.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        view.DueHeading.Text = $"お手入れが必要な所持品  {due.Count}件";
        vm.Items = due;
    }
    /// <summary>
    /// 選択した期限対象品の履歴画面を開きます。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        if (((Button)sender).Name == "HistoryButton" && vm.SelectedItem is DueItem row) Ui.Show(this, new HistoryWindow(row.Item.Id));
    });
}

