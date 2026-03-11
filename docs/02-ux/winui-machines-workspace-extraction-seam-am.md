# WinUI Machines Workspace Extraction Seam (AM4)

**Purpose:** Define the first capability-specific extraction seam for AM so `Machines` can move out of `MainWindow` ownership without changing its approved behavior.

**Status:** Approved implementation contract for Milestone AM.

**Scope:** WinUI `Machines` extraction seam only.

**Out of scope:** Runtime extraction implementation, Machines workflow redesign, broader Assets/Deploy/Templates/Diagnostics extraction, and performance tuning.

**Related:**
- `docs/01-requirements/srs.md` (`FR-122`..`FR-124`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-025`)
- `docs/01-requirements/machines-capability-contract.md`
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/winui-view-interaction-contract-am.md`
- `docs/02-ux/winui-ui-test-convergence-contract-am.md`
- `docs/02-ux/winui-shell-view-consistency-contract-al.md`

---

## 1) Why Machines goes first

`Machines` is the first extraction target because it is:
- already a real migrated surface
- single-surface rather than Overview-first
- master/detail oriented
- behaviorally rich enough to prove the AM pattern
- less risky than starting with `Deploy`

The goal is to establish the first stable shell-to-workspace seam before larger capability extraction begins.

---

## 2) What should move behind the Machines seam

Machines-local ownership should include:
- machine inventory collection
- selected VM state
- edit draft state
- dirty-state tracking
- RDP readiness state and refresh state
- action enablement logic
- Machines-specific status text and feedback state
- Machines-specific action orchestration

These should stop being long-term shell-owned state in `MainWindow`.

---

## 3) What remains shell-owned

Shell still owns:
- route and capability resolution
- shell header/title/description
- shell compact navigation mode
- right-panel infrastructure (Machines does not depend on it by default)
- shell host visibility

The Machines seam does not move shell composition responsibilities away from `MainWindow`.

---

## 4) Behavior that must not change

Machines extraction must preserve:
- `machines.overview` route continuity
- single-surface master/detail capability shape
- list-first compact behavior already approved in AL
- draft-based edit/apply workflow
- separate Console and RDP actions
- explicit RDP disabled-state reasoning
- delete policy and delete safety behavior
- current structured logging expectations for Machines actions

AM4 is not a Machines redesign issue.

---

## 5) Async/non-blocking rules

The Machines seam must preserve:
- asynchronous inventory loading
- asynchronous/non-blocking RDP readiness evaluation
- non-fatal readiness refresh behavior
- capability responsiveness while refresh/readiness work is happening

Extraction must not preserve responsiveness by leaving `MainWindow` as the persistent owner of Machines-specific state.

---

## 6) Interaction boundary implication

AM4 inherits the AM2 interaction rule:
- bindings/commands first
- limited narrow view-local events allowed where justified

For Machines, this means:
- shell should stop being the broad updater of Machines child controls
- later AM slices may introduce a Machines workspace owner with a pragmatic pattern
- the exact type name (`controller`, `presenter`, `viewmodel`, etc.) is not mandated here

---

## 7) Sequencing from this seam

This seam exists to support:
1. `AM5` - extract Machines workspace state from `MainWindow`
2. `AM6` - extract Machines actions and orchestration
3. `AM7` - reduce Machines view control exposure
4. `AM8` - converge Machines UI tests to the new seam

If AM4 is not explicit, later slices will guess.  
That is what this issue is preventing.

---

## 8) Non-goals

AM4 does **not** introduce:
- new Machines semantics
- new RDP readiness semantics
- new delete semantics
- new section-navigation redesign
- right-panel usage for Machines
- performance tuning as a separate concern

---

## 9) Traceability

- `FR-122`
  - Machines-local state/orchestration ownership boundary
- `FR-123`
  - async/non-blocking readiness and inventory preservation during extraction
- `FR-124`
  - preserved Machines user-visible behavior with narrow seam definition

Mapped acceptance criteria:
- `AC-025`

---

## 10) Open Questions / TBDs

- `TBD:` Exact Machines workspace owner type once implementation begins (`controller`, `presenter`, `viewmodel`, or mixed pattern).
- `TBD:` Whether any small shell-hosted dialog helpers remain acceptable during AM5/AM6 as temporary bridges, provided Machines workflow ownership does not drift back into `MainWindow`.
- `TBD:` Whether Machines readiness refresh should be split into its own narrower seam later if extraction shows that RDP readiness logic is disproportionately coupled to current shell state.
