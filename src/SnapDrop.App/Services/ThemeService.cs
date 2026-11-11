using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using SnapDrop.Core;

namespace SnapDrop.App.Services;

public sealed class ThemeService : IDisposable
{
    private ResourceDictionary? _palette;
    private AppTheme _preference;
    public bool IsDark { get; private set; }
    public ThemeService() { SystemParameters.StaticPropertyChanged += SystemChanged; SystemEvents.UserPreferenceChanged += UserPreferenceChanged; }
    public void Apply(AppTheme preference)
    {
        _preference = preference;
        var dark = preference == AppTheme.Dark || preference == AppTheme.System && IsSystemDark();
        IsDark = dark && !SystemParameters.HighContrast;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (_palette is not null) dictionaries.Remove(_palette);
        var name = SystemParameters.HighContrast ? "HighContrast" : dark ? "Dark" : "Light";
        _palette = new ResourceDictionary { Source = new Uri($"/SnapDrop.App;component/Themes/Colors.{name}.xaml", UriKind.Relative) };
        dictionaries.Add(_palette);
        foreach (Window window in Application.Current.Windows) ApplyCaption(window);
    }
    public void ApplyCaption(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }
    private static bool IsSystemDark()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }
    private void SystemChanged(object? sender, PropertyChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(new Action(() => Apply(_preference)));
    private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(new Action(() => Apply(_preference)));
    public void Dispose() { SystemParameters.StaticPropertyChanged -= SystemChanged; SystemEvents.UserPreferenceChanged -= UserPreferenceChanged; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
