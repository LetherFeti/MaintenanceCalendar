using Microsoft.Win32;

namespace MaintenanceCalendar;

public sealed class SettingsWindow : ToolWindow
{
    private readonly Dictionary<string, TextBox> colors = [];
    private readonly SettingsWindowView view;
    private readonly SettingsViewModel vm = new();
    private ComboBox shape => view.Shape;
    private ComboBox missingRecurrenceDate => view.MissingRecurrenceDate;
    private CheckBox showInTaskbar => view.ShowInTaskbar;
    private ComboBox sidebarPosition => view.SidebarPosition;
    private TextBlock holidayStatus => view.HolidayStatus;
    private TextBlock startupStatus => view.StartupStatus;
    private TextBlock operationStatus => view.OperationStatus;
    private bool busy;
    private bool closingAfterImport;
    /// <summary>
    /// 現在の設定値・祝日取得状況・データ管理・スタートアップの操作画面を構成します。
    /// </summary>
    public SettingsWindow() : base("設定", 590, 730)
    {
        view = new SettingsWindowView(); view.DataContext = vm; Root.Children.Add(view);
        colors["Theme"] = view.ThemeColor; colors["Background"] = view.BackgroundColor;
        colors["Today"] = view.TodayColor; colors["Use"] = view.UseColor; colors["Care"] = view.CareColor;
        vm.Shapes = ["●", "■", "▼"];
        vm.MissingDateBehaviors = new Dictionary<string, string> { ["LastDay"] = "月末日にする", ["Skip"] = "その回は作らない" };
        vm.SidebarPositions = new Dictionary<string, string> { ["Left"] = "左", ["Right"] = "右", ["Top"] = "上", ["Bottom"] = "下" };
        foreach (var button in new[] { view.SaveButton, view.HolidayButton, view.CloseButton, view.BackupButton, view.ImportButton, view.StartupOnButton, view.StartupOffButton }) button.Click += Button_Click;
        var settings = Services.Settings;
        colors["Theme"].Text = settings.ThemeColor; colors["Background"].Text = settings.BackgroundColor;
        colors["Today"].Text = settings.TodayColor; colors["Use"].Text = settings.UseColor; colors["Care"].Text = settings.CareColor;

        vm.SelectedShape = settings.TodayShape;
        vm.SelectedSidebarPosition = settings.SidebarPosition;
        showInTaskbar.IsChecked = settings.ShowInTaskbar;
        vm.SelectedMissingDateBehavior = settings.MissingRecurrenceDateBehavior;
        view.DataPath.Text = "保存先：" + Services.Data;

        AddDirtyTracking(); UpdateStatus();

        Closing += (_, e) =>
        {
            if (busy) { e.Cancel = true; return; }
            if (!closingAfterImport && HasUnsavedChanges() && !Ui.Confirm("保存されていない設定があります。保存せずに終了しますか？")) e.Cancel = true;
        };
    }

    private void AddDirtyTracking()
    {
        foreach (var field in colors.Values) field.TextChanged += (_, _) => UpdateDirtyStatus();
        shape.SelectionChanged += (_, _) => UpdateDirtyStatus();
        sidebarPosition.SelectionChanged += (_, _) => UpdateDirtyStatus();
        missingRecurrenceDate.SelectionChanged += (_, _) => UpdateDirtyStatus();
        showInTaskbar.Checked += (_, _) => UpdateDirtyStatus();
        showInTaskbar.Unchecked += (_, _) => UpdateDirtyStatus();
    }

    private bool HasUnsavedChanges()
    {
        var current = Services.Settings;
        return colors["Theme"].Text.Trim() != current.ThemeColor || colors["Background"].Text.Trim() != current.BackgroundColor
            || colors["Today"].Text.Trim() != current.TodayColor || colors["Use"].Text.Trim() != current.UseColor
            || colors["Care"].Text.Trim() != current.CareColor || vm.SelectedShape != current.TodayShape
            || vm.SelectedSidebarPosition != current.SidebarPosition || showInTaskbar.IsChecked != current.ShowInTaskbar
            || vm.SelectedMissingDateBehavior != current.MissingRecurrenceDateBehavior;
    }

