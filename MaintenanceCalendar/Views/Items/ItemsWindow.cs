using Microsoft.Win32;
using System.Windows.Data;

namespace MaintenanceCalendar;

public sealed class ItemsWindow : ToolWindow
{
    private readonly DataGrid grid = Ui.Grid(("メーカー", "Manufacturer"), ("モデル", "Model"), ("色", "Color"),
        ("購入日", "PurchaseDate"), ("間隔", "IntervalLabel"), ("状態", "Status"));

    private readonly CheckBox includeDeleted = new() { Content = "削除済みも表示", Margin = new Thickness(3, 8, 3, 5) };

    /// <summary>
    /// 所持品一覧と選択用チェック欄、登録・編集・削除・復旧の操作を構成します。
    /// </summary>
    public ItemsWindow() : base("所持品マスタ", 920, 570)
    {
        AddTop(Ui.Text("所持品マスタ", 21)); AddTop(Ui.Text("行を選んで編集・コピー。チェックした行はまとめて削除・復旧できます。"));

        AddTop(includeDeleted); includeDeleted.Checked += (_, _) => Ui.Run(Refresh); includeDeleted.Unchecked += (_, _) => Ui.Run(Refresh);

        grid.IsReadOnly = false;

        foreach (var c in grid.Columns) c.IsReadOnly = true;
        grid.Columns.Insert(0, new DataGridCheckBoxColumn { Header = "選択", Binding = new Binding("IsChecked") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = DataGridLength.Auto });

        ((DataGridTextColumn)grid.Columns[4]).Binding.StringFormat = "yyyy/MM/dd";

        Root.Children.Add(grid);

        AddButton("AddButton", "追加", Button_Click); AddButton("EditButton", "編集", Button_Click);

        AddButton("CopyButton", "コピー", Button_Click); AddButton("DeleteButton", "削除", Button_Click);

        AddButton("RestoreButton", "削除解除", Button_Click); AddButton("CheckAllButton", "全選択 / 解除", Button_Click);

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
