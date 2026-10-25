using System.Windows.Media.Animation;

namespace MaintenanceCalendar;

internal static class UiMotion
{
    /// <summary>
    /// 画面の読み込み完了時に、コンテンツをフェード表示する処理を登録します。
    /// </summary>
    public static void Attach(Window window) => window.Loaded += (_, _) =>
    {
        if (window.Content is FrameworkElement content) FadeIn(content);
    };

    /// <summary>
    /// Windowsでアニメーションが有効な場合に、読み込み済み要素を180msでフェード表示します。
    /// </summary>
    public static void FadeIn(FrameworkElement content)
    {
        if (!SystemParameters.ClientAreaAnimation || !content.IsLoaded) return;
        content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
    }
}
