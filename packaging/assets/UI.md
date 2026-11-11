# SnapDrop — UI/UX Specification

> **Document:** `UI.md`  
> **Product:** SnapDrop — macOS-like screenshot workflow for Windows  
> **Platform:** Windows 11 first; Windows 10 where APIs support it  
> **Implementation:** C# / .NET 10 / WPF (XAML), MVVM, Win32 interoperability  
> **Status:** UI design specification based on the 14 approved screen mockups  
> **Last updated:** 2026-11-11

---

## 1. Vision and guiding rules

SnapDrop is a lightweight, local-first Windows screenshot utility. Its defining experience is **capture → floating thumbnail → drag, copy, or save anywhere**. The full app exists for history, editing, and configuration; routine captures should never require opening it.

### Experience principles

1. **Zero-friction capture:** a shortcut or tray action triggers capture; after capture, show the draggable preview without stealing focus.
2. **Draggable means a real transfer:** drag exposes a real image file to compatible Windows drop targets; don't merely animate a thumbnail.
3. **Keep screenshots safe:** thumbnail dismissal is *not* deletion. Preserve captured originals until explicit deletion or a clearly defined retention policy.
4. **Progressive disclosure:** minimal floating preview first; advanced editing/settings only on demand.
5. **Native and accessible:** familiar Windows interaction patterns, keyboard access, focus indicators, high-DPI correctness, screen reader labels, light/dark themes.
6. **No silent surprises:** clipboard monitoring and automatic cleanup are clearly disclosed and controllable; never assume all clipboard images came from Snipping Tool.
7. **Performance-sensitive:** async encoding/storage and lightweight UI; preview latency is measured, not assumed.

### Screens covered

| # | Screen | UI form | Priority |
|---|---|---|---|
| 01 | Home Dashboard | Main window | MVP |
| 02 | Floating Screenshot Thumbnail | Non-activating overlay | **Critical MVP** |
| 03 | Expanded Screenshot Preview & Editor | Main/editor window | Viewer MVP; annotation later |
| 04 | System Tray Menu | Tray popup + submenu | MVP |
| 05 | Screenshot History | Main window | MVP basic |
| 06 | General Settings | Settings page | MVP |
| 07 | Capture Settings | Settings page | Native capture phase |
| 08 | Save As | Native Windows file picker | **Critical MVP** |
| 09 | Keyboard Shortcuts | Settings page | MVP basic; more actions later |
| 10 | Welcome & Onboarding | First-run wizard | MVP compact |
| 11 | Notifications & States | Toasts, overlays, status | MVP |
| 12 | Save & Files Settings | Settings page | MVP |
| 13 | Appearance Settings | Settings page | MVP basic; rich effects later |
| 14 | Advanced Settings | Settings page | MVP basic; diagnostics later |

**Design references:** The approved visual mockups from the conversation are references, not exact implementation screenshots. The image mockups include illustrative data, shortcuts, and functions that may be deferred. This specification resolves functional inconsistencies and defines implementable behavior.

---

## 2. Visual language / design system

### 2.1 Design direction

Modern Windows 11 Fluent-inspired dark UI with a deep navy background, soft blue accents, restrained glow, subtle 1 px borders, rounded cards, high-contrast foreground text, and acrylic-style surfaces when supported. The app must also offer Light and Follow System themes. Avoid excessive neon effects in production; prioritize legibility.

### 2.2 Design tokens (initial proposed values)

| Token | Dark value | Usage |
|---|---|---|
| `color.bg.base` | `#071426` | App background |
| `color.bg.sidebar` | `#0A1B31` | Sidebar/navigation |
| `color.bg.surface` | `#10233D` | Main panels/cards |
| `color.bg.surfaceHover` | `#183352` | Hover states |
| `color.bg.input` | `#152A45` | Inputs/selects |
| `color.accent.primary` | `#176BFF` | Active navigation/primary CTA |
| `color.accent.hover` | `#3586FF` | Hovered CTA |
| `color.accent.focus` | `#60B4FF` | Focus ring |
| `color.text.primary` | `#F5F8FF` | Headlines/text |
| `color.text.secondary` | `#AFBED2` | Descriptions/metadata |
| `color.text.muted` | `#7F94AC` | Hint text |
| `color.border.default` | `#294260` | Card borders/dividers |
| `color.success` | `#22C88A` | Saved/active |
| `color.warning` | `#F7B545` | Warning |
| `color.error` | `#FF5D6B` | Error/destructive |
| `color.editor.purple` | `#6955FF` | Optional editor Done accent |
| `radius.card` | `14px` | Cards |
| `radius.panel` | `18px` | Large containers |
| `radius.control` | `9px` | Buttons/fields |
| `radius.thumbnail` | `16px` | Floating preview |
| `space.unit` | `4px` | 4/8/12/16/24/32 spacing scale |
| `border.default` | `1px` | Subtle separators |

