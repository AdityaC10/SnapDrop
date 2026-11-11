using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SnapDrop.Core;
using SnapDrop.Windows;
using SnapDrop.App.Services;
using SnapDrop.App.ViewModels;

namespace SnapDrop.App;

public partial class FloatingPreviewWindow : Window
{
    public CaptureItem Item { get; }
    private readonly PreviewLifetime _lifetime = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _feedbackTimer;
    private Point? _mouseStart;
    private bool _busy;
    private readonly CaptureActionsViewModel _viewModel;
    public bool IsDragging { get; private set; }
    public bool IsEngaged => _busy || IsDragging || IsMouseOver || IsKeyboardFocusWithin || PinToggle.IsChecked == true || _lifetime.IsPaused;

    public FloatingPreviewWindow(CaptureItem item, int seconds)
    {
        Item = item;
        InitializeComponent();
        _viewModel = new(item, new AppUiActions(App.Host, () => this), Close);
        DataContext = _viewModel;
        ApplySize();
        _lifetime.Reset(item.IsRecovery ? 0 : seconds);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); _lifetime.Resume("feedback"); App.Host.InteractionEnded(); };
        _timer.Tick += (_, _) => { if (_lifetime.Expired) { Close(); App.Host.InteractionEnded(); } };
        MouseEnter += (_, _) => { _lifetime.Pause("hover"); _viewModel.IsExpanded = true; };
        MouseLeave += (_, _) => { _lifetime.Resume("hover"); _viewModel.IsExpanded = _busy || IsKeyboardFocusWithin; App.Host.InteractionEnded(); };
        IsKeyboardFocusWithinChanged += (_, _) =>
        {
            if (IsKeyboardFocusWithin) { _lifetime.Pause("focus"); _viewModel.IsExpanded = true; }
            else { _lifetime.Resume("focus"); _viewModel.IsExpanded = _busy || IsMouseOver; App.Host.InteractionEnded(); }
        };
        ContentRendered += (_, _) => { Reposition(); _timer.Start(); App.Host.RecordPreviewRendered(item); };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(Reposition));
        Closed += (_, _) => { _timer.Stop(); _feedbackTimer.Stop(); _viewModel.Dispose(); };
        SizeChanged += (_, _) => { if (IsLoaded) Reposition(); };
        Closing += (_, e) => { if (_busy && !App.Host.IsStopping && !App.Host.IsSessionLocked) e.Cancel = true; };
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is 0x007E or 0x001A) Dispatcher.BeginInvoke(new Action(Reposition));
        return IntPtr.Zero;
    }

    public void Reposition() => MonitorService.PlacePreview(this, Item.Monitor);
    public void ApplySize()
    {
        Width = App.Host.Settings.ThumbnailSize switch { PreviewSize.Small => 290, PreviewSize.Large => 390, _ => 320 };
        ThumbnailImage.Height = (Width - 24) * .43;
    }
    public void UpdateDuration(int seconds) => _lifetime.Reset(Item.IsRecovery ? 0 : seconds);
    private void PinChanged(object sender, RoutedEventArgs e)
    {
        if (PinToggle.IsChecked == true) _lifetime.Pause("pin");
        else { _lifetime.Resume("pin"); App.Host.InteractionEnded(); }
    }
    public void SetBusy(bool busy)
    {
        _busy = busy;
        _viewModel.IsBusy = busy;
        _viewModel.IsExpanded = busy || IsMouseOver || IsKeyboardFocusWithin;
        if (busy)
        {
            _lifetime.Pause("action"); _feedbackTimer.Stop(); _lifetime.Resume("feedback");
        }
        else
        {
            _lifetime.Resume("action", true);
            _lifetime.Pause("feedback"); _feedbackTimer.Start();
        }
    }

    private async void CopyClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.CopyAsync(Item));
    private async void SaveAsClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.SaveAsAsync(Item, this));
    private async void SaveDefaultClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.SaveDefaultAsync(Item));
    private async void KeepClick(object sender, RoutedEventArgs e) => await Action(() => App.Host.ToggleKeepAsync(Item));
    private void RecentClick(object sender, RoutedEventArgs e) => App.Host.ShowRecent();
    private void SettingsClick(object sender, RoutedEventArgs e) => App.Host.ShowSettings();
    private void DismissClick(object sender, RoutedEventArgs e) { if (_busy) return; Close(); App.Host.InteractionEnded(); }
    private void WindowKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape && !_busy) { Close(); App.Host.InteractionEnded(); e.Handled = true; } }
    private Task Action(Func<Task> action) => _busy ? Task.CompletedTask : App.Host.RunActionAsync(Item, this, action);

    private void MoreClick(object sender, RoutedEventArgs e)
    {
        var menu = ((Button)sender).ContextMenu!;
        _lifetime.Pause("menu");
        menu.Closed -= MenuClosed;
        menu.Closed += MenuClosed;
        menu.PlacementTarget = (Button)sender;
        menu.IsOpen = true;
    }

    private void MenuClosed(object sender, RoutedEventArgs e) { _lifetime.Resume("menu"); App.Host.InteractionEnded(); }
    private async void OpenClick(object sender, RoutedEventArgs e) => await OpenAsync();
    private async Task OpenAsync()
    {
        if (Item.Record is { } record) await Action(async () => { await App.Host.OpenViewerAsync(record); Item.Status = "Opened the larger preview."; });
        else
        {
            var viewer = new ScreenshotViewerWindow(new CaptureItem { RecoveryFrame = Item.RecoveryFrame, Thumbnail = Item.Thumbnail });
            viewer.Show(); viewer.Activate();
        }
    }

    private void ImageDown(object sender, MouseButtonEventArgs e)
    {
        if (!_busy) _mouseStart = e.GetPosition(ThumbnailImage);
    }

    private async void ImageMove(object sender, MouseEventArgs e)
    {
        if (_busy || _mouseStart is not { } start || e.LeftButton != MouseButtonState.Pressed || Item.IsRecovery) return;
        var current = e.GetPosition(ThumbnailImage);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _mouseStart = null;
        IsDragging = true;
        try { await Action(() => App.Host.DragAsync(Item, ThumbnailImage)); }
        finally { IsDragging = false; App.Host.InteractionEnded(); }
    }

    private async void ImageUp(object sender, MouseButtonEventArgs e)
    {
        var start = _mouseStart; _mouseStart = null;
        if (start is not null && !_busy && !IsDragging) await OpenAsync();
    }
}
