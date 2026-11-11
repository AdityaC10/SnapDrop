# SnapDrop for Windows

A native Windows screenshot companion: **Win + Shift + S → floating thumbnail → Drag / Copy / Save As**.

This repository contains the v0.1 source candidate, regression tests, probe tools, Windows CI, build/publish scripts, and installer source. **It is not yet a verified build or production release.** See [Context.md](Context.md) for current evidence and open gates.

## Build and run

Install a stable [.NET 10 x64 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows 11. A runtime alone is insufficient. From the repository root:

Alternatively, install into this workspace and use the scripts below. The setup command selects the latest stable .NET 10 Windows x64 ZIP from official Microsoft metadata, verifies SHA-512, and installs under `.tools/dotnet`. It does not require a system installation or change PATH. Run it in a Windows terminal with access to Microsoft's download servers:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-sdk.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/run.ps1 -NoBuild
```

Existing workspace SDK installations are preserved. Offline setup is also available with `-ArchivePath`, `-Sha512`, and `-SdkVersion`; supply an official SDK ZIP and its checksum/version from [Microsoft release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). Build/test/run/publish scripts automatically prefer the workspace SDK, and default their CLI/NuGet caches to `.tools`. An explicitly configured `SNAPDROP_DOTNET`, `DOTNET_CLI_HOME`, or `NUGET_PACKAGES` remains honored.

With a system-installed SDK, direct CLI commands remain available:

```powershell
dotnet restore SnapDrop.sln --use-lock-file
dotnet build SnapDrop.sln -c Release --no-restore
dotnet test SnapDrop.sln -c Release --no-build --filter "Category!=InteractiveWindows"
dotnet run --project src/SnapDrop.App/SnapDrop.App.csproj
```

The first run opens Settings with clipboard image monitoring unchecked. Enable it and apply settings, then capture with Win + Shift + S. Closing Settings leaves the tray app running; Exit is in the tray menu. Monitoring may also ingest ordinary copied images, as explained during onboarding.

`scripts/build.ps1`, `scripts/test.ps1`, and `scripts/publish.ps1` automate build/test/publish. If local script execution is disabled and your policy permits a process-only override, run for example `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`; this does not change the saved machine/user execution policy. Review and commit generated dependency lock files after the first successful restore. Subsequent scripted builds use locked restore unless `-UpdateLocks` is explicit.

## Verify the real workflow

- Drag a thumbnail into an Explorer folder and open the resulting PNG.
- Use Copy, paste into another image-capable process, exit SnapDrop, and paste again.
- Use Save As for PNG/JPEG; cancel and retry without losing the recovery copy.
- Reopen dismissed images through Recent; Keep prevents automatic expiry.
- After a storage failure, use the tray's Recover unsaved image action. One in-memory image is protected across thumbnail dismissal/session lock; save or explicitly discard it before resuming. Graceful Exit warns before discarding unsaved data.
- Run the detailed [manual test plan](docs/TEST_PLAN.md), including DPI, focus, failures, and cleanup.

Test-only tools provide controlled cross-process inputs and targets:

```powershell
dotnet run --project tools/SnapDrop.ClipboardProbe
dotnet run --project tools/SnapDrop.DropProbe
```

ClipboardProbe replaces the real clipboard only when you click a publish action; its read-only inspector can check persistent image data from another process. DropProbe receives a real file path and can read it again after the drag finishes. These tools are excluded from the application publish.

Real clipboard tests are excluded from default runs. In a dedicated unlocked test session, set `SNAPDROP_INTERACTIVE_TESTS=1`, then run `scripts/test.ps1 -Interactive`. See [release evidence](docs/VERIFICATION.md) for what has actually been checked.

## Package

`scripts/publish.ps1` builds, tests, and creates a self-contained, untrimmed Windows x64 portable ZIP. Add `-Installer` with a selected Inno Setup compiler configured through `SNAPDROP_ISCC` to build the per-user installer. Signing is optional tooling input; artifacts are labeled unsigned unless signing and signature verification actually succeed. See [release instructions](docs/RELEASE_PLAN.md).

## Start here

1. [Implementation plan and proposed repository structure](Implementation.md) — product scope, stack, ordered work packages, estimates, and release gates.
2. [Architecture](docs/ARCHITECTURE.md) — dependencies, threading, clipboard ingestion, persistence, drag operations, and recovery.
3. [User experience specification](docs/UX_SPEC.md) — onboarding, thumbnail interactions, Save As, history, and settings.
4. [Test plan](docs/TEST_PLAN.md) — automated coverage, real application compatibility, performance measurements, and manual acceptance steps.
5. [Build and release plan](docs/RELEASE_PLAN.md) — solution initialization, Windows CI, packaging, signing, and distribution.
6. [Technical sources](docs/SOURCES.md) — official documentation and the decisions it supports.
7. [Implementation agent prompt](docs/IMPLEMENTATION_AGENT_PROMPT.md) — copy-ready instructions to start the build and resume unfinished milestones.

## Recommended first release

- C# with .NET 10 and WPF; Windows 11 x64 first.
- Optional, clearly explained clipboard image monitoring.
- A preview that appears without stealing focus.
- Real PNG file drag-and-drop, image copy, native Save As with PNG/JPEG export.
- Tray operation, recent captures, recovery storage, and safe cleanup.
- Local processing and storage; no service backend or account.

Independent region, window, and display capture follows the companion MVP. Drag-and-drop compatibility must be tested against each target application.

## Current verification

The installed .NET host reports version 8.0.21, with no SDKs. A shell request to the official .NET download server failed. Source syntax/structure checks and four explicitly synthetic WPF markup-fixture renders succeeded; they do not compile or execute the .NET 10 app.

No application build, C# application regression-test execution, live clipboard/drop workflow, performance measurement, installer compilation, code signing, or clean-machine installation has passed yet. The independent UI review requests actual running-app evidence before acceptance. These are open release gates, not claims inferred from source code.

The independent SDK setup helper has **16 passing guard tests**, run using Windows PowerShell without downloading or executing an SDK fixture. This does not change the application verification status. Run `scripts/test-sdk-setup.ps1` to reproduce those tests; results are in `artifacts/sdk-setup-tests.json`.
