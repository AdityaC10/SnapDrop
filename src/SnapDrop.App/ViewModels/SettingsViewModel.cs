using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using SnapDrop.App.Services;
using SnapDrop.Core;

namespace SnapDrop.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IUiActions _actions;
    private AppSettings _draft;
    private string _category = "General", _status = "Changes are saved automatically.";
    private long _version;
    public string Category { get => _category; set { if (Set(ref _category, value)) { foreach (var name in Categories) Changed("Is" + name.Replace(" ", "")); Changed(nameof(Title)); Changed(nameof(Description)); } } }
    public static string[] Categories { get; } = ["General", "Capture", "Save Files", "Shortcuts", "Appearance", "Advanced", "About"];
    public IReadOnlyList<string> NavigationCategories => Categories;
    public bool IsGeneral => Category == "General";
    public bool IsCapture => Category == "Capture";
    public bool IsSaveFiles => Category == "Save Files";
    public bool IsShortcuts => Category == "Shortcuts";
    public bool IsAppearance => Category == "Appearance";
    public bool IsAdvanced => Category == "Advanced";
    public bool IsAbout => Category == "About";
    public string Title => Category == "Save Files" ? "Save & files" : Category + (Category is "About" or "Shortcuts" ? "" : " settings");
    public string Description => Category switch
    {
        "General" => "Choose how SnapDrop runs and what happens after a screenshot.",
        "Capture" => "Windows Snipping Tool is the capture source for this companion release.",
        "Save Files" => "Choose where images go and how exported copies are named.",
        "Shortcuts" => "Windows owns capture shortcuts. SnapDrop does not replace them.",
        "Appearance" => "Personalize the app and the floating screenshot preview.",
        "Advanced" => "Manage recovery storage and inspect local diagnostics.",
        _ => "A screenshot, ready to place. Local-first and account-free."
    };
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string MonitoringStatus => _actions.MonitoringStatus;
    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    public bool Startup { get => _draft.StartWithWindows; set => Change(s => s with { StartWithWindows = value }); }
    public bool Monitoring { get => _draft.MonitoringEnabled; set => Change(s => s with { MonitoringEnabled = value }); }
    public bool ShowPreview { get => _draft.ShowFloatingPreview; set => Change(s => s with { ShowFloatingPreview = value }); }
    public int Duration { get => _draft.PreviewSeconds; set => Change(s => s with { PreviewSeconds = value }); }
    public string DefaultFolder { get => _draft.DefaultFolder; set => Change(s => s with { DefaultFolder = value.Trim() }); }
    public bool AutoSave { get => _draft.AutomaticExport; set => Change(s => s with { AutomaticExport = value }); }
    public bool AutoSaveAs { get => _draft.AutoOpenSaveAs; set => Change(s => s with { AutoOpenSaveAs = value }); }
    public bool RememberFolder { get => _draft.RememberLastFolder; set => Change(s => s with { RememberLastFolder = value }); }
    public bool OrganizeByDate { get => _draft.OrganizeByDate; set => Change(s => s with { OrganizeByDate = value }); }
    public int Format { get => (int)_draft.DefaultFormat; set { Change(s => s with { DefaultFormat = (ExportFormat)value }); Changed(nameof(IsJpeg)); } }
    public bool IsJpeg => Format == 1;
    public int JpegQuality { get => _draft.JpegQuality; set => Change(s => s with { JpegQuality = value }); }
    public int Naming { get => (int)_draft.FileNamePreset; set { Change(s => s with { FileNamePreset = (FileNamePreset)value }); Changed(nameof(NamingExample)); } }
    public string NamingExample => Naming == 0 ? "Screenshot_2026-11-11_10-24-30_<unique-id>.png" : "Snap_20261111_102430_<unique-id>.png";
    public int Theme { get => (int)_draft.Theme; set => Change(s => s with { Theme = (AppTheme)value }); }
    public int PreviewSize { get => (int)_draft.ThumbnailSize; set => Change(s => s with { ThumbnailSize = (SnapDrop.Core.PreviewSize)value }); }
    public int RetentionDays { get => _draft.RetentionDays; set => Change(s => s with { RetentionDays = value }); }
    public long BudgetMiB { get => _draft.LibraryBudgetBytes / (1024 * 1024); set => Change(s => s with { LibraryBudgetBytes = checked(value * 1024 * 1024) }); }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand OpenRecoveryCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public AsyncCommand ResumeCommand { get; }
    public AsyncCommand CaptureCommand { get; }
    public AsyncCommand CleanupCommand { get; }
    public AsyncCommand DiagnosticsCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public SettingsViewModel(IUiActions actions)
    {
        _actions = actions; _draft = actions.Settings;
        NavigateCommand = new(value => Category = value as string ?? "General");
        BrowseCommand = new(_ =>
        {
            var picker = new OpenFolderDialog { Title = "Choose default screenshot folder" };
            if (Directory.Exists(DefaultFolder)) picker.InitialDirectory = DefaultFolder;
            if (picker.ShowDialog() == true) { DefaultFolder = picker.FolderName; Changed(nameof(DefaultFolder)); }
        });
        OpenRecoveryCommand = new(_ => Try(() => actions.OpenFolder()));
        OpenLogsCommand = new(_ => Try(actions.OpenLogs));
        ResumeCommand = new(async _ => { await actions.TogglePauseAsync(); RefreshStatus(); }, Fail);
        CaptureCommand = new(_ => actions.CaptureAsync(), Fail);
        CleanupCommand = new(async _ => Status = $"Removed {await actions.CleanupAsync()} eligible recovery copies. User exports were preserved.", Fail);
        DiagnosticsCommand = new(async _ => { await actions.ExportDiagnosticsAsync(); Status = "Diagnostic export finished or was cancelled. No screenshot contents were included."; }, Fail);
        ResetCommand = new(async _ =>
        {
            if (MessageBox.Show("Reset preferences? Your screenshot history and exported files will be preserved.", "Reset SnapDrop preferences", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await actions.UpdateSettingsAsync(current => new AppSettings { OnboardingCompleted = current.OnboardingCompleted, DefaultFolder = current.DefaultFolder });
            _draft = actions.Settings; foreach (var property in GetType().GetProperties().Where(p => p.CanRead)) Changed(property.Name);
            Status = "Preferences reset. Clipboard monitoring is disabled; your images were preserved.";
        }, Fail);
    }
    public void RefreshStatus() => Changed(nameof(MonitoringStatus));
    private void Change(Func<AppSettings, AppSettings> change, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        try
        {
            var next = change(_draft).Validate(); if (next == _draft) return;
            _draft = next; Changed(property); Status = "Saving preferences…";
            var version = Interlocked.Increment(ref _version);
            _ = PersistAsync(change, version);
        }
        catch (Exception ex) { Fail(ex); Changed(property); }
    }
    private async Task PersistAsync(Func<AppSettings, AppSettings> change, long version)
    {
        try { await _actions.UpdateSettingsAsync(change); if (version == _version) Status = "Preferences saved."; RefreshStatus(); }
        catch (Exception ex) { Fail(ex); }
    }
    private void Try(Action action) { try { action(); } catch (Exception ex) { Fail(ex); } }
    private void Fail(Exception ex) => Status = ex is InvalidDataException ? ex.Message : App.FriendlyError(ex);
}
