namespace MaintenanceCalendar;

/// <summary>
/// 週数または月数で手入れ間隔を表し、次回の期限を計算します。
/// </summary>
public record Interval(string Key, string Label, int Weeks, int Months)
{
    /// <summary>
    /// 基準日に設定月数または週数を加算して手入れ期限を計算します。
    /// </summary>
    public DateTime Due(DateTime date) => Months > 0 ? date.AddMonths(Months) : date.AddDays(Weeks * 7);
}
