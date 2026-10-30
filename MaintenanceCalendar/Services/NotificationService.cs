using System.Drawing;
using System.Runtime.InteropServices;

namespace MaintenanceCalendar;

public sealed class NotificationService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly System.Windows.Threading.DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(8) };
    private readonly System.Windows.Threading.DispatcherTimer eventTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly Queue<string> messages = new();
    private readonly Queue<PendingEventNotification> eventMessages = new();
    private readonly HashSet<string> pendingEventKeys = [];
    private readonly Window owner;
    private readonly System.Windows.Forms.ContextMenuStrip trayMenu = new();
    private WindowState restoreState = WindowState.Normal;
    private bool restoring;
    private EventNotificationWindow? activeEventWindow;
    private DateTime lastPrunedDate = DateTime.MinValue;
    private sealed record PendingEventNotification(CalendarEvent Event, string Heading, string Timing, int DueMask, bool Startup);
    private enum UserNotificationState { NotPresent = 1, Busy = 2, Fullscreen3D = 3, Presentation = 4, AcceptsNotifications = 5, QuietTime = 6, App = 7 }
    [DllImport("shell32.dll", PreserveSig = true)] private static extern int SHQueryUserNotificationState(out UserNotificationState state);
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
        eventTimer.Tick += (_, _) => CheckCalendarEvents();
        eventTimer.Start();
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
        PruneExpiredEventState();
        var due = AppServices.Current.Db.Due(DateTime.Today);
        LogManager.GetCurrentClassLogger().Info("Startup notifications due={0}", due.Count);
        if (due.Count >= 3) messages.Enqueue($"お手入れが必要なアイテムが複数あります（{due.Count}件）。\nアプリの「通知」で確認してください。");
        else foreach (var item in due) messages.Enqueue("お手入れが必要なアイテムがあります\n" + item.Name);
        EnqueueTomorrowEvents();
        DisplayNext(); if (messages.Count > 0) timer.Start();
        CheckCalendarEvents();
    }
    /// <summary>日付が変わったときに翌日分の予定を追加して、通知を再確認します。</summary>
    public void OnDateChanged()
    {
        PruneExpiredEventState();
        EnqueueTomorrowEvents();
        ShowNextEventWindow();
        CheckCalendarEvents();
    }

    private void EnqueueTomorrowEvents()
    {
        var tomorrow = AppServices.Current.Db.Events(DateTime.Today.AddDays(1), DateTime.Today.AddDays(2), AppServices.Current.Settings.MissingRecurrenceDateBehavior);
        foreach (var entry in tomorrow.Where(x => !x.NotificationsSuppressed)) EnqueueEvent(new(entry, "明日の予定", "", 0, true));
    }
    /// <summary>通知可能な状態なら、予定の通知時刻を確認して独立ウィンドウを表示します。</summary>
    private void CheckCalendarEvents()
    {
        try
        {
            PruneExpiredEventState();
            if (!AcceptsNotificationUi()) return;
            foreach (var candidate in AppServices.Current.Db.EventNotificationsDue(DateTime.Now, AppServices.Current.Settings.MissingRecurrenceDateBehavior))
                EnqueueEvent(new(candidate.Event, "予定のお知らせ", candidate.Timing, candidate.DueMask, false));
            ShowNextEventWindow();
        }
        catch (Exception ex) { LogManager.GetCurrentClassLogger().Warn(ex, "Calendar event notification check failed"); }
    }

    private void PruneExpiredEventState()
    {
        if (lastPrunedDate == DateTime.Today) return;
        AppServices.Current.Db.PruneExpiredEventState(DateTime.Now); lastPrunedDate = DateTime.Today;
    }

    private static bool AcceptsNotificationUi()
    {
        try { return SHQueryUserNotificationState(out var state) >= 0 && state == UserNotificationState.AcceptsNotifications; }
        catch (Exception ex) { LogManager.GetCurrentClassLogger().Warn(ex, "Windows notification state query failed"); return false; }
    }

    private static string EventKey(CalendarEvent entry) => $"{entry.Id}:{(entry.OriginalStart ?? entry.Start):O}";

    private void EnqueueEvent(PendingEventNotification notification)
    {
        string key = EventKey(notification.Event) + (notification.Startup ? ":startup" : ":" + notification.Timing);
        if (pendingEventKeys.Add(key)) eventMessages.Enqueue(notification);
    }

    private void ShowNextEventWindow()
    {
        if (activeEventWindow is not null || eventMessages.Count == 0 || !AcceptsNotificationUi()) return;
        while (eventMessages.TryDequeue(out var pending))
        {
            string occurrenceKey = EventKey(pending.Event);
            string queuedKey = occurrenceKey + (pending.Startup ? ":startup" : ":" + pending.Timing);
            if (pending.Startup && pending.Event.Start.Date != DateTime.Today.AddDays(1)) { pendingEventKeys.Remove(queuedKey); continue; }
            var current = AppServices.Current.Db.Events(pending.Event.Start.Date, pending.Event.Start.Date.AddDays(1), AppServices.Current.Settings.MissingRecurrenceDateBehavior)
                .FirstOrDefault(x => x.Id == pending.Event.Id && x.OriginalStart == pending.Event.OriginalStart);
            if (current is null || current.NotificationsSuppressed) { pendingEventKeys.Remove(queuedKey); continue; }
            if (!pending.Startup)
            {
                var latest = AppServices.Current.Db.EventNotificationsDue(DateTime.Now, AppServices.Current.Settings.MissingRecurrenceDateBehavior)
                    .FirstOrDefault(x => x.Event.Id == current.Id && x.Event.OriginalStart == current.OriginalStart);
                if (latest is null) { pendingEventKeys.Remove(queuedKey); continue; }
                current = latest.Event; pending = pending with { Event = current, Timing = latest.Timing, DueMask = latest.DueMask };
                string latestKey = occurrenceKey + ":" + pending.Timing;
                if (latestKey != queuedKey) { pendingEventKeys.Remove(queuedKey); pendingEventKeys.Add(latestKey); }
                queuedKey = latestKey;
            }
            string displayedKey = queuedKey;
            var window = new EventNotificationWindow(current, pending.Heading, pending.Timing, suppress =>
            {
                if (!suppress) return;
                Ui.Run(() => AppServices.Current.Db.SuppressEventOccurrenceNotifications(current));
                RemoveQueuedOccurrence(current);
            });
            activeEventWindow = window;
            window.Closed += (_, _) =>
            {
                activeEventWindow = null; pendingEventKeys.Remove(displayedKey);
                owner.Dispatcher.BeginInvoke(ShowNextEventWindow);
            };
            window.Show();
            if (!pending.Startup) AppServices.Current.Db.MarkEventReminderDisplayed(current, pending.DueMask);
            return;
        }
    }

    private void RemoveQueuedOccurrence(CalendarEvent entry)
    {
        string occurrenceKey = EventKey(entry);
        var kept = eventMessages.Where(x => EventKey(x.Event) != occurrenceKey).ToList();
        eventMessages.Clear();
        foreach (var notification in kept) eventMessages.Enqueue(notification);
        pendingEventKeys.RemoveWhere(x => x.StartsWith(occurrenceKey + ":", StringComparison.Ordinal));
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
    public void Dispose() { owner.StateChanged -= Owner_StateChanged; timer.Stop(); eventTimer.Stop(); activeEventWindow?.Close(); icon.Visible = false; icon.Dispose(); trayMenu.Dispose(); }
}
