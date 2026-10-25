namespace MaintenanceCalendar;

/// <summary>
/// 手入れメモに追加する定型語句と論理削除状態を保持します。
/// </summary>
public sealed class CareTemplate
{
    public string Summary { get; set; } = "";
    public bool IsDeleted { get; set; }
}
