# SnapDrop v0.1 user experience

Status: implementation specification. These are proposed behaviors, not screenshots of a running app.

## 1. First launch

Show a small ordinary window explaining the three steps: capture with Win + Shift + S, use the thumbnail, and drag/copy/save.

Use an unchecked setting labeled **Show previews for new clipboard images**, with this explanation:

> While enabled, SnapDrop reads new images copied to your clipboard, including screenshots and images copied from other apps. It stores recovery copies on this computer for 7 days. You can pause this from the tray menu.

Provide Enable and continue, Continue without monitoring, and a link to storage/retention settings. Continuing without monitoring opens Recent/Settings with monitoring disabled and a clear way to enable it. Do not pretend capture is available before a source is enabled.

Offer Start with Windows as a separate unchecked option. Explain that Snipping Tool's own notifications and automatic saving are independent. SnapDrop does not change those Windows settings.

## 2. Floating thumbnail

Initial design dimensions: approximately 280 × 190 device-independent pixels, with a 16-DIP inset from the selected monitor's working area. Adapt to tiny working areas and user text scaling. Image area preserves aspect ratio with letterboxing; never crop the screenshot to fit.

```text
┌─────────────────────────────────┐
│ Screenshot                 Pin ×│
│                                 │
│      Actual image thumbnail     │
│                                 │
│ Copy       Save As…        ⋯     │
└─────────────────────────────────┘
```

This is an interaction wireframe. Use Windows system typography, restrained surfaces, visible control boundaries, and standard focus indicators during implementation.

The image is the primary drag surface. A tooltip says “Drag to attach or copy a file.” Copy and Save As are visible buttons. The overflow menu contains Save to default folder, Open preview, Show in Recent, and storage details. Pin and Dismiss have accessible names and tooltips; Dismiss is not Delete.

Show only after a real managed file is ready for drag. If persistence is unusually slow, a small non-interactive “Preparing screenshot…” state may appear, but it must not look drag-ready. If commit fails, show an explicit recovery state with Copy/Save As and no enabled file-drag action.

## 3. Focus and pointer behavior

- Arrival uses ShowActivated=false and appropriate non-activating show behavior. The foreground app keeps keyboard focus and caret.
- User interaction can explicitly activate the preview or a normal viewer window; don't permanently disable focus on every control.
- Mouse-down records position. Crossing Windows drag thresholds starts file drag; releasing below the thresholds opens the viewer.
- The Copy, Save As, Pin, Dismiss, and overflow hit targets never start an image drag.
- Escape cancels drag through OLE, cancels a dialog through the native dialog, or dismisses a focused preview. Do not intercept Escape globally in other applications.
- A keyboard-only user can reach Recent from the tray and use its normal window for all actions. Automatic non-activation must not make the feature inaccessible.

## 4. Placement and timing

In companion mode, sample the pointer's monitor when the image event arrives. Fall back to the foreground window's monitor, then primary. Do not label this as detection of where Snipping Tool captured pixels.

Use that monitor's working area, including taskbar placement, in physical coordinates. Convert window sizing through its actual DPI. Handle WM_DPICHANGED, work-area/display changes, negative coordinates, and monitor removal. Clamp the window to an available working area.

The default timeout is five seconds of unengaged time after Ready. Pause for hover, keyboard focus, Pin, context menu, viewer/dialog interaction, drag, or export. Resume with the remaining time; after an action allow at least two seconds for the user to read feedback. A unit-testable timer policy computes deadlines; the UI only schedules ticks.

Respect reduced animation preferences. Use a short optional opacity transition; no motion is required for the workflow. Pin survives the timeout of that preview only; it is distinct from Keep in the recovery library.

## 5. Repeated screenshots

Use one floating preview, not an unbounded stack.

