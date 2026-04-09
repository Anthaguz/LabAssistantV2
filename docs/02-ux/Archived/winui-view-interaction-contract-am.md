# WinUI View Interaction Contract (AM2)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the interaction boundary between WinUI views, capability-local workspace owners, and the shell so extraction work can reduce raw child-control exposure without forcing an artificial purity model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI view interaction patterns for migrated capabilities and shell-hosted workspaces.

**Out of scope:** Full shell composition boundary details (AM1), UI test strategy details (AM3), domain-semantic redesign, and capability-specific workflow redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-116`..`FR-118`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-023`)
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`

---

## 1) Core interaction rule

Bindings and commands are the default interaction model.

This means:
- capability-local state should be presented through bindings where practical
- routine actions should prefer command-oriented or capability-local interaction seams
- the shell should not remain the normal mechanism for mutating child controls directly

This contract is deliberately pragmatic, not framework-dogmatic.

---

## 2) What is allowed

### 2.1 Preferred
- bindings for displayed state
- commands or capability-local action handlers for routine actions
- narrow capability-local workspace owners driving UI state

### 2.2 Still allowed
- small explicit code-behind event surfaces for genuinely local view concerns
- narrow view-to-workspace callbacks when they are simpler and clearer than forced command abstractions

Examples of acceptable small view-local interactions:
- row remove requested
- tab selection changed
- local size-changed layout adaptation
- editor-local UI events that remain within the capability seam

---

## 3) What should be reduced

The following pattern should be reduced over AM:
- views exposing large numbers of raw controls back to `MainWindow`
- shell-owned direct mutation of routine child control state
- capability workflows depending on typed view-control bags as the primary interaction seam

This is the key anti-pattern the contract is addressing.

---

## 4) What this contract does not require

AM2 does **not** require:
- pure MVVM everywhere
- zero code-behind
- one mandatory naming convention like `ViewModel` only
- replacing every event with a command whether or not that improves clarity

The goal is not architectural theater.  
The goal is reduced shell/view coupling and clearer capability-local ownership.

---

## 5) Interaction boundary by layer

### 5.1 Shell (`MainWindow`)
- owns shell composition
- routes between capability hosts
- does not remain the routine updater of child view controls

### 5.2 Capability workspace owner
- owns capability-local state and orchestration
- drives the view through bindings, commands, and narrow view seams

### 5.3 View
- renders capability-local state
- raises only narrow, intentional local interactions when needed
- does not act as a broad exported control inventory for the shell

---

## 6) Preservation rules

AM2 interaction refactors must preserve:
- AL shell/view behavior
- AM1 shell composition boundary
- current capability semantics and routes
- Templates Library-first exception behavior
- Deploy workflow-local issue/progress ownership behavior already approved

AM2 is about interaction ownership, not changing product behavior.

---

## 7) Practical extraction guidance

### 7.1 Good direction
- extract capability-local state first
- then reduce direct control exposure
- let tests evolve in the same issue stack

### 7.2 Bad direction
- “clean MainWindow” with no seam
- force a total framework rewrite
- swap one giant orchestrator for another equally broad object with a different name

---

## 8) Traceability

- `FR-116`
  - bindings/commands-first default
- `FR-117`
  - typed control-bag pattern reduced
- `FR-118`
  - pragmatic interaction extraction preserving behavior

Mapped acceptance criteria:
- `AC-023`

---

## 9) Open Questions / TBDs

- `TBD:` Exact acceptable balance between commands and narrow view-local events per capability once the first extraction slices (`Machines`, then `Assets`) establish the working pattern.
- `TBD:` Whether some current size/layout adaptation code should remain in view code-behind permanently or move behind a thinner layout adapter seam later.
- `TBD:` Which remaining direct child-control proxies, if any, are acceptable as temporary bridges during phased extraction.

