# WinUI Deploy Composition Cleanup Target (AM78)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the shared Deploy composition cleanup target so Deploy runtime extraction starts from the refined post-Machines/post-Assets/post-Templates AM model instead of repeating a shell-centric composition pattern.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** Shared WinUI `Deploy` composition cleanup target only.

**Out of scope:** Deploy Overview extraction details, From Template extraction details, Quick Deploy extraction details, runtime implementation, Deploy workflow redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-155`..`FR-157`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-036`)
- `docs/02-ux/Archived/winui-deploy-from-template-contract-af.md`
- `docs/02-ux/Archived/winui-deploy-on-the-fly-contract-ag.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-templates-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Deploy needs a shared cleanup target

AF and AG already defined the current Deploy workflow and route model, and AL preserved the Overview-first local navigation contract.

Machines, Assets, and Templates showed that:
- state extraction alone is not enough
- orchestration extraction alone is not enough
- if shared capability composition still terminates in `MainWindow`, the god-file problem survives in a softer form

This target exists so Deploy starts from the corrected model before runtime extraction of shared Deploy concerns begins.

---

## 2) Desired end-state for shared Deploy composition

The target is:
- `MainWindow` hosts shell composition and app-level workspace lifetime
- a Deploy-local composition owner becomes the long-term home for shared Deploy-local composition
- that Deploy-local composition owner coordinates shared interaction boundaries across:
  - `deploy.overview`
  - `deploy.on_the_fly`
  - `deploy.from_template`

The exact type name is not mandated here.
Examples:
- workspace
- workspace host
- presenter
- coordinator

What matters is ownership. Shared Deploy-local composition should converge behind a Deploy-local owner, not terminate in `MainWindow`.

---

## 3) What MainWindow still owns

`MainWindow` remains responsible only for:
- shell route switching
- shell title/description
- shell compact/drawer behavior
- shell host visibility
- right-panel infrastructure
- app-level workspace lifetime

AM78 does not widen or redesign the shell boundary.

---

## 4) What moves behind the shared Deploy-local composition owner

The Deploy-local composition owner becomes responsible for:
- Deploy-local composition and wiring
- shared Deploy route-activation handling
- shared workspace lifetime participation
- shared local interaction boundaries for:
  - Deploy Overview
  - Quick Deploy
  - From Template

This includes the shared capability-level coordination that would otherwise accumulate in `MainWindow` as Deploy runtime extraction proceeds.

---

## 5) Deploy navigation and workflow boundaries remain preserved

Deploy remains an Overview-first capability under the shared composition target.

That means:
- `deploy.overview` remains the route-entry and index surface for `Deploy`
- `deploy.on_the_fly` remains the deep editor-oriented `Quick Deploy` workflow
- `deploy.from_template` remains a review/remediation/deploy workflow and does not collapse into the Quick Deploy editor surface
- shared Deploy composition does not redesign existing Deploy routes or route-entry semantics

This issue narrows ownership only. It does not redesign the approved Deploy workflow contract.

---

## 6) Temporary bridge rule for Deploy

Capability-specific host interfaces implemented by `MainWindow` are temporary migration bridges only.

That means:
- a narrow shell-hosted bridge may remain temporarily when needed during extraction
- but Deploy-specific host-interface accumulation in `MainWindow` is not the final target
- the intended direction is to reduce those bridges behind the Deploy-local composition boundary

Temporary bridge patterns are acceptable only as migration scaffolding.
Permanent shell-centric Deploy composition is unacceptable.

---

## 7) No direct MainWindow injection into Deploy views

Views must not depend on or receive `MainWindow` directly.

If a Deploy view needs shell-owned or cross-capability behavior:
- expose it through a narrow abstraction
- or expose it through a Deploy-local seam that talks to the shell

Do not solve shared Deploy composition by passing `MainWindow` into Deploy Overview, Quick Deploy, or From Template views.

---

## 8) Workspace lifetime and route activation

Deploy remains long-lived while the app session is open.

That means:
- the Deploy workspace is not recreated on every route change
- navigation between `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template` activates and reconciles shared Deploy state rather than recreating the workspace every route change
- route activation remains the trigger for refresh/reconcile behavior where needed

Any future move toward per-navigation workspace recreation remains a `TBD` and requires explicit re-contracting.

---

## 9) What this issue does not define

AM78 does **not** define:
- Deploy Overview extraction details
- From Template extraction details
- Quick Deploy extraction details
- runtime implementation
- Deploy workflow redesign
- performance redesign

Those belong to later narrow issues.

---

## 10) Sequencing implication

Deploy follow-up runtime issues should use this order:
1. keep `MainWindow` limited to shell-only ownership
2. introduce the shared Deploy-local composition owner
3. move shared Deploy composition and route-activation coordination behind it
4. reduce temporary shell-host bridges as later narrow Deploy slices land
5. keep Overview, Quick Deploy, and From Template ownership boundaries explicit while later Deploy slices narrow lane-local ownership

This keeps later Deploy runtime issues narrow and prevents guesswork about shared ownership or route semantics.

---

## 11) Traceability

- `FR-155`
  - shared Deploy composition converges behind a Deploy-local composition owner
- `FR-156`
  - temporary shell-bridge rule, no direct `MainWindow` view dependency, and explicit Deploy-local ownership for shared interaction boundaries
- `FR-157`
  - long-lived Deploy workspace lifetime with route-activation refresh and preserved Overview-first / lane-specific workflow boundaries

Mapped acceptance criteria:
- `AC-036`

---

## 12) Open Questions / TBDs

- `TBD:` Exact Deploy-local composition owner type name during implementation.
- `TBD:` Whether any Deploy-specific shell-hosted helper remains temporarily necessary during early shared Deploy extraction slices.
- `TBD:` Whether later Deploy cleanup should split Overview, Quick Deploy, and From Template into separate narrow cleanup targets after the shared ownership boundary is in place.

