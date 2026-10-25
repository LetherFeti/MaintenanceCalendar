namespace MaintenanceCalendar;

public sealed class TemplatesWindow : ToolWindow
{
    private readonly DataGrid grid = Ui.Grid(("手入れ概要", "Summary"), ("状態", "Status"));
    private readonly TextBox summary = new() { MaxLength = 50 };
    private readonly CheckBox all = new() { Content = "削除済みも表示" };
    /// <summary>
    /// 手入れ概要の入力欄・一覧・登録と削除の操作を構成します。
    /// </summary>
    public TemplatesWindow() : base("お手入れマスタ", 550, 480)
    {
        AddTop(Ui.Text("お手入れマスタ", 21));

        AddTop(Ui.Text("例：ブラッシング / 乾拭き（50文字まで）\n名前を変更する場合は新しい概要を登録し、旧概要を削除してください。"));

        AddTop(summary); AddTop(all); Root.Children.Add(grid);

        all.Checked += (_, _) => Ui.Run(Refresh); all.Unchecked += (_, _) => Ui.Run(Refresh);

        grid.SelectionChanged += (_, _) => { if (grid.SelectedItem is TemplateRow row) summary.Text = row.Summary; };

        AddButton("SaveButton", "登録 / 復旧", Button_Click); AddButton("DeleteButton", "削除", Button_Click); AddButton("NewButton", "入力をクリア", Button_Click);

        Refresh();
    }

    /// <summary>
    /// 削除済み表示の設定に応じて手入れ概要マスタを読み直します。
    /// </summary>
    private void Refresh() => grid.ItemsSource = Services.Db.Templates(all.IsChecked == true);

    /// <summary>
    /// 入力した定型語句を登録・復旧・論理削除するか、入力欄を消去します。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        switch (((Button)sender).Name)
        {
            case "SaveButton": Services.Db.SaveTemplate(summary.Text, false); Refresh(); break;
            case "DeleteButton":
                if (grid.SelectedItem is TemplateRow row && Ui.Confirm("選択した手入れ概要を削除しますか？")) { Services.Db.SaveTemplate(row.Summary, true); Refresh(); } break;
            case "NewButton": grid.SelectedItem = null; summary.Clear(); summary.Focus(); break;
        }
    });
}
