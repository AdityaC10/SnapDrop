using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using SnapDrop.App.Services;
using SnapDrop.Core;

namespace SnapDrop.App.ViewModels;

public sealed class ScreenshotEntryViewModel(CaptureItem capture, GalleryViewModel owner, bool missing = false)
{
    public CaptureItem Capture { get; } = capture;
    public GalleryViewModel Owner { get; } = owner;
    public bool IsMissing { get; } = missing;
    public BitmapSource Thumbnail => Capture.Thumbnail;
    public string FileName => Capture.Record!.ManagedFileName;
    public string Metadata => Capture.Record!.Description;
    public string Size => $"{Capture.Record!.OriginalFileBytes / (1024.0 * 1024):F2} MiB";
    public string DateGroup => Capture.Record!.ReceivedAtUtc.LocalDateTime.Date == DateTime.Today ? "Today" : Capture.Record.ReceivedAtUtc.ToLocalTime().ToString("D");
    public string Availability => IsMissing ? "This screenshot is missing or cannot be read." : Capture.RecoveryPolicy;
}

public sealed class GalleryViewModel : ObservableObject
{
    private readonly IUiActions _actions;
    private IReadOnlyList<ScreenshotRecord> _records = [];
    private string _search = "", _status = "", _timeRange = "All time";
    private bool _list, _loading;
    private int _limit = 50, _generation;
    private ScreenshotEntryViewModel? _selected;
    public ObservableCollection<ScreenshotEntryViewModel> Items { get; } = [];
    public string Search { get => _search; set { if (Set(ref _search, value)) _ = ReloadVisibleAsync(); } }
    public string TimeRange { get => _timeRange; set { if (Set(ref _timeRange, value)) _ = ReloadVisibleAsync(); } }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IsLoading { get => _loading; private set { Set(ref _loading, value); Changed(nameof(IsEmpty)); } }
    public bool IsEmpty => !IsLoading && Items.Count == 0;
    public bool HasSelection => Selected is not null;
    public ScreenshotEntryViewModel? Selected { get => _selected; set { if (Set(ref _selected, value)) { Changed(nameof(HasSelection)); RefreshCommands(); } } }
    public bool ListView { get => _list; set { if (Set(ref _list, value)) _ = PersistListAsync(value); } }
    public string Summary => $"{_records.Count} recovery copies · {_records.Sum(x => x.OriginalFileBytes) / (1024.0 * 1024):F1} MiB";
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand LoadMoreCommand { get; }
    public AsyncCommand CaptureCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public AsyncCommand CopyCommand { get; }
    public AsyncCommand SaveAsCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand KeepCommand { get; }
    public AsyncCommand DeleteCommand { get; }
    public RelayCommand FolderCommand { get; }
    public GalleryViewModel(IUiActions actions)
    {
        _actions = actions; _list = actions.Settings.GalleryListView;
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Items);
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ScreenshotEntryViewModel.DateGroup)));
        RefreshCommand = new(_ => RefreshAsync(), Fail);
        LoadMoreCommand = new(async _ => { _limit += 50; await ReloadVisibleAsync(); }, Fail, _ => _limit < MatchingRecords().Count());
        CaptureCommand = new(_ => actions.CaptureAsync(), Fail);
        OpenCommand = ImageCommand(actions.OpenAsync); CopyCommand = ImageCommand(actions.CopyAsync);
        SaveAsCommand = ImageCommand(actions.SaveAsAsync); SaveCommand = ImageCommand(actions.SaveAsync); KeepCommand = ImageCommand(actions.KeepAsync);
        DeleteCommand = new(async parameter =>
        {
            if (Resolve(parameter) is not { } item) return;
            if (await actions.DeleteAsync(item.Capture)) await RefreshAsync(); else Status = item.Capture.Status;
        }, Fail, parameter => Resolve(parameter) is not null);
        FolderCommand = new(parameter => { try { actions.OpenFolder(Resolve(parameter)?.Capture); } catch (Exception ex) { Fail(ex); } });
    }
    private AsyncCommand ImageCommand(Func<CaptureItem, Task> action) => new(async parameter =>
    {
        if (Resolve(parameter) is not { } entry) return;
        await action(entry.Capture); Status = entry.Capture.Status;
    }, Fail, parameter => Resolve(parameter) is { IsMissing: false });
    private ScreenshotEntryViewModel? Resolve(object? parameter) => parameter as ScreenshotEntryViewModel ?? Selected;
    public async Task RefreshAsync()
    {
        Status = "Loading screenshots…";
        try { _records = await _actions.LoadRecordsAsync(); Changed(nameof(Summary)); await ReloadVisibleAsync(); }
        catch (Exception ex) { Fail(ex); }
    }
    public static bool Matches(ScreenshotRecord record, string query) => string.IsNullOrWhiteSpace(query) ||
        record.ManagedFileName.Contains(query, StringComparison.CurrentCultureIgnoreCase) || record.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    private IEnumerable<ScreenshotRecord> MatchingRecords() => _records.Where(x => Matches(x, Search) &&
        (TimeRange == "All time" || TimeRange == "Today" && x.ReceivedAtUtc.LocalDateTime.Date == DateTime.Today || TimeRange == "Last 7 days" && x.ReceivedAtUtc >= DateTimeOffset.Now.AddDays(-7)));
    private async Task ReloadVisibleAsync()
    {
        var generation = ++_generation; IsLoading = true;
        var matching = MatchingRecords().ToArray();
        Items.Clear(); Selected = null;
        try
        {
            foreach (var record in matching.Take(_limit))
            {
                CaptureItem capture;
                var missing = false;
                try { capture = await _actions.LoadItemAsync(record); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
                {
                    missing = true;
                    var empty = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4); empty.Freeze();
                    capture = new CaptureItem { Record = record, Thumbnail = empty, Status = "This screenshot was moved or deleted." };
                }
                if (generation != _generation) { capture.Dispose(); return; }
                Items.Add(new(capture, this, missing));
            }
            Status = matching.Length == 0 ? "No screenshots match. Capture a screenshot or change the search." : $"Showing {Items.Count} of {matching.Length} matching screenshots.";
            Selected = Items.FirstOrDefault();
        }
        catch (Exception ex) { Fail(ex); }
        finally { if (generation == _generation) { IsLoading = false; Changed(nameof(IsEmpty)); LoadMoreCommand.Refresh(); } }
    }
    private async Task PersistListAsync(bool value) { try { await _actions.UpdateSettingsAsync(s => s with { GalleryListView = value }); } catch (Exception ex) { Fail(ex); } }
    private void Fail(Exception ex) => Status = App.FriendlyError(ex);
    private void RefreshCommands() { OpenCommand?.Refresh(); CopyCommand?.Refresh(); SaveAsCommand?.Refresh(); SaveCommand?.Refresh(); KeepCommand?.Refresh(); DeleteCommand?.Refresh(); }
}
