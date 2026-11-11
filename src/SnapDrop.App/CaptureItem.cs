using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using SnapDrop.Core;

namespace SnapDrop.App;

public sealed class CaptureItem : INotifyPropertyChanged, IDisposable
{
    private string _status = "Drag the image, copy it, or choose Save As.";
    private ScreenshotRecord? _record;
    private IDisposable? _lease;
    private CapturedFrame? _recovery;
    private IDisposable? _recoveryLease;
    private string? _lastExport;
    public ScreenshotRecord? Record { get => _record; set { _record = value; Changed(); Changed(nameof(Details)); Changed(nameof(IsKept)); Changed(nameof(RecoveryPolicy)); Changed(nameof(HasExport)); Changed(nameof(HasHistoryExports)); Changed(nameof(LastExportPath)); } }
    public CapturedFrame? RecoveryFrame { get => _recovery; init { _recovery = value; _recoveryLease = value?.Retain(); } }
    public required BitmapSource Thumbnail { get; init; }
    public IntPtr Monitor { get; init; }
    public long ReceivedTimestamp { get; set; }
    public bool IsRecovery => Record is null;
    public bool IsKept => Record?.IsKept == true;
    public string Details => Record is { } item ? item.Description : "Not saved to recovery storage";
    public string RecoveryPolicy => IsRecovery ? "This image exists only in memory. Copy or save it before exiting." : IsKept ? "Kept in recovery storage" : $"Recovery expires {Expiry():g}";
    public string? LastExportPath { get => _lastExport ?? Record?.Exports.LastOrDefault()?.Path; set { _lastExport = value; Changed(); Changed(nameof(HasExport)); } }
    public bool HasExport => LastExportPath is not null;
    public bool HasHistoryExports => Record?.Exports.Length > 0;
    public string Status { get => _status; set { _status = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void SetLease(IDisposable lease) => _lease = lease;
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    public void Dispose()
    {
        Interlocked.Exchange(ref _lease, null)?.Dispose();
        Interlocked.Exchange(ref _recoveryLease, null)?.Dispose();
        _recovery = null;
    }
    private DateTimeOffset Expiry()
    {
        var expiry = Record!.ReceivedAtUtc.AddDays(App.Host.Settings.RetentionDays);
        if (Record.LastTransferAtUtc is { } transfer && transfer.AddHours(24) > expiry) expiry = transfer.AddHours(24);
        return expiry.ToLocalTime();
    }
}
