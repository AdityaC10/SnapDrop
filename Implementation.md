H# SnapDrop: end-to-end implementation plan

Prepared: 2026-11-11. Status: implementation blueprint; no application code implemented.

## 1. Product contract

Build a Windows tray application that makes a newly captured screenshot immediately available as an object the user can drag, copy, or save to a chosen destination.

The first release must complete this real workflow:

1. User enables clipboard image monitoring during onboarding.
2. User captures with Windows Snipping Tool using Win + Shift + S.
3. Windows publishes an image to the clipboard.
4. SnapDrop copies the pixels into its own memory, commits a managed PNG, and shows a floating thumbnail.
5. User drags the PNG into a supported target, copies the image, or uses Save As.
6. Closing or timing out the thumbnail leaves the managed capture available in Recent captures until its disclosed retention expires.

Both drag and destination selection ship in v0.1. Save As is always one explicit click away. An optional “Open Save As after each capture” setting supports users who want an immediate destination prompt; it defaults off to preserve the uninterrupted thumbnail workflow.

“Drag anywhere” means anywhere that accepts the offered file format and Windows drag operation. It does not promise every app, editor pane, browser page, or elevated process will accept the drop.

## 2. Scope and platform decisions

### v0.1: Screenshot Companion

Include:

- Single instance per interactive user session and notification-area icon.
- First-run explanation and opt-in monitoring of clipboard images.
- Event-driven ingestion, bounded clipboard retries, self-write suppression, and duplicate event protection.
- Durable managed PNG originals and metadata; recent captures after restart.
- Floating thumbnail with Drag, Copy, Save As, Save to default folder, Pin, and Dismiss.
- PNG and JPEG export; native Save As dialog; lossless managed originals.
- Preview timeout, hover/focus/dialog/drag pause, burst handling, and reopen from Recent.
- Windows 11 x64, multiple displays, mixed DPI, negative desktop coordinates, keyboard access, and high contrast.
- Configurable retention, local diagnostics, startup preference, and a signed release package.

Defer to v0.2:

- SnapDrop-owned region capture and configurable non-reserved shortcut.
- Tray capture commands, native monitor/window capture, and capture cursor options.
- Windows ARM64 qualification after access to real ARM64 hardware.
- Additional transfer formats only where compatibility evidence warrants them.

Defer beyond v0.2:

