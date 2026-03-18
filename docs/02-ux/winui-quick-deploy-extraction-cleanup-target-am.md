# WinUI Deploy Quick Deploy Extraction Cleanup Target (AM95)

**Purpose:** Define the narrow `Deploy Quick Deploy` cleanup target so Quick Deploy runtime extraction starts from an explicit ownership boundary inside the refined shared Deploy composition model.

**Status:** Approved implementation contract for the `Deploy Quick Deploy` post-AM87 cleanup sequence.

**Scope:** WinUI `Deploy Quick Deploy` extraction cleanup target only.

**Out of scope:** From Template extraction details, Deploy Overview extraction details, runtime implementation, Quick Deploy redesign beyond ownership cleanup, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-164`..`FR-166`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-039`)
- `docs/02-ux/winui-deploy-from-template-contract-af.md`
- `docs/02-ux/winui-deploy-on-the-fly-contract-ag.md`
- `docs/02-ux/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/winui-deploy-overview-extraction-cleanup-target-am.md`
- `docs/02-ux/winui-from-template-extraction-cleanup-target-am.md`
- `docs/02-ux/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/ui-migration-execution-plan.md`

---

## 1) Why Deploy Quick Deploy needs its own cleanup target

AM78 already defines:
- the shared Deploy composition cleanup target

AM83 already defines:
- the narrow Deploy Overview cleanup target

AM87 already defines:
- the narrow Deploy From Template cleanup target

That is still not narrow enough for Quick Deploy runtime work.

Without a Quick Deploy-specific cleanup target, later slices could drift into:
- leaving Quick Deploy-specific state or orchestration in shared Deploy composition
- reintroducing `MainWindow` as a Quick Deploy host hub
- letting Quick Deploy absorb From Template semantics because both lanes deploy labs
- letting Quick Deploy absorb Overview ownership because Overview remains the route-entry surface
- letting shared `DeployWorkspaceComposition` become the de facto Quick Deploy workflow owner

AM95 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Deploy Quick Deploy

The target is:
- `Deploy Quick Deploy` remains under shared `DeployWorkspaceComposition`
- shared Deploy composition remains responsible only for shared capability-level composition concerns
- a Quick Deploy-local seam becomes the long-term home for Quick Deploy-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Quick Deploy-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Quick Deploy-specific coordination should converge behind a Quick Deploy-local seam, not stay in shared Deploy composition and not move back to `MainWindow`.

---

## 3) What stays in shared Deploy composition

Shared `DeployWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Deploy`
- shared route activation handoff across `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template`
- shared capability composition and wiring that spans more than one Deploy child surface
- shared lifetime participation for the long-lived Deploy workspace

This issue does not redefine the shared Deploy owner created by AM78.
It narrows what should not remain trapped there once Quick Deploy extraction begins.

---

## 4) What becomes Quick Deploy-local

Quick Deploy-local ownership should include:
- Quick Deploy-specific state
- Quick Deploy-specific orchestration
- Quick Deploy-specific interaction boundaries used by the Quick Deploy surface
- Quick Deploy-specific composition or host cleanup
- Quick Deploy-specific refresh or reconcile behavior triggered by `deploy.on_the_fly` activation

This keeps the Quick Deploy slice narrow:
- on-the-fly workflow state
- editor-oriented deploy orchestration
- local interaction and composition cleanup

It does not turn Quick Deploy into the shared owner for all Deploy behavior.

---

## 5) Quick Deploy host cleanup expectation

AM95 is also the cleanup target for Quick Deploy-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Quick Deploy-specific host bridges remain migration scaffolding only
- Quick Deploy-specific control exposure or host-driven UI coordination should reduce behind the Quick Deploy-local seam
- shared `DeployWorkspaceComposition` must not become a permanent host proxy for Quick Deploy-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Deploy Quick Deploy` becoming a new shared Deploy god object
- shared `DeployWorkspaceComposition` becoming the Quick Deploy workflow owner
- Quick Deploy taking ownership of From Template semantics
- Quick Deploy taking ownership of Deploy Overview semantics
- direct `MainWindow` injection into Quick Deploy views
- shell-owned host growth that treats Quick Deploy as a shell surface instead of a Deploy-local surface

Quick Deploy is a Deploy child surface with an on-the-fly deploy role.
It is not the semantic owner of From Template, Deploy Overview, or shared Deploy composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `deploy.on_the_fly` as the distinct on-the-fly deploy surface for `Deploy`
- Quick Deploy as a workflow surface that remains separate from the template-driven From Template lane
- approved AG Quick Deploy routing, readiness, correction affordances, and results visibility boundaries
- approved AF Deploy From Template workflow boundaries outside Quick Deploy ownership cleanup
- approved AL shell/header/navigation/layout behavior around the Deploy surface
- approved AM78 shared Deploy composition boundary, AM83 Deploy Overview boundary, AM87 Deploy From Template boundary, and long-lived workspace behavior

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM95 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Deploy-local seams are the correct way to cross shell boundaries when needed

For Quick Deploy, this means:
- Quick Deploy views do not talk to `MainWindow` directly
- shared Deploy composition may coordinate with shell-owned behavior only through narrow seams
- Quick Deploy-local interaction and composition cleanup should not widen shared Deploy composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Deploy Quick Deploy` continues to participate in the long-lived Deploy workspace.

That means:
- Quick Deploy is not recreated on every navigation
- activation of `deploy.on_the_fly` refreshes or reconciles Quick Deploy state within the existing Deploy workspace
- route activation remains the trigger for Quick Deploy refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Quick Deploy follow-up runtime slices should do this in order:
1. keep Quick Deploy under shared `DeployWorkspaceComposition`
2. introduce the Quick Deploy-local seam
3. move Quick Deploy-specific state and orchestration behind that seam
4. reduce temporary Quick Deploy-specific host bridges or control exposure behind the seam
5. keep From Template and Deploy Overview extraction concerns in their own narrow slices

This sequencing keeps AM96-AM103 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM95 does **not** define:
- From Template extraction details
- Deploy Overview extraction details
- runtime implementation
- Quick Deploy redesign beyond ownership cleanup
- performance redesign

---

## 12) Traceability

- `FR-164`
  - Quick Deploy stays under shared Deploy composition while converging Quick Deploy-specific ownership behind a Quick Deploy-local seam
- `FR-165`
  - behavior-preserving Quick Deploy role with long-lived workspace participation and `deploy.on_the_fly` route-activation refresh
- `FR-166`
  - no direct `MainWindow` view dependency, no shared Deploy workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-039`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM78` defined the shared Deploy composition cleanup target
- `AM83` defined the Deploy Overview cleanup target
- `AM87` defined the Deploy From Template cleanup target
- `AF`, `AG`, and `AL` remain the behavioral baseline for Deploy routing, workflow boundaries, and shell/view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact Quick Deploy-local seam type name during implementation.
- `TBD:` Whether Quick Deploy route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Quick Deploy-specific shell-facing helper still needs a temporary narrow abstraction during runtime extraction.
