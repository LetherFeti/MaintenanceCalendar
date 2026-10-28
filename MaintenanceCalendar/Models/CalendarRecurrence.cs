namespace MaintenanceCalendar;

/// <summary>定期予定の開催日時を、シリーズの開始日時を基準に算出します。</summary>
public static class CalendarRecurrence
{
    public static IEnumerable<DateTime> Starts(CalendarEvent series, DateTime from, DateTime to, string missingDateBehavior)
    {
        var anchor = series.Start;
        if (series.Recurrence == "None")
        {
            if (anchor >= from && anchor < to) yield return anchor;
            yield break;
        }
        if (series.RecurrenceEnd is { } endDate) to = to < endDate.Date.AddDays(1) ? to : endDate.Date.AddDays(1);
        if (to <= from || to <= anchor) yield break;
        int interval = series.Recurrence == "Biweekly" ? 2 : 1;
        if (series.Recurrence is "Weekly" or "Biweekly")
        {
            DateTime first = from.Date > anchor.Date ? from.Date : anchor.Date;
            for (var day = first; day < to.Date.AddDays(to.TimeOfDay > TimeSpan.Zero ? 1 : 0); day = day.AddDays(1))
            {
                int difference = (day - anchor.Date).Days;
                if (difference >= 0 && difference % (7 * interval) == 0)
                {
                    var start = day + anchor.TimeOfDay;
                    if (start >= from && start < to) yield return start;
                }
            }
            yield break;
        }
        if (series.Recurrence == "Monthly")
        {
            int monthDifference = Math.Max(0, (from.Year - anchor.Year) * 12 + from.Month - anchor.Month);
            int occurrence = (monthDifference + interval - 1) / interval;
            for (int n = occurrence; ; n++)
            {
                int offset = n * interval;
                DateTime month = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(offset);
                if (month >= to) yield break;
                int days = DateTime.DaysInMonth(month.Year, month.Month);
                if (anchor.Day <= days || missingDateBehavior == "LastDay")
                {
                    var start = new DateTime(month.Year, month.Month, Math.Min(anchor.Day, days), anchor.Hour, anchor.Minute, anchor.Second, anchor.Millisecond).AddTicks(anchor.Ticks % TimeSpan.TicksPerMillisecond);
                    if (start >= from && start < to) yield return start;
                }
            }
        }
        if (series.Recurrence == "Yearly")
        {
            int yearDifference = Math.Max(0, from.Year - anchor.Year);
            int occurrence = (yearDifference + interval - 1) / interval;
            for (int n = occurrence; ; n++)
            {
                int year = anchor.Year + n * interval;
                if (year > 9998 || new DateTime(year, 1, 1) >= to) yield break;
                int days = DateTime.DaysInMonth(year, anchor.Month);
                if (anchor.Day <= days || missingDateBehavior == "LastDay")
                {
                    var start = new DateTime(year, anchor.Month, Math.Min(anchor.Day, days), anchor.Hour, anchor.Minute, anchor.Second, anchor.Millisecond).AddTicks(anchor.Ticks % TimeSpan.TicksPerMillisecond);
                    if (start >= from && start < to) yield return start;
                }
            }
        }
    }

    public static CalendarEvent Occurrence(CalendarEvent series, DateTime start) => new()
    {
        Id = series.Id, Title = series.Title, Start = start, End = start + (series.End - series.Start),
        Location = series.Location, Description = series.Description,
        NotifyPreviousDay = series.NotifyPreviousDay, Notify15Minutes = series.Notify15Minutes, Notify5Minutes = series.Notify5Minutes,
        Notified = series.Notified, Recurrence = series.Recurrence, RecurrenceEnd = series.RecurrenceEnd,
        OriginalStart = start, SeriesStart = series.Start,
        IsRecurringOccurrence = series.Recurrence != "None"
    };
}
