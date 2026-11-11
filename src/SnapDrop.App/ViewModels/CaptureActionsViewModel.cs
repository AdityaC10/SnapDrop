using System.Collections.ObjectModel;
using System.ComponentModel;
using SnapDrop.App.Services;
using SnapDrop.Core;

namespace SnapDrop.App.ViewModels;

public sealed class CaptureActionsViewModel : ObservableObject, IDisposable
{
    private readonly IUiActions _actions;
    private bool _busy, _expanded, _pinned;
    public CaptureItem Item { get; }
    public bool IsBusy { get => _busy; set { if (Set(ref _busy, value)) RefreshCommands(); } }
    public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    public bool IsPinned { get => _pinned; set => Set(ref _pinned, value); }
    public ExportRecord? SelectedExport { get; set; }
    public ObservableCollection<CaptureItem> Recent { get; } = [];
    public AsyncCommand CopyCommand { get; }
    public AsyncCommand SaveAsCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public AsyncCommand KeepCommand { get; }
    public AsyncCommand RecentOpenCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand HistoryCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand FolderCommand { get; }
    public RelayCommand OpenSavedFolderCommand { get; }
    public CaptureActionsViewModel(CaptureItem item, IUiActions actions, Action close)
    {
        Item = item; _actions = actions;
        CopyCommand = Action(actions.CopyAsync); SaveAsCommand = Action(actions.SaveAsAsync);
        SaveCommand = Action(actions.SaveAsync, () => !item.IsRecovery); OpenCommand = Action(actions.OpenAsync);
        KeepCommand = Action(actions.KeepAsync, () => !item.IsRecovery);
        RecentOpenCommand = new(async value => { if (value is CaptureItem recent) await actions.OpenAsync(recent); }, Fail, _ => !IsBusy);
        CloseCommand = new(_ => close(), _ => !IsBusy);
        HistoryCommand = new(_ => actions.Navigate("History")); SettingsCommand = new(_ => actions.Navigate("Settings"));
        FolderCommand = new(_ => { try { actions.OpenFolder(item); } catch (Exception ex) { Fail(ex); } }, _ => !item.IsRecovery);
        OpenSavedFolderCommand = new(_ =>
        {
            try { actions.OpenSavedFolder(SelectedExport?.Path ?? item.LastExportPath ?? throw new IOException("Save the image first.")); item.Status = "Opened the saved image folder."; }
            catch (Exception ex) { Fail(ex); }
        }, _ => !IsBusy && item.HasExport);
        item.PropertyChanged += ItemChanged;
    }
    private AsyncCommand Action(Func<CaptureItem, Task> action, Func<bool>? allowed = null) => new(async _ =>
    {
        IsBusy = true;
        try { await action(Item); } finally { IsBusy = false; }
    }, Fail, _ => !IsBusy && (allowed?.Invoke() ?? true));
    public async Task LoadRecentAsync()
    {
        try
        {
            foreach (var record in (await _actions.LoadRecordsAsync()).Take(6))
            {
                try { Recent.Add(await _actions.LoadItemAsync(record)); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { /* Missing entries are omitted from the optional viewer strip. */ }
            }
        }
        catch (Exception ex) { Fail(ex); }
    }
    private void Fail(Exception ex) => Item.Status = App.FriendlyError(ex);
    private void ItemChanged(object? sender, PropertyChangedEventArgs e) => RefreshCommands();
    private void RefreshCommands()
    {
        CopyCommand?.Refresh(); SaveAsCommand?.Refresh(); SaveCommand?.Refresh(); OpenCommand?.Refresh(); KeepCommand?.Refresh();
        CloseCommand?.Refresh(); OpenSavedFolderCommand?.Refresh(); RecentOpenCommand?.Refresh();
    }
    public void Dispose() { Item.PropertyChanged -= ItemChanged; foreach (var item in Recent) item.Dispose(); }
}
