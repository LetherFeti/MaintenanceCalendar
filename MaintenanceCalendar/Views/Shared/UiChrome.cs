using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MaintenanceCalendar;

/// <summary>テーマに追従するウィンドウ操作領域を、常時表示または上端ホバーで表示します。</summary>
internal static class UiChrome
{
    /// <summary>標準のサイズ変更・システムメニューを保ったままタイトル領域を置き換えます。</summary>
    public static void Attach(Window window, bool autoHide = false)
    {
        if (window.Content is not UIElement content) return;
        window.Content = null; // 既存コンテンツを論理ツリーから外してから、新しい枠へ移します。
        window.WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false
        });
        var layer = new Grid();
        var body = new Border { Padding = new Thickness(0, autoHide ? 12 : 44, 0, 0), Child = content };
        layer.Children.Add(body);
        var edge = new Border { Height = 12, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent };
        layer.Children.Add(edge);
        var title = new Border { VerticalAlignment = VerticalAlignment.Top, Visibility = autoHide ? Visibility.Hidden : Visibility.Visible, Padding = new Thickness(10, 4, 4, 4) };
        title.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
        var bar = new DockPanel(); title.Child = bar;
        var commands = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(commands, Dock.Right); bar.Children.Add(commands);
        void Command(string label, string hint, Action action)
        {
            var button = new Button { Content = label, ToolTip = hint, Padding = new Thickness(10, 3, 10, 3), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            button.SetResourceReference(Control.ForegroundProperty, "AccentTextBrush");
            button.Click += (_, _) => action(); commands.Children.Add(button);
        }
        Command("−", "最小化", () => SystemCommands.MinimizeWindow(window));
        Command("□", "最大化 / 元に戻す", () =>
        {
            if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
            else SystemCommands.MaximizeWindow(window);
        });
        Command("×", "閉じる", window.Close);
        var caption = new TextBlock { Text = "◷  " + window.Title, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeights.SemiBold };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "AccentTextBrush"); bar.Children.Add(caption);
        layer.Children.Add(title);
        var frame = new Border { BorderThickness = new Thickness(1), Child = layer };
        frame.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); window.Content = frame;
        if (!autoHide)
        {
            // Windows 11のネイティブ角丸を使い、透過ウィンドウによる描画・サイズ変更の制約を避けます。
            window.SourceInitialized += (_, _) =>
            {
                int preference = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 33, ref preference, sizeof(int));
            };
        }
        bool keyboardReveal = false;
        var hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        hide.Tick += (_, _) =>
        {
            if (!autoHide) { hide.Stop(); return; }
            if (title.IsMouseOver || edge.IsMouseOver || (keyboardReveal && title.IsKeyboardFocusWithin)) return;
            hide.Stop(); title.Visibility = Visibility.Hidden;
        };
        void Reveal() { hide.Stop(); title.Visibility = Visibility.Visible; UiMotion.FadeIn(title); }
        edge.MouseEnter += (_, _) => { keyboardReveal = false; Reveal(); };
        title.MouseEnter += (_, _) => hide.Stop();
        title.MouseLeave += (_, _) => { if (autoHide) hide.Start(); };
        edge.MouseLeave += (_, _) => { if (autoHide) hide.Start(); };
        title.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
            if (e.ClickCount == 2)
            {
                if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
                else SystemCommands.MaximizeWindow(window);
            }
            else if (e.LeftButton == MouseButtonState.Pressed) window.DragMove();
        };
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Handled) return;
            if (e.Key == Key.F6) { keyboardReveal = true; Reveal(); ((Button)commands.Children[0]).Focus(); e.Handled = true; }
            if (autoHide && e.Key == Key.Escape && title.IsVisible) { title.Visibility = Visibility.Hidden; content.Focus(); }
        };
        window.Closed += (_, _) => hide.Stop();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
