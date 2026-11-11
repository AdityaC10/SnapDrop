using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SnapDrop.Windows;

public static class MonitorService
{
    public static IntPtr PointerMonitor()
    {
        if (NativeMethods.GetCursorPos(out var point)) return NativeMethods.MonitorFromPoint(point, 2);
        return NativeMethods.MonitorFromWindow(NativeMethods.GetForegroundWindow(), 2);
    }

    public static void PlacePreview(Window window, IntPtr monitor)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            monitor = PointerMonitor();
            if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;
        }
        // First move into the destination monitor so WPF processes its DPI transition, then anchor using physical bounds.
        if (NativeMethods.MonitorFromWindow(hwnd, 2) != monitor)
            NativeMethods.SetWindowPos(hwnd, new IntPtr(-1), info.Work.Left, info.Work.Top, 0, 0, 0x0010 | 0x0001);
        NativeMethods.GetWindowRect(hwnd, out var rect);
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        var inset = (int)Math.Round(16 * dpi.DpiScaleX);
        NativeMethods.SetWindowPos(hwnd, new IntPtr(-1), Math.Max(info.Work.Left, info.Work.Right - (rect.Right - rect.Left) - inset),
            Math.Max(info.Work.Top, info.Work.Bottom - (rect.Bottom - rect.Top) - inset), 0, 0, 0x0010 | 0x0001);
    }
}
