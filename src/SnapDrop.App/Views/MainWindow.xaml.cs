using System.Windows;
using System.Windows.Threading;
using SnapDrop.App.Services;
using SnapDrop.App.ViewModels;

namespace SnapDrop.App.Views;
public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(300) };
    public MainWindow()
    {
        InitializeComponent(); ViewModel = new(new AppUiActions(App.Host, () => this)); DataContext = ViewModel;
        SourceInitialized += (_, _) => App.Host.Themes.ApplyCaption(this);
        Loaded += async (_, _) => await ViewModel.Gallery.RefreshAsync();
        Activated += (_, _) => _refresh.Start();
        _refresh.Tick += async (_, _) => { _refresh.Stop(); if (IsVisible && WindowState != WindowState.Minimized) await ViewModel.Gallery.RefreshAsync(); };
        App.Host.UiStateChanged += UiStateChanged;
        App.Host.LibraryChanged += LibraryChanged;
        Closed += (_, _) => { _refresh.Stop(); App.Host.UiStateChanged -= UiStateChanged; App.Host.LibraryChanged -= LibraryChanged; };
    }
    public void Navigate(string page) { ViewModel.Page = page; Show(); Activate(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; }
    private void UiStateChanged(object? sender, EventArgs e) => ViewModel.UpdateState(App.Host.GlobalStatus);
    private void LibraryChanged(object? sender, EventArgs e) { if (IsVisible && WindowState != WindowState.Minimized) _refresh.Start(); }
}
