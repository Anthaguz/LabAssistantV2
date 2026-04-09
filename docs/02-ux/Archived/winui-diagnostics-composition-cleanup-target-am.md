# WinUI Diagnostics Composition Cleanup Target (AM105)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the shared Diagnostics composition cleanup target so Diagnostics runtime extraction starts from the refined post-Machines/post-Assets/post-Templates/post-Deploy AM model instead of repeating a shell-centric composition pattern.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** Shared WinUI `Diagnostics` composition cleanup target only.

**Out of scope:** Diagnostics Overview extraction details, Diagnostics Logs extraction details, runtime implementation, Diagnostics workflow redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-167`..`FR-169`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-040`)
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Diagnostics needs a shared cleanup target

AL already defined `Diagnostics` as an Overview-first capability shape and preserved current shell/header/navigation behavior.

Machines, Assets, Templates, and Deploy showed that:
- state extraction alone is not enough
- orchestration extraction alone is not enough
- if shared capability composition still terminates in `MainWindow`, the god-file problem survives in a softer form

This target exists so Diagnostics starts from the corrected model before runtime extraction of shared Diagnostics concerns begins.

---

## 2) Desired end-state for shared Diagnostics composition

The target is:
- `MainWindow` hosts shell composition and app-level workspace lifetime
- a Diagnostics-local composition owner becomes the long-term home for shared Diagnostics-local composition
- that Diagnostics-local composition owner coordinates shared interaction boundaries across:
  - `Diagnostics Overview`
  - `Diagnostics Logs`

The exact type name is not mandated here.
Examples:
- workspace
- workspace host
- presenter
- coordinator

What matters is ownership. Shared Diagnostics-local composition should converge behind a Diagnostics-local owner, not terminate in `MainWindow`.

---

## 3) What MainWindow still owns

`MainWindow` remains responsible only for:
- shell route switching
- shell title/description
- shell compact/drawer behavior
- shell host visibility
- right-panel infrastructure
- app-level workspace lifetime

AM105 does not widen or redesign the shell boundary.

---

## 4) What moves behind the shared Diagnostics-local composition owner

The Diagnostics-local composition owner becomes responsible for:
- Diagnostics-local composition and wiring
- shared Diagnostics route-activation handling
- shared workspace lifetime participation
- shared local interaction boundaries for:
  - Diagnostics Overview
  - Diagnostics Logs

This includes the shared capability-level coordination that would otherwise accumulate in `MainWindow` as Diagnostics runtime extraction proceeds.

---

## 5) Diagnostics route model and lifetime remain preserved

Diagnostics remains an Overview-first capability under the shared composition target.

That means:
- `Diagnostics Overview` remains the route-entry and index surface for `Diagnostics`
- `Diagnostics Logs` remains a child troubleshooting surface rather than a new top-level shell destination
- shared Diagnostics composition does not redesign existing Diagnostics routes or route-entry semantics
- navigation should activate and reconcile shared Diagnostics state rather than recreate the Diagnostics workspace every route change

This issue narrows ownership only. It does not redesign the approved Diagnostics workflow contract.

---

## 6) Temporary bridge rule for Diagnostics

Capability-specific host interfaces implemented by `MainWindow` are temporary migration bridges only.

That means:
- a narrow shell-hosted bridge may remain temporarily when needed during extraction
- but Diagnostics-specific host-interface accumulation in `MainWindow` is not the final target
- the intended direction is to reduce those bridges behind the Diagnostics-local composition boundary

Temporary bridge patterns are acceptable only as migration scaffolding.
Permanent shell-centric Diagnostics composition is unacceptable.

---

## 7) No direct MainWindow injection into Diagnostics views

Views must not depend on or receive `MainWindow` directly.

If a Diagnostics view needs shell-owned or cross-capability behavior:
- expose it through a narrow abstraction
- or expose it through a Diagnostics-local seam that talks to the shell

Do not solve shared Diagnostics composition by passing `MainWindow` into Diagnostics Overview or Diagnostics Logs views.

---

## 8) Workspace lifetime and route activation

Diagnostics remains long-lived while the app session is open unless a later approved contract explicitly changes that rule.

That means:
- the Diagnostics workspace is not recreated on every route change
- navigation between Diagnostics Overview and Diagnostics Logs activates and reconciles shared Diagnostics state rather than recreating the workspace every route change
- route activation remains the trigger for refresh/reconcile behavior where needed

Any future move toward per-navigation workspace recreation remains a `TBD` and requires explicit re-contracting.

---

## 9) What this issue does not define

AM105 does **not** define:
- Diagnostics Overview extraction details
- Diagnostics Logs extraction details
- runtime implementation
- Diagnostics workflow redesign
- performance redesign

Those belong to later narrow issues.

---

## 10) Sequencing implication

Diagnostics follow-up runtime issues should use this order:
1. keep `MainWindow` limited to shell-only ownership
2. introduce the shared Diagnostics-local composition owner
3. move shared Diagnostics composition and route-activation coordination behind it
4. reduce temporary shell-host bridges as later narrow Diagnostics slices land
5. keep shared Diagnostics ownership separate from later Overview-local and Logs-local ownership decisions

This keeps later Diagnostics runtime issues narrow and prevents guesswork about shared ownership or lane-specific scope.

---

## 11) Traceability

- `FR-167`
  - shared Diagnostics composition converges behind a Diagnostics-local composition owner
- `FR-168`
  - temporary shell-bridge rule, no direct `MainWindow` view dependency, and explicit Diagnostics-local ownership for shared interaction boundaries
- `FR-169`
  - long-lived Diagnostics workspace lifetime with route-activation refresh and preserved Overview-first route-entry semantics

Mapped acceptance criteria:
- `AC-040`

---

## 12) Open Questions / TBDs

- `TBD:` Exact Diagnostics-local composition owner type name during implementation.
- `TBD:` Whether any Diagnostics-specific shell-hosted helper remains temporarily necessary during early shared Diagnostics extraction slices.
- `TBD:` Whether later Diagnostics cleanup should split Overview and Logs into separate narrow cleanup targets after the shared ownership boundary is in place.

