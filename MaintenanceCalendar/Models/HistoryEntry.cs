namespace MaintenanceCalendar;

/// <summary>
/// 日付・所持品・作業種別を複合キーとする履歴の永続化モデルです。
/// </summary>
public sealed class HistoryEntry
{
    public DateTime Date { get; set; }
    public long ItemId { get; set; }
    public int Work { get; set; }
    public string Memo { get; set; } = "";
}
