namespace MaintenanceCalendar;

/// <summary>
/// 手入れ期限の対象品と、最終手入れ日・計算基準日・期限を保持します。
/// </summary>
public record DueItem(Item Item, DateTime? LastCare, DateTime BaseDate, DateTime DueDate)
{
    public string Name => Item.DisplayName;
    public string LastCareText => LastCare?.ToString("yyyy/MM/dd") ?? "未登録";
    public string DueText => DueDate.ToString("yyyy/MM/dd");
    public string BaseText => BaseDate.ToString("yyyy/MM/dd");
}
