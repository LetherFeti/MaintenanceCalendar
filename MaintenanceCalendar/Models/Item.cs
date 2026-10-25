namespace MaintenanceCalendar;

/// <summary>
/// メーカー・購入日・写真・手入れ間隔など、所持品の情報を保持します。
/// </summary>
public sealed class Item
{
    public long Id { get; set; }
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string Color { get; set; } = "";
    public DateTime? PurchaseDate { get; set; }
    public string IntervalKey { get; set; } = "M1";
    public string Memo { get; set; } = "";
    public string? Photo { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Today;
    public bool IsChecked { get; set; }
    public string DisplayName => $"{Manufacturer} {Model}".Trim();
    public string Status => IsDeleted ? "削除済み" : "有効";
    public string IntervalLabel => Intervals.All.First(x => x.Key == IntervalKey).Label;
    /// <summary>
    /// ID・登録日・削除状態を引き継がず、所持品の入力内容と写真を複製します。
    /// </summary>
    public Item Copy() => new() { Manufacturer = Manufacturer, Model = Model, Color = Color,
        PurchaseDate = PurchaseDate, IntervalKey = IntervalKey, Memo = Memo, Photo = Photo };
}
