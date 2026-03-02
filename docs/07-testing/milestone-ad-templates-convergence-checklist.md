# Milestone AD Templates Convergence Checklist

**Purpose:** Manual closure checklist for Milestone AD Templates capability convergence (`#300`, `#301`, `#302`, `#307`, `#308`, `#303`).

**Scope:** WinUI Templates capability only (`templates.library`, `templates.editor`) plus contract-level shell compatibility checks.

---

## 1) Routing and Navigation

- [ ] Launch WinUI and open Templates from global navigation.
- [ ] Confirm Templates parent lands on `templates.library`.
- [ ] Navigate to `templates.editor` using local Templates navigation.
- [ ] Return to `templates.library` and confirm selected-template context remains coherent.
- [ ] Confirm no fallback to filesystem-first editing is required for normal Library -> Editor flow.

## 2) Library Workflow

- [ ] List templates loads and selection works.
- [ ] Open selected template in editor works.
- [ ] Create template entry flow is reachable and operational.
- [ ] Delete template requires confirmation and result is explicit.
- [ ] Import entry point is present and usable.
- [ ] Export entry point is present and usable.

## 3) Editor Workflow

- [ ] Template name/description edits are possible.
- [ ] Save and Save As entry points are present and operational.
- [ ] Validate action is present and returns actionable status.
- [ ] Error states are explicit (no silent failures).

## 4) VM Entry Parity Workflow (AC-012)

- [ ] VM list is visible in editor context.
- [ ] Add VM action works.
- [ ] Remove VM action requires confirmation.
- [ ] Existing-schema VM fields can be edited and applied.
- [ ] Save/reload round-trip keeps VM add/remove/edit changes.
- [ ] Library <-> Editor switching preserves selected-template continuity after VM edits.

## 5) Layout and Overflow Sanity

- [ ] Compact width: controls remain reachable and usable.
- [ ] Normal width: no clipping of primary actions.
- [ ] Wide width: surfaces expand correctly without unused dead zones that hide workflow context.
- [ ] No infinite vertical growth regressions in Library or Editor.
- [ ] Scroll ownership is stable for dense editor content (no parent layout breakage from long content).

## 6) Recording Template

- **Build/commit tested:** `<commit>`
- **Environment:** `<OS version / WinAppSDK / .NET SDK>`
- **Result by section:**
  - Routing and Navigation: `Pass | Fail`
  - Library Workflow: `Pass | Fail`
  - Editor Workflow: `Pass | Fail`
  - VM Entry Parity Workflow: `Pass | Fail`
  - Layout and Overflow Sanity: `Pass | Fail`
- **Findings / limitations:**
  - `<item>`

---

## Open Questions / TBDs

- `TBD`: whether AD closure should include a required baseline dataset for repeatable import/export validation across environments.
