using NLog.Config;
using NLog.Targets;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace MaintenanceCalendar
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private Mutex? instance;

        /// <summary>
        /// ログと例外処理を初期化し、多重起動を防いで設定を適用したメイン画面を開きます。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var config = new LoggingConfiguration();
            var file = new FileTarget("file")
            {
                FileName = System.IO.Path.Combine(AppContext.BaseDirectory, "Logs", "calendar-${shortdate}.log"),
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}",
                ArchiveAboveSize = 5 * 1024 * 1024, MaxArchiveDays = 30, MaxArchiveFiles = 60
            };
            config.AddRule(LogLevel.Info, LogLevel.Fatal, file); LogManager.Configuration = config;
            DispatcherUnhandledException += (_, args) => { Ui.Error(args.Exception); args.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += (_, args) => LogManager.GetCurrentClassLogger().Fatal(args.ExceptionObject as Exception, "Unhandled exception");
            try
            {
                instance = new Mutex(true, "Local\\MaintenanceCalendar-60A97C34", out bool created);
                if (!created) { MessageBox.Show("お手入れカレンダーは既に起動しています。"); Shutdown(); return; }
                LogManager.GetCurrentClassLogger().Info("Application starting");
                AppServices.Current = new AppServices(AppContext.BaseDirectory);
                AppServices.Current.Settings.Apply(); MainWindow = new MainWindow(); MainWindow.Show();
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Fatal(ex, "Startup failed");
                MessageBox.Show("起動できませんでした。書き込み可能なフォルダに配置してください。\n" + ex.Message, "お手入れカレンダー"); Shutdown(1);
            }
        }
        /// <summary>
        /// 終了コードを記録し、多重起動用のMutexとログ出力を終了します。
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            LogManager.GetCurrentClassLogger().Info("Application exiting code={0}", e.ApplicationExitCode);
            instance?.Dispose(); LogManager.Shutdown(); base.OnExit(e);
        }
    }

}
