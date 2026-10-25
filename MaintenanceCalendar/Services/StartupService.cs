using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MaintenanceCalendar;

public static class StartupService
{
    private static string Link => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MaintenanceCalendar.lnk");
    public static bool Registered => File.Exists(Link);
    /// <summary>
    /// このアプリ専用のスタートアップ用ショートカットを登録または解除します。
    /// </summary>
    public static void Set(bool enabled)
    {
        if (!enabled) { if (File.Exists(Link)) File.Delete(Link); }
        else
        {
            object? shell = null, shortcut = null;

            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("ショートカット機能が利用できません。");

                shell = Activator.CreateInstance(type)!;

                shortcut = ((dynamic)shell).CreateShortcut(Link);

                string executable = System.IO.Path.Combine(AppContext.BaseDirectory, "MaintenanceCalendar.exe");

                if (!File.Exists(executable)) throw new FileNotFoundException("MaintenanceCalendar.exeを含むフォルダから登録してください。");
                ((dynamic)shortcut).TargetPath = executable;
                ((dynamic)shortcut).WorkingDirectory = AppContext.BaseDirectory;
                ((dynamic)shortcut).Description = "お手入れカレンダー";
                ((dynamic)shortcut).Save();

            }
            finally
            {
                if (shortcut is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                if (shell is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        LogManager.GetCurrentClassLogger().Info("Startup enabled={0}", enabled);
    }
}
