using System.Windows.Data;

namespace MaintenanceCalendar;

public static class Ui
{
    public static AppServices Services => AppServices.Current;
    /// <summary>
    /// 例外をログに記録し、処理失敗とログの確認先を利用者へ表示します。
    /// </summary>
    public static void Error(Exception ex)
    {
        LogManager.GetCurrentClassLogger().Error(ex, "Operation failed");
        MessageBox.Show("処理を完了できませんでした。\n" + ex.Message + "\n詳細はLogsフォルダを確認してください。", "お手入れカレンダー", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// 処理を実行し、発生した例外を共通のエラー表示へ渡します。
    /// </summary>
    public static void Run(Action action) { try { action(); } catch (Exception ex) { Error(ex); } }

    /// <summary>
    /// 確認ダイアログを表示し、利用者が「はい」を選んだか返します。
    /// </summary>
    public static bool Confirm(string message)
    {
        var owner = Application.Current.Windows.Cast<Window>().LastOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        var dialog = new ConfirmWindow(message) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    /// <summary>
    /// 名前・表示文言・クリック処理を設定した共通ボタンを生成します。
    /// </summary>
    public static Button Button(string name, string text, RoutedEventHandler click)
    {
        var b = new Button { Name = name, Content = text }; b.Click += click; return b;
    }

    /// <summary>
    /// 文字サイズ・折り返し・余白を設定した説明用テキストを生成します。
    /// </summary>
    public static TextBlock Text(string text, double size = 13) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 2, 6) };

    /// <summary>
    /// 入力項目のラベルとコントロールを指定したパネルへ順に追加します。
    /// </summary>
    public static void Field(Panel panel, string label, UIElement control) { panel.Children.Add(Text(label)); panel.Children.Add(control); }

    /// <summary>
    /// 見出しとバインド先から、読み取り専用の一覧グリッドを生成します。
    /// </summary>
    public static DataGrid Grid(params (string Header, string Path)[] columns)
    {
        var grid = new DataGrid { IsReadOnly = true, Margin = new Thickness(0, 8, 0, 8) };
        foreach (var c in columns) grid.Columns.Add(new DataGridTextColumn { Header = c.Header, Binding = new Binding(c.Path), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        return grid;
    }

    /// <summary>
    /// 所持品の選択・空欄・初期値・写真プレビューを備えたコンボボックスを生成します。
    /// </summary>
    public static ComboBox ItemCombo(IReadOnlyList<Item> items, long? selected = null, bool blank = true)
    {
        var combo = new ComboBox { DisplayMemberPath = nameof(Item.DisplayName), SelectedValuePath = nameof(Item.Id) };

        var source = new List<Item>(); if (blank) source.Add(new Item()); source.AddRange(items);

        combo.ItemsSource = source; combo.SelectedValue = selected ?? (blank ? 0L : items.FirstOrDefault()?.Id);

        var photos = new Dictionary<string, System.Windows.Media.Imaging.BitmapImage>();

        // 写真を遅延読み込みしてキャッシュし、ツールチップ用の画像を返します。
        object? Preview(Item? item)
        {
            if (string.IsNullOrEmpty(item?.Photo)) return null;
            if (!photos.TryGetValue(item.Photo, out var image))
            {
                image = Services.LoadPhoto(item.Photo); if (image is null) return null; photos[item.Photo] = image;
            }
            return new Image { Source = image, MaxWidth = 120, MaxHeight = 120, Stretch = Stretch.Uniform };
        }

        var style = new Style(typeof(ComboBoxItem)); style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "写真"));

        style.Setters.Add(new EventSetter(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler((s, e) =>
        {
            try { var c = (ComboBoxItem)s; var image = Preview(c.DataContext as Item); if (image is null) e.Handled = true; else c.ToolTip = image; }
            catch (Exception ex) { LogManager.GetCurrentClassLogger().Warn(ex, "Photo preview failed"); e.Handled = true; }
        })));

        combo.ItemContainerStyle = style; combo.ToolTip = "写真";

        combo.ToolTipOpening += (_, e) =>
        {
            if (e.OriginalSource is ComboBoxItem) return;
            try { var image = Preview(combo.SelectedItem as Item); if (image is null) e.Handled = true; else combo.ToolTip = image; }
            catch (Exception ex) { LogManager.GetCurrentClassLogger().Warn(ex, "Photo preview failed"); e.Handled = true; }
        };
        return combo;
    }

    /// <summary>
    /// 所有者を設定して子画面をモーダル表示します。
    /// </summary>
    public static void Show(Window owner, Window dialog) { dialog.Owner = owner; dialog.ShowDialog(); }
}
