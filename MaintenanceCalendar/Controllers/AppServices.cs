using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MaintenanceCalendar;

public sealed class AppServices
{
    public static AppServices Current { get; set; } = null!;
    public string Root { get; }
    public string Data => System.IO.Path.Combine(Root, "Data");
    public string Photos => System.IO.Path.Combine(Data, "Photos");
    public string SettingsPath => System.IO.Path.Combine(Data, "settings.json");
    public Database Db { get; }
    public Settings Settings { get; set; }

    /// <summary>
    /// 保存フォルダとDBを初期化し、設定を読み込んで検証・保存します。
    /// </summary>
    public AppServices(string root)
    {
        Root = root; Directory.CreateDirectory(Photos);
        Db = new Database(System.IO.Path.Combine(Data, "calendar.db")); Db.Initialize();
        Settings = Settings.Load(SettingsPath); Settings.Validate(); Settings.Save(SettingsPath);
    }

    /// <summary>
    /// 画像の容量上限を検証し、長辺720px以内のJPEGとして写真フォルダへ取り込みます。
    /// </summary>
    public string ImportPhoto(string path)
    {
        if (new FileInfo(path).Length > 30 * 1024 * 1024) throw new InvalidDataException("写真は30MB以下にしてください。");
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 720; image.UriSource = new Uri(path, UriKind.Absolute); image.EndInit(); image.Freeze();
        BitmapSource source = image;
        if (image.PixelHeight > 720)
            source = new TransformedBitmap(image, new ScaleTransform(720.0 / image.PixelHeight, 720.0 / image.PixelHeight));
        string file = Guid.NewGuid().ToString("N") + ".jpg";
        var encoder = new JpegBitmapEncoder { QualityLevel = 82 }; encoder.Frames.Add(BitmapFrame.Create(source));
        using (var stream = File.Create(System.IO.Path.Combine(Photos, file))) encoder.Save(stream);
        LogManager.GetCurrentClassLogger().Info("Photo imported {0}", file); return file;
    }

    /// <summary>
    /// 管理写真を最大幅120pxで読み込み、未設定または存在しない場合はnullを返します。
    /// </summary>
    public BitmapImage? LoadPhoto(string? file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        string path = System.IO.Path.Combine(Photos, System.IO.Path.GetFileName(file));
        if (!File.Exists(path)) return null;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 120; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image;
    }
}

