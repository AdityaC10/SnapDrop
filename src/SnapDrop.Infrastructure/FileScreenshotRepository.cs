using System.Buffers.Binary;
using System.Text.Json;
using SnapDrop.Core;

namespace SnapDrop.Infrastructure;

public sealed class FileScreenshotRepository : IScreenshotRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IImageEncoder _encoder;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly object _sync = new();
    private readonly Dictionary<Guid, ScreenshotRecord> _items = [];
    private readonly Dictionary<Guid, int> _leases = [];
    private bool _loaded;
    public List<string> RecoveryNotices { get; } = [];
    public string Root { get; }

    public FileScreenshotRepository(string root, IImageEncoder encoder, TimeProvider? clock = null)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _encoder = encoder;
        _clock = clock ?? TimeProvider.System;
        Directory.CreateDirectory(Root);
        RejectReparsePoint(Root);
    }

    public async Task<IReadOnlyList<ScreenshotRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_loaded)
            {
                foreach (var directory in Directory.EnumerateDirectories(Root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out var id)) continue;
                    try
                    {
                        RejectReparsePoint(directory);
                        var manifest = Path.Combine(directory, "capture.json");
                        if (!File.Exists(manifest))
                        { RecoveryNotices.Add($"Recovery files without an index remain in capture {id:N}. Use Open recovery folder to inspect them."); continue; }
                        RejectReparsePoint(manifest);
                        await using var stream = File.OpenRead(manifest);
                        var item = await JsonSerializer.DeserializeAsync<ScreenshotRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                            ?? throw new InvalidDataException("Empty manifest.");
                        Validate(item, id);
                        ValidatePng(GetOriginalPath(item), item.PixelWidth, item.PixelHeight);
                        lock (_sync) _items[id] = item;
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or ArgumentException or OverflowException)
                    { RecoveryNotices.Add($"Capture {id:N} could not be indexed. Its recovery files were preserved."); }
                }
                _loaded = true;
            }
            lock (_sync) return _items.Values.OrderByDescending(x => x.ReceivedAtUtc).ToArray();
        }
        finally { _writer.Release(); }
    }

    public async Task<ScreenshotRecord> CommitAsync(CapturedFrame frame, long budgetBytes, CancellationToken cancellationToken = default)
    {
        frame.Validate();
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RejectReparsePoint(Root);
            if (UsageBytes() >= budgetBytes) throw new LibraryFullException();
            var id = Guid.NewGuid();
            var directory = CaptureDirectory(id);
            Directory.CreateDirectory(directory);
            var filename = ScreenshotNaming.Create(frame.ReceivedAtUtc, id);
            var original = Path.Combine(directory, filename);
            await AtomicFile.WriteAsync(original, s => _encoder.EncodeAsync(frame, s, ExportFormat.Png, cancellationToken), false, cancellationToken).ConfigureAwait(false);
            var size = new FileInfo(original).Length;
            if (UsageBytes() > budgetBytes)
            {
                File.Delete(original);
                Directory.Delete(directory);
                throw new LibraryFullException();
            }
            var item = new ScreenshotRecord
            {
                Id = id, ReceivedAtUtc = frame.ReceivedAtUtc, PixelWidth = frame.Width, PixelHeight = frame.Height,
                ManagedFileName = filename, OriginalFileBytes = size
            };
            await WriteManifestAsync(item, cancellationToken).ConfigureAwait(false);
            lock (_sync) _items[id] = item;
            return item;
        }
        finally { _writer.Release(); }
    }

    public async Task<ScreenshotRecord> UpdateAsync(Guid id, Func<ScreenshotRecord, ScreenshotRecord> update, CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ScreenshotRecord current;
            lock (_sync) current = _items.TryGetValue(id, out var found) ? found : throw new FileNotFoundException("This recovery image is no longer available.");
            var next = update(current);
            Validate(next, id);
            if (next.ManagedFileName != current.ManagedFileName || next.PixelWidth != current.PixelWidth || next.PixelHeight != current.PixelHeight)
                throw new InvalidOperationException("Original image identity cannot change.");
            await WriteManifestAsync(next, cancellationToken).ConfigureAwait(false);
            lock (_sync) _items[id] = next;
            return next;
        }
        finally { _writer.Release(); }
    }

    public IDisposable AcquireLease(Guid id)
    {
        lock (_sync)
        {
            if (!_items.TryGetValue(id, out var item) || !File.Exists(GetOriginalPath(item)))
                throw new FileNotFoundException("This recovery image is no longer available.");
            _leases[id] = _leases.GetValueOrDefault(id) + 1;
        }
        return new Lease(() => { lock (_sync) { if (_leases[id] == 1) _leases.Remove(id); else _leases[id]--; } });
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { lock (_sync) return DeleteLocked(id); }
        finally { _writer.Release(); }
    }

    public async Task<int> CleanupAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var count = 0;
            lock (_sync)
            {
                foreach (var item in _items.Values.ToArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (RetentionPolicy.CanDelete(item, _clock.GetUtcNow(), retentionDays, _leases.ContainsKey(item.Id)) && DeleteLocked(item.Id)) count++;
                }
            }
            return count;
        }
        finally { _writer.Release(); }
    }

    public string GetOriginalPath(ScreenshotRecord item)
    {
        Validate(item, item.Id);
        var directory = CaptureDirectory(item.Id);
        RejectReparsePoint(directory);
        var path = Path.Combine(directory, item.ManagedFileName);
        if (File.Exists(path)) RejectReparsePoint(path);
        return path;
    }

    private bool DeleteLocked(Guid id)
    {
        if (_leases.ContainsKey(id) || !_items.TryGetValue(id, out var item)) return false;
        var directory = CaptureDirectory(id);
        if (!Directory.Exists(directory)) { _items.Remove(id); return true; }
        RejectReparsePoint(directory);
        File.Delete(GetOriginalPath(item));
        File.Delete(Path.Combine(directory, "capture.json"));
        var thumbnail = Path.Combine(directory, "thumbnail.png");
        if (File.Exists(thumbnail)) { RejectReparsePoint(thumbnail); File.Delete(thumbnail); }
        if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        _items.Remove(id);
        return true;
    }

    private string CaptureDirectory(Guid id)
    {
        RejectReparsePoint(Root);
        return Path.Combine(Root, id.ToString("N"));
    }

    private Task WriteManifestAsync(ScreenshotRecord item, CancellationToken cancellationToken) =>
        AtomicFile.WriteAsync(Path.Combine(CaptureDirectory(item.Id), "capture.json"),
            s => JsonSerializer.SerializeAsync(s, item, JsonOptions, cancellationToken), true, cancellationToken);

    private long UsageBytes()
    {
        long size = 0;
        foreach (var directory in Directory.EnumerateDirectories(Root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            RejectReparsePoint(directory);
            foreach (var file in Directory.EnumerateFiles(directory))
            { RejectReparsePoint(file); size = checked(size + new FileInfo(file).Length); }
        }
        return size;
    }

    private static void Validate(ScreenshotRecord item, Guid expectedId)
    {
        if (item.Id != expectedId || item.Id == Guid.Empty || item.SchemaVersion != 1 || item.Exports is null ||
            string.IsNullOrWhiteSpace(item.ManagedFileName) || Path.GetFileName(item.ManagedFileName) != item.ManagedFileName ||
            !item.ManagedFileName.StartsWith("Screenshot_", StringComparison.Ordinal) ||
            !item.ManagedFileName.EndsWith($"_{item.Id:N}.png", StringComparison.Ordinal) || item.ManagedFileName.Length > 120)
            throw new InvalidDataException("Invalid capture metadata.");
        ImageLimits.Validate(item.PixelWidth, item.PixelHeight);
    }

    public static void RejectReparsePoint(string path)
    {
        if ((Directory.Exists(path) || File.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Recovery storage cannot contain linked files or folders.");
    }

    private static void ValidatePng(string path, int width, int height)
    {
        Span<byte> header = stackalloc byte[24];
        using var stream = File.OpenRead(path);
        stream.ReadExactly(header);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!header[..8].SequenceEqual(signature) || BinaryPrimitives.ReadInt32BigEndian(header[16..20]) != width ||
            BinaryPrimitives.ReadInt32BigEndian(header[20..24]) != height) throw new InvalidDataException("Invalid original PNG.");
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

public sealed class LibraryFullException() : IOException("Recovery storage is full. Save or delete recovery images, then resume monitoring.") { }
