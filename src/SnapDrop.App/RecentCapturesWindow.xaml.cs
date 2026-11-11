using System.Collections.ObjectModel;
using System.Windows;
using SnapDrop.Core;

namespace SnapDrop.App;

public partial class RecentCapturesWindow : Window
{
    private readonly ObservableCollection<CaptureItem> _visible = [];
    private IReadOnlyList<ScreenshotRecord> _records = [];
    private bool _busy;
    private int _nextOffset;
    public RecentCapturesWindow()
    {
        InitializeComponent(); CaptureList.ItemsSource = _visible;
        Loaded += async (_, _) => await RefreshAsync();
        Closing += (_, e) => { if (_busy) { e.Cancel = true; StatusText.Text = "Wait for the active action to finish before closing."; } };
    }
    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            foreach (var item in _visible) item.Dispose();
            _visible.Clear();
            _nextOffset = 0;
            _records = await App.Host.Repository.LoadAsync();
            PolicyText.Text = $"Recovery copies expire after {App.Host.Settings.RetentionDays} days unless kept. Dismissing a thumbnail does not delete it. User exports are preserved.";
            await LoadPageAsync();
            StatusText.Text = _records.Count == 0 ? "No recovery copies yet. Capture with Win + Shift + S after enabling clipboard images in Settings." : $"{_records.Count} recovery copies.";
        }
        catch (Exception ex) { StatusText.Text = App.FriendlyError(ex); }
        finally { _busy = false; }
    }
    private async Task LoadPageAsync()
    {
        foreach (var record in _records.Skip(_nextOffset).Take(50))
        {
            _nextOffset++;
            try { _visible.Add(await App.Host.CreateItemAsync(record, IntPtr.Zero, false)); }
            catch (Exception ex) { StatusText.Text = "Some recovery images are unavailable. " + App.FriendlyError(ex); }
        }
        MoreButton.IsEnabled = _nextOffset < _records.Count;
    }
    private async Task WithSelection(Func<CaptureItem, Task> action)
    {
        if (_busy) return;
        if (CaptureList.SelectedItem is not CaptureItem item) { StatusText.Text = "Select a capture first."; return; }
        _busy = true;
        try { await App.Host.RunActionAsync(item, this, () => action(item)); StatusText.Text = item.Status; }
        finally { _busy = false; }
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void MoreClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        try { await LoadPageAsync(); } finally { _busy = false; }
    }
    private async void OpenClick(object sender, RoutedEventArgs e) => await WithSelection(async item =>
    { await App.Host.OpenViewerAsync(item.Record!); item.Status = "Opened the larger preview."; });
    private async void CopyClick(object sender, RoutedEventArgs e) => await WithSelection(App.Host.CopyAsync);
    private async void SaveAsClick(object sender, RoutedEventArgs e) => await WithSelection(item => App.Host.SaveAsAsync(item, this));
    private async void KeepClick(object sender, RoutedEventArgs e) => await WithSelection(App.Host.ToggleKeepAsync);
    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (CaptureList.SelectedItem is not CaptureItem item || _busy) return;
        if (MessageBox.Show(this, "Delete this recovery copy? Exported copies are preserved. Close any preview/viewer first.", "Delete recovery copy",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await WithSelection(async selected =>
        {
            var deleted = await App.Host.Repository.DeleteAsync(selected.Record!.Id);
            selected.Status = deleted ? "Recovery copy deleted. Exported copies were preserved." : "This image is in use. Dismiss its thumbnail and close its viewer before deleting.";
            if (deleted) _visible.Remove(selected);
        });
    }
}
