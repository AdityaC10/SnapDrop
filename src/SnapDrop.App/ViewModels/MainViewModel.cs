using SnapDrop.App.Services;

namespace SnapDrop.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IUiActions _actions;
    private string _page = "Home", _notice = "";
    public GalleryViewModel Gallery { get; }
    public SettingsViewModel Settings { get; }
    public string Page { get => _page; set { if (Set(ref _page, value)) { Changed(nameof(IsHome)); Changed(nameof(IsHistory)); Changed(nameof(IsSettings)); Changed(nameof(IsGallery)); Changed(nameof(PageTitle)); } } }
    public bool IsHome => Page == "Home";
    public bool IsHistory => Page == "History";
    public bool IsSettings => Page == "Settings";
    public bool IsGallery => !IsSettings;
    public string PageTitle => IsHome ? "Your screenshots, ready to use anywhere" : IsHistory ? "Screenshot history" : "Settings";
    public string MonitoringStatus => _actions.MonitoringStatus;
    public string Notice { get => _notice; set => Set(ref _notice, value); }
    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);
    public RelayCommand NavigateCommand { get; }
    public RelayCommand DismissNoticeCommand { get; }
    public MainViewModel(IUiActions actions)
    {
        _actions = actions; Gallery = new(actions); Settings = new(actions);
        NavigateCommand = new(value => { Page = value as string ?? "Home"; if (IsGallery) _ = Gallery.RefreshAsync(); });
        DismissNoticeCommand = new(_ => { Notice = ""; Changed(nameof(HasNotice)); });
    }
    public void UpdateState(string notice = "")
    {
        Changed(nameof(MonitoringStatus)); Settings.RefreshStatus();
        if (!string.IsNullOrEmpty(notice)) { Notice = notice; Changed(nameof(HasNotice)); }
    }
}
