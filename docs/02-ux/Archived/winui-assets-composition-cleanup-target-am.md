# WinUI Assets Composition Cleanup Target (AM10)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the shared Assets composition cleanup target so Assets runtime extraction starts from the refined AM33/AM34 model instead of repeating a softer `MainWindow`-centric pattern.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** Shared WinUI `Assets` composition cleanup target only.

**Out of scope:** Base Disks-specific extraction details, Switches-specific extraction details, Overview-specific extraction details, runtime implementation, per-navigation workspace recreation, behavior redesign, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-134`..`FR-136`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-029`)
- `docs/02-ux/Archived/winui-assets-workspace-extraction-seam-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-machines-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Assets needs a shared cleanup target

AM9 already defined the Assets extraction seam, but that seam alone is not enough.

Machines showed that:
- state extraction alone is not enough
- orchestration extraction alone is not enough
- if shared capability composition still terminates in `MainWindow`, the god-file problem survives in a softer form

This target exists so Assets starts from the corrected model before runtime extraction of shared Assets concerns begins.

---

## 2) Desired end-state for Assets shared composition

The target is:
- `MainWindow` hosts shell composition and app-level workspace lifetime
- an Assets-local composition owner becomes the long-term home for shared Assets-local composition
- that Assets-local composition owner coordinates shared interaction boundaries across:
  - `assets.overview`
  - `assets.base_disks`
  - `assets.switches`

The exact type name is not mandated here.
Examples:
- workspace
- workspace host
- presenter
- coordinator

What matters is ownership. Shared Assets-local composition should converge behind an Assets-local owner, not terminate in `MainWindow`.

---

## 3) What MainWindow still owns

`MainWindow` remains responsible only for:
- shell route switching
- shell title/description
- shell compact/drawer behavior
- shell host visibility
- right-panel infrastructure
- app-level workspace lifetime

AM10 does not widen or redesign the shell boundary.

---

## 4) What moves behind the shared Assets-local composition owner

The Assets-local composition owner becomes responsible for:
- Assets-local composition and wiring
- shared Assets route-activation handling
- shared workspace lifetime participation
- shared local interaction boundaries across Overview, Base Disks, and Switches

This includes the shared capability-level coordination that would otherwise accumulate in `MainWindow` as Assets runtime extraction proceeds.

---

## 5) Temporary bridge rule for Assets

Capability-specific host interfaces implemented by `MainWindow` are temporary migration bridges only.

That means:
- a narrow shell-hosted bridge may remain temporarily when needed during extraction
- but Assets-specific host-interface accumulation in `MainWindow` is not the final target
- the intended direction is to reduce those bridges behind the Assets-local composition boundary

Temporary bridge patterns are acceptable only as migration scaffolding.
Permanent shell-centric Assets composition is unacceptable.

---

## 6) No direct MainWindow injection into Assets views

Views must not depend on or receive `MainWindow` directly.

If an Assets view needs shell-owned or cross-capability behavior:
- expose it through a narrow abstraction
- or expose it through an Assets-local seam that talks to the shell

Do not solve shared Assets composition by passing `MainWindow` into Overview, Base Disks, or Switches views.

---

## 7) Workspace lifetime and route activation

Assets remains long-lived while the app session is open.

That means:
- the Assets workspace is not recreated on every route change
- navigation between `assets.overview`, `assets.base_disks`, and `assets.switches` activates and reconciles shared state
- route activation remains the trigger for refresh/reconcile behavior where needed

Any future move toward per-navigation workspace recreation remains a `TBD` and requires explicit re-contracting.

---

## 8) What this issue does not define

AM10 does **not** define:
- Base Disks-specific extraction details
- Switches-specific extraction details
- Overview-specific extraction details
- runtime implementation
- performance redesign

Those belong to later narrow issues.

---

## 9) Sequencing implication

Assets follow-up runtime issues should use this order:
1. keep `MainWindow` limited to shell-only ownership
2. introduce the shared Assets-local composition owner
3. move shared Assets composition and route-activation coordination behind it
4. reduce temporary shell-host bridges as later narrow Assets slices land

This keeps later Assets runtime issues narrow and prevents guesswork about shared ownership.

---

## 10) Traceability

- `FR-134`
  - shared Assets composition converges behind an Assets-local composition owner
- `FR-135`
  - temporary shell-bridge rule and no direct `MainWindow` view dependency
- `FR-136`
  - long-lived Assets workspace lifetime with route-activation refresh and explicit non-goals

Mapped acceptance criteria:
- `AC-029`

---

## 11) Open Questions / TBDs

- `TBD:` Exact Assets-local composition owner type name during implementation.
- `TBD:` Whether any shell-hosted dialog helper remains temporarily necessary during early shared Assets extraction slices.
- `TBD:` Whether a later capability-specific issue needs a more explicit shared refresh contract for Overview summaries beyond route-activation reconciliation.

