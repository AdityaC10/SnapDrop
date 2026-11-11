using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapDrop.App.ViewModels;

namespace SnapDrop.App.Views;
public partial class GalleryView : UserControl
{
    public GalleryView() => InitializeComponent();
    private void ViewportChanged(object sender, SizeChangedEventArgs e)
    {
        var show = ActualWidth >= 980; DetailRail.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DetailColumn.Width = new GridLength(show ? 280 : 0);
    }
    private void OpenSelected(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is GalleryViewModel vm && vm.OpenCommand.CanExecute(vm.Selected)) vm.OpenCommand.Execute(vm.Selected);
    }
}
