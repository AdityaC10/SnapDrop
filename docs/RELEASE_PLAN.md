# SnapDrop build and release plan

Status: source candidate created; build/release qualification pending. Application projects and scripts now exist. Use README.md and scripts/build.ps1 for the existing solution; the bootstrap section below is retained as architecture history and should not be rerun over the implementation. See Context.md and docs/VERIFICATION.md for actual command outcomes.

## 1. Development prerequisites

- Windows 11 x64 with a supported edition/build and an interactive desktop for integration work.
- Current stable .NET 10 SDK. A runtime alone cannot create/build the solution.
- Visual Studio with an SDK-compatible .NET desktop development workload, or another editor plus CLI tools.
- Git, package restore access, and a selected Windows SDK if/when native capture development requires it.
- At release time: selected installer tooling, code-signing identity/service, and a clean Windows VM/physical test machine.

Check `dotnet --info` and `dotnet --list-sdks` first. Keep .NET, Windows, and installer tool support decisions current; consult [supported Windows/.NET combinations](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

For workspace-only setup, run `scripts/setup-sdk.ps1` in a Windows terminal with Microsoft download access. It installs the latest stable .NET 10 Windows x64 ZIP under `.tools/dotnet` after SHA-512 and archive-path validation. It preserves existing installations and supports offline `-ArchivePath`, `-Sha512`, and `-SdkVersion` parameters. Scripts automatically prefer this SDK, so no system PATH change is needed. `scripts/run.ps1` launches the app using the selected SDK after a successful build. This agent session could run 16 setup guard tests, but could not download or execute an SDK; installation and application build proof remain pending.

## 2. Initialize the solution during M0/M1

Run from `D:\Projects\SnapDrop` after installing the SDK. These are bootstrap instructions for the planned projects, not an implemented screenshot application. Inspect existing files before running so later work is not overwritten.

Select and pin the latest installed stable 10.x SDK:

```powershell
$snapDropSdk = dotnet --list-sdks |
    ForEach-Object {
        if ($_ -match '^(10\.0\.\d+)\s') { [version]$Matches[1] }
    } |
    Sort-Object -Descending |
    Select-Object -First 1

if (-not $snapDropSdk) { throw 'Install a supported stable .NET 10 SDK first.' }
dotnet new globaljson --sdk-version ($snapDropSdk.ToString()) --roll-forward latestFeature

dotnet new sln --name SnapDrop --format sln
dotnet new wpf --name SnapDrop.App --output src/SnapDrop.App --framework net10.0
dotnet new classlib --name SnapDrop.Core --output src/SnapDrop.Core --framework net10.0
dotnet new classlib --name SnapDrop.Windows --output src/SnapDrop.Windows --framework net10.0
dotnet new classlib --name SnapDrop.Infrastructure --output src/SnapDrop.Infrastructure --framework net10.0

dotnet new xunit --name SnapDrop.Core.Tests --output tests/SnapDrop.Core.Tests --framework net10.0
dotnet new xunit --name SnapDrop.Infrastructure.Tests --output tests/SnapDrop.Infrastructure.Tests --framework net10.0
dotnet new xunit --name SnapDrop.Windows.Tests --output tests/SnapDrop.Windows.Tests --framework net10.0

dotnet sln SnapDrop.sln add src/SnapDrop.App/SnapDrop.App.csproj src/SnapDrop.Core/SnapDrop.Core.csproj src/SnapDrop.Windows/SnapDrop.Windows.csproj src/SnapDrop.Infrastructure/SnapDrop.Infrastructure.csproj
dotnet sln SnapDrop.sln add tests/SnapDrop.Core.Tests/SnapDrop.Core.Tests.csproj tests/SnapDrop.Infrastructure.Tests/SnapDrop.Infrastructure.Tests.csproj tests/SnapDrop.Windows.Tests/SnapDrop.Windows.Tests.csproj

dotnet add src/SnapDrop.App/SnapDrop.App.csproj reference src/SnapDrop.Core/SnapDrop.Core.csproj src/SnapDrop.Windows/SnapDrop.Windows.csproj src/SnapDrop.Infrastructure/SnapDrop.Infrastructure.csproj
dotnet add src/SnapDrop.Windows/SnapDrop.Windows.csproj reference src/SnapDrop.Core/SnapDrop.Core.csproj
dotnet add src/SnapDrop.Infrastructure/SnapDrop.Infrastructure.csproj reference src/SnapDrop.Core/SnapDrop.Core.csproj
dotnet add tests/SnapDrop.Core.Tests/SnapDrop.Core.Tests.csproj reference src/SnapDrop.Core/SnapDrop.Core.csproj
dotnet add tests/SnapDrop.Infrastructure.Tests/SnapDrop.Infrastructure.Tests.csproj reference src/SnapDrop.Infrastructure/SnapDrop.Infrastructure.csproj
```

Before adding Windows-test references/building, change Windows and Windows.Tests to `net10.0-windows`; enable UseWPF in both and UseWindowsForms in Windows. Add the Windows test reference to Windows. App already gets its Windows target/UseWPF from the WPF template. Keep Core/Infrastructure independent of WindowsDesktop.

The WPF template's framework argument is the template option `net10.0`; its emitted project targets the Windows-specific framework. Inspect output and template help on the installed SDK. Explicit `--format sln` keeps the repository tree consistent rather than relying on the SDK's default solution format.

Next:

1. Add central package versions for MVVM, DI, logging abstractions, and test dependencies; move template package versions out of individual project files when central management is enabled.
2. Add nullable reference types, implicit usings, deterministic builds, and selected analyzers to Directory.Build.props. Add the executable manifest and resource icon.
3. Remove unused template classes/test stubs, replace StartupUri with explicit tray lifecycle, and implement M0's real OS spike.
4. Enable lock-file generation and commit per-project `packages.lock.json` files after the intended dependencies resolve. Use locked restore in CI.
5. Create test probes/support projects when their first real tests need them; do not ship them inside the application publish directory.

## 3. Build/run commands after implementation

```powershell
dotnet restore SnapDrop.sln --locked-mode
dotnet build SnapDrop.sln --configuration Release --no-restore
dotnet test SnapDrop.sln --configuration Release --no-build --filter "Category!=InteractiveWindows"
dotnet run --project src/SnapDrop.App/SnapDrop.App.csproj
```

Use xUnit's `[Trait("Category", "InteractiveWindows")]` on tests that touch the real desktop/clipboard. Interactive tests run separately in a dedicated unlocked Windows test session:

```powershell
dotnet test tests/SnapDrop.Windows.Tests/SnapDrop.Windows.Tests.csproj --configuration Release --filter "Category=InteractiveWindows"
```

Restore/build commands assume completed project setup and committed dependency lock files. A generated template window is not an MVP. All actual screenshot actions must be implemented before calling the workflow complete.

## 4. Windows CI

On pull requests:

1. Check out source using a pinned action revision.
2. Install the SDK from global.json on a supported Windows runner.
3. Restore in locked mode; build Release with the selected analyzer policy.
4. Run Core, filesystem, and noninteractive Windows tests. Publish test results and explicit skipped reasons.
5. Publish an unsigned win-x64 candidate for internal verification; label it unsigned.

Do not run real clipboard tests on a shared desktop or claim headless CI proves focus, multi-monitor placement, or external-app drops. Execute those through a controlled interactive test machine/session. A CI runner version alone does not qualify every advertised Windows build.

On a release tag, reuse the tested commit, build the package, apply signing through protected release credentials, verify signatures/checksums, and attach the acceptance evidence. No signing credentials or certificates belong in the repository.

## 5. Publish model

Start with an untrimmed self-contained directory:

```powershell
dotnet publish src/SnapDrop.App/SnapDrop.App.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false --output artifacts/publish/win-x64
```

Ship the entire publish directory through the installer or ZIP. It contains SnapDrop.App.exe and required runtime/framework files; copying just the EXE is not a standalone deployment.

WPF trimming is currently unsupported by the .NET SDK; keep it disabled. Native AOT is also outside this WPF release plan. Single-file deployment is a separate optional optimization with architecture-specific behavior; measure startup, native library extraction, clipboard, and drag regressions before adopting it. See [publish documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish), [trimming constraints](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities), and [single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

## 6. Installer and startup

Proposed beta route: an Inno Setup per-user installer installing beneath `%LOCALAPPDATA%\Programs\SnapDrop`. Pin the chosen installer version, review its applicable terms, and keep a stable installer identity across upgrades. [Official Inno Setup information](https://jrsoftware.org/isinfo.php).

Installer responsibilities:

- Install the complete self-contained publish directory and a Start menu shortcut.
- Request no elevation for a normal per-user install.
- Close/defer the running app gracefully before updating in-use files; never replace files mid-drag.
- Preserve settings and recovery storage through upgrades; preserve user exports through every operation.
- Remove app-owned startup registration and binaries on uninstall. Retain recovery/settings by default or offer clearly described explicit removal; never delete arbitrary export folders.
- Test paths with spaces/Unicode and an ordinary non-administrator account.

For the unpackaged build, the startup adapter may create/remove a uniquely named shortcut in the current user's Startup known folder. Quote target/arguments correctly, register the installed executable, and recognize stale registration after a portable folder moves. Show failures instead of toggling the setting optimistically.

MSIX is a later alternative when Store/package identity becomes useful. It requires a separate packaging/startup/update adapter and real installation testing. Do not combine unpackaged startup shortcuts and packaged startup-task registration accidentally.

## 7. Release artifacts and gates

Expected artifacts after implementation and qualification:

- `SnapDrop-<version>-win-x64-setup.exe` — signed per-user installer.
- `SnapDrop-<version>-win-x64-portable.zip` — complete runtime-inclusive directory; sign the executable before zipping.
- `SHA256SUMS.txt`, release notes, dependency notices, and test/compatibility record.

Signing does not automatically guarantee a warning-free installation or SmartScreen reputation. Verify actual clean-machine behavior and describe it accurately.

Mandatory installation checks:

1. Clean supported Windows 11 x64 machine with no .NET SDK/Desktop Runtime installed; app works using the self-contained publish.
2. Offline launch and primary local workflow after installation.
3. Opt-in startup works; disabling startup and uninstall remove the app's registration.
4. Upgrade preserves settings/history and handles a running app without corrupting captures.
5. Uninstall preserves user-exported images in unrelated folders.
6. Artifact version, signature, checksums, and architecture match release notes.

Publish only compatibility actually checked. ARM64, Windows 10, HDR accuracy, every chat client, and automatic updates remain outside the v0.1 support claim.

## 8. Current environment record

Observed on 2026-11-11 in `D:\Projects\SnapDrop`:

- Workspace initially contained no project/document files.
- dotnet host: 8.0.21, x64.
- .NET SDKs: none installed.
- Runtime listed: Microsoft.NETCore.App 8.0.21; no WPF Desktop Runtime was listed.
- No solution creation, package restore, application build, integration test, publish, or install has been performed.

This environment is sufficient for preparing/reviewing the plan. M0 begins by installing the intended development toolchain.

Source implementation has since been added. The missing SDK and failed shell download still block compilation, tests, and publish. A build-script invocation correctly stops at SDK preflight. No application or installer binary has been generated.
