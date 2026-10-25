namespace MaintenanceCalendar;

/// <summary>
/// 取得済みの祝日の日付と名称を保持します。
/// </summary>
public sealed class Holiday
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
}
