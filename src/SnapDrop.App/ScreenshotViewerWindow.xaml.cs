using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapDrop.Windows;
using SnapDrop.Core;
using SnapDrop.App.Services;
using SnapDrop.App.ViewModels;

namespace SnapDrop.App;

public partial class ScreenshotViewerWindow : Window
{
    private readonly CaptureItem _item;
    private Point? _start;
    private bool _busy;
    private readonly CaptureActionsViewModel _viewModel;
    public bool UsesRecoveryFrame(CapturedFrame frame) => ReferenceEquals(_item.RecoveryFrame, frame);
    public ScreenshotViewerWindow(CaptureItem item)
    {
        _item = item;
        InitializeComponent();
        _viewModel = new(item, new AppUiActions(App.Host, () => this), Close); DataContext = _viewModel;
        SourceInitialized += (_, _) => App.Host.Themes.ApplyCaption(this);
        Loaded += async (_, _) =>
        {
            if (_item.Record is { } record)
                await Action(async () =>
                {
                    _item.Status = "Loading full-size image…";
                    FullImage.Source = await Task.Run(() => WpfImageCodec.LoadBitmap(App.Host.Repository.GetOriginalPath(record)));
                    Fit(); _item.Status = "Drag the image, copy it, or choose Save As.";
                });
            else if (_item.RecoveryFrame is { } frame)
                await Action(async () =>
                {
                    FullImage.Source = await Task.Run(() => WpfImageCodec.ToBitmap(frame));
                    Fit(); _item.Status = "This image is held in memory. Copy or Save As before closing.";
                });
            else Fit();
            await _viewModel.LoadRecentAsync();
        };
        Closing += (_, e) => { if (_busy || _viewModel.IsBusy) { e.Cancel = true; _item.Status = "Wait for the active action to finish before closing."; } };
        Closed += (_, _) => { _viewModel.Dispose(); _item.Dispose(); };
    }
    private async Task Action(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _viewModel.IsBusy = true;
        ActionPanel.IsEnabled = false;
        try { await App.Host.RunActionAsync(_item, this, action); }
        finally { _busy = false; _viewModel.IsBusy = false; ActionPanel.IsEnabled = true; }
    }
    private async void CopyClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.CopyAsync(_item));
    private async void SaveAsClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.SaveAsAsync(_item, this));
    private async void SaveDefaultClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.SaveDefaultAsync(_item));
    private async void KeepClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.ToggleKeepAsync(_item));
    private async void OpenSavedFolderClick(object sender, RoutedEventArgs e) => await Action(() =>
    {
        var path = (ExportLocations.SelectedItem as ExportRecord)?.Path ?? _item.LastExportPath ?? throw new IOException("Save the image first.");
        if (!File.Exists(path)) throw new IOException("The exported image is no longer at its recorded location. The recovery copy may still be available.");
        App.OpenFolder(Path.GetDirectoryName(path)!);
        _item.Status = "Opened the saved image's folder.";
        return Task.CompletedTask;
    });
    private void ZoomChanged(object sender, SelectionChangedEventArgs e) { if (FullImage is not null) Fit(); }
    private void ViewportChanged(object sender, SizeChangedEventArgs e) => Fit();
    private void Fit()
    {
        if (FullImage?.Source is not System.Windows.Media.Imaging.BitmapSource image || ImageScroll is null) return;
        var scale = Zoom.SelectedIndex switch { 1 => .5, 2 => 1.0, 3 => 2.0, _ => Math.Min(Math.Max(1, ImageScroll.ActualWidth - 24) / image.PixelWidth, Math.Max(1, ImageScroll.ActualHeight - 24) / image.PixelHeight) };
        FullImage.Width = Math.Max(1, image.PixelWidth * scale);
        FullImage.Height = Math.Max(1, image.PixelHeight * scale);
    }
    private void ImageDown(object sender, MouseButtonEventArgs e) { if (!_busy) _start = e.GetPosition(FullImage); }
    private async void ImageMove(object sender, MouseEventArgs e)
    {
        if (_busy || _item.IsRecovery || e.LeftButton != MouseButtonState.Pressed || _start is not { } start) return;
        var point = e.GetPosition(FullImage);
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _start = null;
        await Action(() => App.Host.DragAsync(_item, FullImage));
    }
}
