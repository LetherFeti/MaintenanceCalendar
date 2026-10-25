namespace MaintenanceCalendar;

/// <summary>アプリ共通のテーマに合わせた確認ダイアログです。</summary>
internal sealed class ConfirmWindow : ToolWindow
{
    public ConfirmWindow(string message) : base("確認", 430, 220)
    {
        Root.Children.Add(Ui.Text(message, 15));
        AddButton("YesButton", "はい", (_, _) => DialogResult = true);
        AddButton("NoButton", "いいえ", (_, _) => DialogResult = false);
    }
}
