# UI Framework Shell Spike (Milestone Y / #252)

Purpose: isolated, disposable prototype artifacts for evaluating shell/navigation ergonomics for the UI framework decision (`WPF modernized` vs `WinUI 3`).

This folder is **not** part of the production app and is **not** included in `LabAssistant.sln`.

## Scope (intentional)

- Shell foundation only
- Capability Scope vs Context Scope switching
- Left-panel behavior concept
- Shell-level error feed placement concept
- Dense content placeholder region for layout/density evaluation
- ViewModel-driven interactions with stub data

## Out of Scope (intentional)

- Full Deploy migration
- Production styling/theme system
- Machines capability implementation
- Feature parity migration
- Release packaging work

## Structure

- `shared-shell-fixture.md` - common interaction fixture both prototypes implement
- `wpf-shell-spike/` - WPF modernization shell spike
- `winui3-shell-spike/` - WinUI 3 shell spike

## Validation note

Build/run commands are intentionally separate from the main solution. See `docs/02-ux/ui-framework-spike-findings-y2.md` for what was run and environment limitations.
