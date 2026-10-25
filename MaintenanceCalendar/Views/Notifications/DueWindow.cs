namespace MaintenanceCalendar;

public sealed class DueWindow : ToolWindow
{
    private readonly DataGrid grid = Ui.Grid(("所持品", "Name"), ("最終お手入れ", "LastCareText"), ("基準日", "BaseText"), ("お手入れ期限", "DueText"));
    /// <summary>
    /// 手入れ期限へ達した所持品の一覧と履歴表示ボタンを構成します。
    /// </summary>
    public DueWindow() : base("お手入れ通知", 800, 450)
    {
        var due = Services.Db.Due(DateTime.Today);

        AddTop(Ui.Text($"お手入れが必要な所持品  {due.Count}件", 20));

        AddTop(Ui.Text("未手入れの場合は購入日、購入日がなければ登録日を基準にしています。"));

        grid.ItemsSource = due; Root.Children.Add(grid);

        AddButton("HistoryButton", "選択した所持品の履歴", Button_Click);
    }
    /// <summary>
    /// 選択した期限対象品の履歴画面を開きます。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        if (((Button)sender).Name == "HistoryButton" && grid.SelectedItem is DueItem row) Ui.Show(this, new HistoryWindow(row.Item.Id));
    });
}

