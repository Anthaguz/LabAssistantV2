# WinUI Machines Composition Cleanup Target (AM34)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the post-AM33 cleanup target for `Machines` before the same extraction pattern is repeated across other capabilities.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Machines` composition cleanup target only.

**Out of scope:** Runtime implementation in this issue, broader capability rollout, Machines workflow redesign, per-navigation workspace recreation, and performance tuning.

**Related:**
- `docs/01-requirements/srs.md` (`FR-128`..`FR-130`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-027`)
- `docs/02-ux/Archived/winui-machines-workspace-extraction-seam-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`

---

## 1) Why Machines needs a follow-up target

AM5 through AM7 improved `Machines` substantially:
- state moved behind `MachinesWorkspaceViewModel`
- orchestration moved behind `MachinesWorkspaceController`
- broad raw view-control exposure was reduced

But the AM33 review showed a remaining problem:
- capability-local UI composition still terminates in `MainWindow`
- `MainWindow` still acts as the practical Machines host hub through capability-specific bridge responsibilities

This target exists so `Machines` becomes the corrected pattern before the same approach is repeated elsewhere.

---

## 2) Desired end-state for Machines

The target is:
- `MainWindow` hosts the `Machines` workspace lifetime and route visibility
- a Machines-local composition owner becomes the long-term home for Machines-specific UI composition
- `MachinesWorkspaceViewModel` remains the state owner
- `MachinesWorkspaceController` remains the workflow/orchestration owner
- the Machines-local composition owner becomes the place that coordinates view/controller/state interaction

The exact type name is not mandated here.  
Examples:
- workspace
- workspace host
- presenter
- coordinator

What matters is that the shell is no longer the intended final hub for Machines-local UI composition.

---

## 3) What MainWindow should stop doing for Machines

The following are now explicitly considered transitional if they still live in `MainWindow`:
- Machines-specific controller host implementation
- Machines-specific view refresh coordination
- Machines-specific selection synchronization
- Machines-specific edit-control apply/clear/dirty coordination
- Machines-specific action-state refresh coordination

Those responsibilities should reduce over the next Machines follow-up slices.

---

## 4) What MainWindow still owns

`MainWindow` still correctly owns:
- shell route switching
- shell title/description
- shell navigation and drawer behavior
- shell right-panel infrastructure
- shell host visibility
- app-level workspace lifetime

AM34 does not change the shell boundary.  
It tightens the Machines-local side of that boundary.

---

## 5) What must remain unchanged

The cleanup target must preserve:
- `machines.overview` route continuity
- single-surface master/detail layout
- list-first compact behavior
- draft/apply workflow
- separate Console and RDP actions
- delete safety behavior
- current RDP disabled-state reasoning
- async/non-blocking inventory and readiness behavior

This remains a composition-ownership cleanup, not a user-visible Machines redesign.

---

## 6) Workspace lifetime rule for Machines

Machines remains long-lived during the app session.

That means:
- the Machines workspace is not recreated on every navigation
- route activation refreshes or reconciles state when needed
- any future recreation-per-navigation model remains a `TBD` and would require explicit re-contracting

This preserves the current AM33 lifetime direction.

---

## 7) Sequencing implication

The next Machines follow-up slices should do this in order:
1. introduce the Machines-local composition owner
2. move the current shell-side host bridge behind it
3. remove the remaining Machines-specific UI coordination from `MainWindow`
4. converge the Machines AM tests to the refined target

This sequence is deliberately narrower than broad capability rollout.

---

## 8) Non-goals

AM34 does **not** require:
- replacing `MachinesWorkspaceViewModel`
- replacing `MachinesWorkspaceController`
- recreating Machines per navigation
- changing Machines semantics
- changing Machines layout
- changing RDP or delete policy behavior

---

## 9) Traceability

- `FR-128`
  - Machines-local composition owner as the target
- `FR-129`
  - shell-side Machines bridge reduction target
- `FR-130`
  - preserved long-lived workspace and current Machines behavior during cleanup

Mapped acceptance criteria:
- `AC-027`

---

## 10) Open Questions / TBDs

- `TBD:` Exact Machines-local composition owner type name during implementation.
- `TBD:` Whether any delete-dialog or shell-owned helper remains temporarily shell-hosted after the host bridge is reduced.
- `TBD:` Whether later capability rollouts should adopt the same composition-owner pattern exactly or with capability-specific variations.

