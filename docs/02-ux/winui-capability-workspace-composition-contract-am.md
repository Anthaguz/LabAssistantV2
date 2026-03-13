# WinUI Capability Workspace Composition Contract (AM33)

**Purpose:** Refine the AM shell/workspace extraction target after the first Machines slices so capability-local UI composition does not keep terminating in `MainWindow`.

**Status:** Approved implementation contract for Milestone AM refinement.

**Scope:** WinUI capability workspace composition refinement after the first Machines extraction slices.

**Out of scope:** Runtime implementation in this issue, capability workflow redesign, per-navigation workspace recreation, domain-semantic redesign, and performance tuning.

**Related:**
- `docs/01-requirements/srs.md` (`FR-125`..`FR-127`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-026`)
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/winui-view-interaction-contract-am.md`
- `docs/02-ux/winui-machines-workspace-extraction-seam-am.md`
- `docs/02-ux/ui-migration-execution-plan.md`

---

## 1) Why this refinement exists

AM1 through AM7 were strong enough to begin extraction work, but the first Machines slices exposed a remaining risk:
- `MainWindow` still stays too capability-aware if capability-specific host interfaces and local UI coordination continue to terminate in the shell
- moving state/orchestration alone is not enough if shell-level host bridges become the new long-term pattern

This refinement exists so later AM capability slices do not repeat that softer version of the god-file problem.

---

## 2) Target composition model

The long-term target is:
- `MainWindow` hosts a capability workspace object
- that capability workspace composes the capability view, capability-local state owner, and capability-local workflow owner
- shell concerns remain in `MainWindow`

In practical terms:
- `MainWindow` remains the composition root for the app shell
- capability-local UI coordination should increasingly move behind capability workspace composition objects
- shell should not remain the permanent final destination for capability-local host interfaces

This does **not** require one naming convention.  
The composition object may be named:
- workspace
- presenter
- coordinator
- controller host
- or similar

What matters is ownership, not jargon.

---

## 3) What MainWindow still owns

`MainWindow` remains responsible for:
- shell route switching
- shell navigation and drawer behavior
- shell title/description
- shell theme state
- right-panel container and lifecycle
- shell-level host visibility
- app-level composition and workspace lifetime

This remains the correct shell boundary.

---

## 4) What should stop terminating in MainWindow

The following should not scale indefinitely as `MainWindow` responsibilities:
- capability-specific host interface implementations
- capability-specific local UI coordination
- capability-specific selection/update/apply sequencing
- capability-specific view mutation routines
- capability-specific refresh/reconcile rules

These should converge into capability-local workspace composition.

Temporary shell bridges remain acceptable during migration, but they are not the end-state target.

---

## 5) Temporary bridge rule

Capability-specific host interfaces implemented by `MainWindow` are allowed only as:
- transitional migration bridges
- narrow shell-owned helpers

They are **not** the intended long-term architecture.

That means:
- if a capability still needs a shell-hosted dialog bridge or shell-owned helper during extraction, it may keep one temporarily
- but repeated capability-specific host-interface accumulation in `MainWindow` should be treated as technical debt to reduce, not a success condition

---

## 6) No direct MainWindow injection into views

Views must not depend on or receive `MainWindow` directly.

If a view needs something shell-owned or cross-capability:
- expose it through a narrow abstraction
- or a capability-local seam that itself talks to the shell

Do **not** solve cross-view/global concerns by injecting `MainWindow` into capability views.

Why:
- it couples views directly to the shell
- it hides ownership boundaries
- it makes testing and lifecycle reasoning worse
- it recreates shell-centric architecture through a different dependency path

---

## 7) Workspace lifetime rule

Current AM target:
- capability workspaces are long-lived while the app session is open
- route activation refreshes or reconciles state as needed
- capability workspace recreation on every navigation is **not** the default model

This preserves:
- current hosted-view performance characteristics
- explicit route-activation refresh rules
- predictable capability-local state lifetime during the session

Future lifetime changes remain a `TBD` and require explicit re-contracting.

---

## 8) What this means for Machines

Machines is now the proof point for this refinement.

The docs should treat Machines as:
- useful first extraction evidence
- but also the place where the remaining shell-heavy pattern was discovered

That means:
- Machines may need follow-up cleanup after AM7
- later capability slices should not blindly copy the exact current host-interface shape if it keeps `MainWindow` too central

---

## 9) Sequencing implication

Before broader capability extraction continues:
1. refine the workspace composition contract
2. re-evaluate Machines against that refined target
3. create follow-up cleanup issues if needed
4. then continue with `Assets`, `Templates`, `Deploy`, and `Diagnostics`

This is a deliberate correction point, not a rollback of AM.

---

## 10) Non-goals

AM33 does **not** require:
- recreating workspaces on every route change
- injecting shell classes into capability views
- immediate elimination of every temporary bridge
- a full framework rewrite
- product-semantic redesign

The goal is a clearer final target before scaling the pattern further.

---

## 11) Traceability

- `FR-125`
  - capability-local workspace composition as the target architecture
- `FR-126`
  - no direct `MainWindow` injection into views
- `FR-127`
  - long-lived capability workspaces with route-activation refresh

Mapped acceptance criteria:
- `AC-026`

---

## 12) Open Questions / TBDs

- `TBD:` Exact capability workspace composition type per capability once follow-up cleanup begins (`workspace`, `presenter`, `coordinator`, etc.).
- `TBD:` Whether any shell-hosted dialog helpers remain acceptable after each capability reaches its post-extraction cleanup stage.
- `TBD:` Whether any capability later proves to require a different lifetime model than the current long-lived workspace default.
