namespace MaintenanceCalendar;

/// <summary>
/// 手入れ概要マスタの一覧に表示する語句と削除状態を保持します。
/// </summary>
public record TemplateRow(string Summary, bool IsDeleted)
{
    public string Status => IsDeleted ? "削除済み" : "有効";
}
