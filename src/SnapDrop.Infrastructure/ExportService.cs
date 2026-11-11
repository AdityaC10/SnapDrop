using SnapDrop.Core;

namespace SnapDrop.Infrastructure;

public sealed class ExportService(IScreenshotRepository repository, IOriginalTranscoder transcoder, IImageEncoder encoder,
    TimeProvider? clock = null) : IExportService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<ExportResult> ExportAsync(ScreenshotRecord item, string destination, bool overwrite, CancellationToken cancellationToken = default)
    {
        using var lease = repository.AcquireLease(item.Id);
        destination = ValidateDestination(destination);
        var format = ScreenshotNaming.FromPath(destination);
        await AtomicFile.WriteAsync(destination, async output =>
        {
            var source = repository.GetOriginalPath(item);
            if (format == ExportFormat.Png)
            {
                await using var input = File.OpenRead(source);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            else await transcoder.TranscodeAsync(source, output, format, cancellationToken).ConfigureAwait(false);
        }, overwrite, cancellationToken).ConfigureAwait(false);
        var historyUpdated = true;
        try
        {
            await repository.UpdateAsync(item.Id, current => current with
            { Exports = [.. current.Exports, new(destination, format, _clock.GetUtcNow())] }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException) { historyUpdated = false; }
        return new(destination, format, historyUpdated);
    }

    public async Task<string> ExportRecoveryAsync(CapturedFrame frame, string destination, bool overwrite, CancellationToken cancellationToken = default)
    {
        destination = ValidateDestination(destination);
        var format = ScreenshotNaming.FromPath(destination);
        await AtomicFile.WriteAsync(destination, s => encoder.EncodeAsync(frame, s, format, cancellationToken), overwrite, cancellationToken).ConfigureAwait(false);
        return destination;
    }

    private string ValidateDestination(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository.Root));
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase) || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a destination outside SnapDrop's managed recovery library.");
        return full;
    }
}
