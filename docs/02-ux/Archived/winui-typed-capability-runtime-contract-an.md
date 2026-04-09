# WinUI Typed Capability Bootstrap and Runtime Contract (AN2)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the typed capability bootstrap/runtime boundary so `MainWindow` can converge toward one typed runtime per capability instead of hand-wiring each lane seam, helper, and capability-local composition detail directly.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI shell-to-capability runtime boundary, typed capability runtime ownership, and the relationship between capability runtime, shared capability composition, and lane-local seams.

**Out of scope:** Runtime implementation, per-lane cleanup, shell behavior redesign, lane-local architecture details already covered by AN1, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-179`..`FR-181`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-044`)
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-lane-architecture-standard-an.md`
- `docs/02-ux/Archived/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-templates-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-diagnostics-composition-cleanup-target-am.md`

---

## 1) Why this contract exists

AM1 established the shell boundary.
AM33 established the capability composition boundary.
AN1 established the lane-local role split for non-trivial lanes.

The next remaining gap is the shell-to-capability runtime boundary.

Current post-AM code still leaves `MainWindow` doing too much capability construction detail:
- constructing individual lane seams directly
- wiring capability-local helpers directly
- holding multiple capability-specific fields instead of one typed runtime boundary per capability

That is better than the old shell-centric ownership model, but it is still not the intended scaling shape.

This contract exists to define the next step before more capability cleanup lands.

---

## 2) Core terms

### 2.1 Capability bootstrap

Capability bootstrap is the shell-owned construction step that creates a capability's typed runtime boundary.

Bootstrap may:
- resolve services from DI or other app-level sources
- bind fixed shell-owned hosts or view roots that the capability needs
- create shell bridges or other shell-owned adapters needed by the capability runtime
- instantiate the typed capability runtime

Bootstrap must not become the long-lived capability owner after construction.

What matters is the phase boundary:
- bootstrap constructs and wires
- runtime owns and runs

### 2.2 Typed capability runtime

A typed capability runtime is the long-lived capability-local runtime boundary owned by the shell at the app level.

Examples of the intended direction:
- `MachinesCapabilityRuntime`
- `AssetsCapabilityRuntime`
- `TemplatesCapabilityRuntime`
- `DeployCapabilityRuntime`
- `DiagnosticsCapabilityRuntime`

The exact type names are not mandated here.

What matters is that `MainWindow` interacts with each capability through one capability-typed runtime boundary rather than a loose set of lane owners, compositions, helpers, and shell callbacks.

---

## 3) What `MainWindow` still owns after the runtime boundary exists

`MainWindow` remains responsible for:
- shell route switching and route resolution
- shell navigation configuration and selection
- shell header/title/description
- shell compact/drawer behavior
- shell theme state
- shell-owned panel/container infrastructure
- shell-level host visibility
- app-level lifetime of capability runtimes
- capability bootstrap entry points

This contract does not reduce the shell into a passive window shell with no composition responsibilities.

The change is narrower:
- `MainWindow` should stop being the place where capability-local runtime details are hand-wired field by field
- `MainWindow` should instead create and hold one typed runtime per capability

---

## 4) What capability bootstrap owns

Capability bootstrap is shell-owned and construction-only.

It may own:
- instantiating the typed capability runtime
- supplying shell-owned view roots or fixed host elements
- supplying shell-owned bridges, delegates, or adapters through named abstractions
- wiring the runtime to app-level services needed at construction time

It must not own:
- ongoing route-activation handling as the final capability owner
- capability-local helper coordination after construction
- lane-local workflow ownership
- shared capability composition behavior after construction
- ad hoc lane-by-lane shell logic that should have moved behind the runtime boundary

Bootstrap may be implemented as:
- a dedicated bootstrap type
- a factory
- a builder
- or a narrow `MainWindow` construction block

What is required is the ownership boundary, not a specific factory pattern.

---

## 5) What typed capability runtime owns

The typed capability runtime is the long-lived owner for capability-local runtime composition after bootstrap completes.

It may own:
- shared capability composition lifetime
- capability-shared helper lifetime
- lane-local seam lifetime for lanes inside the capability
- capability-local route-activation handoff
- capability-level refresh or reconcile entry points
- capability-local shared UI-state refresh that spans more than one lane

It must not own:
- shell route resolution
- shell navigation configuration
- shell header or theme ownership
- shell container mechanics
- direct `MainWindow` dependency
- lane-local workflow details that belong in lane owners or lane controllers

The capability runtime is the capability boundary, not a replacement god object for everything below it.

---

## 6) Relationship to shared capability composition and lane-local seams

The intended hierarchy is:

`MainWindow`  
-> capability bootstrap  
-> typed capability runtime  
-> shared capability composition and capability-shared helpers where needed  
-> lane-local seams under the capability boundary

This means:
- the runtime may own the shared capability composition object
- the runtime may own capability-shared helpers used across sibling lanes
- the runtime may delegate lane-local concerns to lane owners, lane controllers, lane compositions, and lane shell bridges

This does **not** mean:
- shared capability composition becomes the capability runtime
- the capability runtime replaces the lane-owner pattern from AN1
- lane-local workflow ownership moves upward just because a typed runtime exists

Shared capability composition remains limited to genuinely shared capability-level concerns.
Lane-local seams remain responsible for lane-local workflow and lane-local view application.

---

## 7) Lifetime rule

Current default target:
- capability bootstrap may run at startup or first activation
- once created, the typed capability runtime remains long-lived for the app session unless a later approved contract says otherwise

This preserves the current long-lived workspace direction from AM33 while clarifying that:
- bootstrap lifetime is short-lived construction lifetime
- runtime lifetime is capability session lifetime

This issue does not decide whether every capability must eagerly bootstrap at app startup.
It only requires that bootstrap and runtime stay conceptually separate.

---

## 8) What should stop living directly in `MainWindow`

After this boundary is applied, `MainWindow` should not remain the long-term direct owner of:
- multiple lane-local composition fields for the same capability
- multiple lane-local owner fields for the same capability
- capability-shared helper fields whose meaning is local to one capability
- capability-local route-activation or refresh wiring scattered across shell methods

The target is one typed runtime field per capability, not one shell field per capability sub-piece.

This does not require immediate elimination of every transitional field in one runtime slice.
It defines the target that later refactors should converge toward.

---

## 9) Non-goals

This contract does **not** require:
- immediate runtime implementation
- one generic runtime interface shared by every capability
- per-navigation runtime recreation
- removal of already-approved lane-local seams
- product behavior redesign

The goal is a typed shell-to-capability runtime boundary, not an abstract runtime framework for its own sake.

---

## 10) Sequencing implication

After this contract lands:
1. later capability cleanup may introduce one typed runtime per capability
2. shared capability composition should move behind that runtime rather than remaining directly shell-wired
3. lane-local cleanup should continue using AN1 underneath the runtime boundary
4. `MainWindow` should lose capability-local field sprawl over time rather than just renaming it

This keeps later runtime slices narrow without reopening the shell boundary question every time.

---

## 11) Traceability

- `FR-179`
  - capability bootstrap is a shell-owned construction step distinct from the long-lived typed capability runtime
- `FR-180`
  - the typed capability runtime owns capability-local runtime composition, shared capability composition lifetime, capability-shared helpers, and lane-local seam lifetime without absorbing shell ownership
- `FR-181`
  - `MainWindow` remains shell-only after runtime introduction and should converge toward one typed runtime per capability instead of direct lane/helper hand-wiring

Mapped acceptance criteria:
- `AC-044`

---

## 12) Open Questions / TBDs

- `TBD:` Whether each capability will use a dedicated bootstrap type or whether some capability bootstraps remain narrow construction blocks inside `MainWindow`.
- `TBD:` Whether some capabilities will be eagerly bootstrapped at startup while others remain lazily bootstrapped on first activation.
- `TBD:` Whether any capability later proves to need a second typed runtime boundary inside the same capability, or whether lane-local seams remain sufficient below the first runtime layer.

