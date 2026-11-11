# SnapDrop implementation agent prompt

Open `D:\Projects\SnapDrop` as the coding agent's workspace. Give the agent access to edit this repository, run build/test commands, and use an interactive Windows desktop when available. Paste the prompt below. This requests implementation of v0.1; v0.2 native capture remains a separate release.

## Copy-ready prompt

```text
Implement SnapDrop v0.1 end to end in this repository using the existing blueprint. Work as a senior Windows desktop engineer responsible for implementation, verification, and a reviewable release candidate.

This is an implementation request. Start editing and testing after a short repository/toolchain preflight; do not stop after proposing another plan or generating an empty solution.

READ FIRST
Follow applicable repository instructions, then read:
- README.md
- Implementation.md
- docs/ARCHITECTURE.md
- docs/UX_SPEC.md
- docs/TEST_PLAN.md
- docs/RELEASE_PLAN.md
- docs/SOURCES.md
- Context.md, if it already exists

Treat the blueprint as the scope and architecture baseline. Inspect the current source and preserve existing user changes. Use current official documentation for Windows APIs and dependencies when needed. Document necessary deviations with their reason and effect on acceptance criteria; never remove a requirement merely to declare completion.

PRODUCT OUTCOME
Ship the complete v0.1 companion workflow:
Win + Shift + S -> Windows publishes a clipboard image -> SnapDrop commits a managed PNG -> a non-activating floating thumbnail appears -> the user drags the file, copies the image, or chooses Save As.

Implement the complete planned v0.1, including onboarding/opt-in monitoring, tray/single-instance lifecycle, history/recovery, settings, safe retention, PNG/JPEG export, accessibility, mixed-DPI behavior, startup preference, tests, build scripts, CI, and packaging. Native region/window capture, OCR, annotation, recording, AI, cloud features, and automatic updates remain out of scope.

EXECUTION
1. Inspect the workspace, toolchain, OS, permissions, and any existing verification evidence. Check for a stable .NET 10 SDK; a runtime alone is insufficient. Obtain missing prerequisites only through permitted methods. Do not silently retarget the application to an older framework.
2. Follow M0-M6 in Implementation.md, in dependency order. First prove the real capture-to-thumbnail-to-Explorer-drop and Save As interaction. Then complete the remaining MVP and release work.
3. Use the planned App, Core, Windows, and Infrastructure boundaries. Keep Windows/WPF/COM types out of Core. Use focused abstractions, explicit resource ownership, and minimal dependencies.
4. After each meaningful milestone, build, run appropriate tests, fix failures, and update the verification record before continuing dependent work. Continue through already authorized local implementation milestones without asking for routine reconfirmation.
5. If an interactive desktop, SDK/package restore, signing identity, or test hardware is unavailable, record the exact constraint. Continue independent work, leave affected release gates open, and provide reproducible setup/manual verification steps. Never report a skipped check as passed.

CORRECTNESS REQUIREMENTS
- Clipboard monitoring is optional and explicitly covers clipboard images; do not claim reliable Snipping Tool source attribution.
- Do not intercept Win + Shift + S or change Windows Snipping Tool settings.
- Clipboard work is event-driven, with bounded retries, detached image data, allocation limits, and protection against self-copy loops and duplicate events. Separate identical captures remain separate items.
- Perform OLE drag on the UI STA with an existing PNG FileDrop payload and Copy effect. A preview image alone is not file transfer.
- Do not replace or dispose the screenshot while the user is interacting with it. Protect source files through drag/export and the documented transfer grace.
- Dismissal never means deletion. Cleanup touches only eligible managed-library files; user exports are never automatic cleanup targets.
- Save As uses a real native dialog. Preserve the source on cancellation or failure, protect existing destinations, and report success only after a completed write.
- Persist history/settings safely and recover coherently after interrupted writes. Retain original pixel dimensions and distinguish lossless PNG from lossy JPEG.
- No screenshot upload, remote telemetry, cloud backend, or credentials in source/logs. Keep UI responsive, bound memory/queues/logs, and release native handles, streams, event handlers, and image resources.
- Every shipped control performs its real action. No placeholder handlers, fake success, swallowed failures, or simulated OS integration in production code. Test doubles belong in tests.

VERIFICATION AND RELEASE
Implement meaningful tests for lifecycle/retention, ingestion identity, export failures, settings, actual filesystem persistence, recovery, and cleanup races. Tag real interactive Windows tests separately and avoid overwriting a user's clipboard in default unattended tests.

Run the applicable scenarios in docs/TEST_PLAN.md. Verify Explorer file drops, real image paste, Save As, focus, timeout pauses, restart recovery, and mixed DPI on an interactive desktop when available. Record exact target applications/drop zones; DataObject unit tests do not prove WhatsApp/browser/mail compatibility.

Measure performance on a documented Release build; do not invent benchmark results or call targets achieved without measurements. Produce a self-contained, untrimmed win-x64 publish, reproducible installer configuration, portable ZIP, and Windows CI. Apply and verify release signing only with an available authorized identity; otherwise label artifacts unsigned and leave signing readiness pending. Qualify installation on a clean supported Windows machine.

CONTINUITY AND DELIVERABLES
Maintain Context.md after each milestone and before ending a work session. Record implemented behavior, files/decisions, exact commands and outcomes, manual evidence, skipped/unverified checks, blockers, and the next concrete task. On continuation, read it and resume the first incomplete milestone instead of restarting or rewriting working code.

Before finishing, review the changes against the specification and repair material defects. Deliver working source, tests, scripts/CI, local publish/installer artifacts where tooling permits, build/run instructions, and a verified-versus-unverified feature list.

Do not label the product production-ready merely because it compiles or automated tests pass. Release readiness requires the blueprint's desktop, compatibility, recovery, performance, installation, and signing gates. If a gate remains open, deliver the implementation and state precisely what still prevents release.

Begin now with repository/toolchain preflight and M0, then continue implementation.
```

## Continuation prompt

Use this only if a later session needs to resume unfinished work:

```text
Continue SnapDrop v0.1 implementation. Read applicable repository instructions, Context.md, Implementation.md, and docs/IMPLEMENTATION_AGENT_PROMPT.md. Inspect current source and verification evidence. Resume the first incomplete milestone, preserve working functionality and user changes, implement and verify the next dependent work, and update Context.md. Continue toward the existing release gates; do not produce another standalone plan or assume prior unverified checks passed.
```

## Why this prompt is structured this way

The existing blueprint holds detailed product decisions; this prompt supplies execution and evidence requirements without redefining the product. Milestone validation and a continuously updated status document also appear in [OpenAI's guidance on long-running Codex tasks](https://developers.openai.com/blog/run-long-horizon-tasks-with-codex). They support continuity and reviewability; they are not a guarantee of release quality.
