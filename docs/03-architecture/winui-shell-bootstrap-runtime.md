# WinUI Shell Bootstrap And Runtime

**Purpose:** Define the current authoritative boundary between the WinUI shell, capability bootstrap, typed capability runtime, and long-lived capability workspace lifetime.

**Status:** Current authoritative WinUI architecture rule.

**Absorbs:**
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-typed-capability-runtime-contract-an.md`

**Source basis:**
- `docs/01-requirements/srs.md` (`FR-113`..`FR-115`, `FR-125`..`FR-127`, `FR-179`..`FR-181`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-022`, `AC-026`, `AC-044`)
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`

## 1) Core Boundary Rule

`MainWindow` is the shell composition root.
It is not the long-term owner of capability-local workflow state, capability-local helper coordination, or lane-by-lane runtime wiring.

The shell owns shell concerns.
Capability-local runtime ownership must converge behind capability-local runtime boundaries.

## 2) What `MainWindow` Owns

`MainWindow` remains responsible for:
- shell route switching and route resolution
- shell navigation configuration and selection
- shell header/title/description
- shell theme and compact/drawer behavior
- shell-owned panel and container infrastructure
- shell-level host visibility
- app-level lifetime of capability runtimes
- capability bootstrap entry points

The shell may still perform capability bootstrap.
It must not remain the long-lived capability owner after that construction step.

## 3) Capability Bootstrap

Capability bootstrap is the shell-owned construction phase that creates a capability runtime boundary.

Bootstrap may:
- resolve services from DI or app-level sources
- bind shell-owned view roots or host elements
- create shell bridges or adapters needed by the capability runtime
- instantiate the typed capability runtime

Bootstrap must not become the capability's ongoing workflow owner after construction.

Bootstrap may be implemented as:
- a dedicated bootstrap type
- a factory or builder
- or a narrow `MainWindow` construction block

What matters is the ownership boundary, not the factory shape.

## 4) Typed Capability Runtime

A typed capability runtime is the long-lived capability-local runtime boundary that sits below shell bootstrap and above shared capability composition and lane-local seams.

It may own:
- shared capability composition lifetime
- capability-shared helper lifetime
- lane-local seam lifetime
- capability-local route-activation handoff
- capability-level refresh or reconcile entry points

It must not own:
- shell route resolution
- shell navigation configuration
- shell header or theme ownership
- shell container mechanics
- direct `MainWindow` dependency
- lane-local workflow details that belong in lane-local seams

The intended hierarchy is:

`MainWindow`
-> capability bootstrap
-> typed capability runtime
-> shared capability composition and capability-shared helpers where needed
-> lane-local seams

## 5) What Should Stop Terminating In `MainWindow`

`MainWindow` should not remain the long-term direct owner of:
- multiple lane-local fields for the same capability
- capability-local helper fields whose meaning is local to one capability
- capability-specific host interface implementations as the end-state architecture
- capability-specific local UI coordination
- scattered capability-local route-activation refresh or reconcile wiring

The target is one typed runtime field per capability rather than one shell field per capability sub-piece.

## 6) View And Capability Boundary Support Rules

Views must not depend on or receive `MainWindow` directly.

If a capability or lane needs shell-owned cooperation:
- expose it through a narrow shell bridge or adapter
- keep the dependency named and explicit

Shared capability composition remains limited to genuinely shared capability-level concerns.
It must not become the hidden owner of lane-local workflow or lane-local semantics.

## 7) Lifetime Rule

The current default is long-lived capability runtime and workspace lifetime for the app session.

That means:
- bootstrap lifetime is short-lived construction lifetime
- runtime lifetime is capability session lifetime
- route activation may refresh or reconcile state as needed
- per-navigation workspace recreation is not the default model

Any lifetime change beyond that requires a new explicit contract update.

## 8) Non-Goals

This doc does not require:
- immediate typed runtime implementation
- eager bootstrap for every capability
- one generic runtime interface across all capabilities
- injecting shell classes into capability views
- product behavior redesign

## 9) Update Rule

If the durable shell/bootstrap/runtime rule changes, update this file in place.
Do not create a new milestone-coded shell/runtime contract for a rule that should survive beyond the originating slice.

## Open Questions / TBDs

- `TBD:` Which capabilities should bootstrap eagerly at startup versus lazily on first activation.
- `TBD:` Whether any capability later proves to need a second runtime boundary inside the same capability.
