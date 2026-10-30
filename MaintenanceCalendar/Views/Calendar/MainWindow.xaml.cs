using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace MaintenanceCalendar;

public partial class MainWindow : Window
{
    private DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<Item> items = [];
    private ComboBox combo = null!;
    private Dictionary<DateTime, string> holidays = [];
    private bool locked;
    private int dueCount;
    private int todayEventCount;
    private NotificationService? notifications;
    private readonly System.Windows.Threading.DispatcherTimer midnight = new() { Interval = TimeSpan.FromMinutes(1) };
    private DateTime displayedToday = DateTime.Today;
    private AppServices Services => AppServices.Current;
    public bool CanOpenDialogs => IsEnabled && !locked;
    /// <summary>保存済みのAlt+Tab表示設定をメイン画面へ反映します。</summary>
    public void ApplyTaskbarSetting()
    {
        bool show = WindowState != WindowState.Minimized && Services.Settings.ShowInTaskbar;
        ShowInTaskbar = show;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        long style = GetWindowLongPtr(hwnd, -20).ToInt64();
        const long ToolWindow = 0x00000080, AppWindow = 0x00040000;
        style = show ? (style & ~ToolWindow) | AppWindow : (style & ~AppWindow) | ToolWindow;
        SetWindowLongPtr(hwnd, -20, new IntPtr(style));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0027);
    }
    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));
    private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value) => IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    /// <summary>
    /// メイン画面を初期化し、画面表示・通知・日付更新・誤クリック予防のイベントを設定します。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        UiChrome.Attach(this, autoHide: false);
        MenuItems.Icon = UiIcon.Create("Items"); MenuTemplates.Icon = UiIcon.Create("Care");
        MenuHistory.Icon = UiIcon.Create("History"); MenuDashboard.Icon = UiIcon.Create("Dashboard");
        MenuSettings.Icon = UiIcon.Create("Settings");
        MenuLock.Icon = UiIcon.Create("Lock"); MenuExit.Icon = UiIcon.Create("Exit");
        RenderSidebar();
        UiMotion.Attach(this);
        SourceInitialized += (_, _) => { RestorePlacement(); HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowProc); ApplyTaskbarSetting(); };
        Loaded += (_, _) => Ui.Run(() => { RefreshData(); notifications = new NotificationService(this); notifications.ShowDue(); });
        Closed += (_, _) => { midnight.Stop(); notifications?.Dispose(); };
        Closing += (_, _) => Ui.Run(SavePlacement);
        midnight.Tick += (_, _) =>
        {
            DateTime today = DateTime.Today;
            if (today == displayedToday) return;
            DateTime previousToday = displayedToday;
            displayedToday = today;
            Ui.Run(() =>
            {
                if (month.Year == previousToday.Year && month.Month == previousToday.Month)
                    month = new DateTime(today.Year, today.Month, 1);
                RefreshDueCount();
                RenderCalendar();
                notifications?.OnDateChanged();
            });
        };
        midnight.Start();
    }
    /// <summary>
    /// 誤クリック予防中のマウス操作によるウィンドウの前面化を抑止します。
    /// </summary>
    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (locked && msg == 0x21) { handled = true; return new IntPtr(3); } // WM_MOUSEACTIVATE: MA_NOACTIVATE
        return IntPtr.Zero;
    }
    private long? SelectedId => combo?.SelectedItem is Item { Id: > 0 } item ? item.Id : null;
    /// <summary>
    /// 所持品・祝日・選択状態を再取得し、設定と期限件数を反映してカレンダーを再描画します。
    /// </summary>
    private void RefreshData()
    {
        long? selected = SelectedId; items = Services.Db.GetItems(); holidays = Services.Db.Holidays();
        combo = Ui.ItemCombo(items, items.Any(x => x.Id == selected) ? selected : null);
        combo.SelectionChanged += (_, _) => Ui.Run(RenderCalendar); ComboHost.Content = combo;
        Services.Settings.Apply(); RenderSidebar(); RefreshDueCount(); RenderCalendar();
    }
    /// <summary>
    /// 今日までに手入れ期限へ達した所持品の件数を更新します。
    /// </summary>
    private void RefreshDueCount()
    {
        dueCount = Services.Db.Due(DateTime.Today).Count;
        todayEventCount = Services.Db.Events(DateTime.Today, DateTime.Today.AddDays(1), Services.Settings.MissingRecurrenceDateBehavior).Count;
    }
    /// <summary>
    /// 表示月の記録・祝日・設定色と図形から、日曜始まり6週のカレンダーを描画します。
    /// </summary>
    private void RenderCalendar()
    {
        var settings = Services.Settings; MonthLabel.Text = month.ToString("yyyy年 M月");
        SetLegend(TodayLegend, settings.TodayShape, "今日", settings.TodayColor);
        SetLegend(UseLegend, "●", "使用", settings.UseColor);
        SetLegend(CareLegend, "●", "お手入れ", settings.CareColor);
        var marks = Services.Db.Month(month, SelectedId).ToLookup(x => x.Date);
        var events = Services.Db.Events(month.Date, month.AddMonths(1).Date, Services.Settings.MissingRecurrenceDateBehavior).ToLookup(x => x.Start.Date);
        var itemNames = items.ToDictionary(x => x.Id, x => x.DisplayName);
        DaysPanel.Children.Clear(); DateTime first = month.AddDays(-(int)month.DayOfWeek);
        for (int n = 0; n < 42; n++)
        {
            DateTime date = first.AddDays(n); if (date.Month != month.Month) { DaysPanel.Children.Add(new Border()); continue; }
            // A normalized drawing inside a Viewbox keeps the date marks circular at any window size.
            var visual = new Grid { Width = 40, Height = 40 }; var dayRecords = marks[date].ToList();
            bool use = dayRecords.Any(x => x.Work == 1), care = dayRecords.Any(x => x.Work == 2);
            if (date == DateTime.Today)
            {
                Shape shape = settings.TodayShape switch { "■" => new Rectangle(), "▼" => new Polygon { Points = new PointCollection([new(0,0), new(1,0), new(0.5,1)]), Stretch = Stretch.Fill }, _ => new Ellipse() };
                shape.Fill = Settings.Brush(settings.TodayColor); shape.Margin = new Thickness(1); visual.Children.Add(shape);
            }
            if (use || care) visual.Children.Add(new Ellipse { Fill = Settings.Brush(use ? settings.UseColor : settings.CareColor), Margin = new Thickness(5) });
            if (care && use) visual.Children.Add(new Ellipse { Stroke = Settings.Brush(settings.CareColor), StrokeThickness = 3, Margin = new Thickness(3) });
            if (events[date].Any()) visual.Children.Add(new Ellipse { Stroke = Settings.Brush(settings.ThemeColor), StrokeThickness = 2, Margin = new Thickness(1) });
            bool holiday = holidays.TryGetValue(date, out var holidayName);
            var number = new TextBlock { Text = date.Day.ToString(), FontSize = 22, FontFamily = new FontFamily("Segoe UI, Yu Gothic UI"),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = holiday || date.DayOfWeek == DayOfWeek.Sunday ? Settings.Brush("#BB3030") : date.DayOfWeek == DayOfWeek.Saturday ? Settings.Brush("#2859B0") : (Brush)FindResource("TextBrush") };
            if (!holiday && date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && (use || care || date == DateTime.Today))
                number.Foreground = UiPalette.OnColor(Settings.Brush(use ? settings.UseColor : care ? settings.CareColor : settings.TodayColor).Color);
            visual.Children.Add(number);
            var button = Ui.Button("DayButton", "", Button_Click); button.Content = new Viewbox { Child = visual, Stretch = Stretch.Uniform }; button.Padding = new Thickness(0); button.Margin = new Thickness(1);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
            button.MinHeight = 0; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Tag = date;
            var details = new List<string>();
            if (holiday) details.Add(holidayName!);
            foreach (var item in dayRecords.GroupBy(x => x.ItemId).OrderBy(x => itemNames[x.Key]))
                details.Add($"{string.Join("・", item.OrderBy(x => x.Work).Select(x => x.Work == 1 ? "使用" : "お手入れ"))} : {itemNames[item.Key]}");
            foreach (var entry in events[date]) details.Add($"予定: {entry.Start:HH:mm} {entry.Title}");
            button.ToolTip = details.Count == 0 ? null : string.Join("\n", details);
            DaysPanel.Children.Add(button);
        }
        DueButton.Content = $"通知  {dueCount + todayEventCount}件";
        UiMotion.FadeIn(DaysPanel);
    }
    /// <summary>
    /// 誤クリック予防を確認し、月移動・子画面表示・終了などの選択された操作を実行します。
    /// </summary>
    private void Button_Click(object sender, RoutedEventArgs e)
    {
        string name = ((FrameworkElement)sender).Name;
        if (locked && name is not "MenuLock" and not "MenuExit") return;
        Ui.Run(() =>
        {
            switch (name)
            {
                case "PrevButton": if (month.Year > 1900) month = month.AddMonths(-1); RenderCalendar(); break;
                case "NextButton": if (month.Year < 9998) month = month.AddMonths(1); RenderCalendar(); break;
                case "TodayButton": month = new(DateTime.Today.Year, DateTime.Today.Month, 1); RenderCalendar(); break;
                case "DayButton": Ui.Show(this, new DayWindow((DateTime)((Button)sender).Tag, items, SelectedId)); RefreshDueCount(); RenderCalendar(); break;
                case "ItemsButton": case "MenuItems": Ui.Show(this, new ItemsWindow()); RefreshData(); break;
                case "MenuTemplates": Ui.Show(this, new TemplatesWindow()); RefreshData(); break;
                case "DashboardButton": case "MenuDashboard": Ui.Show(this, new DashboardWindow()); break;
                case "HistoryButton": case "MenuHistory": Ui.Show(this, new HistoryWindow(SelectedId)); break;
                case "MenuCalendarEvents": Ui.Show(this, new CalendarEventsWindow()); RefreshDueCount(); RenderCalendar(); break;
                case "SettingsButton": case "MenuSettings": Ui.Show(this, new SettingsWindow()); RefreshData(); break;
                case "DueButton": Ui.Show(this, new DueWindow()); break;
                case "MenuLock":
                    locked = sender is MenuItem ? MenuLock.IsChecked : !locked;
                    MenuLock.IsChecked = locked;
                    LockStatus.Text = locked ? "誤クリック予防中：鍵アイコンまたは右クリックから解除できます" : "";
                    RenderSidebar(); LogManager.GetCurrentClassLogger().Info("Click prevention={0}", locked); break;
                case "MenuExit": Close(); break;
            }
        });
    }
    /// <summary>
    /// 誤クリック予防中は、解除操作に必要な右クリック以外のマウス入力を抑止します。
    /// </summary>
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) { if (locked && e.ChangedButton != MouseButton.Right && !IsLockCommand(e.OriginalSource)) e.Handled = true; }
    /// <summary>
    /// 誤クリック予防中は、終了用のAlt+F4を除くキー入力を抑止します。
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (locked && !IsLockCommand(e.OriginalSource) && e.Key != Key.Tab && !(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && e.SystemKey == Key.F4)) e.Handled = true; }
    /// <summary>
    /// 誤クリック予防の状態に応じて、解除と終了以外のメニューを無効化します。
    /// </summary>
    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        foreach (var entry in ContextMenu.Items.OfType<MenuItem>()) entry.IsEnabled = !locked || entry.Name is "MenuLock" or "MenuExit";
    }

    /// <summary>サイドバーのアイコンに添える文字の表示を切り替えて保存します。</summary>
    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (locked) return;
        Ui.Run(() =>
        {
            Services.Settings.SidebarShowLabels = !Services.Settings.SidebarShowLabels;
            Services.Settings.Save(Services.SettingsPath);
            RenderSidebar();
        });
    }

    /// <summary>上下左右に配置でき、常時操作できるサイドバーを構成します。</summary>
    private void RenderSidebar()
    {
        var settings = Services.Settings;
        var dock = Enum.Parse<Dock>(settings.SidebarPosition);
        bool horizontal = dock is Dock.Top or Dock.Bottom;
        DockPanel.SetDock(Sidebar, dock);
        SidebarButtons.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        SidebarScroll.HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        SidebarScroll.VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        SidebarButtons.Children.Clear();
        Add("NavigationButton", "Menu", settings.SidebarShowLabels ? "文字を隠す" : "文字を表示", NavigationButton_Click);
        Add("MenuItems", "Items", "所持品マスタ", Button_Click);
        Add("MenuTemplates", "Care", "お手入れマスタ", Button_Click);
        Add("MenuHistory", "History", "手入れ・使用履歴", Button_Click);
        Add("MenuCalendarEvents", "Calendar", "予定一覧", Button_Click);
        Add("MenuDashboard", "Dashboard", "ダッシュボード", Button_Click);
        Add("MenuSettings", "Settings", "設定", Button_Click);
        Add("MenuLock", "Lock", locked ? "誤クリック予防を解除" : "誤クリック予防", Button_Click);
        Add("MenuExit", "Exit", "終了", Button_Click);

        // 文字を表示してもカレンダーの最小幅を維持します。
        double minimum = horizontal ? 410 : settings.SidebarShowLabels ? 550 : 410;
        MinWidth = minimum;
        if (WindowState == WindowState.Normal && Width < minimum) Width = minimum;

        void Add(string name, string icon, string label, RoutedEventHandler action)
        {
            var button = Ui.Button(name, "", action);
            button.ToolTip = label;
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Padding = new Thickness(8); button.MinWidth = 36;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Background = locked && name == "MenuLock" ? (Brush)FindResource("SurfaceBrush") : Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.IsEnabled = !locked || name is "MenuLock" or "MenuExit";
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(UiIcon.Create(icon, 18));
            if (settings.SidebarShowLabels) content.Children.Add(new TextBlock { Text = label, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            button.Content = content; SidebarButtons.Children.Add(button);
        }
    }

    /// <summary>ロック中にもサイドバーの解除と終了へ入力を通します。</summary>
    private static bool IsLockCommand(object source)
    {
        var element = source as DependencyObject;
        while (element != null)
        {
            if (element is Button button) return button.Name is "MenuLock" or "MenuExit";
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }

    /// <summary>保存済みの通常時の位置と大きさを復元し、利用可能なモニター内に収めます。</summary>
    private void RestorePlacement()
    {
        var s = Services.Settings;
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!;
        var transform = source.CompositionTarget.TransformFromDevice;
        var areas = System.Windows.Forms.Screen.AllScreens.Select(screen =>
        {
            var area = screen.WorkingArea;
            return new Rect(transform.Transform(new Point(area.Left, area.Top)), transform.Transform(new Point(area.Right, area.Bottom)));
        }).ToArray();
        double left = s.WindowLeft ?? SystemParameters.WorkArea.Left, top = s.WindowTop ?? SystemParameters.WorkArea.Top;
        var desired = new Rect(left, top, Math.Max(MinWidth, s.WindowWidth ?? Width), Math.Max(MinHeight, s.WindowHeight ?? Height));
        var target = areas.OrderByDescending(area => { var intersection = Rect.Intersect(area, desired); return intersection.IsEmpty ? 0 : intersection.Width * intersection.Height; }).First();
        Width = Math.Max(MinWidth, Math.Min(desired.Width, target.Width));
        Height = Math.Max(MinHeight, Math.Min(desired.Height, target.Height));
        Left = Math.Max(target.Left, Math.Min(left, target.Right - Width));
        Top = Math.Max(target.Top, Math.Min(top, target.Bottom - Height));
    }

    /// <summary>最小化・最大化中も通常表示の位置と大きさを保存します。</summary>
    private void SavePlacement()
    {
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty) return;
        var s = Services.Settings;
        s.WindowLeft = bounds.Left; s.WindowTop = bounds.Top;
        s.WindowWidth = bounds.Width; s.WindowHeight = bounds.Height;
        s.Save(Services.SettingsPath);
    }

    /// <summary>設定色のマークと読みやすい凡例文字を組み合わせます。</summary>
    private void SetLegend(TextBlock target, string marker, string label, string color)
    {
        target.Inlines.Clear();
        target.Inlines.Add(new Run(marker) { Foreground = Settings.Brush(color) });
        target.Inlines.Add(new Run(" " + label) { Foreground = (Brush)FindResource("TextBrush") });
    }
}
