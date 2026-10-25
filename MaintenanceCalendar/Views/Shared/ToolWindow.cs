using System.Windows.Data;

namespace MaintenanceCalendar;

public class ToolWindow : Window
{
    protected readonly DockPanel Root = new() { Margin = new Thickness(16) };
    protected readonly WrapPanel Actions = new() { Margin = new Thickness(0, 8, 0, 0) };
    protected AppServices Services => AppServices.Current;
    /// <summary>
    /// タイトル・サイズ・共通レイアウト・表示位置とアニメーションを設定します。
    /// </summary>
    public ToolWindow(string title, double width = 760, double height = 540)
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Title = title; Width = width; Height = height; MinWidth = 360; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Content = Root;
        UiMotion.Attach(this);
        DockPanel.SetDock(Actions, Dock.Bottom); Root.Children.Add(Actions);
        UiChrome.Attach(this);
        StateChanged += (_, _) =>
        {
            if (WindowState != WindowState.Minimized) return;
            Window root = this;
            while (root.Owner != null) root = root.Owner;
            if (root is MainWindow) root.WindowState = WindowState.Minimized;
        };
    }

    /// <summary>
    /// 指定した要素を共通レイアウトの上部領域へ追加します。
    /// </summary>
    protected void AddTop(UIElement element) { DockPanel.SetDock(element, Dock.Top); Root.Children.Add(element); }

    /// <summary>
    /// 指定したクリック処理を持つ共通ボタンを画面下部の操作領域へ追加します。
    /// </summary>
    protected void AddButton(string name, string text, RoutedEventHandler click) => Actions.Children.Add(Ui.Button(name, text, click));
}
