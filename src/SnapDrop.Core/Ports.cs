namespace SnapDrop.Core;

public interface IImageEncoder
{
    Task EncodeAsync(CapturedFrame frame, Stream output, ExportFormat format, CancellationToken cancellationToken = default);
}

public interface IOriginalTranscoder
{
    Task TranscodeAsync(string source, Stream output, ExportFormat format, CancellationToken cancellationToken);
}

public interface IScreenshotRepository
{
    Task<IReadOnlyList<ScreenshotRecord>> LoadAsync(CancellationToken cancellationToken = default);
    Task<ScreenshotRecord> CommitAsync(CapturedFrame frame, long budgetBytes, CancellationToken cancellationToken = default);
    Task<ScreenshotRecord> UpdateAsync(Guid id, Func<ScreenshotRecord, ScreenshotRecord> update, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CleanupAsync(int retentionDays, CancellationToken cancellationToken = default);
    IDisposable AcquireLease(Guid id);
    string GetOriginalPath(ScreenshotRecord item);
    string Root { get; }
}

public sealed record ExportResult(string Path, ExportFormat Format, bool HistoryUpdated);

public interface IExportService
{
    Task<ExportResult> ExportAsync(ScreenshotRecord item, string destination, bool overwrite, CancellationToken cancellationToken = default);
    Task<string> ExportRecoveryAsync(CapturedFrame frame, string destination, bool overwrite, CancellationToken cancellationToken = default);
}
