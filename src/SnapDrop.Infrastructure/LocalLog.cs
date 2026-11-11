namespace SnapDrop.Infrastructure;

public sealed class LocalLog
{
    private readonly string _root;
    private readonly object _sync = new();
    public LocalLog(string root) { _root = root; Directory.CreateDirectory(root); }
    public void Write(string eventId, Exception? exception = null, string? details = null)
    {
        lock (_sync)
        {
            try
            {
                var path = Path.Combine(_root, $"snapdrop-{DateTime.UtcNow:yyyy-MM-dd}.log");
                if (!File.Exists(path) || new FileInfo(path).Length < 1024 * 1024)
                    File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {eventId} {exception?.GetType().Name} {exception?.HResult:X} {details}{Environment.NewLine}");
                var files = Directory.GetFiles(_root, "snapdrop-*.log").OrderByDescending(x => x).ToArray();
                long total = 0;
                for (var i = 0; i < files.Length; i++)
                {
                    total += new FileInfo(files[i]).Length;
                    if (i >= 7 || total > 10L * 1024 * 1024) File.Delete(files[i]);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine("Local log unavailable."); }
        }
    }
}
