namespace MaintenanceCalendar;

/// <summary>通常のカレンダー予定と通知設定を保存します。</summary>
public sealed class CalendarEvent
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public bool NotifyPreviousDay { get; set; } = true;
    public bool Notify15Minutes { get; set; } = true;
    public bool Notify5Minutes { get; set; } = true;
    public string Notified { get; set; } = "";
    public string Recurrence { get; set; } = "None";
    public DateTime? RecurrenceEnd { get; set; }
    public DateTime? OriginalStart { get; set; }
    public DateTime SeriesStart { get; set; }
    public bool IsRecurringOccurrence { get; set; }
    public bool NotificationsSuppressed { get; set; }
    public int NotificationMask { get; set; }
    public string RecurrenceLabel => Recurrence switch
    {
        "Weekly" => "毎週", "Biweekly" => "隔週", "Monthly" => "毎月", "Yearly" => "毎年", _ => "繰り返しなし"
    };
}

/// <summary>シリーズの特定開催回に対する変更または取り消しです。</summary>
public sealed class CalendarEventException
{
    public long EventId { get; set; }
    public DateTime OriginalStart { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool? NotifyPreviousDay { get; set; }
    public bool? Notify15Minutes { get; set; }
    public bool? Notify5Minutes { get; set; }
    public bool SuppressNotifications { get; set; }
    public int NotifiedMask { get; set; }
}

/// <summary>表示対象の開催回と、その場で処理する通知タイミングのマスクです。</summary>
public sealed record EventReminderCandidate(CalendarEvent Event, string Timing, int DueMask);
