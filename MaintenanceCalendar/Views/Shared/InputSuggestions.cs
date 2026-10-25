using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MaintenanceCalendar;

/// <summary>自由入力を保ち、上下キー・Tab・EnterでCSVの候補を確定する入力補助です。</summary>
internal sealed class InputSuggestions
{
    private readonly TextBox input;
    private readonly Func<string, IEnumerable<InputSuggestion>> find;
    private readonly Action<InputSuggestion>? accepted;
    private readonly ListBox list = new() { DisplayMemberPath = "Label", MaxHeight = 190, Focusable = false, BorderThickness = new Thickness(0) };
    private readonly Popup popup;
    private bool committing;

    /// <summary>入力欄の直下に候補を表示し、キーボード・マウス操作を接続します。</summary>
    public InputSuggestions(TextBox input, Func<string, IEnumerable<InputSuggestion>> find, Action<InputSuggestion>? accepted = null)
    {
        this.input = input; this.find = find; this.accepted = accepted;
        list.SetResourceReference(Control.BackgroundProperty, "CreamBrush");
        list.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(UIElement.FocusableProperty, false));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        list.ItemContainerStyle = itemStyle;
        var border = new Border { Child = list, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(3) };
        border.SetResourceReference(Border.BackgroundProperty, "CreamBrush"); border.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        popup = new Popup { PlacementTarget = input, Placement = PlacementMode.Bottom, AllowsTransparency = true, StaysOpen = true, Focusable = false, Child = border };
        input.TextChanged += (_, _) => { if (!committing) Refresh(); };
        input.GotKeyboardFocus += (_, _) => Refresh();
        input.LostKeyboardFocus += (_, _) => { if (!list.IsMouseOver) popup.IsOpen = false; };
        input.PreviewKeyDown += OnKeyDown;
        list.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) is ListBoxItem { Content: InputSuggestion suggestion })
            { Commit(suggestion); input.Focus(); e.Handled = true; }
        };
        input.Unloaded += (_, _) => popup.IsOpen = false;
        input.Loaded += (_, _) =>
        {
            var window = Window.GetWindow(input);
            window.Deactivated += (_, _) => popup.IsOpen = false;
            window.LocationChanged += (_, _) => popup.IsOpen = false;
            window.PreviewMouseDown += (_, _) => { if (!input.IsMouseOver && !list.IsMouseOver) popup.IsOpen = false; };
        };
    }

    /// <summary>入力に一致する候補だけを表示し、入力中の文字列自体は変更しません。</summary>
    private void Refresh()
    {
        if (!input.IsKeyboardFocused) return;
        var candidates = find(input.Text).Take(100).ToList();
        list.ItemsSource = candidates; list.SelectedIndex = candidates.Count > 0 ? 0 : -1;
        list.MinWidth = Math.Max(100, input.ActualWidth - 8);
        popup.IsOpen = candidates.Count > 0;
    }

    /// <summary>候補の移動・確定・取消を処理し、Tabのフォーカス移動も維持します。</summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!popup.IsOpen)
        {
            if (e.Key == Key.Down) { Refresh(); e.Handled = popup.IsOpen; }
            return;
        }
        if (e.Key is Key.Down or Key.Up)
        {
            list.SelectedIndex = Math.Clamp(list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, list.Items.Count - 1);
            list.ScrollIntoView(list.SelectedItem); e.Handled = true;
        }
        else if (e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; }
        else if (e.Key is Key.Tab or Key.Enter)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && list.SelectedItem is InputSuggestion suggestion) Commit(suggestion);
            popup.IsOpen = false; e.Handled = e.Key == Key.Enter;
        }
    }

    /// <summary>選択した候補を入力し、必要ならメーカーも補完します。</summary>
    private void Commit(InputSuggestion suggestion)
    {
        committing = true;
        try { input.Text = suggestion.Value; input.CaretIndex = input.Text.Length; accepted?.Invoke(suggestion); }
        finally { committing = false; popup.IsOpen = false; }
    }
}
