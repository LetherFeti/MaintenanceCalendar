using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MaintenanceCalendar;

/// <summary>WPF Bindingへ変更を通知する画面ViewModelの基底クラスです。</summary>
public abstract class ScreenViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>一覧画面の行データと選択行を保持します。</summary>
public abstract class ListScreenViewModel<T> : ScreenViewModel where T : class
{
    private IReadOnlyList<T> items = Array.Empty<T>();
    private T? selectedItem;
    public IReadOnlyList<T> Items { get => items; set { items = value; Notify(); } }
    public T? SelectedItem { get => selectedItem; set { selectedItem = value; Notify(); } }
}

public sealed class ItemsViewModel : ListScreenViewModel<Item>
{
    private bool showDeletedOnly;
    public bool ShowDeletedOnly { get => showDeletedOnly; set { showDeletedOnly = value; Notify(); } }
}
public sealed class ItemComboViewModel : ScreenViewModel
{
    private IReadOnlyList<Item> items = Array.Empty<Item>();
    private Item? selectedItem;
    public IReadOnlyList<Item> Items { get => items; set { items = value; Notify(); } }
    public Item? SelectedItem { get => selectedItem; set { selectedItem = value; Notify(); } }
}
public sealed class TemplatesViewModel : ListScreenViewModel<TemplateRow> { }
public sealed class DueItemsViewModel : ListScreenViewModel<DueItem>
{
    private IReadOnlyList<CalendarEvent> events = Array.Empty<CalendarEvent>();
    public IReadOnlyList<CalendarEvent> Events { get => events; set { events = value; Notify(); } }
}
public sealed class EventsViewModel : ListScreenViewModel<CalendarEvent> { }
internal sealed class SuggestionViewModel : ListScreenViewModel<InputSuggestion> { }

public sealed class CareMemoViewModel : ScreenViewModel
{
    private IReadOnlyList<TemplateRow> templates = Array.Empty<TemplateRow>();
    private IReadOnlyList<TemplateRow> selectedTemplates = Array.Empty<TemplateRow>();
    public IReadOnlyList<TemplateRow> Templates { get => templates; set { templates = value; Notify(); } }
    public IReadOnlyList<TemplateRow> SelectedTemplates { get => selectedTemplates; set { selectedTemplates = value; Notify(); } }
}

/// <summary>ComboBoxの候補と選択値を保持します。</summary>
public abstract class ComboScreenViewModel : ScreenViewModel
{
    private object? options;
    private object? selectedValue;
    public object? Options { get => options; set { options = value; Notify(); } }
    public object? SelectedValue { get => selectedValue; set { selectedValue = value; Notify(); } }
}

public sealed class SettingsViewModel : ScreenViewModel
{
    private IReadOnlyList<string> shapes = Array.Empty<string>();
    private IReadOnlyDictionary<string, string> sidebarPositions = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> missingDateBehaviors = new Dictionary<string, string>();
    private string? selectedShape, selectedSidebarPosition, selectedMissingDateBehavior;
    public IReadOnlyList<string> Shapes { get => shapes; set { shapes = value; Notify(); } }
    public IReadOnlyDictionary<string, string> SidebarPositions { get => sidebarPositions; set { sidebarPositions = value; Notify(); } }
    public IReadOnlyDictionary<string, string> MissingDateBehaviors { get => missingDateBehaviors; set { missingDateBehaviors = value; Notify(); } }
    public string? SelectedShape { get => selectedShape; set { selectedShape = value; Notify(); } }
    public string? SelectedSidebarPosition { get => selectedSidebarPosition; set { selectedSidebarPosition = value; Notify(); } }
    public string? SelectedMissingDateBehavior { get => selectedMissingDateBehavior; set { selectedMissingDateBehavior = value; Notify(); } }
}

public sealed class IntervalViewModel : ComboScreenViewModel { }
public sealed class EventEditViewModel : ComboScreenViewModel { }

/// <summary>日別画面の所持品・予定一覧と選択状態を保持します。</summary>
public sealed class DayViewModel : ScreenViewModel
{
    private System.ComponentModel.ICollectionView? dayRows;
    private IReadOnlyList<CalendarEvent> events = Array.Empty<CalendarEvent>();
    private object? selectedDayItem;
    private CalendarEvent? selectedEvent;
    private IReadOnlyDictionary<string, string> recurrenceOptions = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> eventScopeOptions = new Dictionary<string, string>();
    private string? selectedRecurrence, selectedEventScope;
    public System.ComponentModel.ICollectionView? DayRows { get => dayRows; set { dayRows = value; Notify(); } }
    public IReadOnlyList<CalendarEvent> Events { get => events; set { events = value; Notify(); } }
    public object? SelectedDayItem { get => selectedDayItem; set { selectedDayItem = value; Notify(); } }
    public CalendarEvent? SelectedEvent { get => selectedEvent; set { selectedEvent = value; Notify(); } }
    public IReadOnlyDictionary<string, string> RecurrenceOptions { get => recurrenceOptions; set { recurrenceOptions = value; Notify(); } }
    public IReadOnlyDictionary<string, string> EventScopeOptions { get => eventScopeOptions; set { eventScopeOptions = value; Notify(); } }
    public string? SelectedRecurrence { get => selectedRecurrence; set { selectedRecurrence = value; Notify(); } }
    public string? SelectedEventScope { get => selectedEventScope; set { selectedEventScope = value; Notify(); } }
}

/// <summary>履歴画面の所持品選択と記録一覧を保持します。</summary>
public sealed class HistoryViewModel : ScreenViewModel
{
    private IReadOnlyList<Item> items = Array.Empty<Item>();
    private IReadOnlyList<HistoryRow> records = Array.Empty<HistoryRow>();
    private Item? selectedItem;
    private HistoryRow? selectedRecord;
    public IReadOnlyList<Item> Items { get => items; set { items = value; Notify(); } }
    public IReadOnlyList<HistoryRow> Records { get => records; set { records = value; Notify(); } }
    public Item? SelectedItem { get => selectedItem; set { selectedItem = value; Notify(); } }
    public HistoryRow? SelectedRecord { get => selectedRecord; set { selectedRecord = value; Notify(); } }
}

