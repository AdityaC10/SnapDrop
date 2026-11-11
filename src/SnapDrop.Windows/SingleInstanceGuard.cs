using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace SnapDrop.Windows;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipe;
    private readonly CancellationTokenSource _stop = new();
    public bool IsPrimary { get; }
    public event Action? OpenRequested;

    public SingleInstanceGuard()
    {
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("The current Windows identity is unavailable.");
        var name = $"SnapDrop-{identity}-{Process.GetCurrentProcess().SessionId}";
        _pipe = name;
        _mutex = new Mutex(false, "Local\\" + name);
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public void Listen() => _ = ListenAsync();
    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(_pipe, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var command = new byte[4];
                await pipe.ReadExactlyAsync(command, timeout.Token).ConfigureAwait(false);
                if (Encoding.ASCII.GetString(command) == "OPEN") OpenRequested?.Invoke();
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                Debug.WriteLine("Second-instance request could not be read.");
                try { await Task.Delay(250, _stop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public async Task NotifyPrimaryAsync()
    {
        await using var pipe = new NamedPipeClientStream(".", _pipe, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(1500).ConfigureAwait(false);
        await pipe.WriteAsync(Encoding.ASCII.GetBytes("OPEN")).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
