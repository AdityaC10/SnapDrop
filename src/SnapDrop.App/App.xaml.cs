using System.Diagnostics;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using SnapDrop.Core;
using SnapDrop.Infrastructure;
using SnapDrop.Windows;
using SnapDrop.App.Services;
using SnapDrop.App.Views;
using System.Windows.Interop;

namespace SnapDrop.App;

public partial class App : Application
{
    public static App Host => (App)Current;
    public AppSettings Settings { get; private set; } = new();
    public FileScreenshotRepository Repository { get; private set; } = null!;
    public string DataRoot { get; private set; } = "";
    public string GlobalStatus { get; private set; } = "";
    public bool IsPaused => _paused || _storagePaused;
    public bool IsSessionLocked => _locked;
    public bool IsStopping => _shuttingDown;
    public bool MonitoringActive { get; private set; }
    public ThemeService Themes { get; private set; } = null!;
    public event EventHandler? UiStateChanged;
    public event EventHandler? LibraryChanged;
    private Views.MainWindow? _shell;
    private OnboardingWindow? _onboarding;
    public bool HasMemoryRecovery => _memoryRecovery is not null;
    private readonly Channel<(CapturedFrame Frame, IntPtr Monitor)> _frames = Channel.CreateBounded<(CapturedFrame, IntPtr)>(2);
    private readonly CancellationTokenSource _stop = new();
    private SingleInstanceGuard? _instance;
    private ClipboardService? _clipboard;
    private JsonSettingsStore _settingsStore = null!;
    private WpfImageCodec _codec = null!;
    private ExportService _exports = null!;
    private ScreenshotDragService _drag = null!;
    private LocalLog? _log;
    private TrayService? _tray;
    private FloatingPreviewWindow? _preview;
    private CaptureItem? _pending;
    private DispatcherTimer? _cleanupTimer;
    private Task? _consumer;
    private bool _paused, _storagePaused, _locked, _shuttingDown, _exitRequested, _autoDialog;
    private int _operations;
    private int _inflight;
    private CapturedFrame? _memoryRecovery;
    private IDisposable? _memoryRecoveryLease;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _instance = new SingleInstanceGuard();
            if (!_instance.IsPrimary)
            {
                try { await _instance.NotifyPrimaryAsync(); }
                catch (IOException) { MessageBox.Show("SnapDrop is already running. Open it from the notification area.", "SnapDrop"); }
                catch (TimeoutException) { MessageBox.Show("SnapDrop is already running. Open it from the notification area.", "SnapDrop"); }
                Shutdown(); return;
            }
            var configuredDataRoot = Environment.GetEnvironmentVariable("SNAPDROP_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredDataRoot) && !Path.IsPathFullyQualified(configuredDataRoot))
                throw new InvalidDataException("SNAPDROP_DATA_ROOT must be an absolute directory path.");
            DataRoot = string.IsNullOrWhiteSpace(configuredDataRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapDrop")
                : Path.GetFullPath(configuredDataRoot);
            Directory.CreateDirectory(DataRoot);
            _log = new(Path.Combine(DataRoot, "Logs"));
            _codec = new();
            _settingsStore = new(DataRoot);
            var loaded = await _settingsStore.LoadAsync();
            Settings = loaded.Settings;
            if (string.IsNullOrWhiteSpace(Settings.DefaultFolder))
                Settings = Settings with { DefaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapDrop") };
            Settings = Settings with { StartWithWindows = StartupRegistration.IsEnabled };
            _codec.JpegQuality = Settings.JpegQuality;
            Themes = new(); Themes.Apply(Settings.Theme);
            Repository = new(Path.Combine(DataRoot, "Library"), _codec);
            _exports = new(Repository, _codec, _codec);
            _drag = new(Repository);
            await Repository.LoadAsync();
            await Repository.CleanupAsync(Settings.RetentionDays);
            _clipboard = new();
            _clipboard.FrameReceived += ReceiveFrame;
            _clipboard.Notice += (message, error) => Dispatcher.BeginInvoke(new Action(() => Notify(message, error)));
            _consumer = Task.Run(ConsumeAsync);
            _tray = new(this);
            _instance.OpenRequested += () => Dispatcher.BeginInvoke(new Action(() => ShowMain("Home")));
            _instance.Listen();
            SystemEvents.SessionSwitch += SessionChanged;
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _cleanupTimer.Tick += CleanupTick;
            _cleanupTimer.Start();
            try { await _clipboard.Ready; await ApplyMonitoringAsync(); }
            catch (Exception ex) { Notify("Clipboard monitoring could not start. Restart SnapDrop to retry.", ex); }
            if (loaded.Warning is not null) Notify(loaded.Warning);
            if (Repository.RecoveryNotices.Count > 0) Notify("Some recovery files need review. Open the recovery folder from Settings; existing files were preserved.");
            if (!Settings.OnboardingCompleted) ShowOnboarding();
            _log.Write("application.started");
        }
        catch (Exception ex)
        {
            _log?.Write("startup.failed", ex);
            MessageBox.Show("SnapDrop could not start. " + FriendlyError(ex), "SnapDrop", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ReceiveFrame(CapturedFrame frame, IntPtr monitor)
    {
        Interlocked.Increment(ref _inflight);
        if (_shuttingDown || !_frames.Writer.TryWrite((frame, monitor)))
        { Interlocked.Decrement(ref _inflight); frame.Dispose(); _clipboard!.ReleaseFrame(); }
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var incoming in _frames.Reader.ReadAllAsync(_stop.Token))
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    var settings = Settings;
                    var item = await Repository.CommitAsync(incoming.Frame, settings.LibraryBudgetBytes, _stop.Token);
                    var presentation = await CreateItemAsync(item, incoming.Monitor, true);
                    presentation.ReceivedTimestamp = incoming.Frame.ReceivedTimestamp;
                    await Dispatcher.InvokeAsync(() => { Present(presentation); LibraryChanged?.Invoke(this, EventArgs.Empty); });
                    _log?.Write("capture.committed", details: $"pixels={item.PixelWidth}x{item.PixelHeight} elapsedMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}");
                    if (settings.AutomaticExport)
                    {
                        try { await ExportDefaultAsync(item); }
                        catch (Exception ex) { await Dispatcher.InvokeAsync(() => Notify("Automatic saving failed. The recovery copy is available in Recent. " + FriendlyError(ex), ex)); }
                    }
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    var image = await Task.Run(() => WpfImageCodec.Thumbnail(incoming.Frame));
                    var recoveryOperation = Dispatcher.InvokeAsync(async () =>
                    {
                        _storagePaused = true;
                        try { await ApplyMonitoringAsync(); } catch (Exception monitorError) { _log?.Write("monitor.pause.failed", monitorError); }
                        if (RememberRecovery(incoming.Frame))
                        {
                            Present(new CaptureItem { RecoveryFrame = incoming.Frame, Thumbnail = image, Monitor = incoming.Monitor, ReceivedTimestamp = incoming.Frame.ReceivedTimestamp,
                                Status = "Recovery storage failed. Copy or Save As before exiting. Reopen this image from the tray. " + FriendlyError(ex) });
                            Notify("Monitoring paused after a storage failure. Recover the unsaved image from the tray before resuming.", ex);
                        }
                        else Notify("An earlier unsaved image is already retained. A later clipboard change was not retained; save or discard the unsaved image before resuming. Monitoring is paused.", ex);
                    });
                    await (await recoveryOperation);
                }
                finally
                {
                    incoming.Frame.Dispose(); _clipboard!.ReleaseFrame();
                    if (Interlocked.Decrement(ref _inflight) == 0 && _exitRequested)
                        _ = Dispatcher.BeginInvoke(new Action(InteractionEnded));
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { await Dispatcher.InvokeAsync(() => Notify("Screenshot processing stopped. Restart SnapDrop.", ex)); }
    }

    public async Task<CaptureItem> CreateItemAsync(ScreenshotRecord record, IntPtr monitor, bool holdLease)
    {
        var lease = Repository.AcquireLease(record.Id);
        try
        {
            var bitmap = await Task.Run(() => WpfImageCodec.LoadBitmap(Repository.GetOriginalPath(record), 560));
            var item = new CaptureItem { Record = record, Thumbnail = bitmap, Monitor = monitor };
            if (holdLease) item.SetLease(lease); else lease.Dispose();
            return item;
        }
        catch { lease.Dispose(); throw; }
    }

    private void Present(CaptureItem item)
    {
        if (_locked || _shuttingDown) { item.Dispose(); return; }
        if (!Settings.ShowFloatingPreview)
        {
            if (Settings.AutoOpenSaveAs && !_autoDialog)
            {
                var owner = GetShell();
                _ = new WindowInteropHelper(owner).EnsureHandle();
                _autoDialog = true;
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try { if (!_locked && !_shuttingDown) await AutoSaveAsAsync(item, owner); }
                    finally { _autoDialog = false; item.Dispose(); }
                }));
            }
            else item.Dispose();
            return;
        }
        if (_preview is { IsEngaged: true })
        {
            _pending?.Dispose(); _pending = item;
            _preview.Item.Status = "More captures are available in Recent. Finish this action to see the latest.";
            return;
        }
        _preview?.Close();
        _preview = new(item, Settings.PreviewSeconds);
        _preview.Closed += (_, _) => { if (_preview?.Item == item) _preview = null; item.Dispose(); };
        _preview.Show();
        if (Settings.AutoOpenSaveAs && !_autoDialog)
        {
            var owner = _preview;
            _autoDialog = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () =>
            {
                if (owner.IsVisible && !_locked && !_shuttingDown) await AutoSaveAsAsync(item, owner);
                else _autoDialog = false;
            }));
        }
    }

    private async Task AutoSaveAsAsync(CaptureItem item, Window owner)
    {
        _autoDialog = true;
        try { await RunActionAsync(item, owner, () => SaveAsAsync(item, owner)); }
        finally { _autoDialog = false; }
    }

    public void InteractionEnded()
    {
        if (_exitRequested && _operations == 0 && _preview?.IsDragging != true) { _ = ExitAsync(); return; }
        if (_preview?.IsEngaged == true || _pending is null || _shuttingDown) return;
        var next = _pending; _pending = null;
        Present(next);
    }

    public async Task RunActionAsync(CaptureItem item, Window owner, Func<Task> action)
    {
        _operations++;
        IDisposable? recoveryLease = null;
        item.Status = "Working…";
        var preview = owner as FloatingPreviewWindow;
        preview?.SetBusy(true);
        try { recoveryLease = item.RecoveryFrame?.Retain(); await action(); }
        catch (OperationCanceledException) { item.Status = "Action cancelled."; }
        catch (Exception ex) { item.Status = FriendlyError(ex); Notify(item.Status, ex); }
        finally { recoveryLease?.Dispose(); preview?.SetBusy(false); _operations--; InteractionEnded(); }
    }

    public async Task CopyAsync(CaptureItem item)
    {
        CapturedFrame frame;
        if (item.Record is { } record)
        {
            using var lease = Repository.AcquireLease(record.Id);
            frame = await Task.Run(() => WpfImageCodec.Snapshot(WpfImageCodec.LoadBitmap(Repository.GetOriginalPath(record))));
        }
        else frame = item.RecoveryFrame ?? throw new IOException("The image is unavailable.");
        try { await _clipboard!.CopyAsync(frame); }
        finally { if (item.Record is not null) frame.Dispose(); }
        item.Status = "Copied. Paste the image into an app that accepts images.";
    }

    public async Task SaveAsAsync(CaptureItem item, Window owner)
    {
        if (_locked || _shuttingDown) return;
        var recoveryFrame = item.RecoveryFrame;
        owner.Activate();
        var format = Settings.DefaultFormat;
        var initial = Settings.RememberLastFolder && Directory.Exists(Settings.LastSaveFolder) ? Settings.LastSaveFolder :
            Directory.Exists(Settings.DefaultFolder) ? Settings.DefaultFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var dialog = new SaveFileDialog
        {
            Title = "Save screenshot", Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            FilterIndex = format == ExportFormat.Png ? 1 : 2,
            DefaultExt = format == ExportFormat.Png ? ".png" : ".jpg", AddExtension = true,
            FileName = ExportName(item.Record?.ReceivedAtUtc ?? DateTimeOffset.UtcNow, Guid.NewGuid(), format),
            InitialDirectory = initial, OverwritePrompt = false
        };
        if (dialog.ShowDialog(owner) != true) { item.Status = "Save cancelled. Your image is still available."; return; }
        var actualFormat = ScreenshotNaming.FromPath(dialog.FileName);
        if ((dialog.FilterIndex == 1) != (actualFormat == ExportFormat.Png))
            throw new IOException("The filename extension does not match the selected format. Choose Save As again with a matching filename and format.");
        var overwrite = File.Exists(dialog.FileName);
        if (overwrite && MessageBox.Show(owner, "Replace the existing file?", "Save screenshot", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        { item.Status = "Save cancelled."; return; }
        item.Status = "Saving…";
        if (item.Record is { } record)
        {
            var result = await _exports.ExportAsync(record, dialog.FileName, overwrite);
            item.Record = (await Repository.LoadAsync()).FirstOrDefault(x => x.Id == record.Id) ?? record;
            item.Status = result.HistoryUpdated ? $"Saved to {result.Path}" : $"Saved to {result.Path}. The history update failed.";
        }
        else
        {
            var path = await _exports.ExportRecoveryAsync(recoveryFrame ?? throw new IOException("The recovery image is unavailable."), dialog.FileName, overwrite);
            item.Status = $"Saved to {path}. Recovery storage is still unavailable.";
            ForgetRecovery(recoveryFrame);
        }
        item.LastExportPath = dialog.FileName;
        try
        {
            if (Settings.RememberLastFolder)
            {
                Settings = Settings with { LastSaveFolder = Path.GetDirectoryName(dialog.FileName)! };
                await _settingsStore.SaveAsync(Settings);
            }
        }
        catch (Exception ex) { Notify("The image was saved, but the last-used folder could not be remembered.", ex); }
    }

    public async Task SaveDefaultAsync(CaptureItem item)
    {
        if (item.Record is not { } record) throw new IOException("Choose Save As to save this recovery image.");
        var result = await ExportDefaultAsync(record);
        item.Record = (await Repository.LoadAsync()).FirstOrDefault(x => x.Id == record.Id) ?? record;
        item.LastExportPath = result.Path;
        item.Status = $"Saved to {result.Path}" + (result.HistoryUpdated ? "" : ". History update failed.");
    }

    private Task<ExportResult> ExportDefaultAsync(ScreenshotRecord record)
    {
        if (string.IsNullOrWhiteSpace(Settings.DefaultFolder)) throw new IOException("Choose a default folder in Settings.");
        var folder = Settings.OrganizeByDate ? Path.Combine(Settings.DefaultFolder, record.ReceivedAtUtc.ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) : Settings.DefaultFolder;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, ExportName(record.ReceivedAtUtc, Guid.NewGuid(), Settings.DefaultFormat));
        return _exports.ExportAsync(record, path, false);
    }

    private string ExportName(DateTimeOffset received, Guid id, ExportFormat format) => Settings.FileNamePreset == FileNamePreset.Standard
        ? ScreenshotNaming.Create(received, id, format)
        : $"Snap_{received.ToLocalTime():yyyyMMdd_HHmmss}_{id:N}" + (format == ExportFormat.Png ? ".png" : ".jpg");

    public async Task DragAsync(CaptureItem item, UIElement source)
    {
        if (item.Record is not { } record) throw new IOException("Save As is available; file drag requires recovery storage.");
        var result = await _drag.StartAsync(source, record);
        item.Status = result == DragDropEffects.None ? "Drop cancelled or rejected. The recovery copy is intact." : "The target accepted the file transfer.";
    }

    public async Task ToggleKeepAsync(CaptureItem item)
    {
        if (item.Record is not { } record) return;
        item.Record = await Repository.UpdateAsync(record.Id, current => current with { IsKept = !current.IsKept });
        item.Status = item.Record.IsKept ? "Kept. This recovery copy will not expire automatically." : "Keep removed. The retention policy applies.";
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        settings.Validate();
        var previousStartup = StartupRegistration.IsEnabled;
        try
        {
            if (settings.StartWithWindows != previousStartup) StartupRegistration.SetEnabled(settings.StartWithWindows);
            await _settingsStore.SaveAsync(settings);
        }
        catch
        {
            if (StartupRegistration.IsEnabled != previousStartup)
                try { StartupRegistration.SetEnabled(previousStartup); } catch (Exception rollback) { _log?.Write("startup.rollback.failed", rollback); }
            throw;
        }
        Settings = settings;
        _codec.JpegQuality = settings.JpegQuality;
        Themes.Apply(settings.Theme);
        await ApplyMonitoringAsync();
        _preview?.UpdateDuration(Settings.PreviewSeconds);
        _preview?.ApplySize();
        if (!settings.ShowFloatingPreview && _preview?.IsEngaged != true) { _preview?.Close(); _pending?.Dispose(); _pending = null; }
    }

    public async Task TogglePauseAsync()
    {
        if (IsPaused) { await ResumeMonitoringAsync(); return; }
        _paused = true;
        await ApplyMonitoringAsync();
        Notify("Clipboard monitoring paused.");
    }

    public async Task ResumeMonitoringAsync()
    {
        if (HasMemoryRecovery) throw new IOException("Save or discard the unsaved image from the tray before resuming monitoring.");
        _paused = false; _storagePaused = false;
        await ApplyMonitoringAsync();
        Notify(MonitoringActive ? "Clipboard monitoring resumed." : "Clipboard monitoring is inactive. Review Settings or unlock the session.");
    }

    private async Task ApplyMonitoringAsync()
    {
        var enabled = Settings.MonitoringEnabled && !IsPaused && !_locked && !_shuttingDown;
        try
        {
            if (_clipboard is not null) await _clipboard.SetEnabledAsync(enabled).WaitAsync(TimeSpan.FromSeconds(3));
            MonitoringActive = enabled;
        }
        catch { MonitoringActive = false; throw; }
        finally { _tray?.Refresh(); UiStateChanged?.Invoke(this, EventArgs.Empty); }
    }

    public void ShowSettings()
    {
        ShowMain("Settings");
    }

    public void ShowRecent()
    {
        ShowMain("History");
    }

    private Views.MainWindow GetShell()
    {
        if (_shell is null) { _shell = new(); _shell.Closed += (_, _) => _shell = null; }
        return _shell;
    }
    public void ShowMain(string page = "Home") => GetShell().Navigate(page);
    private void ShowOnboarding()
    {
        if (_onboarding is null) { _onboarding = new(); _onboarding.Closed += (_, _) => _onboarding = null; }
        _onboarding.Show(); _onboarding.Activate();
    }
    public Task RequestCaptureAsync() => new AppUiActions(this, GetShell).CaptureAsync();

    public async Task OpenViewerAsync(ScreenshotRecord record)
    {
        var item = await CreateItemAsync(record, IntPtr.Zero, true);
        var viewer = new ScreenshotViewerWindow(item);
        viewer.Show(); viewer.Activate();
    }

    public void Notify(string message, Exception? error = null)
    {
        GlobalStatus = message;
        _log?.Write(error is null ? "notice" : "operation.failed", error);
        _tray?.ShowNotice(message);
        UiStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool RememberRecovery(CapturedFrame frame)
    {
        if (_memoryRecovery is not null) return ReferenceEquals(_memoryRecovery, frame);
        _memoryRecoveryLease = frame.Retain();
        _memoryRecovery = frame;
        _tray?.Refresh();
        return true;
    }

    private void ForgetRecovery(CapturedFrame? expected)
    {
        if (!ReferenceEquals(_memoryRecovery, expected)) return;
        _memoryRecovery = null;
        Interlocked.Exchange(ref _memoryRecoveryLease, null)?.Dispose();
        _tray?.Refresh();
    }

    public async Task ShowMemoryRecoveryAsync()
    {
        var frame = _memoryRecovery;
        if (frame is null) return;
        try
        {
            using var retention = frame.Retain();
            var image = await Task.Run(() => WpfImageCodec.Thumbnail(frame));
            if (!ReferenceEquals(_memoryRecovery, frame) || _locked || _shuttingDown) return;
            var item = new CaptureItem { RecoveryFrame = frame, Thumbnail = image,
                Status = "Unsaved image held in memory. Choose Save As before exiting." };
            var viewer = new ScreenshotViewerWindow(item);
            viewer.Show(); viewer.Activate();
        }
        catch (Exception ex) { Notify("The unsaved image could not be reopened. " + FriendlyError(ex), ex); }
    }

    public void DiscardMemoryRecovery()
    {
        if (_memoryRecovery is not { } frame) return;
        if (_operations > 0) { Notify("Finish the active action before discarding the unsaved image."); return; }
        if (MessageBox.Show("Discard the unsaved image held in memory? This cannot be undone.", "SnapDrop", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        foreach (var viewer in Windows.OfType<ScreenshotViewerWindow>().Where(x => x.UsesRecoveryFrame(frame)).ToArray()) viewer.Close();
        if (ReferenceEquals(_pending?.RecoveryFrame, frame)) { _pending.Dispose(); _pending = null; }
        if (ReferenceEquals(_preview?.Item.RecoveryFrame, frame)) _preview.Close();
        ForgetRecovery(frame);
        Notify("Unsaved recovery image discarded. Monitoring remains paused until resumed.");
        InteractionEnded();
    }

    public void RecordPreviewRendered(CaptureItem item)
    {
        if (item.ReceivedTimestamp != 0)
            _log?.Write("preview.contentRendered", details: $"sourceEventElapsedMs={Stopwatch.GetElapsedTime(item.ReceivedTimestamp).TotalMilliseconds:F1} recovery={item.IsRecovery}");
    }

    public static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("The folder is no longer available.");
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private async void CleanupTick(object? sender, EventArgs e)
    {
        try { await Repository.CleanupAsync(Settings.RetentionDays, _stop.Token); }
        catch (Exception ex) { Notify("Some recovery files could not be cleaned up. Review recovery storage.", ex); }
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(new Action(async () =>
    {
        if (e.Reason is not (SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock)) return;
        _locked = e.Reason == SessionSwitchReason.SessionLock;
        if (_locked) { _preview?.Close(); _pending?.Dispose(); _pending = null; }
        try { await ApplyMonitoringAsync(); } catch (Exception ex) { _log?.Write("session.monitor.failed", ex); }
    }));

    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => _preview?.Reposition()));

    public async Task ExitAsync()
    {
        if (_shuttingDown) return;
        if (_operations > 0 || _preview?.IsDragging == true)
        { _exitRequested = true; Notify("SnapDrop will exit after the active action finishes."); return; }
        if (Volatile.Read(ref _inflight) > 0)
        {
            _exitRequested = true; _paused = true;
            try { await ApplyMonitoringAsync(); } catch (Exception ex) { _log?.Write("exit.intake.stop.failed", ex); }
            Notify("SnapDrop will exit after pending screenshots finish processing.");
            if (_exitRequested && Volatile.Read(ref _inflight) == 0) await ExitAsync();
            return;
        }
        if (HasMemoryRecovery && MessageBox.Show("An unsaved image is held only in memory. Exit and discard it?", "SnapDrop",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        { _exitRequested = false; await ShowMemoryRecoveryAsync(); return; }
        _shuttingDown = true;
        _cleanupTimer?.Stop();
        try { if (_clipboard is not null) await _clipboard.SetEnabledAsync(false).WaitAsync(TimeSpan.FromSeconds(1)); }
        catch (Exception ex) { _log?.Write("clipboard.stop.timeout", ex); }
        _frames.Writer.TryComplete();
        try { if (_consumer is not null) await _consumer.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (Exception ex) { _log?.Write("processing.stop.timeout", ex); _stop.Cancel(); }
        _pending?.Dispose(); _pending = null;
        _preview?.Close();
        _log?.Write("application.stopped");
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shuttingDown = true;
        _stop.Cancel();
        SystemEvents.SessionSwitch -= SessionChanged;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        _clipboard?.Dispose();
        _memoryRecovery = null;
        Interlocked.Exchange(ref _memoryRecoveryLease, null)?.Dispose();
        _tray?.Dispose();
        Themes?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    public static string FriendlyError(Exception ex) => ex switch
    {
        LibraryFullException => ex.Message,
        InvalidDataException => ex.Message,
        UnauthorizedAccessException => "Windows denied access. Choose an accessible folder and try again.",
        DirectoryNotFoundException => "The folder is unavailable. Choose another folder and try again.",
        IOException => ex.Message,
        _ => "The action could not finish. The original was preserved where possible. Review the local log or try again."
    };
}
