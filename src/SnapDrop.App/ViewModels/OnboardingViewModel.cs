using SnapDrop.App.Services;

namespace SnapDrop.App.ViewModels;

public sealed class OnboardingViewModel : ObservableObject
{
    private readonly IUiActions _actions;
    private readonly Action _finished;
    private int _step;
    private string _status = "";
    public SettingsViewModel Preferences { get; }
    public int Step { get => _step; private set { Set(ref _step, value); foreach (var name in new[] { nameof(Heading), nameof(Body), nameof(IsSetup), nameof(IsLast), nameof(CanGoBack), nameof(NextLabel) }) Changed(name); } }
    public string Heading => Step switch { 0 => "Welcome to SnapDrop", 1 => "Capture. Place. Keep moving.", 2 => "Make it work your way", 3 => "A shortcut you already know", _ => "You're ready to capture" };
    public string Body => Step switch
    {
        0 => "A faster way to use screenshots on Windows. Take a screenshot, then drag the floating image straight into your work.",
        1 => "Drag a real PNG into a compatible app or folder. Copy to the clipboard, choose Save As, and revisit captures in your local history.",
        2 => "Clipboard image monitoring is optional. When enabled, it can receive images copied from other apps as well as screenshots. Nothing is uploaded.",
        3 => "Win + Shift + S belongs to Windows Snipping Tool. SnapDrop previews the image after Windows copies it. Native capture shortcuts and annotation tools come in later releases.",
        _ => "Try Windows capture after enabling clipboard images, or finish now and configure it later. No account is needed."
    };
    public bool IsSetup => Step == 2;
    public bool IsLast => Step == 4;
    public bool CanGoBack => Step > 0;
    public string NextLabel => IsLast ? "Finish setup" : "Next";
    public string Status { get => _status; private set => Set(ref _status, value); }
    public AsyncCommand NextCommand { get; }
    public AsyncCommand BackCommand { get; }
    public AsyncCommand SkipCommand { get; }
    public AsyncCommand CaptureCommand { get; }
    public OnboardingViewModel(IUiActions actions, Action finished)
    {
        _actions = actions; _finished = finished; _step = actions.Settings.OnboardingStep; Preferences = new(actions);
        NextCommand = new(async _ =>
        {
            if (IsLast) { await actions.UpdateSettingsAsync(s => s with { OnboardingCompleted = true, OnboardingStep = 4 }); finished(); }
            else { Step++; await actions.UpdateSettingsAsync(s => s with { OnboardingStep = Step }); BackCommand?.Refresh(); }
        }, Fail);
        BackCommand = new(async _ => { if (Step > 0) Step--; await actions.UpdateSettingsAsync(s => s with { OnboardingStep = Step }); BackCommand?.Refresh(); }, Fail, _ => CanGoBack);
        SkipCommand = new(async _ => { await actions.UpdateSettingsAsync(s => s with { OnboardingCompleted = true }); finished(); }, Fail);
        CaptureCommand = new(_ => actions.CaptureAsync(), Fail);
    }
    private void Fail(Exception ex) => Status = App.FriendlyError(ex);
}
