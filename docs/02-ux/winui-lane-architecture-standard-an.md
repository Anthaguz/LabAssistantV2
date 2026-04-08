# WinUI Lane Architecture Standard (AN1)

**Purpose:** Define the canonical lane-local architecture pattern for non-trivial WinUI lanes so later extraction and cleanup work uses an explicit owner/controller/composition/shell-bridge split instead of inventing local variants.

**Status:** Approved implementation contract for Milestone AN.

**Scope:** Lane-local ownership rules for non-trivial WinUI child surfaces that already live under an approved capability composition boundary.

**Out of scope:** Typed capability bootstrap/runtime design, shared capability composition boundaries, runtime implementation, product behavior redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-176`..`FR-178`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-043`)
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/winui-view-interaction-contract-am.md`
- `docs/02-ux/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/winui-from-template-extraction-cleanup-target-am.md`
- `docs/02-ux/winui-quick-deploy-extraction-cleanup-target-am.md`

---

## 1) Why this standard exists

AM1 and AM33 already established the shell boundary and the capability-level composition target.

Post-AM runtime cleanup exposed the next remaining ambiguity:
- lane-local extraction can still drift even after the shell-vs-capability boundary is clear
- one lane may use a real local owner while another keeps composition, host, and shell interaction tangled together
- if that ambiguity stays undocumented, the repo will keep accumulating one-off "almost the same" lane patterns

This standard exists to stop that drift before more runtime slices land.

---

## 2) What this standard applies to

This standard applies to a **non-trivial lane** inside an already-approved capability boundary.

A lane is non-trivial when it owns enough local behavior that leaving the role split implicit would hide ownership or lifecycle decisions.

Typical signs that a lane is non-trivial:
- it has lane-specific workflow orchestration or async sequencing
- it has route-activation refresh or reconcile rules beyond simple visibility
- it coordinates lane-local helper usage or reference-data refresh rules
- it owns more than one bound surface, such as a main view plus a lane-local side panel
- it still needs narrow shell-owned interactions such as dialogs, dispatcher marshalling, or results-panel toggles

When those conditions are present, the lane should not be implemented as a loose mix of host callbacks, shared-capability logic, and direct shell wiring.

---

## 3) Required role split for non-trivial lanes

The canonical role split for a non-trivial lane is:
- `WorkspaceOwner`
- `WorkspaceController`
- `WorkspaceComposition`
- `WorkspaceShellBridge`

The exact concrete type names may vary slightly, but the ownership boundaries must remain explicit.

### 3.1 `WorkspaceOwner`

The lane owner is the top-level boundary for the lane.

It may own:
- lane-local lifetime and top-level wiring
- controller, composition, and shell-bridge lifetime
- lane-specific route-activation entry points
- lane-local refresh or reconcile sequencing
- lane-specific shared-helper coordination
- lane-local panel intent or other lane-specific UI meaning that the shell infrastructure consumes indirectly

It must not own:
- shell route switching
- shell container mechanics or shell panel sizing/lifecycle infrastructure
- shared capability composition across sibling lanes
- direct `MainWindow` dependency
- the detailed step-by-step workflow sequencing that belongs in the controller
- the detailed view-state application rules that belong in composition

The owner is the lane boundary, not a new mixed-responsibility landfill.

### 3.2 `WorkspaceController`

The controller owns lane-local workflow orchestration.

It may own:
- async workflow sequencing
- readiness/evaluation/start or similar lane-local action flow
- lane-local validation or state-transition coordination
- integration with business/services abstractions through workspace state and narrow host callbacks

It must not own:
- direct view references
- shell container or shell route ownership
- direct `MainWindow` dependency
- shared capability composition responsibilities
- lane view-state application or control mutation

If the controller needs shell-owned or view-owned cooperation, it should depend on a narrow host contract rather than taking direct shell or view dependencies.

### 3.3 `WorkspaceComposition`

Composition owns the lane-local view pair and view-state application.

It may own:
- the bound lane view or lane-local view pair
- items-source binding and lane-local visibility application
- translation from workspace state into view state
- interaction-state capture from the bound view
- lane-local UI refresh routines that stay within the view-application boundary

It must not own:
- lane workflow orchestration
- shared capability composition
- shell container mechanics
- direct `MainWindow` dependency
- lane-local helper coordination or route-activation policy as the normal control path

Composition exists to apply lane state to the view, not to become the hidden workflow owner.

### 3.4 `WorkspaceShellBridge`

The shell bridge exposes only the shell-owned interactions that the lane still needs.

It may own:
- route-active or shell-active state queries when they are genuinely shell-owned
- dispatcher marshalling
- narrow dialog launchers
- explicit shell-owned panel toggle/refresh hooks
- access to shell-owned primitives such as `XamlRoot`

It must not own:
- lane workflow state
- lane helper coordination
- shared capability semantics
- broad raw access to `MainWindow`
- open-ended "run arbitrary shell code for the lane" behavior

Shell bridges should name the exact shell interaction they expose.
They are narrow adapters, not general-purpose shell escape hatches.

---

## 4) Banned patterns

The following patterns are explicitly rejected.

### 4.1 Backpack hosts

A backpack host is a host interface or helper object that quietly accumulates unrelated responsibilities because it is already on the wiring path.

Examples of backpack-host drift:
- mixing shell dialog access, view mutation, route activation, helper lookup, and workflow sequencing into one host contract
- using one host interface as the dumping ground for every lane method that does not fit elsewhere

This is banned because it hides ownership instead of defining it.

### 4.2 Attach cycles

An attach cycle is a composition pattern where shell or shared composition creates a lane object, then later re-attaches or re-registers more lane responsibilities through another host or attach step just to finish the wiring.

Examples:
- lane host plus `AttachComposition()`
- composition-host patterns where the final owner is only implied after multiple attachment steps

This is banned because it makes lifecycle and ownership ambiguous.

Lane-local wiring should converge in one clear lane boundary rather than a circular attach sequence.

### 4.3 Lane-specific shell lambdas in `MainWindow`

Ad hoc lane-specific lambdas passed directly from `MainWindow` into lane code are banned as the lane integration pattern.

This includes:
- lane-specific `Func<>` or `Action` wiring scattered through `MainWindow`
- lane logic expressed inline in shell wiring rather than behind a named bridge abstraction

What is still allowed:
- a dedicated shell bridge implementation may internally wrap narrow shell-owned actions with `Func<>` or `Action` delegates

The banned pattern is not "delegates exist."
The banned pattern is leaving lane integration as unnamed shell lambda wiring instead of a dedicated lane bridge.

### 4.4 Lane-specific logic inside shared capability composition

Shared capability composition may own only genuinely shared capability-level concerns.

It must not become the home for:
- lane-local workflow sequencing
- lane-specific helper coordination
- lane-specific panel meaning
- lane-specific refresh policy
- lane-local view mutation routines

If the logic is specific to one child lane, it does not belong in shared capability composition.

---

## 5) Simple-lane escape hatch

Not every lane needs the full four-part split.

A slimmer form is allowed only when the lane is genuinely simple.

A lane is simple enough to avoid the full split only when **all** of the following are true:
- it does not need independent lane-local workflow orchestration beyond narrow state application or trivial actions
- it does not coordinate lane-local shared helpers or reference-data refresh rules
- it does not own a dedicated auxiliary surface such as a lane-local results panel or second bound view
- it does not need a lane-specific shell bridge beyond shell-active visibility already handled elsewhere
- its route-activation behavior is narrow enough that a dedicated lane owner would add indirection without clarifying ownership

When all of those conditions are true, a slimmer form may:
- omit a dedicated `WorkspaceOwner`
- let `WorkspaceComposition` remain the lane root
- omit a dedicated `WorkspaceShellBridge` when the lane has no genuine shell-owned interaction seam

Even in the slimmer form:
- `MainWindow` still remains shell-only
- shared capability composition still stays capability-shared only
- the banned patterns in this document still apply

The simple-lane exception is for genuinely narrow lanes, not for avoiding the work of defining ownership.

---

## 6) How this standard relates to existing AM contracts

This standard does not replace:
- the shell boundary from AM1
- the capability composition boundary from AM33
- any already-approved capability-specific cleanup target

Instead, it adds the missing lane-local rule underneath those contracts.

That means:
- capability-level composition rules still decide what stays shared across sibling lanes
- this standard decides how a non-trivial child lane should be structured once lane-local ownership is required
- typed capability bootstrap/runtime remains a later shell-level contract rather than part of this issue

---

## 7) Sequencing implication

After this standard lands:
1. later lane-specific contract slices should either apply the full non-trivial-lane split or justify the simple-lane exception explicitly
2. later runtime refactors should not invent new host or attach patterns outside this contract
3. if a lane needs additional ownership rules not covered here, that rule should be documented before runtime implementation continues

This keeps later runtime issues narrow and reviewable.

---

## 8) Traceability

- `FR-176`
  - non-trivial lanes use an explicit owner/controller/composition/shell-bridge split with role-specific ownership rules
- `FR-177`
  - backpack hosts, attach cycles, lane-specific shell lambdas in `MainWindow`, and lane-specific logic inside shared capability composition are explicitly rejected
- `FR-178`
  - a simple-lane exception exists, but only behind explicit fit criteria and without weakening the shell/capability boundary rules

Mapped acceptance criteria:
- `AC-043`

---

## 9) Open Questions / TBDs

- `TBD:` Which currently extracted lanes should later be treated as approved simple-lane variants versus backfilled toward the full non-trivial split.
- `TBD:` Whether any future lane proves to need a fifth recurring seam beyond this four-role standard, or whether later needs should remain lane-specific follow-up contracts.