Use **theme resources**, not literal hex codes in individual XAML views. Create corresponding Light theme resources using accessible contrasts. Adopt system text scaling rather than hard-coding pixel text heights.

### 2.3 Typography and icons

- Primary: **Segoe UI Variable**, fallback Segoe UI.
- Page title: 24–28 px semibold; section heading 16–18 px semibold; body 13–14 px; metadata 11–12 px.
- Navigation and buttons: 13–14 px, labels written in sentence case.
- Icons: consistent 18–20 px stroke-style symbols; prefer Segoe Fluent Icons / properly licensed vector assets.
- States must be communicated with text/icon as well as color (e.g. “Monitoring active” + dot).

### 2.4 Layout and responsiveness

- Main window reference size: **1440 × 900 logical px**, suggested minimum **1080 × 680**. On narrower windows hide optional right context panel and reduce gallery columns rather than truncating critical controls.
- Top bar: about 64 px tall; standard Windows minimize/maximize/close affordances.
- Main sidebar: about 210 px; settings category sidebar: about 190 px; content fills remaining area; optional right rail about 300 px.
- Settings: scrolling main column, fixed navigation where space allows.
- Windows should respect DPI scaling from 100%–200%+, mixed-DPI monitors, monitor working area, taskbar position, and high-contrast modes.
- All settings pages share one shell and category navigation rather than independent windows.

### 2.5 Shared UI states

Each interactive control needs default, hovered, pressed, focused, disabled, and loading states where relevant. The window needs loading, empty, offline/irrelevant (avoid fake cloud state), error, and confirmation patterns. Show skeletons only for real work, not decorative loading.

### 2.6 Common components (WPF reusable controls)

`AppShell`, `TitleBar`, `PrimarySidebar`, `SettingsSidebar`, `SectionCard`, `SettingRow`, `SettingToggle`, `SettingDropdown`, `PrimaryButton`, `SecondaryButton`, `DangerButton`, `SearchBox`, `ScreenshotCard`, `ScreenshotGrid`, `ScreenshotMetadata`, `ThumbnailPreview`, `ThumbnailActionBar`, `StatusPill`, `Toast`, `EmptyState`, `ConfirmDialog`, `SelectionOverlay`, `ShortcutRecorder`, `ColorSwatch`, `FilePathPicker`.

All mockup controls must have functional commands; avoid inert decorative controls. Use `ICommand` / MVVM bindings, command enablement, and testable view models.

---

## 3. Navigation and information architecture

```text
SnapDrop (tray process)
├── Quick capture / clipboard ingest
│   ├── Capture overlay (native capture phase)
│   └── Floating screenshot thumbnail
│       ├── Drag into target
│       ├── Copy
│       ├── Save / Save As
│       ├── Open expanded preview/editor
│       └── Pin / Dismiss
├── Open main app
│   ├── Home (recent shots + capture actions)
│   ├── History (search, preview, select, manage)
│   └── Settings
│       ├── General
│       ├── Capture
│       ├── Save & Files
│       ├── Shortcuts
│       ├── Appearance
│       ├── Advanced
│       └── About (simple informational page)
└── First-run onboarding
    ├── Welcome
    ├── Key Features
    ├── Quick Setup
    ├── Shortcuts
    └── Ready / test capture
```

The full editor may be opened from a thumbnail, screenshot card, or history detail panel. The tray opens the application without creating a second process.

---

## 4. Screen-by-screen specifications

### Screen 01 — Home Dashboard

**Purpose:** Quick access to capture actions and recent screenshots.

