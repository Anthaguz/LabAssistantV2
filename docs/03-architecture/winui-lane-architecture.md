# WinUI Lane Architecture

**Purpose:** Define the current authoritative lane-local architecture and interaction rules for WinUI so non-trivial lanes stop inventing new local patterns.

**Status:** Current authoritative WinUI architecture rule.

**Absorbs:**
- `docs/02-ux/Archived/winui-view-interaction-contract-am.md`
- `docs/02-ux/Archived/winui-lane-architecture-standard-an.md`

**Source basis:**
- `docs/01-requirements/srs.md` (`FR-116`..`FR-118`, `FR-176`..`FR-178`)
- `docs/01-requirements/srs.md` (`FR-201`..`FR-204`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-023`, `AC-043`, `AC-050`)
- `docs/03-architecture/winui-shell-bootstrap-runtime.md`

## 1) Core Rule

Bindings and commands are the default interaction model.

When a lane owns meaningful local workflow, refresh/reconcile behavior, helper coordination, auxiliary view composition, or genuine shell interaction seams, the lane must use an explicit local role split instead of leaving ownership implied by host wiring or shared composition.

The canonical non-trivial lane split is:
- `WorkspaceOwner`
- `WorkspaceController`
- `WorkspaceComposition`
- `WorkspaceShellBridge`

The exact type names may vary slightly, but the ownership boundaries must remain explicit.

## 2) Role Boundaries

### `WorkspaceOwner`
May own:
- lane-local lifetime and top-level wiring
- controller, composition, and shell-bridge lifetime
- lane route-activation entry points
- lane refresh or reconcile sequencing
- lane-local helper coordination
- lane-local panel intent or other lane-local UI meaning consumed indirectly by shell infrastructure

Must not own:
- shell route switching
- shell container mechanics
- shared capability composition across sibling lanes
- direct `MainWindow` dependency
- detailed workflow sequencing that belongs in the controller
- detailed view-state application that belongs in composition

### `WorkspaceController`
May own:
- lane-local workflow orchestration
- async sequencing and action flow
- lane-local validation and state transitions
- cooperation through narrow host callbacks

Must not own:
- direct view references
- shell container or shell route ownership
- direct `MainWindow` dependency
- shared capability composition
- routine view mutation

### `WorkspaceComposition`
May own:
- the lane view or lane-local view pair
- items-source binding and lane-local visibility application
- translation from workspace state into view state
- interaction-state capture from the bound view
- lane-local UI refresh routines that remain inside the view-application boundary

Must not own:
- lane workflow orchestration
- helper coordination
- shared capability composition
- shell container mechanics
- direct `MainWindow` dependency

### `WorkspaceShellBridge`
May own:
- narrow shell-owned interaction seams such as dispatcher marshalling, dialogs, route-active queries, or explicit panel toggles
- access to shell-owned primitives such as `XamlRoot`

Must not own:
- lane workflow state
- lane helper coordination
- shared capability semantics
- broad raw access to `MainWindow`
- open-ended shell escape hatches

## 3) View Interaction Boundary

Views should render state and expose narrow, intentional interactions.

Preferred direction:
- bindings for displayed state
- commands or capability-local handlers for routine actions
- narrow lane or capability owners driving UI state

Still allowed when they stay small and local:
- explicit code-behind event surfaces
- narrow view-to-owner or view-to-composition callbacks

Rejected as the long-term pattern:
- views exposing broad raw control inventories back to shell or shared composition
- shell-owned routine mutation of child controls
- typed control-bag seams as the normal workflow boundary

## 4) Banned Patterns

The following are explicitly rejected:
- backpack hosts that accumulate unrelated responsibilities by convenience
- attach cycles that imply the final owner only after multiple attachment steps
- lane-specific shell lambdas in `MainWindow` as the integration pattern
- lane-specific logic inside shared capability composition
- broad raw child-control exposure back to shell or shared composition

A narrow named shell bridge may internally wrap delegates.
The banned pattern is ad hoc lane integration, not delegates themselves.

## 5) Simple-Lane Exception

A slimmer form is allowed only when the lane is genuinely simple.

All of the following must be true:
- it has no independent lane-local workflow orchestration
- it has no lane-local helper coordination
- it has no dedicated auxiliary surface such as a lane-local side panel or second bound view
- it has no genuine shell-owned interaction seam beyond already-approved visibility handling
- a dedicated owner would add indirection without clarifying ownership

When that is true, a lane may:
- omit a dedicated `WorkspaceOwner`
- let `WorkspaceComposition` remain the lane root
- omit a dedicated `WorkspaceShellBridge` when no real shell seam exists

Even then:
- `MainWindow` remains shell-only
- shared capability composition remains capability-shared only
- the banned patterns still apply

## 6) Non-Goals

This doc does not require:
- full MVVM purity
- zero code-behind
- one naming convention for every lane
- per-navigation lane recreation
- product behavior redesign

## 7) Templates V2 Builder Application

`templates.builder` is a non-trivial Templates workflow-state destination, now migrated onto the frame-based capability-page pattern like the rest of Templates.

Builder-specific draft state, validation flow, deterministic-suggestion confirmation, save orchestration, and V2 topology authoring sequence belong to the Builder-local `TemplatesBuilderViewModel` (transient, `x:Bind` MVVM, dispatcher-free), with all draft/topology/section/validation shaping kept in the pure `Builder/*` helpers. Builder-specific view composition and UI coordination live in the declarative `TemplatesBuilderView` bound to that view model.

The transient `TemplatesPage` hosts the Builder subview and reaches reference data, library reload, the Save As file picker, and cross-subview navigation through the injected `ITemplatesBuilderHost` seam. `MainWindow` remains shell-only, and the current Templates Editor remains the V1/simple/legacy editing destination rather than becoming the Builder workflow owner.

## 8) Update Rule

If the durable lane-local rule changes, update this file in place.
Do not create a new milestone-coded lane architecture contract for a rule that should survive beyond that slice.

## Open Questions / TBDs

- `TBD:` Which currently extracted lanes should remain approved simple-lane variants versus being backfilled toward the full split.
- `TBD:` Whether any future lane proves to need a recurring fifth seam beyond the current four-role pattern.
