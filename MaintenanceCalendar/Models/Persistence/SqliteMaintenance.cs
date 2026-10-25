using Microsoft.Data.Sqlite;

namespace MaintenanceCalendar;

// SQLite file maintenance only; application queries are all handled by EF Core.
internal static class SqliteMaintenance
{
    /// <summary>
    /// プーリングを無効にして、外部キー検査を有効にしたSQLite接続を開きます。
    /// </summary>
    private static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Pooling = false, ForeignKeys = true, DefaultTimeout = 5 }.ToString());
        db.Open(); return db;
    }
    /// <summary>
    /// SQLiteファイルに記録されたスキーマ版番号を取得します。
    /// </summary>
    public static int Version(string path)
    {
        using var db = Open(path); using var command = db.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(command.ExecuteScalar());
    }
    /// <summary>
    /// SQLiteファイルのスキーマ版番号を対応版の1として記録します。
    /// </summary>
    public static void MarkVersion(string path)
    {
        using var db = Open(path); using var command = db.CreateCommand();
        command.CommandText = "PRAGMA user_version=1"; command.ExecuteNonQuery();
    }
    /// <summary>
    /// SQLiteの整合性・対応版番号・外部キーの参照整合性を検証します。
    /// </summary>
    public static void Validate(string path)
    {
        using var db = Open(path); using var command = db.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        if (!Equals(command.ExecuteScalar(), "ok")) throw new InvalidDataException("SQLite整合性チェックに失敗しました。");
        if (Version(path) != 1) throw new InvalidDataException("対応していないDB版です。");
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = command.ExecuteReader();
        if (reader.Read()) throw new InvalidDataException("履歴の参照先が不正です。");
    }
    /// <summary>
    /// SQLiteのバックアップAPIでDBを指定ファイルへ複製します。
    /// </summary>
    public static void Backup(string path, string destination)
    {
        using var source = Open(path); using var target = Open(destination);
        source.BackupDatabase(target);
    }
}