**Structure:** App header (logo/name/window controls); left sidebar (`Home`, `History`, `Settings`) with monitoring status at bottom; content header (“Your screenshots, ready to use anywhere”), search field, grid/list control; four action cards; recent screenshot gallery.

**Primary elements:**
- Prominent **Take Screenshot** card with current assigned shortcut.
- Capture Region / Window / Full Screen cards (native capture features disabled or visibly marked upcoming until implemented).
- Recent Screenshots section: image cards with selectable states, filename, resolution, size, overflow menu; “View All” opens History.
- Search filters local history; grid/list selection persists locally.
- Empty state: “No screenshots yet” + action to capture or learn about `Win + Shift + S` integration.

**Behavior:** Double-click screenshot opens viewer; right-click or `…` exposes Copy, Save As, Open Folder, Delete. Do not display a hard-coded user name: generic welcome unless configured locally. Handle missing/deleted files with recoverable warnings.

**MVP:** Working recent list, search, file actions, navigation. Native capture cards phased as they are implemented.

### Screen 02 — Floating Screenshot Thumbnail (**signature interaction**)

**Purpose:** Allow immediate screenshot use without opening the main app.

**Structure:** Small bottom-right floating card above the taskbar; compact title strip with SnapDrop icon, image resolution, Pin, Close; correctly aspect-fitted preview; action bar on hover (`Copy`, `Save As`, `Save`, `Edit`, `More`).

**Behavior:**
1. After screenshot is captured/ingested and safely cached, display thumbnail on the monitor associated with capture (or mouse/active window for clipboard-only input).
2. Image body is a **drag source**, not just a clickable picture; threshold avoids accidental drags.
3. Drag offers actual file data to Windows (FileDrop) and optional bitmap data to compatible targets; use **copy**, not destructive move, as default. Never promise compatibility with every app.
4. Click the image to open expanded preview; action buttons perform only their labeled action.
5. Hover, keyboard focus, active drag, file dialog, and pin pause the dismissal timer.
6. Close hides this floating card but **does not delete** its screenshot.
7. Default auto-hide after **5 seconds** of inactivity, configurable from General.
8. Multiple captures: show newest card; retain prior images in History. Optionally stack in a later release.
9. Do not steal focus; support monitor boundaries, per-monitor DPI, taskbar repositioning, Windows session lock/unlock.
10. Keyboard accessible when intentionally focused; tooltip/accessible names for icon-only actions.

**Sizing:** Default approximately 300 × 190–230 logical px, with Small/Medium/Large settings. Use safe working-area insets (16–24 px). Actual screenshot image always uses `Uniform` stretch and original stays unmodified.

**States:** Default, Hover/Expanded Actions, Pinned, Dragging, Saving, Error, Auto-dismiss. A subtle timer bar is optional and should respect reduced-motion settings.

**Critical constraint:** Windows Snipping Tool companion integration may receive images from unrelated clipboard activity. Label monitoring as **clipboard image monitoring**, not guaranteed Snipping Tool detection.

### Screen 03 — Expanded Screenshot Preview & Editor

**Purpose:** Inspect and optionally annotate a screenshot.

**Structure:** Top title/file metadata and zoom controls; left recent-thumbnail strip; central large image canvas; right editing panel; bottom toolbar with Copy, Save As, Save, Done.

**Tools depicted:** Select, Rectangle, Arrow, Text, Draw, Highlight, Blur, Crop, Erase; swatch palette; stroke size; Undo/Redo; zoom and fit-to-window.

**Interaction details:**
- Pan/zoom image while preserving full-resolution pixels.
- Keep annotations as an editable overlay/document model until export (non-destructive editing).
- Undo/redo history; prompt before discarding unsaved edits.
- Save preserves original unless user explicitly chooses overwrite. Save As exports a new rendered output.
- “Done” commits or finishes edits and returns to previous screen; state of modified image is unambiguous.
- If annotation tools aren't implemented in MVP, use a **viewer-only** version; don't ship fake editing buttons.

**Phasing:** Viewer, zoom, Copy/Save As in MVP; annotation, cropping, blur, undo/redo in later phase.

### Screen 04 — System Tray Menu

