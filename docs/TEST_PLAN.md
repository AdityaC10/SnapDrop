# SnapDrop verification and acceptance plan

Status: planned tests. No application tests or live compatibility checks have run at blueprint delivery.

## 1. Evidence rules

Separate pure logic, real filesystem checks, OS-adapter tests, interactive desktop checks, third-party compatibility, and packaged-installation checks. A passing fake drag service or DataObject inspection does not prove Explorer or WhatsApp accepted a drop.

For each manual result record date, commit/build, Windows build/edition, architecture, DPI/monitor layout, target application version, exact drop zone, image dimensions, steps, observed result, and evidence path. Use synthetic grid/text fixtures without private content.

Desktop tests that require an unlocked interactive session are tagged `InteractiveWindows`; skip with an explicit reason in ordinary headless CI. Release approval requires their actual execution, not a green CI job that skipped them.

## 2. Core unit coverage

Use deterministic time via TimeProvider and focused adapter fakes. Test behavior, not the internal call order of trivial wrappers.

- Retention: visible, pending, pinned interaction, active read/drag/export, Keep, age boundary, and transfer grace all prevent unsafe cleanup as specified.
- Lifetime: timeout starts at Ready; pause/resume retains remaining time; stacked pause reasons are independent; a new idle capture resets the deadline.
- Burst handling: current interaction remains bound to its capture ID; latest pending replaces only pending presentation; all committed records remain in history.
- Ingestion identity: one event token yields one capture; later equal pixels yield another; self-marker suppresses own Copy events.
- Image limits: zero/negative dimensions, excessive pixels, overflow, invalid stride, and allocation-budget rejection.
- Export policy: PNG/JPEG matching, filename collision, cancellation, overwrite confirmation, destination races, and metadata failure after successful file commit.
- Settings: defaults, range validation, schema migration, malformed input, and persistence failure reporting.
- Storage policy: budget cannot evict an unexpired/Kept/leased image; full storage pauses intake explicitly.

## 3. Filesystem integration coverage

Use a unique test-owned temporary directory per test and verify all cleanup targets stay below it.

- Commit produces a decodable PNG and valid matching manifest; no Ready result before both exist.
- Crash/fault injection after original rename but before metadata commit leaves a recoverable orphan, not an indexed corrupt capture.
- Manifest updates are serialized; simultaneous export/Keep changes do not overwrite each other.
- Corrupt JSON or missing originals isolates the entry; healthy history still loads.
- A corrupt cached thumbnail rebuilds from the original.
- Cleanup never deletes exported files, arbitrary paths in tampered metadata, or files reached through directory reparse points.
- Same-process lease acquisition and deletion serialize without a check/delete race.
- File naming handles same-millisecond captures, Unicode destinations, long names, and collisions.
- Permission-denied/full-disk/disconnected-destination failures retain the original and preserve an existing destination. Use injected I/O faults where reproducing full disk is impractical, then qualify representative real failures manually.
- Settings recovery keeps a last-known-good backup and never silently pretends an invalid settings write succeeded.

## 4. Windows adapter tests

Run imaging and noninteractive adapter tests on Windows. Clipboard and cross-process tests need an isolated interactive session; never overwrite a developer's real clipboard through an unattended default test command.

- PNG round trip retains dimensions and canonical opaque SDR pixel values in synthetic text/grid fixtures. Include alpha fixtures and verify the specified compositing/conversion contract.
- JPEG starts with a JPEG signature, decodes at original dimensions, and flattens alpha to white. Do not assert byte/pixel equality for lossy JPEG.
- DataObject advertises an existing absolute FileDrop PNG and Copy effect. A real DropProbe extracts the path and decodes it during and after the drop.
- ClipboardProbe publishes text, bitmap/DIB, PNG where supported, and a self-marked image. Verify supported formats, ignored formats, and no self-copy loop.
- ClipboardProbe holds the clipboard briefly to exercise bounded retries, and replaces data during reads to exercise stale-event handling.
- An intentionally slow delayed-rendering producer verifies that the main UI remains responsive; document the clipboard STA's inability to forcibly cancel the native read.
- Copy persists image paste after SnapDrop exits. Test actual paste into a second process.
- Native SaveFileDialog has the correct owner, filter, extension, overwrite behavior, and cancellation outcome.
- The listener registers/unregisters once; shutdown and pause release or stop reads as intended.

## 5. Manual release scenarios

### A. Primary workflow — mandatory

1. Install on a clean Windows 11 x64 machine and enable monitoring through onboarding.
2. Open a text editor and leave its caret active. Capture a recognizable text/grid region with Win + Shift + S.
3. Confirm one SnapDrop preview appears, with correct aspect ratio, while the editor retains focus.
4. Drag the image to an Explorer folder. Confirm a new PNG exists, opens, and has the expected native dimensions and content.
5. Reopen the capture from Recent and choose Save As. Pick a different folder and custom PNG name. Confirm the result is decodable and correct.
6. Export JPEG from the same item and inspect dimensions, actual encoding, and acceptable visible quality.
7. Copy; paste into a tested image-capable app. Exit SnapDrop and paste again.
8. Dismiss another capture; restart SnapDrop; confirm it remains available in Recent within the retention policy.

### B. Monitoring and identity — mandatory

1. With monitoring disabled, capture an image; no ingestion occurs. Enabling monitoring does not import the old clipboard image.
2. Publish text only; no preview appears. Publish a supported ordinary copied image; it is ingested, consistent with the disclosed behavior.
3. Capture the same scene twice; two items appear in Recent.
4. Use Copy on an existing capture repeatedly; no new SnapDrop history entries appear.
5. Pause, resume, lock, and unlock. No paused/locked image is unexpectedly backfilled.
6. Publish a controlled burst. Check every committed item and overload indicators; do not require recovery of pixels overwritten before the listener could read them.