- If the current preview is idle, the latest Ready capture replaces it and resets the timeout.
- If the user is hovering, focusing, dragging, copying, saving, or has pinned the preview, keep its screenshot fixed.
- Retain one latest pending preview; every successfully committed capture still appears in Recent.
- Show a small “More in Recent” action when several captures arrived. Do not silently imply intermediate captures were deleted.
- After the interaction ends, show the pending latest capture without stealing focus. If pinned, wait until unpinned/dismissed.
- Overload before commit gets a visible “Some clipboard changes could not be saved” message; do not claim the full burst was captured.

## 6. Save As and default saving

Visible Save As opens the native Windows file-save dialog after explicit activation. Suggest a friendly name such as `Screenshot_2026-11-11_10-22-30_a1b2c3.png`. Set initial location to the last successfully used folder, then the configured default folder, then Pictures. Fall back gracefully if a previous path disappears.

Format options are PNG and JPEG. Preserve the selected extension/encoding agreement. Explain JPEG's loss of transparency when it matters. Remember a destination only after a successful save.

The dialog is cancelable; cancellation leaves the capture and preview intact. While writing, show progress/busy state with responsive controls and prevent conflicting exports to the same path. Success feedback shows “Saved” with Open folder. Failure shows the reason and Retry/Save As without discarding the original.

Save to default folder is a separate command with collision-safe names. If the folder is unset or invalid, open the folder configuration or Save As instead of silently inventing a destination.

Optional **Open Save As after each capture** is for the alternate immediate-save workflow. It intentionally opens an activating dialog. Only one auto-prompt runs at a time; further captures go into Recent with a notice instead of queuing endless modal dialogs. Session lock cancels pending auto-prompts. A visible setting can turn this off immediately.

Automatic export to a default folder is a different setting. If both automatic export and auto-prompt are enabled, explain that they can create two user copies; favor a settings UI that makes this consequence clear.

## 7. Viewer and Recent captures

The viewer is a normal resizable window with zoom/fit, dimensions, timestamp, source labeled Clipboard image, and Drag/Copy/Save As. No editor, annotation toolbar, OCR, or simulated controls in v0.1.

Recent shows newest first with lazily loaded thumbnails and capture dates. Each entry offers reopen, Copy, Save As, Keep, and Delete recovery copy. Show the policy “Recovery copies expire after 7 days” and an expiry date for each unkept item. A kept item explicitly says Kept.

Exports list their saved locations. A missing/moved export is labeled unavailable without treating the managed original as missing. Open folder targets a selected export only if it exists.

Delete recovery copy requires confirmation explaining that exported copies are preserved. Active interactions defer deletion. Dismiss never asks to delete and never changes retention. Pinning a thumbnail does not Keep it permanently.

## 8. Tray and settings

Tray menu: Recent captures, Settings, Pause/Resume clipboard monitoring, and Exit. Pause state is visible in the tooltip/icon treatment; do not rely on color alone. No nonfunctional Capture entry ships before native capture exists.

Settings groups:

- **Clipboard:** monitoring consent, paused/active status, explanation of image-source ambiguity.
- **Preview:** timeout/Never, corner preference if implemented, animation preference, current default behavior.
- **Saving:** default folder, PNG/JPEG export, automatic export, automatic Save As prompt.
- **Recovery storage:** folder location, retention days (1–30), usage, 1 GiB soft budget, Open folder, Keep/delete management through Recent.
- **General:** Start with Windows, version, local log location, and diagnostics export initiated by the user if later implemented.

Settings write immediately or through a clear Apply action, with failure feedback. Never show startup/monitoring enabled when the underlying OS operation failed. “Launch at startup” registers this installed executable and does not relocate a portable app automatically.

## 9. Accessibility and appearance acceptance

- All visible actions have AutomationProperties names and a logical tab order.
- Controls work with keyboard, mouse, and Windows screen readers; image-only controls have text equivalents.
- Contrast and focus indicators survive Windows high contrast. Respect system text scaling and avoid clipped labels at 200% display scaling.
- Use system theme resources or a small explicit light/dark token set, not hardcoded text/background pairs scattered across views.
- Errors remain long enough to read and are available through Recent/Settings. Avoid recurring focus-stealing message boxes for background failures.
- Preview hides on session lock and does not reappear automatically with sensitive content on unlock.
