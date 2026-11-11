namespace SnapDrop.Core;

public enum ExportFormat { Png, Jpeg }
public enum AppTheme { System, Dark, Light }
public enum PreviewSize { Small, Medium, Large }
public enum FileNamePreset { Standard, Compact }

public sealed record ExportRecord(string Path, ExportFormat Format, DateTimeOffset SavedAtUtc);

public sealed record ScreenshotRecord
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid Id { get; init; }
    public required DateTimeOffset ReceivedAtUtc { get; init; }
    public required int PixelWidth { get; init; }
    public required int PixelHeight { get; init; }
    public required string ManagedFileName { get; init; }
    public required long OriginalFileBytes { get; init; }
    public bool IsKept { get; init; }
    public DateTimeOffset? LastTransferAtUtc { get; init; }
    public ExportRecord[] Exports { get; init; } = [];
    public string Description => $"{ReceivedAtUtc.ToLocalTime():g} · {PixelWidth} × {PixelHeight}";
}

public sealed class CapturedFrame : IDisposable
{
    private byte[] _pixels = [];
    private int _references = 1;
    private int _ownerReleased;
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
    public required byte[] Pixels { get => Volatile.Read(ref _pixels); init => _pixels = value ?? throw new ArgumentNullException(nameof(value)); }
    public DateTimeOffset ReceivedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public long ReceivedTimestamp { get; init; } = System.Diagnostics.Stopwatch.GetTimestamp();

    public void Validate()
    {
        ImageLimits.Validate(Width, Height);
        if (Stride != checked(Width * 4) || Pixels.LongLength != checked((long)Stride * Height))
            throw new InvalidDataException("The image pixel buffer is invalid.");
    }

    public IDisposable Retain()
    {
        while (true)
        {
            var current = Volatile.Read(ref _references);
            if (current == 0) throw new ObjectDisposedException(nameof(CapturedFrame));
            if (Interlocked.CompareExchange(ref _references, current + 1, current) == current) return new BufferLease(Release);
        }
    }
    public void Dispose() { if (Interlocked.Exchange(ref _ownerReleased, 1) == 0) Release(); }
    private void Release() { if (Interlocked.Decrement(ref _references) == 0) Interlocked.Exchange(ref _pixels, []); }
    private sealed class BufferLease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public bool OnboardingCompleted { get; init; }
    public bool MonitoringEnabled { get; init; }
    public bool StartWithWindows { get; init; }
    public int PreviewSeconds { get; init; } = 5;
    public int RetentionDays { get; init; } = 7;
    public long LibraryBudgetBytes { get; init; } = 1024L * 1024 * 1024;
    public string DefaultFolder { get; init; } = "";
    public string LastSaveFolder { get; init; } = "";
    public ExportFormat DefaultFormat { get; init; }
    public bool AutomaticExport { get; init; }
    public bool AutoOpenSaveAs { get; init; }
    public AppTheme Theme { get; init; } = AppTheme.System;
    public PreviewSize ThumbnailSize { get; init; } = PreviewSize.Medium;
    public bool ShowFloatingPreview { get; init; } = true;
    public bool RememberLastFolder { get; init; } = true;
    public bool GalleryListView { get; init; }
    public int JpegQuality { get; init; } = 90;
    public bool OrganizeByDate { get; init; }
    public FileNamePreset FileNamePreset { get; init; }
    public int OnboardingStep { get; init; }

    public AppSettings Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("This settings version is not supported.");
        if (PreviewSeconds != 0 && PreviewSeconds is < 2 or > 30)
            throw new InvalidDataException("Preview duration must be 2–30 seconds, or Never.");
        if (RetentionDays is < 1 or > 30) throw new InvalidDataException("Retention must be 1–30 days.");
        if (LibraryBudgetBytes is < 64L * 1024 * 1024 or > 100L * 1024 * 1024 * 1024)
            throw new InvalidDataException("The recovery library size is invalid.");
        if (!Enum.IsDefined(DefaultFormat)) throw new InvalidDataException("The export format is invalid.");
        if (!Enum.IsDefined(Theme) || !Enum.IsDefined(ThumbnailSize) || !Enum.IsDefined(FileNamePreset))
            throw new InvalidDataException("An appearance or naming preference is invalid.");
        if (JpegQuality is < 1 or > 100) throw new InvalidDataException("JPEG quality must be between 1 and 100.");
        if (OnboardingStep is < 0 or > 4) throw new InvalidDataException("The onboarding step is invalid.");
        if ((!string.IsNullOrWhiteSpace(DefaultFolder) && !Path.IsPathFullyQualified(DefaultFolder)) ||
            (!string.IsNullOrWhiteSpace(LastSaveFolder) && !Path.IsPathFullyQualified(LastSaveFolder)))
            throw new InvalidDataException("Save folders must be absolute paths.");
        if (AutomaticExport && string.IsNullOrWhiteSpace(DefaultFolder))
            throw new InvalidDataException("Choose a default folder for automatic saving.");
        return this;
    }
}
