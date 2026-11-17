using Microsoft.Win32;

namespace MaintenanceCalendar;

public sealed class ItemsWindow : ToolWindow
{
    private readonly ItemsWindowView view;
    private DataGrid grid => view.Grid;
    private CheckBox includeDeleted => view.IncludeDeleted;

    /// <summary>
    /// 所持品一覧と選択用チェック欄、登録・編集・削除・復旧の操作を構成します。
    /// </summary>
    public ItemsWindow() : base("所持品マスタ", 920, 570)
    {
        view = new ItemsWindowView();
        Root.Children.Add(view);
        includeDeleted.Checked += (_, _) => Ui.Run(Refresh); includeDeleted.Unchecked += (_, _) => Ui.Run(Refresh);
        foreach (var button in new[] { view.AddButton, view.EditButton, view.CopyButton, view.DeleteButton, view.RestoreButton, view.CheckAllButton }) button.Click += Button_Click;

        Refresh();
    }
    /// <summary>
    /// 削除済み表示の設定に応じて所持品一覧を読み直します。
    /// </summary>
    private void Refresh() => grid.ItemsSource = Services.Db.GetItems(includeDeleted.IsChecked == true);

    /// <summary>
    /// 選択状態に応じて所持品の追加・編集・コピー・論理削除・復旧を実行します。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        string name = ((Button)sender).Name;

        if (name == "CheckAllButton")
        {
            var rows = ((IEnumerable<Item>)grid.ItemsSource).ToList(); bool check = rows.Any(x => !x.IsChecked);
            foreach (var row in rows) row.IsChecked = check; grid.Items.Refresh(); return;
        }

        if (name is "DeleteButton" or "RestoreButton")
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            var targets = ((IEnumerable<Item>)grid.ItemsSource).Where(x => x.IsChecked).ToList();
            if (targets.Count == 0 && grid.SelectedItem is Item selected) targets.Add(selected);
            if (targets.Count == 0) { MessageBox.Show("対象の所持品を選択してください。"); return; }
            bool deleted = name == "DeleteButton";
            if (Ui.Confirm($"{targets.Count}件を{(deleted ? "論理削除" : "復旧")}しますか？\n履歴は保持されます。")) { Services.Db.SetDeleted(targets.Select(x => x.Id), deleted); Refresh(); }
            return;
        }

        Item item;

        if (name == "AddButton") item = new Item();
        else
        {
            if (grid.SelectedItem is not Item selected) { MessageBox.Show("所持品を選択してください。"); return; }
            item = selected.Copy();
            if (name == "EditButton") { item.Id = selected.Id; item.IsDeleted = selected.IsDeleted; item.CreatedDate = selected.CreatedDate; }
        }

        Ui.Show(this, new ItemEditWindow(item)); Refresh();
    });
}