**Purpose:** Lightweight operation while full app remains closed.

**Structure:** Compact pop-up anchored above tray icon; branded header with “Running” indicator; commands (Take Screenshot, Capture Region, Capture Window, Capture Full Screen); Recent Screenshots submenu (3 previews); Open SnapDrop; Settings; Pause/Resume clipboard monitoring; Start with Windows; Exit.

**Behavior:**
- Single-click tray can open menu, double-click opens main app (or consistent documented alternative).
- Recent screenshots submenu supports Open / Copy / Show in Explorer.
- `Pause Monitoring` pauses clipboard listener only; does not necessarily disable manually requested capture.
- Startup toggle reflects actual Windows startup registration.
- Exiting should complete/cancel active operations cleanly and not delete user files.

**Shortcut labels are dynamic**, not hard-coded. Avoid claiming SnapDrop owns `Win + Shift + S` or `PrtScn` if Windows already registered them.

### Screen 05 — Screenshot History

**Purpose:** Organize and reuse earlier captures.

**Structure:** Left primary navigation; search and time-range filter; grid/list toggle; selection mode; date-grouped galleries (Today, Yesterday, date); right preview and metadata rail; footer with count, size, pagination or virtualized loading.

**Item:** Thumbnail, filename, resolution, size, selection checkbox, overflow menu.

**Right detail panel:** Larger preview; filename; dimensions, size, capture timestamp, source (if known), format and path; actions Copy, Save As, Edit, Open in Explorer, Delete.

**Behavior:**
- Search by filename and metadata; date filters operate on capture timestamp.
- Multi-select for bulk copy/export/delete where possible; confirm destructive deletions and distinguish **Delete from history** from **Delete file from disk**.
- On large histories use virtualization, thumbnail cache, background loading; keep scroll responsive.
- Missing images: show a broken-link placeholder and “Locate file”/remove entry.
- Screenshots only appear in history if saved to or indexed by SnapDrop; don't claim to index arbitrary user files automatically.

**MVP:** Local history, search, previews, single-item actions; bulk management can be subsequent iteration.

### Screen 06 — General Settings

**Purpose:** Control routine app lifecycle and preview behavior.

**Structure:** Shared settings sidebar; center section cards; right live floating-preview example and quick summary.

**Settings:**
- **Startup & Background:** Start with Windows (default Off), show tray icon (On, necessary for tray mode).
- **Floating Preview:** Show after capture (On), duration (default 5 seconds; 3–15 s plus “Until dismissed” as optional), click behavior (Open Preview), pin behavior.
- **Clipboard Monitoring:** explicit enable/pause (Off until user opts in during onboarding; may be On if consented).
- **Background app:** closing window minimizes to tray, but Exit quits process.

**Behavior:** Auto-save safe changes immediately and show clear contextual errors for changes requiring OS permission. Live preview reacts locally to theme/thumbnail options without capturing user's screen.

### Screen 07 — Capture Settings

**Purpose:** Configure SnapDrop’s own native capture engine.

**Sections:**
- **Default capture mode:** Region, Window, Full Screen; Scrolling shown only as planned/disabled until available.
- **Capture Behavior:** Crosshair cursor, dim outside selection, selection magnifier, include mouse pointer in capture.
- **Capture Feedback:** sound and subtle flash (user-toggleable).
- **Selection Style:** border color, dashed/solid, 1–4 px thickness; right live overlay preview.

**Behavior:** Changing mode affects SnapDrop capture commands, not Windows Snipping Tool settings. Capture source availability and app-specific limitations must be communicated. Screen selection must work with mixed DPI and multiple monitors; don't accidentally capture SnapDrop's own overlay.

**MVP note:** This page becomes fully functional alongside native capture. Companion-only version may show explanatory content rather than nonfunctional controls.

### Screen 08 — Save As Dialog

**Purpose:** Save a screenshot to any writable user-chosen location.

**Preferred implementation:** Use a **native Windows Save File dialog** for reliability, accessibility, existing Windows folder integration, and familiarity. The richer branded three-column design in the mockup is a conceptual reference only.

