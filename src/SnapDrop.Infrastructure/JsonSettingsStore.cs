using System.Text.Json;
using SnapDrop.Core;

namespace SnapDrop.Infrastructure;

public sealed record SettingsLoadResult(AppSettings Settings, string? Warning);

public sealed class JsonSettingsStore(string root)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(root, "settings.json");
    private readonly string _backup = Path.Combine(root, "settings.previous.json");

    public async Task<SettingsLoadResult> LoadAsync()
    {
        if (!File.Exists(_path)) return new(new AppSettings(), null);
        try { return new(await ReadAsync(_path).ConfigureAwait(false), null); }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            try { return new(await ReadAsync(_backup).ConfigureAwait(false), "Settings were damaged. The previous settings were recovered."); }
            catch (Exception backupError) when (backupError is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            { return new(new AppSettings(), "Settings could not be loaded. Safe defaults are in use; review Settings before enabling monitoring."); }
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings.Validate();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(root);
            if (File.Exists(_path))
            {
                AppSettings? old = null;
                try { old = await ReadAsync(_path).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { /* Retain good backup. */ }
                if (old is not null)
                    await AtomicFile.WriteAsync(_backup, s => JsonSerializer.SerializeAsync(s, old, Options), true).ConfigureAwait(false);
            }
            await AtomicFile.WriteAsync(_path, s => JsonSerializer.SerializeAsync(s, settings, Options), true).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static async Task<AppSettings> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<AppSettings>(stream).ConfigureAwait(false)
            ?? throw new InvalidDataException("Settings are empty.")).Validate();
    }
}
