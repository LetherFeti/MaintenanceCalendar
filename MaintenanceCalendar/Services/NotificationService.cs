using System.Drawing;

namespace MaintenanceCalendar;

public sealed class NotificationService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly System.Windows.Threading.DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly Queue<string> messages = new();
    private readonly Window owner;
    private readonly System.Windows.Forms.ContextMenuStrip trayMenu = new();
    private WindowState restoreState = WindowState.Normal;
    private bool restoring;
    /// <summary>
    /// 通知アイコンとタイマーを生成し、通知から期限一覧を開くイベントを設定します。
    /// </summary>
    public NotificationService(Window owner)
    {
        this.owner = owner;
        restoreState = owner.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        icon = new System.Windows.Forms.NotifyIcon { Text = "お手入れカレンダー", Icon = new Icon($"{System.AppDomain.CurrentDomain.BaseDirectory}favicon.ico"), Visible = true };
        trayMenu.Items.Add("開く", null, (_, _) => owner.Dispatcher.Invoke(Restore));
        trayMenu.Items.Add("終了", null, (_, _) => owner.Dispatcher.Invoke(() =>
        {
            if (owner.IsEnabled && !HasOpenDialogs) owner.Close();
            else Restore();
        }));
        icon.ContextMenuStrip = trayMenu;
        owner.StateChanged += Owner_StateChanged;
        // 所有画面で操作が許可されている場合に、UIスレッドで期限一覧を開きます。
        void OpenDue() => owner.Dispatcher.BeginInvoke(() =>
        {
            Restore();
            if (!HasOpenDialogs && owner is MainWindow { CanOpenDialogs: true }) Ui.Run(() => Ui.Show(owner, new DueWindow()));
        });
        icon.BalloonTipClicked += (_, _) => OpenDue();
        icon.DoubleClick += (_, _) => owner.Dispatcher.Invoke(Restore);
        timer.Tick += (_, _) => DisplayNext();
    }

    private bool HasOpenDialogs => Application.Current.Windows.Cast<Window>().Any(w => w != owner && w.IsVisible && BelongsToOwner(w));

    /// <summary>最小化時にタスクバーから外し、復元時に元の通常・最大化状態を戻します。</summary>
    private void Owner_StateChanged(object? sender, EventArgs e)
    {
        if (restoring) return;
        if (owner.WindowState == WindowState.Minimized)
        {
            owner.ShowInTaskbar = false;
            if (owner is MainWindow main) main.ApplyTaskbarSetting();
        }
        else
        {
            restoreState = owner.WindowState;
            owner.ShowInTaskbar = owner is MainWindow ? AppServices.Current.Settings.ShowInTaskbar : true;
            if (owner is MainWindow main) main.ApplyTaskbarSetting();
        }
    }

    /// <summary>トレイから画面を復元し、編集中の子画面があればその画面を前面へ戻します。</summary>
    public void Restore()
    {
        restoring = true;
        try
        {
            if (owner.WindowState == WindowState.Minimized) owner.WindowState = restoreState;
            owner.ShowInTaskbar = owner is MainWindow ? AppServices.Current.Settings.ShowInTaskbar : true;
            if (owner is MainWindow main) main.ApplyTaskbarSetting();
            var active = Application.Current.Windows.Cast<Window>().LastOrDefault(w => w.IsVisible && w.IsEnabled && BelongsToOwner(w)) ?? owner;
            if (active.WindowState == WindowState.Minimized) active.WindowState = WindowState.Normal;
            active.Activate();
        }
        finally { restoring = false; }
    }

    /// <summary>アプリのメイン画面に所属する子画面か判定します。</summary>
    private bool BelongsToOwner(Window window)
    {
        for (Window? current = window; current != null; current = current.Owner)
            if (current == owner) return true;
        return false;
    }
    /// <summary>
    /// 期限対象数に応じて個別または集約した通知文を用意し、通知を開始します。
    /// </summary>
    public void ShowDue()
    {
        var due = AppServices.Current.Db.Due(DateTime.Today);
        LogManager.GetCurrentClassLogger().Info("Startup notifications due={0}", due.Count);
        if (due.Count >= 3) messages.Enqueue($"お手入れが必要なアイテムが複数あります（{due.Count}件）。\nアプリの「通知」で確認してください。");
        else foreach (var item in due) messages.Enqueue("お手入れが必要なアイテムがあります\n" + item.Name);
        DisplayNext(); if (messages.Count > 0) timer.Start();
    }
    /// <summary>
    /// 待機中の通知を1件表示し、残りがなければ通知タイマーを停止します。
    /// </summary>
    private void DisplayNext()
    {
        if (messages.TryDequeue(out var message)) icon.ShowBalloonTip(7000, "お手入れカレンダー", message, System.Windows.Forms.ToolTipIcon.Info);
        if (messages.Count == 0) timer.Stop();
    }
    /// <summary>
    /// 通知タイマーを停止し、通知アイコンを非表示にしてリソースを解放します。
    /// </summary>
    public void Dispose() { owner.StateChanged -= Owner_StateChanged; timer.Stop(); icon.Visible = false; icon.Dispose(); trayMenu.Dispose(); }
}