    private void UpdateDirtyStatus() => operationStatus.Text = HasUnsavedChanges() ? "未保存の設定があります。" : string.Empty;
    /// <summary>
    /// 保存済みの祝日取得日時・件数とスタートアップ登録状態を表示します。
    /// </summary>
    private void UpdateStatus()
    {
        holidayStatus.Text = "最終取得：" + (Services.Settings.HolidayFetchedAt?.ToString("yyyy/MM/dd HH:mm") ?? "未取得") + $" / {Services.Db.Holidays().Count}件";

        startupStatus.Text = StartupService.Registered ? "登録済み（ユーザーのスタートアップフォルダ）" : "未登録";
    }
    /// <summary>
    /// 設定保存・祝日取得・ZIP保存と復元・スタートアップ変更を実行し、処理中の操作を制御します。
    /// </summary>
    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;

        string name = ((Button)sender).Name;

        try
        {
            switch (name)
            {
                case "SaveButton":
                    var s = new Settings
                    {
                        ThemeColor = colors["Theme"].Text.Trim(),
                        BackgroundColor = colors["Background"].Text.Trim(),
                        TodayColor = colors["Today"].Text.Trim(),
                        UseColor = colors["Use"].Text.Trim(),
                        CareColor = colors["Care"].Text.Trim(),
                        TodayShape = vm.SelectedShape!,
                        SidebarPosition = vm.SelectedSidebarPosition!,
                        SidebarShowLabels = Services.Settings.SidebarShowLabels,
                        ShowInTaskbar = showInTaskbar.IsChecked == true,
                        MissingRecurrenceDateBehavior = vm.SelectedMissingDateBehavior!,
                        WindowLeft = Services.Settings.WindowLeft,
                        WindowTop = Services.Settings.WindowTop,
                        WindowWidth = Services.Settings.WindowWidth,
                        WindowHeight = Services.Settings.WindowHeight,
                        HolidayFetchedAt = Services.Settings.HolidayFetchedAt,
                        CalendarView = Services.Settings.CalendarView,
                        WeekScrollOffset = Services.Settings.WeekScrollOffset
                    };
                    s.Save(Services.SettingsPath); Services.Settings = s; s.Apply();
                    foreach (Window window in Application.Current.Windows)
                        if (window is MainWindow main) main.ApplyTaskbarSetting();
                    operationStatus.Text = "設定を保存しました。";
                    LogManager.GetCurrentClassLogger().Info("Appearance settings saved"); break;
                case "HolidayButton":
                    busy = true; Root.IsEnabled = false; operationStatus.Text = "内閣府から祝日を取得しています…";
                    var holidays = await HolidayService.Fetch(); Services.Db.ReplaceHolidays(holidays);
                    Services.Settings.HolidayFetchedAt = DateTime.Now; Services.Settings.Save(Services.SettingsPath);
                    operationStatus.Text = $"祝日を{holidays.Count}件保存しました。"; UpdateStatus(); break;
                case "BackupButton":
                    var save = new SaveFileDialog { Filter = "ZIPバックアップ|*.zip", FileName = "MaintenanceCalendar-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip" };
                    if (save.ShowDialog(this) == true)
                    {
                        busy = true; Root.IsEnabled = false; operationStatus.Text = "バックアップを作成しています…";
                        await System.Threading.Tasks.Task.Run(() => new BackupService(Services).Export(save.FileName));
                        operationStatus.Text = "バックアップを保存しました。";
                    }
                    break;
                case "ImportButton":
                    var open = new OpenFileDialog { Filter = "ZIPバックアップ|*.zip" };
                    if (open.ShowDialog(this) == true && Ui.Confirm("選択したバックアップからDB・写真・設定を復元しますか？\n現在のデータは自動退避されます。"))
                    {
                        busy = true; Root.IsEnabled = false; operationStatus.Text = "バックアップを検証・復元しています…";
                        await System.Threading.Tasks.Task.Run(() => new BackupService(Services).Import(open.FileName));
                        Services.Settings.Apply(); MessageBox.Show("復元しました。設定画面を閉じて再読込します。");
                        busy = false; closingAfterImport = true; Close();
                    }
                    break;
                case "StartupOnButton": StartupService.Set(true); UpdateStatus(); break;
                case "StartupOffButton": StartupService.Set(false); UpdateStatus(); break;
                case "CloseButton": Close(); break;
            }
        }
        catch (Exception ex) { operationStatus.Text = "処理に失敗しました。"; Ui.Error(ex); }
        finally { busy = false; Root.IsEnabled = true; }
    }
}
