namespace SnapDrop.Infrastructure;

public static class AtomicFile
{
    public static async Task WriteAsync(string destination, Func<Stream, Task> write, bool overwrite,
        CancellationToken cancellationToken = default)
    {
        destination = Path.GetFullPath(destination);
        var directory = Path.GetDirectoryName(destination) ?? throw new IOException("Choose a destination folder.");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The destination folder is unavailable.");
        var temporary = Path.Combine(directory, $".snapdrop-{Guid.NewGuid():N}.partial");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await write(stream).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (overwrite && File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
