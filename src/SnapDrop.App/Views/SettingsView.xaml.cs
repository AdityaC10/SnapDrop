using System.Windows;
using System.Windows.Controls;

namespace SnapDrop.App.Views;
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    private void ViewportChanged(object sender, SizeChangedEventArgs e)
    {
        var visible = ActualWidth >= 1050;
        SummaryColumn.Width = new GridLength(visible ? 240 : 0); SummaryRail.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
