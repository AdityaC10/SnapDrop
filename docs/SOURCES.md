# Technical sources and decision notes

Checked against official sources on 2026-11-11. These documents establish platform capabilities and constraints; they do not establish that SnapDrop has implemented or tested those capabilities. Defaults, schedules, module boundaries, storage policy, and performance targets are proposed engineering decisions.

## Runtime and platform

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy) — .NET 10 LTS is supported through November 14, 2028; the current table also lists .NET 11 through that date. Keep runtime patching current rather than assuming LTS needs no maintenance.
- [.NET installation/support on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows) — verify supported OS editions/builds when choosing the release matrix. This plan chooses Windows 11 x64 first and makes no blanket Windows 10 promise.
- [.NET SDK built-in templates](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates) — solution/template options; confirm exact WPF template framework choices with the installed SDK's help during bootstrap.

## Clipboard and file transfer

- [AddClipboardFormatListener](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener) — event-driven WM_CLIPBOARDUPDATE notification on a registered window; no need to poll.
- [GetClipboardSequenceNumber](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber) — clipboard change tracking; delayed rendering affects sequence behavior. It is not a screenshot-source attribution API.
- [WPF drag-and-drop](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop) — WPF supports data-object transfers within and across applications. Acceptance depends on the target.
- [Shell data transfer architecture](https://learn.microsoft.com/en-us/windows/win32/shell/dragdrop) — file/shell transfers use data objects and OLE interfaces. The plan starts with a real PNG file rather than virtual-file streaming.
- [Clipboard.SetDataObject](https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard.setdataobject?view=windowsdesktop-10.0) — persistent-copy overload supports retaining clipboard data after process exit; actual image interoperability remains a test requirement.

## Windows UI and future capture

- [SaveFileDialog](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.savefiledialog?view=windowsdesktop-10.0) — native filename/destination selection; writing the completed output remains the application's responsibility.
- [NotifyIcon](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-10.0) — notification-area integration without introducing a browser runtime.
- [High-DPI desktop application development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows) — awareness and handling of per-monitor display-scale changes require deliberate implementation and testing.
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) — reserved Windows-key shortcuts and registration conflicts; handle failures instead of forcing a system shortcut takeover.
- [Windows screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture) — Windows.Graphics.Capture acquires window/display frames; check support, manage frame pools/sessions, and qualify HDR format handling. A rectangle overlay is separate UI/coordinate work.
- [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) — WDA_EXCLUDEFROMCAPTURE is supported from Windows 10 version 2004, applies to owned top-level windows, and is not a security/DRM guarantee.

## Deployment

- [dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) — produce deployment output for a specific runtime and self-contained/framework-dependent model.
- [Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities) — Windows/COM and desktop-framework constraints motivate untrimmed beta packaging.
- [Single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) — bundling and self-contained publishing are separate choices, with platform-specific behavior to qualify.
- [Inno Setup official information](https://jrsoftware.org/isinfo.php) — candidate installer supports per-user/nonadministrative deployment; review version-specific documentation and terms before implementation.

## Implementation dependency and CI references

- [xUnit.net v3 setup](https://xunit.net/docs/getting-started/v3/getting-started) — executable test projects and VSTest integration using the Visual Studio adapter and Microsoft.NET.Test.Sdk. Implementation uses xUnit v3, not the deprecated v2 package.
- [xunit.v3 4.0.3](https://www.nuget.org/packages/xunit.v3/4.0.3), [Visual Studio adapter 4.0.1](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.1), and [Microsoft.NET.Test.Sdk 18.11.1](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.11.1) — centrally pinned test dependencies; restore and compatibility execution remain pending.
- [actions/checkout v7.0.1 commit](https://github.com/actions/checkout/commit/3d3c42e5aac5ba805825da76410c181273ba90b1), [actions/setup-dotnet v6.1.0 commit](https://github.com/actions/setup-dotnet/commit/0f4a354dd5547a28c70b7853dbd95d5b67886e73), and [actions/upload-artifact v7.0.2 commit](https://github.com/actions/upload-artifact/commit/cf430e030ddbb5b0abf93d22962f4752f3646cd9) — workflow revisions verified against the official repositories and pinned to commit hashes. The workflow has not run.

## Evidence still required

The following are hypotheses/targets until measured on SnapDrop builds: p95 preview latency, memory/CPU use, mixed-DPI placement correctness, lossless PNG round trips, clipboard self-suppression, file retention through external consumption, each third-party drop target, and successful clean-machine installation.
