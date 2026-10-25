using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MaintenanceCalendar;

/// <summary>
/// 指定したアプリケーションのDB・設定・写真をZIP形式で保存・復元します。
/// </summary>
public sealed class BackupService(AppServices services)
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// DB・設定・写真を作業フォルダにまとめ、一時ZIPの作成後に指定先へ保存します。
    /// </summary>
    public void Export(string zipPath)
    {
        string stage = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MaintenanceCalendar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(System.IO.Path.Combine(stage, "Data", "Photos"));
        string tempZip = zipPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string data = System.IO.Path.Combine(stage, "Data");
            services.Db.Backup(System.IO.Path.Combine(data, "calendar.db"));
            services.Settings.Save(System.IO.Path.Combine(data, "settings.json"));
            foreach (var file in Directory.EnumerateFiles(services.Photos, "*.jpg")) File.Copy(file, System.IO.Path.Combine(data, "Photos", System.IO.Path.GetFileName(file)));
            ZipFile.CreateFromDirectory(stage, tempZip); File.Move(tempZip, zipPath, true);
            Log.Info("Backup exported");
        }
        finally { Directory.Delete(stage, true); if (File.Exists(tempZip)) File.Delete(tempZip); }
    }

    /// <summary>
    /// ZIPのパス・サイズ・DB・設定・写真を検証し、現在のデータを退避して復元します。
    /// </summary>
    public void Import(string zipPath)
    {
        string stage = System.IO.Path.Combine(services.Root, ".restore-" + Guid.NewGuid().ToString("N"));

        string previous = System.IO.Path.Combine(services.Root, "Data-before-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);

        Directory.CreateDirectory(stage);

        bool moved = false;

        try
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count > 20000 || archive.Entries.Sum(e => e.Length) > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("バックアップの展開サイズまたはファイル数が上限を超えています。");

                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var e in archive.Entries)
                {
                    string name = e.FullName.Replace('\\', '/');

                    if (name.EndsWith('/')) { if (name is "Data/" or "Data/Photos/") continue; throw new InvalidDataException("不正なフォルダです。"); }

                    bool photo = name.StartsWith("Data/Photos/", StringComparison.Ordinal) && name.Count(c => c == '/') == 2 &&
                        Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(name), "N", out _) && name.EndsWith(".jpg", StringComparison.Ordinal);

                    if ((name != "Data/calendar.db" && name != "Data/settings.json" && !photo) || !names.Add(name)) throw new InvalidDataException("バックアップに不正なパスまたは重複があります。");

                    string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(stage, name));
                    if (!target.StartsWith(stage + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("展開先が不正です。");

                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!); e.ExtractToFile(target);
                }
            }

            string incoming = System.IO.Path.Combine(stage, "Data");

            if (!File.Exists(System.IO.Path.Combine(incoming, "calendar.db")) || !File.Exists(System.IO.Path.Combine(incoming, "settings.json"))) throw new InvalidDataException("DBまたは設定がありません。");

            var settings = Settings.Load(System.IO.Path.Combine(incoming, "settings.json")); settings.Validate();

            var database = new Database(System.IO.Path.Combine(incoming, "calendar.db")); database.Validate();

            //foreach (var item in database.GetItems(true))
            //{
            //    if (item.Photo is { } photo && !File.Exists(System.IO.Path.Combine(incoming, "Photos", photo))) throw new InvalidDataException("参照している写真がありません。");
            //}

            Directory.CreateDirectory(System.IO.Path.Combine(incoming, "Photos"));

            foreach (string file in Directory.EnumerateFiles(System.IO.Path.Combine(incoming, "Photos")))
            {
                using var stream = File.OpenRead(file);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.None);
                if (decoder is not JpegBitmapDecoder || decoder.Frames.Count != 1 || decoder.Frames[0].PixelWidth > 720 || decoder.Frames[0].PixelHeight > 720)
                    throw new InvalidDataException("バックアップの写真形式またはサイズが不正です。");
            }

            Directory.Move(services.Data, previous); moved = true;

            Directory.Move(incoming, services.Data);

            services.Settings = settings;

            Log.Info("Backup imported; previous data retained at {0}", previous);

        }
        catch
        {
            if (moved)
            {
                if (Directory.Exists(services.Data)) Directory.Delete(services.Data, true);
                Directory.Move(previous, services.Data);
            }
            throw;
        }
        finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
    }
}