### C. Lifetime and drag — mandatory

1. Leave a preview idle; it hides after the configured timeout without deleting its original.
2. Hover or focus it longer than the timeout; it stays. Pin it; it stays until explicitly unpinned/dismissed.
3. Begin drag, wait longer than timeout, then cancel with Escape. The original remains usable.
4. Capture another image during a drag or Save As. Confirm the action continues using the first capture, and the latest pending preview appears afterward.
5. Drag to a rejecting target. No false upload confirmation or file deletion occurs.
6. Request Exit during drag; graceful shutdown waits for the active drag to end.
7. Restart after a completed drag and inspect persisted transfer grace. A delayed reader inside the stated grace can still open the path.

### D. Displays and accessibility — mandatory

- Single-monitor 100%, 150%, and 200% scaling.
- Dual-monitor 100%/150% and 100%/200%; secondary monitor positioned left/above primary.
- Taskbar on each supported edge, auto-hide, and changing work area.
- Disconnect or change scale of the monitor containing the preview; no unreachable window remains.
- Keyboard-only Recent/Save As/Copy/Keep/Delete, visible focus, screen-reader names, high contrast, and text scaling.
- Arrival does not activate; explicit keyboard interaction does. Escape does not affect unrelated apps while preview is unfocused.
- Lock/unlock hides previews. Remote-session behavior is recorded separately; do not assume local-display findings cover RDP.

### E. Failure recovery — mandatory

- Destination lacks permission; destination removed; disk-full fault; duplicate filename; confirmed overwrite.
- Kill process between original/manifest commits; restart and inspect recovery.
- Corrupt settings or one manifest; other captures remain accessible.
- Storage budget reached with no eligible files; intake pauses visibly without losing kept images.
- Attempt cleanup during view/drag/export; file remains until safe.
- Restart Explorer; notification icon returns and menu still works.
- Confirm exported files survive recovery-library expiry, Delete recovery copy, upgrade, and uninstall.
- Force a storage failure, dismiss the thumbnail, and reopen the unsaved image from the tray. Lock/unlock before reopening; the in-memory image remains recoverable without an automatic sensitive preview on unlock.
- Confirm a second failed burst image does not silently replace the retained unsaved image. Resume is blocked until Save As or explicit discard resolves recovery; cancelling Exit preserves it.
- Request Exit while an accepted capture is processing. Intake pauses, processing drains, and any newly produced unsaved recovery image receives an explicit discard warning.

## 6. Target application qualification

Minimum release requirements: Explorer folder drop, one supported browser's real file-upload drop area, and image paste into a separate app. Unsupported application-specific surfaces remain documented limitations.

Qualify these desired targets with actual installed versions before advertising them:

- WhatsApp Desktop and WhatsApp Web attachment area: image preview/attachment is received; local test only, no need to send a message.
- Outlook/new Outlook or another selected mail client: attachment is created in a draft; no email needs to be sent.
- Chrome/Edge/Firefox: use a real upload control/drop area, not the browser chrome or address bar.
- VS Code: distinguish dropping into the Explorer workspace tree (file placement) from the editor surface (often opens an image or inserts a reference, depending on context).
- Desktop and Explorer: confirm actual file copy and readable PNG.
- Elevated or otherwise restricted targets: record graceful rejection without offering elevation as an automatic workaround.

Compatibility results start as **not tested**. File-drop effects, target previews, uploads, and durable copies are different outcomes and must be described accurately.

## 7. Performance protocol and targets

Use a Release self-contained build on a documented reference machine, for example a modern 4-core-or-better CPU, 16 GiB RAM, SSD, and Windows 11 x64. Record exact hardware/OS rather than treating this example as measured evidence.

- Collect 100 sequential 1920×1080 clipboard-image runs after 10 warm-up runs. Report cold startup separately.
- Measure from notification receipt to an interactive, first-rendered thumbnail whose file drag is ready. Target p95 ≤300 ms for that reference workload; report p50 and p95.
- Also report snapshot, PNG commit, and first-render intervals separately. PNG entropy and disk/antivirus behavior affect latency.
- Repeat with 3840×2160 and a large supported multi-display image; report results without applying the 1080p target blindly.
- Target p95 <100 ms from drag threshold crossing to drag start with an existing file; exclude time spent inside a target application.
- Measure idle CPU after a one-minute settle over five minutes; target average <0.5% of total CPU capacity. Define the normalization used.
- Target idle working set <100 MiB; measure private bytes separately. Report peak during 4K capture and bursts with the configured buffer limits.
- Run a 500-capture soak with bounded history image loading. Handles/memory should settle rather than grow without limit.
- Confirm idle work is event-driven apart from infrequent cleanup; monitor disk and network. SnapDrop should initiate no application-level network requests in normal v0.1 operation.

Targets are goals, not guarantees or current benchmark results. Investigate missed goals before optimizing with native code or changing UI frameworks.

## 8. Release evidence checklist

- [ ] Core and filesystem automated checks pass.
- [ ] Windows image/adapter checks pass on a supported Windows runner.
- [ ] Required interactive scenarios pass on a real unlocked desktop.
- [ ] Application compatibility records include exact versions and drop surfaces.
- [ ] Latency, working set, private bytes, CPU, and soak results recorded.
- [ ] Clean machine installation and offline runtime test pass.
- [ ] Upgrade/uninstall preserve user exports; startup behavior matches settings.
- [ ] Signed artifact and checksum verification pass.
- [ ] Known limitations and skipped/unverified cases appear in release notes.

All boxes remain unchecked until evidence exists.
