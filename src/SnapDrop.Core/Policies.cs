namespace SnapDrop.Core;

public static class ImageLimits
{
    public const long MaxPixels = 32_000_000;
    public static void Validate(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
            throw new InvalidDataException("The clipboard image exceeds the 32-million-pixel limit or has invalid dimensions.");
    }
}

public static class ScreenshotNaming
{
    public static string Create(DateTimeOffset time, Guid id, ExportFormat format = ExportFormat.Png) =>
        $"Screenshot_{time.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{id:N}" + (format == ExportFormat.Png ? ".png" : ".jpg");

    public static ExportFormat FromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ExportFormat.Png,
        ".jpg" or ".jpeg" => ExportFormat.Jpeg,
        _ => throw new InvalidDataException("Use a .png, .jpg, or .jpeg filename.")
    };
}

public static class RetentionPolicy
{
    public static bool CanDelete(ScreenshotRecord item, DateTimeOffset now, int days, bool leased) =>
        !leased && !item.IsKept && now - item.ReceivedAtUtc >= TimeSpan.FromDays(days) &&
        (item.LastTransferAtUtc is null || now - item.LastTransferAtUtc.Value >= TimeSpan.FromHours(24));
}

public sealed class PreviewLifetime(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly HashSet<string> _pauses = [];
    private TimeSpan _remaining;
    private long _started;
    private bool _never;

    public void Reset(int seconds)
    {
        _never = seconds == 0;
        _remaining = TimeSpan.FromSeconds(seconds);
        _started = _clock.GetTimestamp();
    }

    public void Pause(string reason)
    {
        if (_pauses.Count == 0) Consume();
        _pauses.Add(reason);
    }

    public void Resume(string reason, bool allowFeedback = false)
    {
        if (!_pauses.Remove(reason)) return;
        if (allowFeedback && _remaining < TimeSpan.FromSeconds(2)) _remaining = TimeSpan.FromSeconds(2);
        if (_pauses.Count == 0) _started = _clock.GetTimestamp();
    }

    public bool IsPaused => _pauses.Count > 0;
    public bool Expired => !_never && !IsPaused && _remaining - _clock.GetElapsedTime(_started) <= TimeSpan.Zero;
    private void Consume()
    {
        _remaining -= _clock.GetElapsedTime(_started);
        if (_remaining < TimeSpan.Zero) _remaining = TimeSpan.Zero;
    }
}
