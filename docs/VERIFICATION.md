# SnapDrop verification record

Date: 2026-11-11. Candidate version: 0.1.0. This is an evidence record, not a production-readiness declaration.

## Implemented source

- Four production projects: Core, Infrastructure, Windows, and WPF App.
- Single-instance tray lifecycle and same-user/session IPC, opt-in Settings/onboarding, startup shortcut preference.
- Dedicated clipboard STA with format notifications, retries, PNG header bounds, self marker, event identity tracking, and two-frame intake capacity.
- PNG storage, schema-versioned atomic manifests/settings, corrupt/orphan recovery notices, retention/Keep/leases, and 24-hour transfer protection.
- Real FileDrop/Copy drag, persistent bitmap clipboard copy, native Save As, correct PNG/JPEG encoding, and safe destination writes.
- Floating preview state/timer, interaction-aware pending preview, multi-monitor placement adapter, viewer, Recent with paging, and saved locations.
- Explicit full-resolution buffer ownership; recovery UI and active actions retain buffers independently of the processing loop.
- Application-held unsaved-image recovery from the tray after storage failures, guarded discard/Exit, and accepted-frame draining on graceful Exit. These branches have not been runtime tested.
- Three xUnit test projects, two OS probe tools, build/test/publish scripts, pinned GitHub Actions workflow, and per-user Inno Setup installer source.

These statements describe code present in the repository. They are not verification that these behaviors work on Windows.

## Checks performed

- `dotnet --info` / `dotnet --list-sdks`: host 8.0.21, no SDK installed.
- Official .NET 10 metadata request through curl: failed to connect to builds.dotnet.microsoft.com:443, exit 7.
- C# grammar parsing using an existing local tree-sitter installation: 29 source files parsed without syntax-error nodes after corrections. This is not compilation, reference resolution, analyzer execution, or type checking.
- Project/XAML/manifest XML parsing: files checked as well-formed XML; project-reference targets exist.
- Named XAML event-handler checks: all declared handlers found after correcting the missing Pin handler.
- PowerShell parser checks: build/test/publish/common scripts parse; the markup validator also executed successfully.
- Final structure checks covered 29 C# files, 17 XML/project/property/manifest files, nine projects, all named XAML handlers, five PowerShell files, and local Markdown links/fences. Workflow action pins were checked; a YAML parser was unavailable and CI has not executed. See artifacts/source-checks.json for the machine-readable boundary.
- `tools/validate-xaml.ps1 -Snapshots`: all four view markups loaded under installed .NET Framework WPF after event handlers were removed. Source-hashed, explicitly synthetic fixtures live under `.impeccable/review/`.
- The synthetic PNG used by filesystem tests was decoded with Pillow and corrected to valid PNG data. That check validates a test fixture, not the production image codec.
- `scripts/build.ps1`: stopped at SDK preflight with an actionable .NET 10 SDK requirement. No restore or compilation occurred.

The markup fixtures were visually inspected. They do not show a running .NET 10 executable, window focus, clipboard I/O, user interactions, high-DPI transitions, native dialogs, or an external file transfer.

## Independent UI review

Disposition: **recapture**. The finish reviewer found the fixture labeling honest but insufficient for application acceptance. Full source/craft/fidelity review remains unopened until actual running-app captures and interaction evidence exist. See `.impeccable/review/REVIEW_STATUS.md`.

## SDK setup continuation

- The .NET host still reports no SDK. A fresh Windows HTTP request to official Microsoft release metadata failed to connect.
- Added `scripts/setup-sdk.ps1` for a workspace-local, checksum-verified .NET 10 x64 install, plus offline archive/hash/version parameters and `scripts/run.ps1`.
- **16 SDK-bootstrap guard tests passed** using the actual helper module and synthetic ZIP fixtures. Tests checked stable metadata selection, Microsoft HTTPS URL restrictions, SHA-512 checks, directory boundaries, traversal/alternate streams, linked entries, ambiguous/reserved Windows names, duplicate paths, normal extraction, and preservation of an existing destination. They did not execute the fixture named dotnet.exe.
- Running the real setup command stopped at the metadata download, before an SDK install. No .NET 10 source compilation or C# application tests ran.
- CI now includes the SDK helper tests and their result JSON, but the workflow itself has not executed.

## Not run / not verified

- .NET 10 restore, lock-file generation/review, compile, analyzers, and all xUnit execution.
- Capture with Snipping Tool; cross-process image paste including after exit; Explorer and third-party file drops.
- Save As/native dialogs, overwrite/cancellation, JPEG/PNG production round trips, failure handling, and runtime recovery.
- Foreground focus, accessible keyboard/screen reader, high contrast, text scaling, mixed DPI, negative monitor coordinates, lock/unlock, and Explorer restart.
- Timed 1080p/4K latency runs, idle working set/CPU, peak memory, and 500-capture soak.
- Self-contained publish, portable ZIP, installer compilation, signing/verification, CI execution, and clean-machine install/upgrade/uninstall.

## Next validation commands

After installing a stable .NET 10 SDK with restore access:

```powershell
dotnet restore SnapDrop.sln --use-lock-file
dotnet build SnapDrop.sln --configuration Release --no-restore
dotnet test SnapDrop.sln --configuration Release --no-build --filter "Category!=InteractiveWindows"
dotnet run --project src/SnapDrop.App/SnapDrop.App.csproj
```

Fix any compiler/test failures before depending on the source candidate. Review/commit generated locks, execute docs/TEST_PLAN.md on an unlocked Windows 11 desktop, gather actual app captures, and reopen full UI review. A signed public release remains blocked until all mandatory gates pass.
