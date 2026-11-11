using System.Windows;
using SnapDrop.App.Services;
using SnapDrop.App.ViewModels;

namespace SnapDrop.App.Views;
public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
        DataContext = new OnboardingViewModel(new AppUiActions(App.Host, () => this), () => { Close(); App.Host.ShowMain("Home"); });
        SourceInitialized += (_, _) => App.Host.Themes.ApplyCaption(this);
    }
}
