# SnapDrop 0.1.0 — implementation candidate

Status: source implementation; build, OS integration, and installation qualification pending. No production release is declared.

## Implemented in source

- Optional event-driven clipboard image monitoring with a dedicated STA, bounded intake, retries, and self-write marker.
- Managed PNG originals, atomic manifests/settings, recovery notices, history, Keep, leases, expiry, and transfer grace.
- Non-activating thumbnail, hover/focus/Pin/action timeout pauses, drag threshold, Copy, Save As, and default-folder export.
- Native Windows tray, settings/onboarding, viewer, startup registration, and single-instance IPC.
- Unit/filesystem/image regression tests, opt-in real clipboard test, probe tools, build/test/publish scripts, and installer source.

## Qualification still required

- Restore/build/tests using a stable .NET 10 SDK and dependency lock-file review.
- Real Win + Shift + S, Explorer drop, paste persistence, Save As, focus, bursts, and error recovery.
- Mixed DPI/displays, keyboard/screen-reader/high-contrast inspection, lock/unlock, and Explorer restart.
- Actual third-party target applications and precise drop zones.
- Release performance/soak measurements, code signing, and clean-machine installation/upgrade/uninstall.

Companion mode may ingest any supported new clipboard image while enabled. Snipping Tool's own notification and automatic saving remain independent. File transfer requires a compatible target; long-lived path references require Keep or an exported copy. v0.1 makes no general Windows 10, ARM64, HDR accuracy, or universal app-compatibility claim.
