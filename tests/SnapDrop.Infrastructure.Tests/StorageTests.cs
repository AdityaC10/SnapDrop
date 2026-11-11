using System.Text;
using System.Text.Json;
using SnapDrop.Core;
using Xunit;

namespace SnapDrop.Infrastructure.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SnapDrop-tests", Guid.NewGuid().ToString("N"));
    private readonly TestCodec _codec = new();
    private FileScreenshotRepository Repository() => new(Path.Combine(_root, "Library"), _codec);
    private static CapturedFrame Frame(int daysOld = 0) => new()
    { Width = 1, Height = 1, Stride = 4, Pixels = [255, 0, 0, 255], ReceivedAtUtc = DateTimeOffset.UtcNow.AddDays(-daysOld) };

    [Fact]
    public async Task OriginalAndMetadataSurviveRepositoryRestart()
    {
        var repo = Repository();
        var item = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(File.Exists(repo.GetOriginalPath(item)));
        var loaded = Assert.Single(await Repository().LoadAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal(item.PixelWidth, loaded.PixelWidth);
    }

    [Fact]
    public async Task SeparateIdenticalCapturesRemainSeparate()
    {
        var repo = Repository(); await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken); await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, (await repo.LoadAsync(cancellationToken: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task ActiveLeasesBlockDeleteAndCleanup()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(10), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        using (repo.AcquireLease(item.Id))
        {
            Assert.False(await repo.DeleteAsync(item.Id, cancellationToken: TestContext.Current.CancellationToken)); Assert.Equal(0, await repo.CleanupAsync(7, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(File.Exists(repo.GetOriginalPath(item)));
        }
        Assert.Equal(1, await repo.CleanupAsync(7, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CleanupPreservesKeptAndTransferredItems()
    {
        var repo = Repository(); var kept = await repo.CommitAsync(Frame(10), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken); var transferred = await repo.CommitAsync(Frame(10), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        await repo.UpdateAsync(kept.Id, x => x with { IsKept = true }, cancellationToken: TestContext.Current.CancellationToken);
        await repo.UpdateAsync(transferred.Id, x => x with { LastTransferAtUtc = DateTimeOffset.UtcNow }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, await repo.CleanupAsync(7, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, (await Repository().LoadAsync(cancellationToken: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task MetadataMutationsDoNotLoseConcurrentChanges()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        await Task.WhenAll(repo.UpdateAsync(item.Id, x => x with { IsKept = true }, cancellationToken: TestContext.Current.CancellationToken),
            repo.UpdateAsync(item.Id, x => x with { LastTransferAtUtc = DateTimeOffset.UtcNow }, cancellationToken: TestContext.Current.CancellationToken));
        var loaded = Assert.Single(await Repository().LoadAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(loaded.IsKept); Assert.NotNull(loaded.LastTransferAtUtc);
    }

    [Fact]
    public async Task PartialCommitIsNotReportedAsReady()
    {
        var repo = Repository(); _codec.FailEncode = true;
        await Assert.ThrowsAsync<IOException>(() => repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await Repository().LoadAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BudgetExhaustionDoesNotDeleteExistingCapture()
    {
        var repo = Repository(); var first = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<LibraryFullException>(() => repo.CommitAsync(Frame(), 1, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(File.Exists(repo.GetOriginalPath(first)));
    }

    [Fact]
    public async Task CorruptManifestIsIsolatedAndOriginalPreserved()
    {
        var repo = Repository(); var first = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken); var second = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repo.Root, first.Id.ToString("N"), "capture.json"), "{broken", cancellationToken: TestContext.Current.CancellationToken);
        var restarted = Repository(); var loaded = Assert.Single(await restarted.LoadAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(second.Id, loaded.Id); Assert.True(File.Exists(repo.GetOriginalPath(first))); Assert.NotEmpty(restarted.RecoveryNotices);
    }

    [Fact]
    public async Task TamperedPathCannotEscapeManagedDirectory()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        var tampered = item with { ManagedFileName = "../../outside.png" };
        Assert.Throws<InvalidDataException>(() => repo.GetOriginalPath(tampered));
    }

    [Fact]
    public async Task FailedExportPreservesExistingDestinationAndOriginal()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        var destination = Path.Combine(_root, "existing.jpg"); await File.WriteAllTextAsync(destination, "existing", cancellationToken: TestContext.Current.CancellationToken);
        _codec.FailEncode = true; var exports = new ExportService(repo, _codec, _codec);
        await Assert.ThrowsAsync<IOException>(() => exports.ExportAsync(item, destination, true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("existing", await File.ReadAllTextAsync(destination, cancellationToken: TestContext.Current.CancellationToken)); Assert.True(File.Exists(repo.GetOriginalPath(item)));
        Assert.Empty(Directory.GetFiles(_root, "*.partial"));
    }

    [Fact]
    public async Task ExportIsNotDeletedByRecoveryCleanup()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(10), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        var destination = Path.Combine(_root, "export.png"); var exports = new ExportService(repo, _codec, _codec);
        var result = await exports.ExportAsync(item, destination, false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.HistoryUpdated); Assert.Equal(1, await repo.CleanupAsync(7, cancellationToken: TestContext.Current.CancellationToken)); Assert.True(File.Exists(destination));
    }

    [Fact]
    public async Task ExportCannotOverwriteManagedOriginal()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<IOException>(() => new ExportService(repo, _codec, _codec).ExportAsync(item, repo.GetOriginalPath(item), true, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NoOverwriteExportProtectsExistingFile()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        var destination = Path.Combine(_root, "existing.png"); await File.WriteAllTextAsync(destination, "keep", cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<IOException>(() => new ExportService(repo, _codec, _codec).ExportAsync(item, destination, false, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("keep", await File.ReadAllTextAsync(destination, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CorruptSettingsRecoverLastKnownGood()
    {
        Directory.CreateDirectory(_root); var store = new JsonSettingsStore(_root);
        await store.SaveAsync(new AppSettings { PreviewSeconds = 8 }); await store.SaveAsync(new AppSettings { PreviewSeconds = 10 });
        await File.WriteAllTextAsync(Path.Combine(_root, "settings.json"), "broken", cancellationToken: TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(); Assert.Equal(8, loaded.Settings.PreviewSeconds); Assert.NotNull(loaded.Warning);
    }

    [Fact]
    public async Task UnknownFilesAreNeverRecursivelyPurged()
    {
        var repo = Repository(); var item = await repo.CommitAsync(Frame(10), 1024 * 1024, cancellationToken: TestContext.Current.CancellationToken);
        var extra = Path.Combine(repo.Root, item.Id.ToString("N"), "user-note.txt"); await File.WriteAllTextAsync(extra, "keep", cancellationToken: TestContext.Current.CancellationToken);
        await repo.CleanupAsync(7, cancellationToken: TestContext.Current.CancellationToken); Assert.True(File.Exists(extra));
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(_root);
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SnapDrop-tests")) + Path.DirectorySeparatorChar;
        if (full.StartsWith(expected, StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }

    private sealed class TestCodec : IImageEncoder, IOriginalTranscoder
    {
        public bool FailEncode { get; set; }
        // Test fixture encoder only; production uses WPF's real PNG/JPEG codecs.
        public async Task EncodeAsync(CapturedFrame frame, Stream output, ExportFormat format, CancellationToken cancellationToken = default)
        {
            if (FailEncode) { await output.WriteAsync(Encoding.ASCII.GetBytes("partial"), cancellationToken); throw new IOException("Injected encoder failure."); }
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYPj/HwADAgH/5ncLrgAAAABJRU5ErkJggg==");
            await output.WriteAsync(png, cancellationToken);
        }
        public Task TranscodeAsync(string source, Stream output, ExportFormat format, CancellationToken cancellationToken) => EncodeAsync(Frame(), output, format, cancellationToken);
    }
}
