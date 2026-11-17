using Microsoft.Win32;

namespace MaintenanceCalendar;

public sealed class ItemEditWindow : ToolWindow
{
    private readonly Item item;
    private readonly ItemEditWindowView view;
    private TextBox manufacturer => view.Manufacturer;
    private TextBox model => view.Model;
    private TextBox color => view.Color;
    private TextBox memo => view.Memo;
    private DatePicker purchase => view.Purchase;
    private ComboBox interval => view.Interval;
    private Image photo => view.Photo;
    private TextBlock photoStatus => view.PhotoStatus;
    private string? pendingPhoto;
    private bool clearPhoto;
    private readonly InputSuggestions manufacturerSuggestions, modelSuggestions;

    /// <summary>
    /// 編集対象の値と写真を表示し、入力欄・写真ドロップ・保存操作を構成します。
    /// </summary>
    public ItemEditWindow(Item item) : base(item.Id == 0 ? "所持品の登録" : "所持品の編集", 510, 760)
    {
        this.item = item;
        view = new ItemEditWindowView(); Root.Children.Add(view);
        interval.ItemsSource = Intervals.All;

        manufacturer.Text = item.Manufacturer; model.Text = item.Model; color.Text = item.Color;

        purchase.SelectedDate = item.PurchaseDate; interval.SelectedValue = item.IntervalKey; memo.Text = item.Memo;

        var catalog = new ItemCatalog(ItemCatalog.FilePath);
        manufacturerSuggestions = new InputSuggestions(manufacturer, catalog.Manufacturers);
        modelSuggestions = new InputSuggestions(model, query => catalog.Models(manufacturer.Text, query), suggestion =>
        {
            if (string.IsNullOrWhiteSpace(manufacturer.Text) && suggestion.Manufacturer != null) manufacturer.Text = suggestion.Manufacturer;
        });
        view.CatalogHelp.Text = catalog.Error ?? "メーカー・モデルの候補：↑↓で選択、Tab / Enterで確定、Escで閉じる。手入力も可能です。";

        photo.Source = Services.LoadPhoto(item.Photo);

        view.PhotoDrop.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };

        view.PhotoDrop.Drop += (_, e) => Ui.Run(() => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) SelectPhoto(files[0]); });
        view.PhotoButton.Click += Button_Click; view.ClearPhotoButton.Click += Button_Click;

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
