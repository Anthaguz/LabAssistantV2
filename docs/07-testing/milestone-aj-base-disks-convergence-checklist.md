# Milestone AJ Base Disks Convergence Checklist

**Purpose:** Manual closure checklist for Milestone AJ Assets > Base Disks WinUI convergence (`#355`, `#356`, `#357`, `#358`, `#359`).

**Scope:** WinUI `assets.base_disks` route, operational Base Disks behavior, AJ4 UX/data-binding hardening, and contract-level safety/messaging validation.

---

## 1) Route and Surface Sanity

- [ ] Launch WinUI and navigate to Assets capability.
- [ ] Confirm the canonical Base Disks surface is active at `assets.base_disks`.
- [ ] Confirm the shell hosts the Base Disks view without route fallback or placeholder-only content.
- [ ] Confirm list, details, actions, status, loading, empty, and error regions are all present.

## 2) Loading, Empty, and Error State Behavior

- [ ] Confirm initial load shows usable status messaging without collapsing the page.
- [ ] Confirm empty catalog state explains how to import/register a VHDX.
- [ ] Confirm partial load failures surface actionable error-panel messaging while keeping usable rows visible when available.
- [ ] Confirm error-state visibility is driven by actual error state, not placeholder text drift.

## 3) Refresh and Selection Continuity

- [ ] Select an existing base disk and trigger Refresh.
- [ ] Confirm the same selected disk remains selected when it still exists after refresh.
- [ ] Confirm details stay readable while refresh is running (no unnecessary flicker/reset).
- [ ] Confirm if the selected item disappears after refresh, the surface reconciles to a valid next state instead of leaving stale details bound.

## 4) Import / Register Workflow

- [ ] Use `Import / Register` to choose a `.vhdx`.
- [ ] Confirm the editor enters new-draft mode with path and starter metadata populated.
- [ ] Trigger Refresh while the new draft is still unsaved.
- [ ] Confirm the pending draft remains visible and is not replaced by a random existing row selection.
- [ ] Save the draft and confirm the new disk becomes a registered catalog item.

## 5) Metadata Edit / Save Workflow

- [ ] Select a registered base disk and update one or more editable metadata fields.
- [ ] Confirm the status area makes next actions explicit (`Validate` / `Save Metadata`).
- [ ] Save metadata and confirm selection remains coherent on the updated item.
- [ ] Confirm save failure messaging is actionable if invalid metadata/path is provided.

## 6) Validation and Readiness Presentation

- [ ] Validate a known-good base disk and confirm the result is shown as `Ready`.
- [ ] Validate an invalid or incomplete draft and confirm the result is shown as `Blocking`.
- [ ] Confirm validation details are readable and actionable, not just a raw single-line severity string.
- [ ] Confirm list/detail summaries update consistently after validation.

## 7) Remove Confirmation and Registry-Only Messaging

- [ ] Select a registered base disk and trigger Remove.
- [ ] Confirm the dialog explicitly states this removes the disk from the registry only.
- [ ] Confirm the underlying VHDX file is not described as being deleted.
- [ ] Confirm remove cancellation leaves selection/details intact.
- [ ] Confirm successful removal reconciles selection/details cleanly with no stale row state left on screen.

## 8) Known Template Reference Warning Behavior

- [ ] Use a base disk known to be referenced by at least one template.
- [ ] Trigger remove assessment / remove flow.
- [ ] Confirm template-reference warnings are shown explicitly.
- [ ] Confirm the UI still states that active runtime consumer detection is not implemented.
- [ ] Confirm the warning is presented as a limitation/guardrail, not as a claim of full in-use detection coverage.

## 9) Layout, Overflow, and Scroll Ownership Sanity

- [ ] Compact height: details panel remains usable through scroll rather than clipping content.
- [ ] Normal height: list/details/state regions remain readable and balanced.
- [ ] Wide width: no dead space or broken alignment regressions appear.
- [ ] Confirm scroll ownership remains stable and the page does not grow unbounded vertically.

## 10) Results Recording Template

- **Build/commit tested:** `<commit>`
- **Environment:** `<OS version / WinAppSDK / .NET SDK>`
- **Result by section:**
  - Route and Surface Sanity: `Pass | Fail`
  - Loading, Empty, and Error State Behavior: `Pass | Fail`
  - Refresh and Selection Continuity: `Pass | Fail`
  - Import / Register Workflow: `Pass | Fail`
  - Metadata Edit / Save Workflow: `Pass | Fail`
  - Validation and Readiness Presentation: `Pass | Fail`
  - Remove Confirmation and Registry-Only Messaging: `Pass | Fail`
  - Known Template Reference Warning Behavior: `Pass | Fail`
  - Layout, Overflow, and Scroll Ownership Sanity: `Pass | Fail`
- **Findings / limitations:**
  - `<item>`

---

## Open Questions / TBDs

- `TBD`: whether AJ closure should require a standard template fixture set for repeatable known-reference warning verification.
