namespace MaintenanceCalendar;

public sealed class CareMemoWindow : ToolWindow
{
    private readonly TextBox memo = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 20000 };
    private readonly ListBox templates = new() { SelectionMode = SelectionMode.Multiple, DisplayMemberPath = "Summary", MaxHeight = 140 };
    public string Memo => memo.Text;
    /// <summary>
    /// 定型語句の一覧と初期メモを読み込み、追記・記録・キャンセル操作を設定します。
    /// </summary>
    public CareMemoWindow(string initial) : base("お手入れメモ", 500, 530)
    {
        AddTop(Ui.Text("マスタから複数選択して追加できます。自由に追記・編集できます。"));

        templates.ItemsSource = Services.Db.Templates(); AddTop(templates);

        AddTop(Ui.Button("AppendButton", "選択した内容をメモに追加", Button_Click));

        memo.Text = initial; Root.Children.Add(memo);

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
                foreach (TemplateRow row in templates.SelectedItems)
                { if (memo.Text.Length > 0 && !memo.Text.EndsWith(Environment.NewLine)) memo.AppendText(Environment.NewLine); memo.AppendText(row.Summary); }
                memo.Focus(); memo.CaretIndex = memo.Text.Length; break;
            case "SaveButton": DialogResult = true; break;
            case "CancelButton": DialogResult = false; break;
        }
    }
}