- Annotation, OCR, recording, scrolling capture, cloud synchronization, AI, global media search, virtual-file streaming, and automatic updates.
- General Windows 10 support. It requires a separate edition/build/runtime support decision and test matrix; do not advertise all Windows 10 installations as supported. See [Microsoft's Windows/.NET matrix](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

### Stack

- **C# + .NET 10:** supported LTS baseline. Pin a current stable 10.x SDK during bootstrap and stay on supported patches. .NET 10 and .NET 11 currently have the same listed end-of-support date, November 14, 2028; choosing 10 here is a baseline decision, not a claim of a later remaining support date. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
- **WPF/XAML:** preview windows, recent captures, settings, and MVVM presentation. No browser engine.
- **System.Windows.Forms.NotifyIcon:** tray integration inside the WPF process; no separate WinForms application.
- **Win32 clipboard notifications:** AddClipboardFormatListener/WM_CLIPBOARDUPDATE, isolated behind a Windows adapter. [API documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener).
- **WPF DataObject/OLE:** real file-drop payload, Copy effect, and STA drag execution. [WPF drag-and-drop](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop).
- **WPF imaging:** decode, pixel snapshot, thumbnail generation, PNG/JPEG encode. Retain original dimensions; encode on a worker after copying data out of the clipboard.
- **System.Text.Json:** versioned settings and one metadata file per capture; a rebuildable in-memory history index.
- **CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection:** small, focused dependencies. Choose and lock compatible stable versions at bootstrap.
- **Microsoft.Extensions.Logging:** bounded local file sink; no image or clipboard contents in logs.
- **xUnit + Microsoft.NET.Test.Sdk:** core tests and isolated Windows integration tests; confirm compatible package versions together.
- **Self-contained x64 publish + per-user installer:** first public beta; MSIX is an alternative distribution workstream, not a prerequisite for proving the workflow.

This is a modular desktop application in one process. No HTTP API, database server, web frontend, Windows Service, or worker daemon is needed.

## 3. Proposed code repository

The tree below describes files to implement. Only the documentation files exist at plan delivery.

```text
SnapDrop/
├── SnapDrop.sln
├── global.json                         # selected, supported 10.x SDK
├── Directory.Build.props              # nullable, analyzers, deterministic builds
├── Directory.Packages.props           # central exact dependency versions
├── .editorconfig
├── .gitignore
├── README.md
├── Implementation.md
├── src/
│   ├── SnapDrop.App/                   # WPF executable and composition root
│   │   ├── SnapDrop.App.csproj
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── app.manifest                # asInvoker; PerMonitorV2
│   │   ├── Bootstrap/ServiceRegistration.cs
│   │   ├── Lifecycle/ApplicationLifetime.cs
│   │   ├── Presentation/PreviewController.cs
│   │   ├── Presentation/UiDispatcher.cs
│   │   ├── ViewModels/
│   │   │   ├── PreviewViewModel.cs
│   │   │   ├── RecentCapturesViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   ├── Views/
│   │   │   ├── OnboardingWindow.xaml
│   │   │   ├── FloatingPreviewWindow.xaml
│   │   │   ├── ScreenshotViewerWindow.xaml
│   │   │   ├── RecentCapturesWindow.xaml
│   │   │   └── SettingsWindow.xaml
│   │   └── Resources/                  # styles, icons, accessible strings
│   ├── SnapDrop.Core/                  # net10.0; no Windows/UI dependencies
│   │   ├── SnapDrop.Core.csproj
│   │   ├── Models/
│   │   │   ├── ScreenshotRecord.cs
│   │   │   ├── CapturedFrame.cs
│   │   │   ├── ExportRecord.cs
│   │   │   └── AppSettings.cs
│   │   ├── Ports/
│   │   │   ├── IScreenshotRepository.cs
│   │   │   ├── IImageEncoder.cs
│   │   │   ├── IImageClipboard.cs
│   │   │   ├── ISettingsStore.cs
│   │   │   └── IFileLeaseRegistry.cs
│   │   ├── Workflows/
│   │   │   ├── IngestScreenshot.cs
│   │   │   ├── ExportScreenshot.cs
│   │   │   ├── CopyScreenshot.cs
│   │   │   └── CleanupLibrary.cs
│   │   └── Policies/
│   │       ├── ImageLimits.cs
│   │       ├── RetentionPolicy.cs
│   │       ├── PreviewLifetime.cs
│   │       └── ScreenshotNaming.cs
│   ├── SnapDrop.Windows/               # net10.0-windows; OS/UI adapters
│   │   ├── SnapDrop.Windows.csproj
│   │   ├── Interop/                    # minimal native declarations/SafeHandles
│   │   ├── Clipboard/
│   │   │   ├── ClipboardListener.cs
│   │   │   ├── ClipboardStaDispatcher.cs
│   │   │   ├── ClipboardImageReader.cs
│   │   │   └── ImageClipboardWriter.cs
│   │   ├── Imaging/WpfImageEncoder.cs
│   │   ├── DragDrop/ScreenshotDragService.cs
│   │   ├── Dialogs/SaveDialogService.cs
│   │   ├── Displays/MonitorService.cs
│   │   ├── Tray/TrayService.cs
│   │   ├── Startup/StartupRegistration.cs
│   │   └── Lifecycle/SingleInstanceGuard.cs
│   └── SnapDrop.Infrastructure/        # net10.0; filesystem and configuration
│       ├── SnapDrop.Infrastructure.csproj
│       ├── Storage/FileScreenshotRepository.cs
│       ├── Storage/AtomicFileWriter.cs
│       ├── Storage/FileLeaseRegistry.cs
│       ├── Storage/LibraryRecovery.cs
│       ├── Configuration/JsonSettingsStore.cs
│       └── Diagnostics/BoundedFileLogger.cs
├── tests/
│   ├── SnapDrop.Core.Tests/            # policies/workflows with deterministic time
│   ├── SnapDrop.Infrastructure.Tests/  # real temporary filesystem
│   ├── SnapDrop.Windows.Tests/         # STA fixtures; interactive tests tagged
│   └── SnapDrop.TestSupport/           # fixtures and synthetic image patterns
├── tools/
│   ├── SnapDrop.DropProbe/             # test-only real drag target
│   └── SnapDrop.ClipboardProbe/        # test-only image publisher/lock holder
├── scripts/
│   ├── build.ps1
│   ├── test.ps1
│   └── publish.ps1
├── packaging/
│   ├── installer/SnapDrop.iss          # proposed Inno Setup per-user installer
│   ├── assets/                        # icon and installer artwork
│   └── release-notes.md
├── .github/workflows/windows-ci.yml
└── docs/
    ├── ARCHITECTURE.md
    ├── UX_SPEC.md
    ├── TEST_PLAN.md
    ├── RELEASE_PLAN.md
    └── SOURCES.md
```

Avoid empty service layers, a generic repository framework, a mediator/event-bus dependency, and speculative capture abstractions in v0.1. Add a new interface when a real OS boundary or meaningful independent policy needs one.

Dependency rule: App → Core, Windows, Infrastructure; Windows → Core; Infrastructure → Core; Core → .NET base libraries only. Infrastructure and Windows do not reference each other. App wires them together.

## 4. MVP behavior and defaults

- Monitoring: disabled until explicitly enabled; labeled “Clipboard images,” not “Snipping Tool detection.” All new supported clipboard images may be ingested while enabled.
- Preview: bottom-right of the pointer's monitor when the image event arrives; fall back to foreground-window monitor, then primary. Clipboard input does not reveal the screenshot's original monitor.
- Timeout: five seconds of unengaged time after Ready; range 2–30 seconds or Never. Hover, keyboard focus, Pin, active drag, dialog, and export pause expiry.
- Burst policy: latest capture replaces the visible idle thumbnail; every committed capture enters Recent. During an interaction, keep that screenshot fixed and queue one latest pending thumbnail.
- Originals: PNG in an application-managed recovery library. Thumbnail dismissal never deletes the file.
- Recovery: keep for seven days by default; visibly label that policy in onboarding and Recent. “Keep” exempts a capture from automatic expiry.
- Capacity: 1 GiB soft library budget; reclaim only eligible expired files. If still full, pause new ingestion and show a storage action rather than silently evicting protected/recent captures.
- Permanent automatic export: off by default. Users may enable a default folder; default suggestion is the Pictures known folder plus SnapDrop. Use the Windows known-folder API rather than a hardcoded user path.
- Save As: PNG default, JPEG optional at quality 92; JPEG flattens alpha on white and is explicitly lossy.
- Auto-open Save As: off by default; process one dialog at a time, leaving subsequent captures in Recent.
- Start with Windows: off by default; per-user setting with real OS registration and error feedback.
- Privacy: no screenshot upload, remote telemetry, or background content analysis. OS clipboard history/cloud clipboard and third-party sync folders remain controlled by Windows and the user.

See [UX specification](docs/UX_SPEC.md) for action and focus details.

## 5. Ordered implementation work

Estimates assume one engineer experienced with Windows desktop development and access to real Windows 11 test machines. They are planning ranges, not measured delivery promises. Complete each exit gate before expanding scope.

### M0 — Prove the Windows integration, 2–3 engineer-days

Tasks SD-001–SD-004:

1. Install a current .NET 10 SDK and confirm WPF build tooling; pin the SDK and dependency versions.
2. Build a disposable spike that receives one Snipping Tool clipboard image and writes a real PNG.
3. Show it in a non-activating WPF window and initiate Copy-style file drag.
4. Verify Explorer, one browser attachment area, Save As, and mixed-DPI placement. Record actual app/OS versions and results.

Exit gate: manually complete capture → thumbnail → Explorer drop and Save As on real Windows. Document unsupported targets. Do not build a large settings UI before this proves the core interaction.

### M1 — Application foundation, 2–3 engineer-days

Tasks SD-101–SD-105; depends on M0:

- Create the four production projects, tests, references, common build properties, and Windows CI.
- Bootstrap dependency injection from App. Set ShutdownMode=OnExplicitShutdown; remove template StartupUri.
- Implement per-user/session mutex and bounded same-user IPC commands for a second launch to open Recent; never ingest arbitrary file paths through IPC.
- Add NotifyIcon with Recent, Settings, Pause/Resume, and Exit. Closing settings keeps the tray running; Exit stops it.
- Implement versioned atomic settings, local log rotation, onboarding consent, and lifecycle disposal.

Exit gate: one process; tray survives all windows closing; corrupt settings recover visibly; Exit releases the mutex and notification icon.

### M2 — Ingestion and recovery storage, 4–5 engineer-days

Tasks SD-201–SD-207; depends on M1:

- Register/remove a clipboard listener on a dedicated STA dispatcher with a message pump.
- Snapshot supported image formats with dimension limits and bounded retries; never block WM_CLIPBOARDUPDATE.
- Reconcile sequence numbers around the read and suppress SnapDrop's own clipboard marker.
- Normalize to a detached immutable BGRA32 frame; transfer ownership to a bounded processing queue.
- Write original PNG and metadata using sibling temporary files and commit renames; publish Ready only after both commit.
- Rebuild Recent from per-capture manifests on restart; repair interrupted commits and isolate corrupt entries.
- Implement storage budgets and idle cleanup using leases, retention, persisted transfer timestamps, and canonical-root checks.

Exit gate: events are not duplicated; two separate identical screenshots remain separate captures; recent committed images survive restart; failures never produce a fake Ready item.

### M3 — Floating preview and history, 3–4 engineer-days

Tasks SD-301–SD-306; depends on M2:

- Implement preview layout, image thumbnail, accessible controls, and compact busy/error states.
- Show without activation; allow explicit user activation for keyboard controls and viewer.
- Position using monitor working area, window DPI, PerMonitorV2 awareness, and display-change handling.
- Add stateful timeout pauses and interaction leases; do not replace the image during a drag or save.
- Handle capture bursts with one current interaction and one latest pending preview.
- Add Recent with lazy thumbnail loading, viewer, Keep, and deletion restricted to managed items.

Exit gate: no focus stolen on arrival; hover/pin/actions pause expiry; mixed-DPI and negative-coordinate cases work; a timed-out item reopens from Recent.

### M4 — Drag, copy, and export, 4–5 engineer-days

Tasks SD-401–SD-407; depends on M3:

- Begin drag only after Windows drag thresholds; offer an existing PNG through FileDrop and Copy effect on the UI STA.
- Retain file leases through the OLE nested message loop; persist a transfer grace deadline; cancelled drops keep the original.
- Copy a self-marked image DataObject with persistence across app exit; use bounded retry and verify self-suppression.
- Implement native Save As with a deliberate owner window and PNG/JPEG format validation.
- Encode/write on a worker, commit only completed output, and retain the source on cancellation, denial, full disk, or disconnected storage.
- Implement Save to default folder with collision-safe names and optional automatic export.
- Add optional auto-open Save As with serialization and a pause control.

Exit gate: Explorer receives a valid PNG; copy pastes after SnapDrop exits; PNG export retains dimensions/pixels; JPEG is correctly encoded; save failures are recoverable and successful saves are accurately reported.

### M5 — Hardening and measurable beta readiness, 4–6 engineer-days

Tasks SD-501–SD-507; depends on M4:

- Execute the [test plan](docs/TEST_PLAN.md), including clipboard locks, bursts, cancellation, cleanup races, corrupted manifests, and restart recovery.
- Exercise real application drop targets and document exact drop zones and application versions.
- Verify 100/150/200% scaling, monitor disconnect, taskbar positions, session lock/unlock, remote-session behavior, and Explorer restart.
- Validate keyboard and screen-reader access, high contrast, reduced motion, and focus restoration.
- Measure idle resources and p50/p95 ingestion-to-interactive-preview latency using local structured timings.
- Fix leaks: bitmap buffers, streams, event subscriptions, HBITMAPs, COM/D3D resources if introduced, windows, and tray handles.
- Freeze public defaults and update the release evidence record.

Exit gate: all mandatory release scenarios pass on a real interactive desktop; pending checks are marked unverified, never assumed from CI.

### M6 — Package and distribute v0.1, 2–3 engineer-days

Tasks SD-601–SD-605; depends on M5:

- Publish an untrimmed self-contained win-x64 directory and a portable ZIP; qualify before considering single-file publish.
- Build a per-user installer with Inno Setup or an equivalently verified installer tool; pin its version and scripts.
- Implement explicit startup registration; remove it on uninstall without touching another app's entries.
- Sign executable/installer with a release-controlled code-signing identity; timestamp and verify signatures.
- Test clean-machine install, upgrade, uninstall, offline runtime independence, settings preservation, and export preservation.

Exit gate: signed artifact, checksums, install instructions, release notes, and tested compatibility list. Certificate availability and signing infrastructure are external release dependencies, not prerequisites for local development.

Total working estimate: **21–29 engineer-days**, plus **4–6 days contingency** for external-app behavior, DPI, and installer issues. A focused internal prototype can appear after M0; it is not the public MVP.

## 6. v0.2 independent capture plan

Budget another 8–12 engineer-days after v0.1 evidence is stable; re-estimate after the capture spike.

1. Introduce ICaptureSource only when there is a real native implementation; route completed frames into the existing ingestion workflow.
2. Register a configurable Ctrl + Shift + 4 default with MOD_NOREPEAT. Handle registration failure with a settings action; keep Win + Shift + S under Windows control. [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).
3. Build a WPF overlay per monitor with shared physical-pixel selection coordinates, Escape cancel, and display-change cancellation.
4. Start with one-shot SDR desktop pixels via a carefully tested Win32 GDI backend for rectangular visible-screen capture. Hide owned UI before acquiring pixels; test compositor timing and release all native resources. This is not a window-content capture solution.
5. Add a Windows.Graphics.Capture backend for chosen displays/windows after a D3D interop spike. Check runtime support, initialize desktop picker ownership, await a valid frame with timeout, and dispose session/frame pool/device resources. Its picker chooses a window/display; custom rectangle selection remains separate. [Screen capture documentation](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture).
6. Define native full-display capture as the selected monitor. Defer synchronized multi-monitor “all displays” capture until frames, scaling, and color behavior are specified.
7. Preserve a BGRA32/sRGB-oriented SDR export contract. Treat HDR/ICC handling as a separate qualified pipeline; PNG alone does not guarantee HDR/colorimetric equivalence.
8. Coordinate native output with companion monitoring using the self-write marker; one capture must not create two history items.

Native capture must pass coordinate-grid tests at each monitor DPI, across monitor boundaries, and with negative origins. Protected content, secure desktops, and unavailable capture sources must fail clearly. Self-window capture exclusion is best effort, not a security guarantee; see [display affinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity).

## 7. Definition of done

Release v0.1 only when all of the following are evidenced:

- A new user can enable monitoring and complete capture → preview → Drag / Copy / Save As.
- No unrelated clipboard content is changed by monitoring; text events are ignored.
- Self-copy events do not loop and repeated independent identical captures are retained.
- Preview focus, timeout, bursts, DPI, and session behavior meet the UX specification.
- Managed originals survive UI dismissal, restart, save errors, and cancelled drops within the stated recovery policy.
- Exported files are never targets of automatic managed-library cleanup.
- Mandatory unit/filesystem checks and real-desktop acceptance checks pass.
- Performance results include hardware, image size, build configuration, and percentile definitions.
- A clean machine runs the packaged application without a developer SDK.
- Release notes distinguish tested compatibility, known failures, and unverified scenarios.

## 8. Risks and decisions to revisit with evidence

- **Clipboard source ambiguity:** v0.1 explicitly monitors clipboard images; do not add a brittle process-name heuristic and present it as reliable screenshot attribution.
- **Clipboard event loss:** another app may replace pixels before SnapDrop reads them. Bounded retries cannot recreate overwritten data; report overload and record diagnostics. Native capture removes this ownership gap later.
- **Two previews:** Snipping Tool may show its own notification/preview and auto-save independently. SnapDrop does not disable Windows settings; explain coexistence.
- **Target behavior:** Explorer copy is mandatory; WhatsApp, browsers, mail, and editor panes require versioned real-app tests. Accepted OLE effect is not proof of successful upload.
- **File retention:** external apps can retain a path beyond the drag call. A lease plus retention/grace helps but cannot guarantee an arbitrary deferred reader forever. Users can Keep or export for durable paths.
- **Memory:** full-resolution pixel buffers dominate. Bound concurrent frames and lazy-load history instead of assuming WPF guarantees a RAM ceiling.
- **Distribution:** signing and support branding need real release infrastructure; local builds alone do not prove installation or trust behavior.

## 9. First development session

Read this plan and [architecture](docs/ARCHITECTURE.md), install/pin the SDK, then perform M0. Commit the spike's measured findings before implementing M1. Keep a short verification record with each milestone: changed behavior, automated evidence, manual evidence, and remaining limitations.
