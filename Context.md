# SnapDrop implementation status

## Current work

v0.1 source candidate is implemented across four production projects, three regression-test projects, and two interactive test tools. Build and release qualification remain blocked by the missing SDK. Do not describe this candidate as compiled, working end to end, or production-ready.

Read docs/VERIFICATION.md before making capability claims. No application tests or actual capture/drag/save workflow have run.

### Latest continuation

- The SDK remains absent. A fresh `Invoke-WebRequest` request to the official .NET 10 release metadata endpoint failed to connect. Known local SDK/Downloads locations did not yield an SDK archive or installation.
- Added `scripts/setup-sdk.ps1` and `scripts/SdkSetup.psm1`: select stable official Microsoft Windows x64 metadata, verify SHA-512, validate every ZIP entry before extraction, stage beneath `.tools`, verify the extracted SDK version, and move only into an absent install destination. Existing installations are never overwritten. Offline ZIP/checksum/version parameters are supported.
- Added `scripts/run.ps1`. Build/test/run/publish scripts automatically select a workspace SDK; CLI/NuGet caches default under `.tools`, with explicit user overrides honored. Local SDK app hosts receive the matching DOTNET_ROOT settings. No persistent PATH or machine-wide SDK configuration is changed.
- **16 SDK setup guard tests passed** under Windows PowerShell. They test the actual helper with synthetic archives, including digest mismatch, extraction, boundary/traversal protection, linked/ambiguous/device/duplicate paths, and preservation of existing files. Fixtures named dotnet.exe were never executed. Results: `artifacts/sdk-setup-tests.json`.
- The real setup command stopped before installation at the unavailable metadata download. Application compilation, xUnit, and Windows workflow verification are still blocked. The helper tests do not satisfy those gates.
- SDK helper tests were added to the Windows CI workflow; CI itself has not executed.
- Newer appearance/settings, view model, view, and Snipping Tool launcher code was present beyond the older handoff. This continuation preserved those files and concentrated on toolchain setup; it does not certify the newer UI behavior.
- Next unblocking command, in a normal Windows terminal with Microsoft download access: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/setup-sdk.ps1`. Then run `scripts/build.ps1`, `scripts/test.ps1`, and `scripts/run.ps1 -NoBuild` using the same process-scoped invocation if required by local script policy.

## Environment and blockers

- `dotnet --info`: .NET host 8.0.21, no installed SDK.
- Attempt to retrieve official .NET 10 release metadata with curl failed with exit 7: connection to builds.dotnet.microsoft.com:443 could not be established.
- Rechecked `dotnet --list-sdks` after source implementation: still no SDK.
- No SDK installation, package restore, compilation, application tests, interactive workflow, or signing is verified.
- A setup question was sent to the user asking whether they can install the .NET 10 x64 SDK while source work continues; no answer has been received at this checkpoint. Do not interpret silence as installation or approval.

## Milestone status

- **M0:** toolchain inspected; real workflow spike cannot run without the SDK. Exit gate open.
- **M1:** project structure, explicit composition, single-instance/session IPC, tray, onboarding/settings, startup adapter, logging, and shutdown source exist. Build/lifecycle runtime checks pending.
- **M2:** clipboard STA, image limits, retries, self marker, bounded intake, detached/ref-counted pixel ownership, managed PNG/manifests, settings backup, leases, recovery notices, and cleanup source exist. Runtime/filesystem test execution pending.
- **M3:** preview, hover/focus/Pin/action lifetime, pending-capture handling, two-second feedback pause, monitor/DPI positioning adapter, viewer, Recent paging, and accessibility names exist. Four markup-only fixtures load; actual UI/focus/DPI/accessibility checks pending.
- **M4:** real FileDrop/Copy drag, persistent bitmap clipboard copy, native Save As, PNG/JPEG export, default export, saved-location history, and in-memory recovery actions exist. Live Windows/app compatibility checks pending.
- **M5:** meaningful policy/ownership/filesystem/codec tests and an opt-in clipboard test exist. Syntax/markup checks completed, but no xUnit run, benchmark, soak, or desktop acceptance has run.
- **M6:** build/test/publish scripts, pinned Windows CI, icon, portable ZIP/signing logic, and per-user installer source exist. No publish/ZIP/installer/signature/clean-machine proof exists.

No milestone with an unrun exit gate is marked verified complete.

## Decisions

- Preserve the approved v0.1 companion scope and native Windows UX.
- Use explicit composition and small local MVVM helpers rather than introduce unnecessary runtime package dependencies. This keeps the production application dependent only on the .NET Windows Desktop shared framework. Tests use xUnit.
- global.json uses the .NET 10 minimum SDK feature band with latestFeature roll-forward; a current supported stable 10.x SDK should be installed for builds.
- Tests use current xUnit v3 with the VSTest adapter; global.json explicitly selects VSTest. Direct dependency versions are centralized. Lock files must be generated by the first real restore, reviewed, and committed; they are not fabricated.
- Captured pixel buffers have an owning pipeline reference and retained leases for recovery windows/actions. Completion releases raw pixels so the channel's last item cannot keep a full-resolution screenshot alive indefinitely.
- A storage failure retains at most one unsaved frame under an application lease, accessible through the tray in a keyboard-accessible viewer. Thumbnail dismissal/session lock does not release that application lease. A second failed burst item is explicitly not retained; it does not silently replace the first. Save As or explicit confirmed discard releases the application lease, and graceful Exit warns before discarding unsaved data. Intake is paused until recovery is resolved; Exit drains accepted frames before shutting down.
- Managed cleanup deletes only known generated capture files, never recursive arbitrary directories or recorded user export paths. Corrupt/orphan files are preserved for manual recovery and count toward storage budget where scanned.
- Native Windows UX follows the approved brief. Impeccable context/init used existing explicit product/stack/visual commitments; no new visual direction was introduced.

## Evidence collected

- 29 C# files passed tree-sitter grammar parsing after corrections. This is syntax-only, not type checking/compilation.
- Project/XAML/manifest XML parsed, project reference paths exist, and named XAML handlers resolve in source. A missing Pin handler found by this check was implemented.
- Build/test/publish/common PowerShell files passed parser checks. Build invocation now fails clearly at SDK preflight, before restore/compilation.
- Final static result: 29 C# files, 17 XML/project/manifest/property files, nine projects in the solution, all named XAML handlers, local document links/code fences, and five PowerShell files checked without structural errors. Three workflow action references have verified full commit pins. A YAML parser was unavailable; YAML execution/CI was not validated. The machine-readable result is artifacts/source-checks.json.
- `powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/validate-xaml.ps1 -Snapshots` loaded four view markups using installed .NET Framework WPF. Event handlers were deliberately removed, synthetic data supplied, and fixtures labeled. No persistent execution-policy setting was changed.
- `.impeccable/review/fixture-evidence.json` records source hashes and the markup-only boundary. PNGs were visually opened and inspected. A transparent offscreen backdrop was corrected in the fixture renderer; this was a fixture issue, not proof about a running app.
- The filesystem-test PNG fixture was decoded with Pillow and corrected to a valid 1×1 PNG.
- Independent finish reviewer: **recapture**. Actual running .NET 10 app evidence is required; source/craft/fidelity review remains unopened. See `.impeccable/review/REVIEW_STATUS.md`.
- Impeccable documenter left DESIGN.md and .impeccable/design.json absent because its contract requires a completed build. This documentation gate also remains open; actual XAML remains the source of the current theme definitions.

## Next

1. Install a supported stable .NET 10 x64 SDK and enable permitted NuGet restore access. Official download: https://dotnet.microsoft.com/download/dotnet/10.0.
2. Run `dotnet restore SnapDrop.sln --use-lock-file`, `dotnet build SnapDrop.sln -c Release --no-restore`, then `dotnet test SnapDrop.sln -c Release --no-build --filter "Category!=InteractiveWindows"`. Fix compiler/test failures before progressing. Review/commit locks.
3. Run App plus the clipboard/drop probes on an unlocked Windows 11 desktop. Prove Win + Shift + S → thumbnail → Explorer drop / Copy / Save As, then execute docs/TEST_PLAN.md.
4. Measure performance/soak behavior, gather real UI captures, reopen full UI review and design documentation, and build/package/sign/test installation according to docs/RELEASE_PLAN.md.

Preserve existing user changes, including the original leading `H#` in Implementation.md; it was not an implementation blocker and was not silently edited.
