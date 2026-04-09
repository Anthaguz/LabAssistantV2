# WinUI Templates Composition Cleanup Target (AM56)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the shared Templates composition cleanup target so Templates runtime extraction starts from the refined post-Machines/post-Assets AM model instead of repeating a softer `MainWindow`-centric pattern.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** Shared WinUI `Templates` composition cleanup target only.

**Out of scope:** Templates Library extraction details, Templates Editor extraction details, runtime implementation, editor behavior redesign, performance redesign, and per-navigation workspace recreation.

**Related:**
- `docs/01-requirements/srs.md` (`FR-146`..`FR-148`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-033`)
- `docs/02-ux/Archived/winui-templates-capability-contract-ad.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-assets-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Templates needs a shared cleanup target

AD already defined the unified Templates capability and AL preserved the Library-first navigation exception.

AM33 and the later Machines/Assets follow-up work showed that:
- state extraction alone is not enough
- orchestration extraction alone is not enough
- if shared capability composition still terminates in `MainWindow`, the god-file problem survives in a softer form

This target exists so Templates starts from the corrected model before Templates runtime extraction of shared concerns begins.

---

## 2) Desired end-state for shared Templates composition

The target is:
- `MainWindow` hosts shell composition and app-level workspace lifetime
- a Templates-local composition owner becomes the long-term home for shared Templates-local composition
- that Templates-local composition owner coordinates shared interaction boundaries across:
  - `templates.library`
  - `templates.editor`

The exact type name is not mandated here.
Examples:
- workspace
- workspace host
- presenter
- coordinator

What matters is ownership. Shared Templates-local composition should converge behind a Templates-local owner, not terminate in `MainWindow`.

---

## 3) What MainWindow still owns

`MainWindow` remains responsible only for:
- shell route switching
- shell title/description
- shell compact/drawer behavior
- shell host visibility
- right-panel infrastructure
- app-level workspace lifetime

AM56 does not widen or redesign the shell boundary.

---

## 4) What moves behind the shared Templates-local composition owner

The Templates-local composition owner becomes responsible for:
- Templates-local composition and wiring
- shared route-activation handling for Templates
- shared workspace lifetime participation
- shared local interaction boundaries for:
  - Library
  - Editor

This includes the shared capability-level coordination that would otherwise accumulate in `MainWindow` as Templates runtime extraction proceeds.

---

## 5) Templates navigation exception remains preserved

Templates remains a special navigation case under the shared composition target.

That means:
- `templates.library` remains the stable/default Templates surface
- `templates.editor` remains a workflow-state destination entered from explicit actions
- shared Templates composition must not collapse back into a peer-tab or Overview-first model
- Library and Editor share a Templates-local composition owner, but they do not become equivalent always-visible peer destinations

This issue narrows ownership only. It does not redesign the approved Templates navigation contract.

---

## 6) Temporary bridge rule for Templates

Capability-specific host interfaces implemented by `MainWindow` are temporary migration bridges only.

That means:
- a narrow shell-hosted bridge may remain temporarily when needed during extraction
- but Templates-specific host-interface accumulation in `MainWindow` is not the final target
- the intended direction is to reduce those bridges behind the Templates-local composition boundary

Temporary bridge patterns are acceptable only as migration scaffolding.
Permanent shell-centric Templates composition is unacceptable.

---

## 7) No direct MainWindow injection into Templates views

Views must not depend on or receive `MainWindow` directly.

If a Templates view needs shell-owned or cross-capability behavior:
- expose it through a narrow abstraction
- or expose it through a Templates-local seam that talks to the shell

Do not solve shared Templates composition by passing `MainWindow` into Library or Editor views.

---

## 8) Workspace lifetime and route activation

Templates remains long-lived while the app session is open.

That means:
- the Templates workspace is not recreated on every route change
- navigation activates and reconciles shared Templates state rather than recreating the workspace every route change
- route activation remains the trigger for refresh/reconcile behavior where needed

Any future move toward per-navigation workspace recreation remains a `TBD` and requires explicit re-contracting.

---

## 9) What this issue does not define

AM56 does **not** define:
- Templates Library extraction details
- Templates Editor extraction details
- runtime implementation
- editor behavior redesign
- performance redesign

Those belong to later narrow issues.

---

## 10) Sequencing implication

Templates follow-up runtime issues should use this order:
1. keep `MainWindow` limited to shell-only ownership
2. introduce the shared Templates-local composition owner
3. move shared Templates composition and route-activation coordination behind it
4. reduce temporary shell-host bridges as later narrow Templates slices land
5. keep the Library-first / Editor workflow-state exception explicit while later Templates slices narrow Library-local and Editor-local ownership

This keeps later Templates runtime issues narrow and prevents guesswork about shared ownership or navigation semantics.

---

## 11) Traceability

- `FR-146`
  - shared Templates composition converges behind a Templates-local composition owner
- `FR-147`
  - temporary shell-bridge rule, no direct `MainWindow` view dependency, and explicit Templates-local ownership for shared interaction boundaries
- `FR-148`
  - long-lived Templates workspace lifetime with route-activation refresh and preserved Library-first navigation exception

Mapped acceptance criteria:
- `AC-033`

---

## 12) Open Questions / TBDs

- `TBD:` Exact Templates-local composition owner type name during implementation.
- `TBD:` Whether any Templates-specific shell-hosted helper remains temporarily necessary during early shared Templates extraction slices.
- Resolved by later AM cleanup targets: separate Library-local and Editor-local cleanup targets are required after the shared ownership boundary is in place.

