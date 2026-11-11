using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using SnapDrop.Core;

namespace SnapDrop.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var settings = App.Host.Settings;
        if (!settings.OnboardingCompleted) { Heading.Text = "A screenshot, ready to place"; ApplyButton.Content = "Save preferences and continue"; }
        Monitoring.IsChecked = settings.MonitoringEnabled;
        PreviewSeconds.Text = settings.PreviewSeconds.ToString(CultureInfo.InvariantCulture);
        DefaultFolder.Text = settings.DefaultFolder;
        Format.SelectedIndex = settings.DefaultFormat == ExportFormat.Png ? 0 : 1;
        AutomaticExport.IsChecked = settings.AutomaticExport;
        AutoSaveAs.IsChecked = settings.AutoOpenSaveAs;
        RetentionDays.Text = settings.RetentionDays.ToString(CultureInfo.InvariantCulture);
        Budget.Text = (settings.LibraryBudgetBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        Startup.IsChecked = settings.StartWithWindows;
        StatusText.Text = App.Host.GlobalStatus;
        Loaded += async (_, _) =>
        {
            try
            {
                var items = await App.Host.Repository.LoadAsync();
                UsageText.Text = $"{items.Count} indexed recovery copies · {items.Sum(x => x.OriginalFileBytes) / (1024.0 * 1024):F1} MiB of indexed image data. Retention and recent transfer protection apply.";
            }
            catch (Exception ex) { ShowStatus(App.FriendlyError(ex)); }
        };
    }

    public void ShowStatus(string status) => StatusText.Text = status;
    private async void ApplyClick(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        try
        {
            var folder = DefaultFolder.Text.Trim();
            if (!string.IsNullOrWhiteSpace(folder) && !Path.IsPathFullyQualified(folder)) throw new IOException("Choose an absolute default folder path.");
            var settings = App.Host.Settings with
            {
                OnboardingCompleted = true, MonitoringEnabled = Monitoring.IsChecked == true,
                PreviewSeconds = int.Parse(PreviewSeconds.Text, CultureInfo.InvariantCulture),
                DefaultFolder = folder, DefaultFormat = Format.SelectedIndex == 0 ? ExportFormat.Png : ExportFormat.Jpeg,
                AutomaticExport = AutomaticExport.IsChecked == true, AutoOpenSaveAs = AutoSaveAs.IsChecked == true,
                RetentionDays = int.Parse(RetentionDays.Text, CultureInfo.InvariantCulture),
                LibraryBudgetBytes = checked(long.Parse(Budget.Text, CultureInfo.InvariantCulture) * 1024 * 1024),
                StartWithWindows = Startup.IsChecked == true
            };
            await App.Host.SaveSettingsAsync(settings);
            ShowStatus("Settings saved. " + (settings.MonitoringEnabled ? "New clipboard images will be previewed when monitoring is active." : "Clipboard monitoring is disabled."));
            ApplyButton.Content = "Apply settings";
            Heading.Text = "Settings";
        }
        catch (Exception ex) { ShowStatus(ex is FormatException or OverflowException ? "Enter valid whole numbers for duration, retention, and storage budget." : App.FriendlyError(ex)); }
        finally { ApplyButton.IsEnabled = true; }
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose the default screenshot folder" };
        if (Directory.Exists(DefaultFolder.Text)) dialog.InitialDirectory = DefaultFolder.Text;
        if (dialog.ShowDialog(this) == true) DefaultFolder.Text = dialog.FolderName;
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void RecentClick(object sender, RoutedEventArgs e) => App.Host.ShowRecent();
    private void RecoveryClick(object sender, RoutedEventArgs e) => TryOpen(App.Host.Repository.Root);
    private void LogsClick(object sender, RoutedEventArgs e) => TryOpen(Path.Combine(App.Host.DataRoot, "Logs"));
    private void TryOpen(string path) { try { App.OpenFolder(path); } catch (Exception ex) { ShowStatus(App.FriendlyError(ex)); } }
    private async void ResumeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.Host.ResumeMonitoringAsync();
            ShowStatus(App.Host.MonitoringActive ? "Clipboard monitoring is active." : "Monitoring is inactive. Enable clipboard images in Settings, or unlock the session.");
        }
        catch (Exception ex) { ShowStatus(App.FriendlyError(ex)); }
    }
}
