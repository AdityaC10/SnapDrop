using System.Windows;
using System.Windows.Controls;

namespace SnapDrop.App.Views.Controls;
public partial class ScreenshotCard : UserControl
{
    public ScreenshotCard() => InitializeComponent();
    private void MoreClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu!.PlacementTarget = button; button.ContextMenu.IsOpen = true;
    }
}