**Fields/actions:** Folder location (Explorer-like navigation), filename prefilled with timestamp-based default, file type PNG/JPEG, Save, Cancel. JPEG quality is controlled in Save & Files settings or a separate format options dialog; **PNG does not have a lossy “quality” slider**. Include cursor only if it was actually captured in the image.

**Behavior:**
- Prompt when target file already exists; no silent overwrite.
- Validate path, permissions and extension; retry gracefully on errors.
- Saving a new copy never deletes/relocates the original cache or auto-saved file.
- Remember last user-selected folder only if enabled.
- Cancel returns to preview without data loss.

### Screen 09 — Keyboard Shortcuts

**Purpose:** View and customize SnapDrop-specific shortcuts.

**Structure:** Global shortcuts toggle; action rows with current binding and Edit icon; keyboard shortcut illustration; explanation panel; conflict-status panel; Reset/Apply controls.

**Actions:** Capture Region, Capture Window, Full Screen, Open Editor, Open Screenshots Folder, Show/Hide Preview; Scrolling Capture stays disabled until implemented.

**Behavior:**
- Clicking Edit activates a hotkey recorder, not a plain text input.
- Validate keys and registerability using Windows API; clearly report collisions when registration fails.
- Never overwrite reserved shortcuts or infer a conflict is absent without verifying actual OS registration.
- Apply changes atomically: retain old working shortcut if new registration fails.
- `Win + Shift + S` is displayed as **Windows Snipping Tool** for companion mode, not a SnapDrop-owned editable shortcut.
- Suggest a distinct default for native capture (e.g. `Ctrl + Shift + 4`, pending conflict check).

### Screen 10 — First-Run Welcome & Onboarding

**Purpose:** Explain the app and enable core preferences without requiring an account.

**Five-step wizard:**
1. **Welcome:** SnapDrop description, floating-preview visual, Next/Skip.
2. **Key Features:** Capture, drag-drop, copy/save, optional editor/history.
3. **Quick Setup:** Choose clipboard monitoring with explicit privacy disclosure, default save folder, auto-save and startup preferences.
4. **Choose Shortcuts:** Explain Windows Snipping Tool companion shortcut versus native SnapDrop shortcut (if available); validate conflicts.
5. **You're All Set:** Test screenshot action and “Finish”.

**Rules:** Skip Setup uses privacy-preserving defaults (no clipboard monitoring without consent); no sign-in required; steps persist if wizard closes unexpectedly; don't mark onboarding complete until Finish or Skip.

### Screen 11 — Notifications & States

**Purpose:** Provide feedback without double-notifying for every screenshot.

**Toast types:**
- **Success:** “Screenshot saved” with folder/file and optional “Show file”.
- **Info:** “Copied to clipboard”.
- **Info:** “Opened in editor”.
- **Warning/Error:** “Save failed” with actionable Retry/Open Settings.
- **Capture acknowledgment:** use floating thumbnail; avoid separate redundant toast by default.

**Thumbnail states:** Default, Hover/Expanded, Pinned, Auto-dismiss, Error. Tray states: Running, Capturing, Saving, Attention. In-app indicators: Monitoring active, Capture in progress, Saved, Error.

**Rules:** Messages must reflect actual operation completion; no success before write/copy succeeds. Don't expose sensitive file paths in OS notifications if privacy mode enabled. Use OS-integrated notifications when practical; accessible, dismissible fallback in-app toasts are acceptable.

### Screen 12 — Save & Files Settings

**Purpose:** Define file storage, naming, format, and organization.

**Controls:**
- Default screenshot directory + Change/Browse.
- Auto-save toggle (On by recommended default for screenshots SnapDrop captures; clipboard mode requires user opt-in).
- Also copy to clipboard (On for SnapDrop native captures; do not override unrelated clipboard data without user action/consent).
- Save edited image as a separate copy (On recommended).
- Filename preset (`Screenshot_{yyyy-MM-dd_HH-mm-ss}`), alternative shorter patterns, custom template and live generated preview.
- File format (PNG default, JPEG optional).
- JPEG quality (default 90%; enabled only for JPEG).
- Folder organization by date (Off by default unless specifically chosen), live example path.
- Right rail: preset picker and read-only summary.

**Behavior:** Unique filename fallback for multiple shots in same second, invalid custom tokens rejected, Unicode path support, atomic writes, safe permission errors. Keep user-owned files separate from disposable thumbnail cache.

