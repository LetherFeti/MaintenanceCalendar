namespace MaintenanceCalendar;

public sealed class CareMemoWindow : ToolWindow
{
    private readonly CareMemoWindowView view;
    private TextBox memo => view.Memo;
    private ListBox templates => view.Templates;
    public string Memo => memo.Text;
    /// <summary>
    /// 定型語句の一覧と初期メモを読み込み、追記・記録・キャンセル操作を設定します。
    /// </summary>
    public CareMemoWindow(string initial) : base("お手入れメモ", 500, 530)
    {
        view = new CareMemoWindowView(); Root.Children.Add(view);
        templates.ItemsSource = Services.Db.Templates();
        view.AppendButton.Click += Button_Click;
        templates.MouseDoubleClick += (_, _) =>
        {
            if (templates.SelectedItem is TemplateRow row) AppendTemplates([row]);
        };
        memo.Text = initial;
        AddButton("SaveButton", "記録", Button_Click); AddButton("CancelButton", "キャンセル", Button_Click);
    }
    /// <summary>
    /// 選択した定型語句の追記、編集内容の確定、またはキャンセルを行います。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e)
    {
        switch (((Button)sender).Name)
        {
            case "AppendButton":
                AppendSelectedTemplates(); break;
            case "SaveButton": DialogResult = true; break;
            case "CancelButton": DialogResult = false; break;
        }
    }

    private void AppendSelectedTemplates()
    {
        AppendTemplates(templates.SelectedItems.Cast<TemplateRow>());
    }

    private void AppendTemplates(IEnumerable<TemplateRow> rows)
    {
        foreach (TemplateRow row in rows)
        {
            if (memo.Text.Length > 0 && !memo.Text.EndsWith(Environment.NewLine)) memo.AppendText(Environment.NewLine);
            memo.AppendText(row.Summary);
        }
        memo.Focus(); memo.CaretIndex = memo.Text.Length;
    }
}
