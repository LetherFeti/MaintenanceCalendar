namespace MaintenanceCalendar;

public sealed class TemplatesWindow : ToolWindow
{
    private readonly TemplatesWindowView view;
    private readonly TemplatesViewModel vm = new();
    private DataGrid grid => view.Grid;
    private TextBox summary => view.Summary;
    private CheckBox all => view.ShowDeleted;
    /// <summary>
    /// 手入れ概要の入力欄・一覧・登録と削除の操作を構成します。
    /// </summary>
    public TemplatesWindow() : base("お手入れマスタ", 550, 480)
    {
        view = new TemplatesWindowView(); view.DataContext = vm; Root.Children.Add(view);

        all.Checked += (_, _) => Ui.Run(Refresh); all.Unchecked += (_, _) => Ui.Run(Refresh);

        grid.SelectionChanged += (_, _) => { if (vm.SelectedItem is TemplateRow row) summary.Text = row.Summary; };

        view.SaveButton.Click += Button_Click; view.DeleteButton.Click += Button_Click; view.NewButton.Click += Button_Click;

        Refresh();
    }

    /// <summary>
    /// 削除済み表示の設定に応じて手入れ概要マスタを読み直します。
    /// </summary>
    private void Refresh() => vm.Items = Services.Db.Templates(all.IsChecked == true);

    /// <summary>
    /// 入力した定型語句を登録・復旧・論理削除するか、入力欄を消去します。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        switch (((Button)sender).Name)
        {
            case "SaveButton": Services.Db.SaveTemplate(summary.Text, false); Refresh(); break;
            case "DeleteButton":
                if (vm.SelectedItem is TemplateRow row && Ui.Confirm("選択した手入れ概要を削除しますか？")) { Services.Db.SaveTemplate(row.Summary, true); Refresh(); }
                break;
            case "NewButton": vm.SelectedItem = null; summary.Clear(); summary.Focus(); break;
        }
    });
}