### Screen 13 — Appearance Settings

**Purpose:** Personalize SnapDrop without compromising performance or accessibility.

**Controls:** Dark / Light / System theme; accent swatches and custom color; optional acrylic transparency; rounded corners; floating preview style/size/shadow/action visibility; animation toggle; Reset Appearance; right live preview.

**Defaults:** **Follow System** theme (dark mockups are design examples), blue accent, medium thumbnail, modest shadow, reduced animations when Windows requests reduced motion.

**Behavior:** Apply changes live and persist; maintain sufficient contrast for all accents. Disable unsupported acrylic effects gracefully; avoid costly effects on large content windows.

### Screen 14 — Advanced Settings

**Purpose:** Diagnostics, maintenance and opt-in experimental controls for power users.

**Sections:**
- **Performance:** Hardware-accelerated drawing/capture when supported; background processing. A generic “CPU limit 50%” slider should **not ship** unless the engine can actually enforce it.
- **Storage & Cleanup:** Clear thumbnail cache; managed temp retention; safe cleanup. **Never auto-delete user-saved screenshots by default**, even if a mockup shows 30-day deletion.
- **Clipboard & Integration:** clipboard listener setting (shared with General), clipboard preservation preferences, Windows integration entry points where supported.
- **Logging & Diagnostics:** opt-in detailed local logs, open log directory; never log screenshot pixels or clipboard contents by default.
- **Experimental Features:** clearly labeled disabled-by-default betas.
- **Right sidebar:** actual installed version, update channel only if updater implemented, cache size, Clear Cache, Rebuild Thumbnails, Reset Settings, Run Diagnostics, Export Diagnostic Report, Open Support Guide.

**Behavior:** Confirm destructive actions; sanitise diagnostic reports for paths/identifiers; avoid claiming updates available or checks succeeded without a functioning update mechanism. Cleanup always excludes original user files outside managed cache unless explicitly selected.

---

## 5. Shared interaction specifications

### 5.1 Capture to drag/save flow

```text
Global shortcut / tray / Windows Snipping Tool
          ↓
Capture engine or consented clipboard listener
          ↓
Validate → create ID → persist managed original safely
          ↓
Display non-activating floating preview
          ├─ Drag → Windows OLE data object (file + optional bitmap) → target
          ├─ Copy → clipboard → success confirmation
          ├─ Save → configured folder → success confirmation
          ├─ Save As → native picker → image encoding/write
          ├─ Edit → viewer/editor
          ├─ Pin → keep preview visible
          └─ Close/timeout → hide preview, retain file/history
```

**Drag implementation guidance:** Expose `DataFormats.FileDrop` using an existing, valid image path and `DragDropEffects.Copy`; consider `DataFormats.Bitmap` for compatible applications. Keep original file available long enough for asynchronous consumers. Do not move/delete the source on drop. A target may reject the transfer; offer copy/save fallbacks.

### 5.2 Thumbnail lifecycle and race conditions

States: `Created → Persisting → Ready → Visible → {Dragging | Copying | Saving | Editing | Pinned | Hidden} → Retained → EligibleForCleanup`.

Separate **UI visibility** from **file lifetime**. Drag and file-dialog operations pause auto-dismiss. If new screenshots arrive while a preview is active, don't destroy data; queue/history always retains each screenshot. Explicit Quit must not silently delete source images.

### 5.3 Clipboard policy

- Opt-in consent; clearly explain monitoring receives **image clipboard changes from many apps**, not just Snipping Tool.
- No clipboard polling loop; use Windows clipboard update notifications.
- Deduplicate bursts and prevent recursive detection from own clipboard writes.
- Safeguard temporary files; don't upload or inspect screenshots for unrelated purposes.
- If clipboard is locked, retry with bounded backoff; don't block UI thread.

### 5.4 Error/empty-state copy examples

