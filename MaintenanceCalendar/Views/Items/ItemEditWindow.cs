using Microsoft.Win32;
using System.Windows.Data;

namespace MaintenanceCalendar;

public sealed class ItemEditWindow : ToolWindow
{
    private readonly Item item;
    private readonly TextBox manufacturer = new() { MaxLength = 100 }, model = new() { MaxLength = 150 }, color = new() { MaxLength = 100 };
    private readonly TextBox memo = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MaxLength = 20000 };
    private readonly DatePicker purchase = new() { DisplayDateStart = new DateTime(1900, 1, 1), DisplayDateEnd = new DateTime(9998, 12, 31) };
    private readonly ComboBox interval = new() { ItemsSource = Intervals.All, DisplayMemberPath = "Label", SelectedValuePath = "Key" };
    private readonly Image photo = new() { MaxHeight = 120, MaxWidth = 160, Stretch = Stretch.Uniform };
    private readonly TextBlock photoStatus = Ui.Text("写真をここにドロップ、または選択してください。");
    private string? pendingPhoto;
    private bool clearPhoto;
    private readonly InputSuggestions manufacturerSuggestions, modelSuggestions;

    /// <summary>
    /// 編集対象の値と写真を表示し、入力欄・写真ドロップ・保存操作を構成します。
    /// </summary>
    public ItemEditWindow(Item item) : base(item.Id == 0 ? "所持品の登録" : "所持品の編集", 510, 760)
    {
        this.item = item;

        var panel = new StackPanel(); Root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        manufacturer.Text = item.Manufacturer; model.Text = item.Model; color.Text = item.Color;

        purchase.SelectedDate = item.PurchaseDate; interval.SelectedValue = item.IntervalKey; memo.Text = item.Memo;

        Ui.Field(panel, "メーカー（必須）", manufacturer); Ui.Field(panel, "モデル（必須）", model); Ui.Field(panel, "色", color);
        var catalog = new ItemCatalog(ItemCatalog.FilePath);
        manufacturerSuggestions = new InputSuggestions(manufacturer, catalog.Manufacturers);
        modelSuggestions = new InputSuggestions(model, query => catalog.Models(manufacturer.Text, query), suggestion =>
        {
            if (string.IsNullOrWhiteSpace(manufacturer.Text) && suggestion.Manufacturer != null) manufacturer.Text = suggestion.Manufacturer;
        });
        panel.Children.Add(Ui.Text(catalog.Error ?? "メーカー・モデルの候補：↑↓で選択、Tab / Enterで確定、Escで閉じる。手入力も可能です。", 11));

        Ui.Field(panel, "購入日（空欄可）", purchase); Ui.Field(panel, "お手入れ間隔", interval); Ui.Field(panel, "メモ", memo);

        var drop = new Border { BorderBrush = Brushes.Tan, BorderThickness = new Thickness(1), Padding = new Thickness(12), AllowDrop = true };

        var photoPanel = new StackPanel(); photoPanel.Children.Add(photoStatus); photoPanel.Children.Add(photo); drop.Child = photoPanel; panel.Children.Add(drop);

        photo.Source = Services.LoadPhoto(item.Photo);

        drop.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };

        drop.Drop += (_, e) => Ui.Run(() => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) SelectPhoto(files[0]); });

        var buttons = new WrapPanel(); buttons.Children.Add(Ui.Button("PhotoButton", "写真を選択", Button_Click)); buttons.Children.Add(Ui.Button("ClearPhotoButton", "写真を外す", Button_Click)); panel.Children.Add(buttons);

        AddButton("SaveButton", "保存", Button_Click); AddButton("CancelButton", "キャンセル", Button_Click);
    }
    /// <summary>
    /// 画像の拡張子と容量を検証し、保存前の写真プレビューを更新します。
    /// </summary>
    private void SelectPhoto(string path)
    {
        if (!new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant())) throw new InvalidDataException("JPG/PNG/BMP/GIFを選択してください。");

        if (new FileInfo(path).Length > 30 * 1024 * 1024) throw new InvalidDataException("写真は30MB以下にしてください。");

        var image = new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;

        image.DecodePixelWidth = 120; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();

        pendingPhoto = path; clearPhoto = false; photo.Source = image; photoStatus.Text = System.IO.Path.GetFileName(path);
    }
    /// <summary>
    /// 写真の選択・解除と所持品の入力保存、または編集のキャンセルを行います。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e) => Ui.Run(() =>
    {
        switch (((Button)sender).Name)
        {
            case "PhotoButton":
                var dialog = new OpenFileDialog { Filter = "写真|*.jpg;*.jpeg;*.png;*.bmp;*.gif" };
                if (dialog.ShowDialog(this) == true) SelectPhoto(dialog.FileName); break;
            case "ClearPhotoButton": pendingPhoto = null; clearPhoto = true; photo.Source = null; photoStatus.Text = "写真なし"; break;
            case "SaveButton":
                if (string.IsNullOrWhiteSpace(manufacturer.Text) || string.IsNullOrWhiteSpace(model.Text)) { MessageBox.Show("メーカーとモデルを入力してください。"); return; }
                item.Manufacturer = manufacturer.Text.Trim(); item.Model = model.Text.Trim(); item.Color = color.Text;
                item.PurchaseDate = purchase.SelectedDate; item.IntervalKey = (string)interval.SelectedValue; item.Memo = memo.Text;
                if (pendingPhoto is not null) item.Photo = Services.ImportPhoto(pendingPhoto); else if (clearPhoto) item.Photo = null;
                Services.Db.SaveItem(item); DialogResult = true; break;
            case "CancelButton": DialogResult = false; break;
        }
    });
}
