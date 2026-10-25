namespace MaintenanceCalendar;

/// <summary>
/// カレンダーに表示する日付・所持品IDと使用・手入れの作業種別を保持します。
/// </summary>
public record CalendarMark(DateTime Date, int Work, long ItemId);