| Situation | Message | Action |
|---|---|---|
| No captures yet | “No screenshots yet. Capture one to get started.” | Take Screenshot |
| Target app rejects drag | “This app may not accept dropped images. Try Copy or Save As.” | Copy / Save As |
| Save permission denied | “SnapDrop couldn't save here. Choose another folder.” | Save As |
| Clipboard image locked | “Image is temporarily unavailable.” | Retry |
| Missing history file | “This screenshot was moved or deleted.” | Locate File / Remove Entry |
| Shortcut unavailable | “This shortcut is already in use or reserved.” | Choose Another |
| Storage nearly full | “Not enough space to save screenshot.” | Change Location |

### 5.5 Confirmation and destructive operations

- Closing preview ≠ deleting screenshot.
- Deleting from history ≠ deleting a file, unless explicitly chosen.
- Cache cleanup ≠ deleting originals.
- Reset settings requires a confirmation; don't reset screenshot history/files.
- Bulk delete requires selection count and clear recovery consequences.

---

## 6. WPF application composition

### Suggested view/view-model structure

```text
SnapDrop.App/
├── App.xaml
├── Themes/
│   ├── Colors.Dark.xaml
│   ├── Colors.Light.xaml
│   ├── Typography.xaml
│   ├── Controls.xaml
│   └── Icons.xaml
├── Views/
│   ├── MainWindow.xaml
│   ├── HomeView.xaml
│   ├── HistoryView.xaml
│   ├── FloatingPreviewWindow.xaml
│   ├── ExpandedPreviewWindow.xaml
│   ├── OnboardingWindow.xaml
│   ├── Settings/
│   │   ├── GeneralSettingsView.xaml
│   │   ├── CaptureSettingsView.xaml
│   │   ├── SaveFilesSettingsView.xaml
│   │   ├── ShortcutsSettingsView.xaml
│   │   ├── AppearanceSettingsView.xaml
│   │   ├── AdvancedSettingsView.xaml
│   │   └── AboutView.xaml
│   └── Controls/
│       ├── ScreenshotCard.xaml
│       ├── SettingsSection.xaml
│       └── ThumbnailActionBar.xaml
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── HomeViewModel.cs
│   ├── HistoryViewModel.cs
│   ├── FloatingPreviewViewModel.cs
│   ├── ExpandedPreviewViewModel.cs
│   ├── OnboardingViewModel.cs
│   └── Settings/
├── Services/
│   ├── NavigationService.cs
│   ├── ThemeService.cs
│   └── NotificationService.cs
└── Resources/
```

Windows services outside the UI assembly handle clipboard listener, capture, hotkeys, OLE drag/drop, SaveFileDialog, monitor enumeration, settings persistence, file storage, and screenshot indexing. Each UI action binds to an interface-backed command, enabling fake services for automated visual/UI tests while preserving real OS integrations in production.

### Suggested persisted configuration shape

```json
{
  "schemaVersion": 1,
  "general": {
    "startWithWindows": false,
    "showTrayIcon": true,
    "showFloatingPreview": true,
    "previewDurationSeconds": 5,
    "clipboardImageMonitoring": false
  },
  "files": {
    "autoSaveNativeCaptures": true,
    "defaultDirectory": "<resolved user Pictures>/SnapDrop",
    "format": "png",
    "jpegQuality": 90,
    "filenamePattern": "Screenshot_{yyyy-MM-dd_HH-mm-ss}",
    "organizeByDate": false
  },
  "appearance": {
    "theme": "system",
    "accent": "#176BFF",
    "thumbnailSize": "medium",
    "animations": true
  }
}
```

`<resolved user Pictures>` is a **placeholder for runtime path resolution**, not a literal folder name. Extend versioned settings with capture, shortcut, retention and accessibility fields when implemented.

---

## 7. Accessibility and localization requirements

- Keyboard navigate every control; visible focus rings; sensible tab order and Escape behavior.
- Screen readers announce names, roles, state changes, notification content, and image metadata.
- Support minimum target sizes around 32–40 logical px, sufficient for pointer input.
- High-contrast theme and Windows text scaling; no information conveyed through color alone.
- Respect reduced-motion and Windows transparency settings.
- Localize every displayed string, date/time format, units and file-size presentation; filenames must support Unicode.
- Ensure popup windows and native dialogs are positioned on reachable screens and remain usable after monitor disconnection.

---

## 8. Scope and implementation order

### Release 0.1 — functional companion MVP

