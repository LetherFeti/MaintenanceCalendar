namespace MaintenanceCalendar;

/// <summary>アプリ共通のテーマに合わせた確認ダイアログです。</summary>
internal sealed class ConfirmWindow : ToolWindow
{
    public ConfirmWindow(string message) : base("確認", 430, 220)
    {
        var view = new ConfirmWindowView { DataContext = message };
        view.Message.Text = message;
        Root.Children.Add(view);
        AddButton("YesButton", "はい", (_, _) => DialogResult = true);
        AddButton("NoButton", "いいえ", (_, _) => DialogResult = false);
    }
}
