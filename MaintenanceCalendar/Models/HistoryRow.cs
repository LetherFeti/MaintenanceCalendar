namespace MaintenanceCalendar;

/// <summary>
/// 履歴画面で表示する日付・所持品・作業種別・メモを保持します。
/// </summary>
public record HistoryRow(DateTime Date, long ItemId, int Work, string Memo)
{
    public string WorkLabel => Work == 1 ? "使用" : "お手入れ";
}
