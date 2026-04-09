# WinUI Deploy Overview Extraction Cleanup Target (AM83)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Deploy Overview` cleanup target so Overview runtime extraction starts from an explicit ownership boundary inside the refined shared Deploy composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Deploy Overview` extraction cleanup target only.

**Out of scope:** Quick Deploy extraction details, From Template extraction details, runtime implementation, Overview redesign beyond ownership cleanup, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-158`..`FR-160`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-037`)
- `docs/02-ux/Archived/winui-deploy-from-template-contract-af.md`
- `docs/02-ux/Archived/winui-deploy-on-the-fly-contract-ag.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Deploy Overview needs its own cleanup target

AM78 already defines:
- the shared Deploy composition cleanup target

That is still not narrow enough for Overview runtime work.

Without an Overview-specific cleanup target, later slices could drift into:
- leaving Overview-specific coordination in shared Deploy composition
- reintroducing `MainWindow` as an Overview host hub
- letting Overview absorb Quick Deploy or From Template concerns because it is the route-entry surface
- letting shared `DeployWorkspaceComposition` become the de facto Overview workflow owner

AM83 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Deploy Overview

The target is:
- `Deploy Overview` remains under shared `DeployWorkspaceComposition`
- shared Deploy composition remains responsible only for shared capability-level composition concerns
- an Overview-local seam becomes the long-term home for Overview-specific state and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Overview-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Overview-specific coordination should converge behind an Overview-local seam, not stay in shared Deploy composition and not move back to `MainWindow`.

---

## 3) What stays in shared Deploy composition

Shared `DeployWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Deploy`
- shared route activation handoff across `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template`
- shared capability composition and wiring that spans more than one Deploy child surface
- shared lifetime participation for the long-lived Deploy workspace

This issue does not redefine the shared Deploy owner created by AM78.
It narrows what should not remain trapped there once Overview extraction begins.

---

## 4) What becomes Overview-local

Overview-local ownership should include:
- Overview summary state
- Overview-local navigation coordination
- Overview-local interaction boundaries used by the Overview surface
- Overview-specific refresh or reconcile behavior triggered by `deploy.overview` activation

This keeps the Overview slice narrow:
- summary
- entry/index behavior
- local interaction coordination

It does not turn Overview into the shared owner for all Deploy behavior.

---

## 5) Overview host cleanup expectation

AM83 is also the cleanup target for Overview-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Overview-specific host bridges remain migration scaffolding only
- Overview-specific control exposure or host-driven UI coordination should reduce behind the Overview-local seam
- shared `DeployWorkspaceComposition` must not become a permanent host proxy for Overview-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Deploy Overview` becoming a new shared Deploy god object
- shared `DeployWorkspaceComposition` becoming the Overview workflow owner
- Overview taking ownership of Quick Deploy semantics
- Overview taking ownership of From Template semantics
- direct `MainWindow` injection into Overview views
- shell-owned host growth that treats Overview as a shell surface instead of a Deploy-local surface

Overview is a Deploy child surface with a summary/navigation role.
It is not the semantic owner of Quick Deploy, From Template, or shared Deploy composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `deploy.overview` as the route-entry and index surface for `Deploy`
- Overview as primarily a summary and navigation surface
- approved AF and AG Deploy workflow boundaries outside Overview ownership cleanup
- approved AL shell/header/navigation/layout behavior around the Deploy surface
- approved AM78 shared Deploy composition boundary and long-lived workspace behavior

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM83 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Deploy-local seams are the correct way to cross shell boundaries when needed

For Overview, this means:
- Overview views do not talk to `MainWindow` directly
- shared Deploy composition may coordinate with shell-owned behavior only through narrow seams
- Overview-local interaction coordination should not widen shared Deploy composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Deploy Overview` continues to participate in the long-lived Deploy workspace.

That means:
- Overview is not recreated on every navigation
- activation of `deploy.overview` refreshes or reconciles Overview state within the existing Deploy workspace
- route activation remains the trigger for Overview refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Overview follow-up runtime slices should do this in order:
1. keep Overview under shared `DeployWorkspaceComposition`
2. introduce the Overview-local seam
3. move Overview-specific state and UI coordination behind that seam
4. reduce temporary Overview-specific host bridges or control exposure behind the seam
5. keep Quick Deploy and From Template extraction concerns in their own narrow slices

This sequencing keeps AM84-AM86 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM83 does **not** define:
- Quick Deploy extraction details
- From Template extraction details
- runtime implementation
- Overview redesign beyond ownership cleanup
- performance redesign

---

## 12) Traceability

- `FR-158`
  - Overview stays under shared Deploy composition while converging Overview-specific ownership behind an Overview-local seam
- `FR-159`
  - behavior-preserving Overview role with long-lived workspace participation and `deploy.overview` route-activation refresh
- `FR-160`
  - no direct `MainWindow` view dependency, no shared Deploy workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-037`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM78` defined the shared Deploy composition cleanup target
- `AF`, `AG`, and `AL` remain the behavioral baseline for Deploy routing, workflow boundaries, and shell/view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact Overview-local seam type name during implementation.
- `TBD:` Whether Overview route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Overview-specific summary action or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.

