using System.ComponentModel;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapDrop.Core;
using Clipboard = System.Windows.Clipboard;

namespace SnapDrop.Windows;

public sealed class ClipboardService : IDisposable
{
    private const string Marker = "SnapDrop.ImageOrigin.v1";
    private readonly string _instanceToken = Guid.NewGuid().ToString("N");
    private readonly SemaphoreSlim _capacity = new(2, 2);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private HwndSource? _window;
    private bool _enabled;
    private bool _queued;
    private bool _disposed;
    private uint _lastSequence;
    private int _generation;
    private uint _notifiedSequence;
    private long _notifiedTimestamp;
    private DateTimeOffset _notifiedUtc;
    private IntPtr _notifiedMonitor;
    public event Action<CapturedFrame, IntPtr>? FrameReceived;
    public event Action<string, Exception?>? Notice;

    public ClipboardService()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "SnapDrop clipboard STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task Ready => _ready.Task;
    public async Task SetEnabledAsync(bool enabled)
    {
        await Ready.ConfigureAwait(false);
        await _dispatcher!.InvokeAsync(() => { _generation++; _enabled = enabled; _lastSequence = NativeMethods.GetClipboardSequenceNumber(); });
    }

    public void ReleaseFrame() => _capacity.Release();

    private void Run()
    {
        try
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _window = new HwndSource(new HwndSourceParameters("SnapDrop.Clipboard")
            { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
            _window.AddHook(Hook);
            if (!NativeMethods.AddClipboardFormatListener(_window.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
            _lastSequence = NativeMethods.GetClipboardSequenceNumber();
            _ready.TrySetResult();
            Dispatcher.Run();
        }
        catch (Exception ex) { _ready.TrySetException(ex); Notice?.Invoke("Clipboard monitoring is unavailable. Restart SnapDrop to retry.", ex); }
        finally
        {
            if (_window is not null)
            {
                NativeMethods.RemoveClipboardFormatListener(_window.Handle);
                _window.RemoveHook(Hook);
                _window.Dispose();
            }
        }
    }

    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.ClipboardUpdate && _enabled)
        {
            _notifiedSequence = NativeMethods.GetClipboardSequenceNumber();
            _notifiedTimestamp = Stopwatch.GetTimestamp();
            _notifiedUtc = DateTimeOffset.UtcNow;
            _notifiedMonitor = MonitorService.PointerMonitor();
            ScheduleRead();
        }
        return IntPtr.Zero;
    }

    private void ScheduleRead()
    {
        if (_queued || !_enabled || _disposed) return;
        _queued = true;
        _dispatcher!.BeginInvoke(new Action(async () => await ReadAsync()));
    }

    private async Task ReadAsync()
    {
        var generation = _generation;
        try
        {
            var delays = new[] { 0, 20, 50, 100, 200 };
            foreach (var delay in delays)
            {
                if (delay > 0) await Task.Delay(delay);
                if (!_enabled || _disposed || generation != _generation) return;
                var sequence = NativeMethods.GetClipboardSequenceNumber();
                if (sequence == 0) { _lastSequence = 0; return; }
                if (sequence == _lastSequence) return;
                try
                {
                    var data = Clipboard.GetDataObject();
                    if (data is null) { _lastSequence = sequence; return; }
                    if (data.GetDataPresent(Marker) && data.GetData(Marker) as string == _instanceToken)
                    { _lastSequence = NativeMethods.GetClipboardSequenceNumber(); return; }
                    if (!data.GetDataPresent(DataFormats.Bitmap) && !data.GetDataPresent(DataFormats.Dib) && !data.GetDataPresent("PNG"))
                    { _lastSequence = sequence; return; }
                    if (!_capacity.Wait(0))
                    {
                        _lastSequence = sequence;
                        Notice?.Invoke("Some clipboard changes could not be saved. Wait for processing to finish, then capture again.", null);
                        return;
                    }
                    var handedOff = false;
                    CapturedFrame? ownedFrame = null;
                    try
                    {
                        BitmapSource? image = null;
                        if (data.GetDataPresent("PNG"))
                        {
                            using var png = data.GetData("PNG") switch
                            {
                                Stream stream => stream,
                                byte[] bytes when bytes.Length <= 128 * 1024 * 1024 => new MemoryStream(bytes, false),
                                _ => null
                            };
                            if (png is not null)
                            {
                                if (png.CanSeek)
                                {
                                    png.Position = 0;
                                    if (png.Length > 128L * 1024 * 1024) throw new InvalidDataException("The clipboard image is too large.");
                                    var header = new byte[24];
                                    png.ReadExactly(header);
                                    ImageLimits.Validate(BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
                                    png.Position = 0;
                                }
                                image = BitmapDecoder.Create(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                            }
                        }
                        image ??= Clipboard.GetImage();
                        if (image is null) { _lastSequence = sequence; return; }
                        var frame = ownedFrame = sequence == _notifiedSequence
                            ? WpfImageCodec.Snapshot(image, _notifiedUtc, _notifiedTimestamp) : WpfImageCodec.Snapshot(image);
                        if (NativeMethods.GetClipboardSequenceNumber() != sequence) continue;
                        _lastSequence = sequence;
                        if (FrameReceived is not null)
                        { FrameReceived(frame, sequence == _notifiedSequence ? _notifiedMonitor : MonitorService.PointerMonitor()); handedOff = true; }
                        return;
                    }
                    finally { if (!handedOff) { ownedFrame?.Dispose(); _capacity.Release(); } }
                }
                catch (ExternalException) when (delay != delays[^1]) { /* Retry clipboard contention. */ }
            }
        }
        catch (Exception ex) when (ex is ExternalException or IOException or InvalidDataException or FileFormatException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            _lastSequence = NativeMethods.GetClipboardSequenceNumber();
            Notice?.Invoke("This clipboard image could not be read. Capture again, or copy a smaller image.", ex);
        }
        finally
        {
            _queued = false;
            // A pause/resume can change generation while this retry is pending. Its fresh baseline
            // still distinguishes later updates, so do not lose a new post-resume notification.
            if (_enabled && !_disposed && NativeMethods.GetClipboardSequenceNumber() != _lastSequence) ScheduleRead();
        }
    }

    public async Task CopyAsync(CapturedFrame frame)
    {
        using var retention = frame.Retain();
        frame.Validate();
        await Ready.ConfigureAwait(false);
        foreach (var delay in new[] { 0, 20, 50, 100, 200 })
        {
            if (delay > 0) await Task.Delay(delay).ConfigureAwait(false);
            try
            {
                await _dispatcher!.InvokeAsync(() =>
                {
                    var data = new DataObject();
                    data.SetData(DataFormats.Bitmap, WpfImageCodec.ToBitmap(frame));
                    data.SetData(Marker, _instanceToken);
                    Clipboard.SetDataObject(data, true);
                });
                return;
            }
            catch (ExternalException) when (delay != 200) { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_dispatcher is not null && !_dispatcher.HasShutdownStarted)
            _dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        // Delayed native clipboard rendering cannot be forcibly cancelled. Never block the UI joining this thread.
    }
}
