using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapDrop.ClipboardProbe;

public static class Program
{
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        // Keep the original OLE object alive for this controlled test session. Do not
        // serialize or print clipboard contents; restoring may fail if its owner exits.
        var originalClipboard = Clipboard.GetDataObject();
        var panel = new StackPanel { Margin = new Thickness(24) };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 0) };
        var window = new Window { Title = "SnapDrop clipboard probe — changes the real clipboard", Width = 560, Height = 540, Content = panel };
        panel.Children.Add(new TextBlock { Text = "Synthetic clipboard publisher", FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "These actions replace the real clipboard. Use a dedicated test session. No content is transmitted.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        var pixels = new byte[320 * 180 * 4];
        for (var y = 0; y < 180; y++) for (var x = 0; x < 320; x++)
        {
            var offset = (y * 320 + x) * 4;
            pixels[offset] = (byte)(x < 160 ? 255 : 0); pixels[offset + 1] = (byte)(y < 90 ? 255 : 0);
            pixels[offset + 2] = (byte)(x % 16 == 0 || y % 16 == 0 ? 255 : 64); pixels[offset + 3] = 255;
        }
        var image = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgra32, null, pixels, 320 * 4); image.Freeze();
        void Add(string label, Action action)
        {
            var button = new Button { Content = label, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 6, 0, 0) };
            button.Click += (_, _) => { try { action(); status.Text = label + " completed."; } catch (Exception ex) { status.Text = ex.Message; } };
            panel.Children.Add(button);
        }
        Add("Publish 320 × 180 test image", () => Clipboard.SetImage(image));
        Add("Publish text only", () => Clipboard.SetText("SnapDrop synthetic text-only clipboard test"));
        Add("Restore clipboard from before this test session", () =>
        {
            if (originalClipboard is not null) Clipboard.SetDataObject(originalClipboard, true);
            else Clipboard.Clear();
        });
        var inspect = new Button { Content = "Inspect clipboard image (read only)", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 6, 0, 0) };
        inspect.Click += (_, _) =>
        {
            try
            {
                var received = Clipboard.GetImage();
                status.Text = received is null ? "No compatible bitmap image is on the clipboard." : $"Another process read {received.PixelWidth} × {received.PixelHeight} image pixels at {DateTimeOffset.Now:O}.";
            }
            catch (Exception ex) { status.Text = "Read failed: " + ex.Message; }
        };
        panel.Children.Add(inspect);
        var locked = new Button { Content = "Publish image, then hold clipboard for 500 ms", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 6, 0, 0) };
        locked.Click += async (_, _) =>
        {
            try
            {
                Clipboard.SetImage(image);
                if (!OpenClipboard(new WindowInteropHelper(window).Handle)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                locked.IsEnabled = false;
                try { await Task.Delay(500); } finally { CloseClipboard(); locked.IsEnabled = true; }
                status.Text = "Clipboard lock released.";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        panel.Children.Add(locked); panel.Children.Add(status);
        app.Run(window);
    }
}
