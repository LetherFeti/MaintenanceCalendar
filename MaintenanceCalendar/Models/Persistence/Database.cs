using Microsoft.EntityFrameworkCore;

namespace MaintenanceCalendar;

/// <summary>
/// 指定したDBファイルに対する所持品・履歴・マスタの検索と更新を提供します。
/// </summary>
public sealed class Database(string path)
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public string Path { get; } = path;
    /// <summary>
    /// このDBファイルに接続する操作単位のEF Coreコンテキストを生成します。
    /// </summary>
    public CalendarDbContext CreateContext() => new(Path);
    /// <summary>
    /// DB版を確認し、新規DBの場合だけテーブルを作成して既存DBを引き継ぎます。
    /// </summary>
    public void Initialize()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        int version = SqliteMaintenance.Version(Path);
        if (version > 1) throw new InvalidDataException("新しい版のDBです。アプリを更新してください。");
        using var db = CreateContext();
        bool created = db.Database.EnsureCreated();
        // Existing version-1 files are used in place, without rebuilding tables.
        if (created || version == 0) SqliteMaintenance.MarkVersion(Path);
        Log.Info("Database initialized (EF Core)");
    }
    /// <summary>
    /// 削除済みを含めるか指定して、メーカー・モデル・ID順に所持品を取得します。
    /// </summary>
    public List<Item> GetItems(bool includeDeleted = false)
    {
        using var db = CreateContext();
        return db.Items.AsNoTracking().Where(x => includeDeleted || !x.IsDeleted)
            .OrderBy(x => x.Manufacturer).ThenBy(x => x.Model).ThenBy(x => x.Id).ToList();
    }
    /// <summary>
    /// 必須項目・間隔・日付を検証し、登録日を保持して所持品を登録または更新します。
    /// </summary>
    public void SaveItem(Item item)
    {
        if (string.IsNullOrWhiteSpace(item.Manufacturer) || string.IsNullOrWhiteSpace(item.Model)) throw new InvalidDataException("メーカーとモデルを入力してください。");
        if (!Intervals.All.Any(x => x.Key == item.IntervalKey)) throw new InvalidDataException("お手入れ間隔が不正です。");
        ValidateItemDates(item);
        using var db = CreateContext();
        item.Manufacturer = item.Manufacturer.Trim(); item.Model = item.Model.Trim();
        if (item.Id == 0) db.Items.Add(item);
        else
        {
            db.Items.Attach(item); db.Entry(item).State = EntityState.Modified;
            db.Entry(item).Property(x => x.CreatedDate).IsModified = false;
        }
        db.SaveChanges(); Log.Info("Item saved Id={0}", item.Id);
    }
    /// <summary>
    /// 指定した所持品の論理削除状態を一括変更し、履歴を保持します。
    /// </summary>
    public void SetDeleted(IEnumerable<long> ids, bool deleted)
    {
        using var db = CreateContext(); var keys = ids.Distinct().ToArray();
        int count = db.Items.Where(x => keys.Contains(x.Id)).ExecuteUpdate(s => s.SetProperty(x => x.IsDeleted, deleted));
        Log.Info("Items deletion flag={0}, count={1}", deleted, count);
    }
    /// <summary>
    /// 指定月と所持品条件に一致する、有効な所持品の作業マークを重複なしで取得します。
    /// </summary>
    public List<CalendarMark> Month(DateTime month, long? itemId)
    {
        using var db = CreateContext(); var start = month.Date; var end = month.AddMonths(1).Date;
        return (from h in db.History.AsNoTracking()
                join i in db.Items on h.ItemId equals i.Id
                where !i.IsDeleted && h.Date >= start && h.Date < end && (itemId == null || h.ItemId == itemId)
                select new { h.Date, h.Work, h.ItemId }).Distinct().ToList().Select(x => new CalendarMark(x.Date, x.Work, x.ItemId)).ToList();
    }
    /// <summary>
    /// 履歴クエリを追跡せずに実行し、表示用の履歴モデルへ変換します。
    /// </summary>
    private static List<HistoryRow> Rows(IQueryable<HistoryEntry> query) => query.AsNoTracking()
        .Select(x => new HistoryRow(x.Date, x.ItemId, x.Work, x.Memo)).ToList();
    /// <summary>
    /// 指定日の使用と手入れの履歴を取得し、所持品指定時はその品だけに絞ります。
    /// </summary>
    public List<HistoryRow> Day(DateTime date, long? id = null)
    {
        using var db = CreateContext(); date = date.Date;
        return Rows(db.History.Where(x => x.Date == date && (id == null || x.ItemId == id)));
    }
    /// <summary>
    /// 指定した所持品の履歴を日付降順・作業種別順で上限件数まで取得します。
    /// </summary>
    public List<HistoryRow> History(long id, int limit = 500) => HistoryPage(id, 0, limit);
    /// <summary>
    /// 指定した所持品の履歴を日付降順・作業種別順でページ取得します。
    /// </summary>
    public List<HistoryRow> HistoryPage(long id, int offset, int limit)
    {
        using var db = CreateContext();
        var query = db.History.Where(x => x.ItemId == id).OrderByDescending(x => x.Date).ThenBy(x => x.Work).Skip(Math.Max(0, offset));
        return Rows(limit < 0 ? query : query.Take(limit));
    }
    /// <summary>
    /// 指定した所持品と作業種別の最終記録日を返し、未登録ならnullを返します。
    /// </summary>
    public DateTime? Last(long id, int work)
    {
        using var db = CreateContext();
        return db.History.Where(x => x.ItemId == id && x.Work == work).Max(x => (DateTime?)x.Date);
    }
    /// <summary>
    /// 日付・作業種別・所持品の有効性を検証し、同日の記録を登録またはメモ更新します。
    /// </summary>
    public void SaveHistory(DateTime date, long id, int work, string memo = "")
    {
        if (date.Year is < 1900 or > 9998) throw new InvalidDataException("作業日は1900～9998年の範囲で指定してください。");
        if (work is not 1 and not 2) throw new InvalidDataException("作業の種類が不正です。");
        using var db = CreateContext(); using var tx = db.Database.BeginTransaction();
        if (!db.Items.Any(x => x.Id == id && !x.IsDeleted)) throw new InvalidDataException("所持品が削除されています。");
        date = date.Date;
        var row = db.History.Find(date, id, work);
        if (row is null) db.History.Add(new HistoryEntry { Date = date, ItemId = id, Work = work, Memo = memo });
        else row.Memo = memo;
        db.SaveChanges(); tx.Commit(); Log.Info("History saved date={0:yyyy-MM-dd}, item={1}, work={2}", date, id, work);
    }
    /// <summary>
    /// 未登録の所持品だけを一括登録し、登録済みの記録とメモは変更しません。
    /// </summary>
    public int SaveMissingHistory(DateTime date, IEnumerable<long> ids, int work, string memo = "")
    {
        if (date.Year is < 1900 or > 9998) throw new InvalidDataException("作業日は1900～9998年の範囲で指定してください。");
        if (work is not 1 and not 2) throw new InvalidDataException("作業の種類が不正です。");
        var keys = ids.Distinct().ToArray();
        using var db = CreateContext(); using var tx = db.Database.BeginTransaction();
        if (db.Items.Count(x => keys.Contains(x.Id) && !x.IsDeleted) != keys.Length)
            throw new InvalidDataException("所持品が削除されています。");
        date = date.Date;
        var registered = db.History.Where(x => x.Date == date && x.Work == work && keys.Contains(x.ItemId)).Select(x => x.ItemId).ToHashSet();
        foreach (long id in keys.Where(x => !registered.Contains(x)))
            db.History.Add(new HistoryEntry { Date = date, ItemId = id, Work = work, Memo = memo });
        int count = db.SaveChanges(); tx.Commit();
        Log.Info("History batch saved date={0:yyyy-MM-dd}, work={1}, count={2}", date, work, count);
        return count;
    }
    /// <summary>
    /// 指定日・所持品・作業種別の記録だけを削除します。
    /// </summary>
    public void DeleteHistory(DateTime date, long id, int work)
    {
        using var db = CreateContext(); date = date.Date;
        db.History.Where(x => x.Date == date && x.ItemId == id && x.Work == work).ExecuteDelete();
        Log.Info("History removed date={0:yyyy-MM-dd}, item={1}, work={2}", date, id, work);
    }
    /// <summary>
    /// 最終手入れ日・購入日・登録日の順に基準日を決め、指定日までに期限が来る所持品を取得します。
    /// </summary>
    public List<DueItem> Due(DateTime today)
    {
        using var db = CreateContext();
        var last = db.History.Where(x => x.Work == 2).GroupBy(x => x.ItemId)
            .Select(g => new { Id = g.Key, Date = g.Max(x => x.Date) }).ToDictionary(x => x.Id, x => x.Date);
        return GetItems().Select(i =>
        {
            DateTime? care = last.TryGetValue(i.Id, out var d) ? d : null;
            var basis = care ?? i.PurchaseDate ?? i.CreatedDate;
            return new DueItem(i, care, basis, Intervals.All.First(x => x.Key == i.IntervalKey).Due(basis));
        }).Where(x => x.DueDate.Date <= today.Date).OrderBy(x => x.DueDate).ToList();
    }
    /// <summary>
    /// 有効な所持品の使用履歴を集計し、使用回数の多い順に返します。
    /// </summary>
    public List<(long Id, long Count)> UsageCounts()
    {
        using var db = CreateContext();
        return (from h in db.History
                join i in db.Items on h.ItemId equals i.Id
                where h.Work == 1 && !i.IsDeleted
                group h by h.ItemId into g
                orderby g.LongCount() descending
                select new { Id = g.Key, Count = g.LongCount() })
            .ToList().Select(x => (x.Id, x.Count)).ToList();
    }
    /// <summary>
    /// 削除済みを含めるか指定して、手入れ概要の定型語句を名前順に取得します。
    /// </summary>
    public List<TemplateRow> Templates(bool all = false)
    {
        using var db = CreateContext();
        return db.CareTemplates.AsNoTracking().Where(x => all || !x.IsDeleted).OrderBy(x => x.Summary)
            .Select(x => new TemplateRow(x.Summary, x.IsDeleted)).ToList();
    }
    /// <summary>
    /// 語句の長さを検証し、手入れ概要の登録または論理削除状態の変更を行います。
    /// </summary>
    public void SaveTemplate(string text, bool deleted)
    {
        text = text.Trim();
        if (text.Length is < 1 or > 50) throw new InvalidDataException("手入れ概要は1～50文字で入力してください。");
        using var db = CreateContext(); using var tx = db.Database.BeginTransaction();
        var row = db.CareTemplates.Find(text);
        if (row is null) db.CareTemplates.Add(new CareTemplate { Summary = text, IsDeleted = deleted });
        else row.IsDeleted = deleted;
        db.SaveChanges(); tx.Commit(); Log.Info("Care template saved deleted={0}", deleted);
    }
    /// <summary>
    /// 保存済みの祝日を、日付をキーとする辞書として取得します。
    /// </summary>
    public Dictionary<DateTime, string> Holidays()
    {
        using var db = CreateContext(); return db.Holidays.AsNoTracking().ToDictionary(x => x.Date, x => x.Name);
    }
    /// <summary>
    /// 空の取得結果を拒否し、祝日データをトランザクション内で一括置換します。
    /// </summary>
    public void ReplaceHolidays(Dictionary<DateTime, string> holidays)
    {
        if (holidays.Count == 0) throw new InvalidDataException("祝日データが空です。");
        using var db = CreateContext(); using var tx = db.Database.BeginTransaction();
        db.Holidays.ExecuteDelete(); db.Holidays.AddRange(holidays.Select(x => new Holiday { Date = x.Key.Date, Name = x.Value }));
        db.SaveChanges(); tx.Commit(); Log.Info("Holidays replaced count={0}", holidays.Count);
    }
    /// <summary>
    /// SQLiteのバックアップAPIで指定先に一貫したDBスナップショットを保存します。
    /// </summary>
    public void Backup(string destination) => SqliteMaintenance.Backup(Path, destination);
    /// <summary>
    /// DB整合性と版番号、所持品・写真パス・定型語句・履歴データの妥当性を検証します。
    /// </summary>
    public void Validate()
    {
        SqliteMaintenance.Validate(Path);
        foreach (var item in GetItems(true))
        {
            ValidateItemDates(item);
            if (!Intervals.All.Any(x => x.Key == item.IntervalKey) || string.IsNullOrWhiteSpace(item.Manufacturer) || string.IsNullOrWhiteSpace(item.Model)) throw new InvalidDataException("所持品データが不正です。");
            if (item.Photo is { } photo && (System.IO.Path.GetFileName(photo) != photo || !Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(photo), "N", out _) || System.IO.Path.GetExtension(photo) != ".jpg")) throw new InvalidDataException("写真のパスが不正です。");
        }
        if (Templates(true).Any(x => x.Summary.Length is < 1 or > 50)) throw new InvalidDataException("手入れ概要が不正です。");
        Holidays(); using var db = CreateContext();
        // Stream through the date converter to reject malformed dates without raw queries.
        foreach (var row in db.History.AsNoTracking().AsEnumerable())
            if (row.Work is not 1 and not 2 || row.Date.Year is < 1900 or > 9998) throw new InvalidDataException("履歴データが不正です。");
    }
    /// <summary>
    /// 購入日または登録日の基準日と登録日が対応年の範囲内にあることを確認します。
    /// </summary>
    private static void ValidateItemDates(Item item)
    {
        var basis = item.PurchaseDate ?? item.CreatedDate;
        if (basis.Year < 1900 || basis.Year > 9998 || item.CreatedDate.Year < 1900 || item.CreatedDate.Year > 9998)
            throw new InvalidDataException("購入日・登録日は1900～9998年の範囲で指定してください。");
    }
}
