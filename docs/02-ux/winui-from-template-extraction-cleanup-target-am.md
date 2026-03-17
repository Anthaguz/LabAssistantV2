# WinUI Deploy From Template Extraction Cleanup Target (AM87)

**Purpose:** Define the narrow `Deploy From Template` cleanup target so From Template runtime extraction starts from an explicit ownership boundary inside the refined shared Deploy composition model.

**Status:** Approved implementation contract for the `Deploy From Template` post-AM83 cleanup sequence.

**Scope:** WinUI `Deploy From Template` extraction cleanup target only.

**Out of scope:** Quick Deploy extraction details, Deploy Overview extraction details, runtime implementation, From Template redesign beyond ownership cleanup, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-161`..`FR-163`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-038`)
- `docs/02-ux/winui-deploy-from-template-contract-af.md`
- `docs/02-ux/winui-deploy-on-the-fly-contract-ag.md`
- `docs/02-ux/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/winui-deploy-overview-extraction-cleanup-target-am.md`
- `docs/02-ux/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/ui-migration-execution-plan.md`

---

## 1) Why Deploy From Template needs its own cleanup target

AM78 already defines:
- the shared Deploy composition cleanup target

AM83 already defines:
- the narrow Deploy Overview cleanup target

That is still not narrow enough for From Template runtime work.

Without a From Template-specific cleanup target, later slices could drift into:
- leaving From Template-specific state or orchestration in shared Deploy composition
- reintroducing `MainWindow` as a From Template host hub
- letting From Template absorb Quick Deploy semantics because both lanes deploy labs
- letting From Template absorb Overview ownership because Overview is the route-entry surface
- letting shared `DeployWorkspaceComposition` become the de facto From Template workflow owner

AM87 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Deploy From Template

The target is:
- `Deploy From Template` remains under shared `DeployWorkspaceComposition`
- shared Deploy composition remains responsible only for shared capability-level composition concerns
- a From Template-local seam becomes the long-term home for From Template-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact From Template-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. From Template-specific coordination should converge behind a From Template-local seam, not stay in shared Deploy composition and not move back to `MainWindow`.

---

## 3) What stays in shared Deploy composition

Shared `DeployWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Deploy`
- shared route activation handoff across `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template`
- shared capability composition and wiring that spans more than one Deploy child surface
- shared lifetime participation for the long-lived Deploy workspace

This issue does not redefine the shared Deploy owner created by AM78.
It narrows what should not remain trapped there once From Template extraction begins.

---

## 4) What becomes From Template-local

From Template-local ownership should include:
- From Template-specific state
- From Template-specific orchestration
- From Template-specific interaction boundaries used by the From Template surface
- From Template-specific composition or host cleanup
- From Template-specific refresh or reconcile behavior triggered by `deploy.from_template` activation

This keeps the From Template slice narrow:
- template-driven workflow state
- review/remediation/deploy orchestration
- local interaction and composition cleanup

It does not turn From Template into the shared owner for all Deploy behavior.

---

## 5) From Template host cleanup expectation

AM87 is also the cleanup target for From Template-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary From Template-specific host bridges remain migration scaffolding only
- From Template-specific control exposure or host-driven UI coordination should reduce behind the From Template-local seam
- shared `DeployWorkspaceComposition` must not become a permanent host proxy for From Template-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Deploy From Template` becoming a new shared Deploy god object
- shared `DeployWorkspaceComposition` becoming the From Template workflow owner
- From Template taking ownership of Quick Deploy semantics
- From Template taking ownership of Deploy Overview semantics
- direct `MainWindow` injection into From Template views
- shell-owned host growth that treats From Template as a shell surface instead of a Deploy-local surface

From Template is a Deploy child surface with a template-driven review/remediation/deploy role.
It is not the semantic owner of Quick Deploy, Deploy Overview, or shared Deploy composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `deploy.from_template` as the distinct template-driven review/remediation/deploy surface for `Deploy`
- From Template as a workflow surface that remains separate from the Quick Deploy editor-oriented lane
- approved AF Deploy from-template routing, readiness, and results boundaries
- approved AG Quick Deploy workflow boundaries outside From Template ownership cleanup
- approved AL shell/header/navigation/layout behavior around the Deploy surface
- approved AM78 shared Deploy composition boundary, AM83 Deploy Overview boundary, and long-lived workspace behavior

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM87 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Deploy-local seams are the correct way to cross shell boundaries when needed

For From Template, this means:
- From Template views do not talk to `MainWindow` directly
- shared Deploy composition may coordinate with shell-owned behavior only through narrow seams
- From Template-local interaction and composition cleanup should not widen shared Deploy composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Deploy From Template` continues to participate in the long-lived Deploy workspace.

That means:
- From Template is not recreated on every navigation
- activation of `deploy.from_template` refreshes or reconciles From Template state within the existing Deploy workspace
- route activation remains the trigger for From Template refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

From Template follow-up runtime slices should do this in order:
1. keep From Template under shared `DeployWorkspaceComposition`
2. introduce the From Template-local seam
3. move From Template-specific state and orchestration behind that seam
4. reduce temporary From Template-specific host bridges or control exposure behind the seam
5. keep Quick Deploy and Deploy Overview extraction concerns in their own narrow slices

This sequencing keeps AM88-AM94 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM87 does **not** define:
- Quick Deploy extraction details
- Deploy Overview extraction details
- runtime implementation
- From Template redesign beyond ownership cleanup
- performance redesign

---

## 12) Traceability

- `FR-161`
  - From Template stays under shared Deploy composition while converging From Template-specific ownership behind a From Template-local seam
- `FR-162`
  - behavior-preserving From Template role with long-lived workspace participation and `deploy.from_template` route-activation refresh
- `FR-163`
  - no direct `MainWindow` view dependency, no shared Deploy workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-038`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM78` defined the shared Deploy composition cleanup target
- `AM83` defined the Deploy Overview cleanup target
- `AF`, `AG`, and `AL` remain the behavioral baseline for Deploy routing, workflow boundaries, and shell/view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact From Template-local seam type name during implementation.
- `TBD:` Whether From Template route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any From Template-specific shell-facing helper still needs a temporary narrow abstraction during runtime extraction.
