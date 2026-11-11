using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using SnapDrop.Core;
using SnapDrop.Infrastructure;
using SnapDrop.Windows;

namespace SnapDrop.App.Services;

public sealed class AppUiActions(App app, Func<Window> owner) : IUiActions
{
    private static readonly SemaphoreSlim PreferenceWriter = new(1, 1);
    public AppSettings Settings => app.Settings;
    public string MonitoringStatus => !Settings.MonitoringEnabled ? "Clipboard images disabled" : app.MonitoringActive ? "Monitoring active" : app.IsPaused ? "Monitoring paused" : "Monitoring inactive";
    public Task<IReadOnlyList<ScreenshotRecord>> LoadRecordsAsync() => app.Repository.LoadAsync();
    public Task<CaptureItem> LoadItemAsync(ScreenshotRecord record) => app.CreateItemAsync(record, IntPtr.Zero, false);
    private Task Run(CaptureItem item, Func<Task> action) => app.RunActionAsync(item, owner(), action);
    public Task OpenAsync(CaptureItem item) => Run(item, async () =>
    {
        if (item.Record is { } record) await app.OpenViewerAsync(record);
        else new ScreenshotViewerWindow(new CaptureItem { RecoveryFrame = item.RecoveryFrame, Thumbnail = item.Thumbnail }).Show();
        item.Status = "Opened screenshot preview.";
    });
    public Task CopyAsync(CaptureItem item) => Run(item, () => app.CopyAsync(item));
    public Task SaveAsAsync(CaptureItem item) => Run(item, () => app.SaveAsAsync(item, owner()));
    public Task SaveAsync(CaptureItem item) => Run(item, () => app.SaveDefaultAsync(item));
    public Task KeepAsync(CaptureItem item) => Run(item, () => app.ToggleKeepAsync(item));
    public async Task<bool> DeleteAsync(CaptureItem item)
    {
        if (MessageBox.Show(owner(), "Delete this recovery copy? Exported copies are preserved. An image in use cannot be deleted.", "Delete recovery copy", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
        var deleted = await app.Repository.DeleteAsync(item.Record!.Id);
        item.Status = deleted ? "Recovery copy removed. Exported images were preserved." : "This image is in use. Close its preview and try again.";
        return deleted;
    }
    public void OpenFolder(CaptureItem? item = null) => App.OpenFolder(item?.Record is { } record ? Path.GetDirectoryName(app.Repository.GetOriginalPath(record))! : app.Repository.Root);
    public async Task CaptureAsync()
    {
        if (!Settings.MonitoringEnabled || !app.MonitoringActive)
        { app.ShowSettings(); throw new InvalidOperationException("Enable clipboard images and resume monitoring in General before capturing with SnapDrop."); }
        var window = owner(); window.WindowState = WindowState.Minimized;
        await Task.Delay(180);
        SnippingToolLauncher.Launch();
    }
    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> change)
    {
        await PreferenceWriter.WaitAsync();
        try { await app.SaveSettingsAsync(change(app.Settings).Validate()); }
        finally { PreferenceWriter.Release(); }
    }
    public Task TogglePauseAsync() => app.TogglePauseAsync();
    public async Task<int> CleanupAsync()
    {
        if (MessageBox.Show(owner(), "Remove only expired, unkept recovery images that are not in use? User-exported files will remain untouched.", "Clean expired recovery copies", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return 0;
        return await app.Repository.CleanupAsync(Settings.RetentionDays);
    }
    public void OpenLogs() => App.OpenFolder(Path.Combine(app.DataRoot, "Logs"));
    public void Navigate(string page) => app.ShowMain(page);
    public void OpenSavedFolder(string path)
    {
        if (!File.Exists(path)) throw new IOException("The exported image is no longer at its recorded location.");
        App.OpenFolder(Path.GetDirectoryName(path)!);
    }
    public async Task ExportDiagnosticsAsync()
    {
        var dialog = new SaveFileDialog { Title = "Export local diagnostic summary", Filter = "JSON report (*.json)|*.json", FileName = "SnapDrop-diagnostics.json", DefaultExt = ".json", OverwritePrompt = true };
        if (dialog.ShowDialog(owner()) != true) return;
        var items = await LoadRecordsAsync();
        var report = new { Version = typeof(App).Assembly.GetName().Version?.ToString(), OS = Environment.OSVersion.VersionString,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            IndexedCaptures = items.Count, IndexedImageBytes = items.Sum(x => x.OriginalFileBytes),
            Monitoring = MonitoringStatus, CreatedAtUtc = DateTimeOffset.UtcNow,
            Scope = "Local summary. No screenshot pixels, clipboard contents, paths, capture identifiers, or test-pass claims." };
        await AtomicFile.WriteAsync(dialog.FileName, stream => JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }), true);
    }
}
