using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using SnapDrop.Windows;

namespace SnapDrop.DropProbe;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        var output = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var target = new Border { AllowDrop = true, Background = SystemColors.ControlBrush, Padding = new Thickness(36),
            Child = new TextBlock { Text = "Drop a real screenshot file here", FontSize = 20, TextWrapping = TextWrapping.Wrap } };
        string? path = null;
        void Read()
        {
            try
            {
                if (path is null) { output.Text = "No file has been dropped."; return; }
                var image = WpfImageCodec.LoadBitmap(path);
                output.Text = $"Read succeeded at {DateTimeOffset.Now:O}\n{path}\n{image.PixelWidth} × {image.PixelHeight}\n{new FileInfo(path).Length} bytes\nFile path received through FileDrop.";
            }
            catch (Exception ex) { output.Text = "Read failed: " + ex.Message; }
        }
        target.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        target.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1) { path = files[0]; Read(); e.Effects = DragDropEffects.Copy; }
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        };
        var read = new Button { Content = "Read the received path again", Padding = new Thickness(12), Margin = new Thickness(16) };
        read.Click += (_, _) => Read();
        var panel = new DockPanel(); DockPanel.SetDock(target, Dock.Top); DockPanel.SetDock(read, Dock.Bottom);
        panel.Children.Add(target); panel.Children.Add(read); panel.Children.Add(output);
        app.Run(new Window { Title = "SnapDrop file-drop probe — test tool", Width = 680, Height = 460, Content = panel });
    }
}
