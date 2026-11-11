using SnapDrop.Core;

namespace SnapDrop.App.Services;

public interface IUiActions
{
    AppSettings Settings { get; }
    string MonitoringStatus { get; }
    Task<IReadOnlyList<ScreenshotRecord>> LoadRecordsAsync();
    Task<CaptureItem> LoadItemAsync(ScreenshotRecord record);
    Task OpenAsync(CaptureItem item);
    Task CopyAsync(CaptureItem item);
    Task SaveAsAsync(CaptureItem item);
    Task SaveAsync(CaptureItem item);
    Task KeepAsync(CaptureItem item);
    Task<bool> DeleteAsync(CaptureItem item);
    void OpenFolder(CaptureItem? item = null);
    Task CaptureAsync();
    Task UpdateSettingsAsync(Func<AppSettings, AppSettings> change);
    Task TogglePauseAsync();
    Task<int> CleanupAsync();
    void OpenLogs();
    Task ExportDiagnosticsAsync();
    void Navigate(string page);
    void OpenSavedFolder(string path);
}
