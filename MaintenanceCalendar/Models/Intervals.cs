namespace MaintenanceCalendar;

/// <summary>
/// 画面と入力検証で共通利用する手入れ間隔の選択肢を定義します。
/// </summary>
public static class Intervals
{
    public static readonly Interval[] All = [new("W1", "1週間", 1, 0), new("W2", "2週間", 2, 0),
        new("W3", "3週間", 3, 0), new("M1", "1か月", 0, 1), new("M2", "2か月", 0, 2),
        new("M3", "3か月", 0, 3), new("M4", "4か月", 0, 4), new("M5", "5か月", 0, 5),
        new("M6", "半年", 0, 6), new("M12", "1年", 0, 12)];
}
