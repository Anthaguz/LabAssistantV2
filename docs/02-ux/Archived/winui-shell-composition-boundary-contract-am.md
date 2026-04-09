# WinUI Shell Composition Boundary Contract (AM1)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the shell composition boundary for `LabAssistant.WinUI` so `MainWindow` keeps shell ownership while capability-local state and workflow orchestration can be extracted into narrower workspace seams.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI shell composition and the boundary between `MainWindow` and migrated capability workspaces.

**Out of scope:** View interaction contract details, UI test strategy details, domain-semantic redesign, capability workflow redesign, and performance tuning beyond ownership boundaries.

**Related:**
- `docs/01-requirements/srs.md` (`FR-113`..`FR-115`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-022`)
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-shell-contract-aa.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Core boundary rule

`MainWindow` is the shell composition root, not the permanent owner of capability-local workflow state.

That means:
- `MainWindow` keeps shell responsibilities
- migrated capabilities move local state/orchestration behind capability-scoped workspace seams
- extraction must preserve approved AL shell/view behavior while reducing shell entanglement

---

## 2) What MainWindow owns

`MainWindow` remains responsible for:
- shell chrome
- global route resolution
- shell navigation configuration and item invocation
- shell header state (capability title/description)
- shell theme state
- shell compact drawer mode behavior
- right-panel container, lifecycle, owner reset, and compact fallback
  - shell infrastructure only; capability or lane semantics for the panel remain outside the shell boundary
- capability host visibility at the shell level

`MainWindow` may temporarily bridge to capability workspaces while extraction is in progress, but AM work should reduce that bridge over time.

AM33 further refines this rule:
- shell still owns workspace lifetime
- but capability-local UI composition should converge behind capability workspace objects rather than permanently expanding shell-owned host interfaces

---

## 3) What MainWindow should stop owning over time

`MainWindow` should not remain the long-term owner of:
- capability inventory collections
- capability selection state
- capability edit drafts
- capability-local validation/readiness state
- capability-specific action enablement logic
- capability-specific async workflow orchestration
- capability-specific control mutation as the normal state-update path

These belong behind capability-local workspace seams.

---

## 4) Capability workspace seam rule

Each migrated capability may introduce a capability-scoped workspace owner such as:
- controller
- presenter
- viewmodel

The exact type name is not mandated by this contract.

What is required:
- capability-local state no longer depends on broad shell-owned fields
- capability-local workflow orchestration no longer depends on shell-level direct control mutation
- shell-to-capability integration stays narrow and explicit

This contract deliberately prioritizes boundary clarity over framework ideology.

---

## 5) Preservation rules

AM extraction must preserve:
- AL shell header ownership rules
- AL Overview-first routing for `Assets`, `Deploy`, and `Diagnostics`
- AL Templates exception model (`Library` primary, `Editor` workflow-state)
- AL Deploy right-panel ownership model
- AL compact/drawer and bounded-scroll behavior
- existing canonical route keys
- existing capability-level workflow semantics unless separately re-contracted

AM is an extraction milestone, not a silent product redesign milestone.

---

## 6) Sequencing implications

### 6.1 Shell-first, capability-second
- define shell composition boundary first
- then define capability-specific extraction seam
- then extract state
- then extract interactions/orchestration
- then reduce raw view control exposure
- then converge tests

### 6.2 Capability order
Recommended order:
1. `Machines`
2. `Assets`
3. `Templates`
4. `Deploy`
5. `Diagnostics`

Deploy remains the capability most likely to require additional issue splitting before implementation if extraction slices prove too large.

---

## 7) Non-goals

AM1 does not require:
- full MVVM purity
- immediate elimination of all code-behind
- renaming everything to specific architectural jargon
- product-semantic changes to capabilities
- performance optimization by itself

The goal is a narrower, more stable shell/workspace ownership boundary.

---

## 8) Traceability

- `FR-113`
  - `MainWindow` owns shell composition only
- `FR-114`
  - capability-local workspace seams enable extraction
- `FR-115`
  - extraction preserves approved shell/view and capability contracts

Mapped acceptance criteria:
- `AC-022`

---

## 9) Open Questions / TBDs

- `TBD:` Exact capability-local seam form for each area (`controller`, `presenter`, `viewmodel`, or mixed pattern) once AM2 defines the view interaction contract.
- `TBD:` Whether some small capability-specific shell bridge helpers remain acceptable after extraction, provided they do not re-centralize capability state in `MainWindow`.
- `TBD:` Whether Deploy extraction should be split further before implementation once Machines/Assets extraction establishes the first stable pattern.

