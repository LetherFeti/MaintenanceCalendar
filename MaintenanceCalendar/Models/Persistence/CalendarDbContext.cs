using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MaintenanceCalendar;

/// <summary>
/// 指定したSQLiteファイルに接続し、既存形式に対応するEF Coreモデルを提供します。
/// </summary>
public sealed class CalendarDbContext(string path) : DbContext
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<HistoryEntry> History => Set<HistoryEntry>();
    public DbSet<CareTemplate> CareTemplates => Set<CareTemplate>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    /// <summary>
    /// DBファイル・外部キー検査・タイムアウトを指定してSQLite接続を構成します。
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(
        new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, ForeignKeys = true, DefaultTimeout = 5 }.ToString());
    /// <summary>
    /// 既存DBの列順・日付形式・主キー・制約・索引に対応するマッピングを定義します。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder model)
    {
        // Preserve the original date-only TEXT format and composite keys.
        var date = new ValueConverter<DateTime, string>(
            v => v.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            v => DateTime.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        var item = model.Entity<Item>();
        item.ToTable("Items"); item.HasKey(x => x.Id);
        string[] columns = ["Id", "Manufacturer", "Model", "Color", "PurchaseDate", "IntervalKey", "Memo", "Photo", "IsDeleted", "CreatedDate"];
        for (int n = 0; n < columns.Length; n++) item.Property(columns[n]).HasColumnOrder(n);
        item.Ignore(x => x.IsChecked); item.Ignore(x => x.DisplayName);
        item.Ignore(x => x.Status); item.Ignore(x => x.IntervalLabel);
        item.Property(x => x.CreatedDate).HasConversion(date);
        item.Property(x => x.PurchaseDate).HasConversion(date);
        item.Property(x => x.IsDeleted).HasDefaultValue(false);
        var history = model.Entity<HistoryEntry>();
        history.ToTable("History", t => t.HasCheckConstraint("CK_History_Work", "Work IN (1,2)"));
        history.HasKey(x => new { x.Date, x.ItemId, x.Work });
        history.Property(x => x.Date).HasConversion(date);
        history.Property(x => x.Memo).HasDefaultValue("");
        history.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        history.HasIndex(x => new { x.ItemId, x.Work, x.Date }).IsDescending(false, false, true).HasDatabaseName("IX_History_ItemWorkDate");
        history.HasIndex(x => new { x.Work, x.ItemId, x.Date }).IsDescending(false, false, true).HasDatabaseName("IX_History_WorkItemDate");
        var template = model.Entity<CareTemplate>();
        template.ToTable("CareTemplates", t => t.HasCheckConstraint("CK_CareTemplates_Summary", "length(Summary) BETWEEN 1 AND 50"));
        template.HasKey(x => x.Summary); template.Property(x => x.IsDeleted).HasDefaultValue(false);
        var holiday = model.Entity<Holiday>();
        holiday.ToTable("Holidays"); holiday.HasKey(x => x.Date); holiday.Property(x => x.Date).HasConversion(date);
    }
}