1. **Shell:** real main window, sidebar, tray menu and settings routing.
2. **Clipboard ingestion (opt-in):** Windows Snipping Tool companion integration with explicit limitations, deduplication and image validation.
3. **Local persistence:** originals in configurable folder, managed temp files, simple index/history.
4. **Floating preview:** capture acknowledgment, drag, Copy, Save, Save As, Pin, dismiss, multi-monitor placement.
5. **History & viewer:** recent screenshots, search, metadata, file actions, viewer without fake tools.
6. **Settings:** General, Save & Files, and basic Appearance/Shortcuts/Advanced preferences wired to actual behavior.
7. **Onboarding and notifications:** clear consent and real success/error states.
8. **Testing & packaging:** test on Windows 11, varied display DPI, File Explorer drag/drop and representative apps; installer/standalone distribution.

### Release 0.2 — independent native capture

Region-selection overlay, window/full screen capture, real custom shortcuts, Capture Settings controls, mouse-pointer handling, magnifier, capture feedback.

### Release 0.3 — richer editor + power features

Non-destructive annotations, crop, blur, undo/redo, bulk history tools, advanced diagnostics, optional scrolling capture investigation. No scrolling capture button should appear as functional until a real implementation exists.

---

## 9. UI acceptance criteria

### Signature screenshot workflow

- [ ] With monitoring enabled, a new supported clipboard image can show a floating preview without opening the main window.
- [ ] Capturing through SnapDrop (once implemented) shows the preview on the appropriate monitor.
- [ ] Dragging thumbnail into File Explorer copies a valid image, with original retained.
- [ ] Copy successfully places a usable image on clipboard.
- [ ] Save As opens native picker and saves PNG/JPEG to a chosen writable location.
- [ ] Auto-dismiss, Close and app-window minimization never accidentally delete saved files.
- [ ] Hover, pin and active drag prevent timeout.
- [ ] File-write, clipboard, and drag failures show truthful, actionable feedback.

### Visual and usability

- [ ] All 14 views and states are visually consistent with shared tokens.
- [ ] Settings navigation and sidebar active states are consistent.
- [ ] Grid/list and search operate on actual screenshot data.
- [ ] No dead or placeholder controls are presented as working.
- [ ] Responsive layouts remain usable down to the documented minimum window size.
- [ ] Mixed-DPI multi-monitor windows appear in bounds and remain crisp.
- [ ] Keyboard and screen-reader workflows pass essential tasks.
- [ ] Light/Dark/System themes preserve contrast and function.

### Reliability / privacy

- [ ] Clipboard monitor is disabled until explicitly enabled by user.
- [ ] Native app copy action does not trigger an infinite clipboard preview loop.
- [ ] Recent images are not silently uploaded, OCR'd, or shared.
- [ ] Cleanup never deletes unrelated or user-owned images outside the declared managed scope.
- [ ] Unsupported/registered Windows shortcuts are not forcibly hijacked.
- [ ] Test reports distinguish automated verification from manual Windows integration testing.

---

## 10. Handoff instructions for coding agent

**Build using this `UI.md` as the UI source of truth**. First implement reusable theme resources, shells, and view models; then wire each user-facing control to actual services. Prioritize the end-to-end capture-to-floating-preview-to-drag/save flow over elaborate visual effects. Match the approved dark visual direction without hard-coding screen-specific styles. Preserve privacy, data safety, and native Windows conventions. Explicitly label planned features instead of using no-op UI. Document any difference between the image mockups and working native UI.

**Design reference mapping:**
- Screen 01: Home Dashboard
- Screen 02: Floating Screenshot Thumbnail
- Screen 03: Expanded Screenshot Preview & Editor
- Screen 04: System Tray Menu
- Screen 05: Screenshot History
- Screen 06: General Settings
- Screen 07: Capture Settings
- Screen 08: Save As
- Screen 09: Keyboard Shortcuts
- Screen 10: First-Run Welcome & Onboarding
- Screen 11: Notifications & States
- Screen 12: Save & Files Settings
- Screen 13: Appearance Settings
- Screen 14: Advanced Settings

When screenshots/images are available in the repository, store them in `docs/design-references/` and link them here using **actual** checked-in filenames; don't invent file paths or assume they are bundled with this Markdown file.
