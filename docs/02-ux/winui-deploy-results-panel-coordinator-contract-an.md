# WinUI Deploy Results Panel Coordinator Contract (AN3)

**Purpose:** Define the narrow ownership contract for `DeployResultsPanelCoordinator` so Deploy can keep a shared results-panel seam without turning that seam into a new shared dumping ground.

**Status:** Approved implementation contract for Milestone AN.

**Scope:** Shared Deploy results-panel coordinator seam only.

**Out of scope:** General shared-helper policy, From Template runtime refactors, Quick Deploy runtime refactors, shell panel redesign, capability runtime implementation, and product behavior redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-182`..`FR-184`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-045`)
- `docs/02-ux/winui-shell-contract-aa.md`
- `docs/02-ux/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/winui-deploy-from-template-contract-af.md`
- `docs/02-ux/winui-deploy-on-the-fly-contract-ag.md`
- `docs/02-ux/winui-lane-architecture-standard-an.md`
- `docs/03-architecture/architecture.md`

---

## 1) Why this seam needs a hard contract

Deploy already has two lane-local surfaces that use the shell-owned right panel:
- `Deploy From Template`
- `Deploy Quick Deploy`

AH2 and AL already established that:
- the shell owns right-panel infrastructure
- the active workflow owns what the panel means and when it is relevant

Post-`#660`, `DeployResultsPanelCoordinator` became the shared Deploy seam that sits between those rules.

That is acceptable, but still risky without a hard contract:
- if panel title selection, auto-open signals, and lane delegation stay undocumented
- then future cleanup can quietly expand the coordinator into route logic, helper coordination, or lane workflow ownership

This contract exists to prevent that drift.

---

## 2) Desired end-state for the coordinator

`DeployResultsPanelCoordinator` is a narrow shared Deploy integration seam.

Its job is to coordinate shared Deploy right-panel intent only:
- which active Deploy lane supplies the current panel title
- whether the active Deploy state implies an auto-open recommendation
- whether Overview should show the shared empty state
- how shell-owned panel visibility/unavailability state is delegated back down to the relevant lane-local seams

It is not the shell right-panel owner.
It is not a lane workflow owner.
It is not the general shared Deploy host.

---

## 3) What the coordinator may own

The coordinator may own:
- shared Deploy results-panel intent aggregation across:
  - `deploy.overview`
  - `deploy.on_the_fly`
  - `deploy.from_template`
- active-lane-based right-panel title selection
- active-lane-based auto-open recommendation for the shell to consume
- shared Overview empty-state decision for the shell-owned panel region
- delegation of shell-owned panel state application into the relevant lane-local seams
- capability-switch reset hooks for lane-local panel presentation state when that reset is specifically about Deploy right-panel behavior

This is a shared integration seam.
It may coordinate which lane participates in the panel handoff, but it must stay focused on panel intent only.

---

## 4) What stays outside the coordinator

### 4.1 Shell-owned right-panel infrastructure stays outside

The coordinator must not own:
- shell panel open-state ownership
- shell panel width or column sizing
- shell compact fallback rules
- shell owner-capability precedence
- shell toggle enablement or shell container visibility
- shell host lifecycle

Those remain shell responsibilities under the existing shell contracts.

### 4.2 Lane-local workflow semantics stay outside

The coordinator must not own:
- lane-local readiness state
- lane-local deployment workflow state
- lane-local result-row or issue-row construction
- lane-local launcher button text semantics beyond reading already-exposed lane intent
- lane-local panel content meaning beyond choosing which lane's already-defined meaning is active

Those remain inside the lane-local seams.

### 4.3 Shared Deploy composition and helper policy stay outside

The coordinator must not own:
- shared Deploy route switching or route resolution
- general shared Deploy helper coordination
- reference-data refresh policy
- template-editor launch policy
- lane-specific refresh/reconcile rules unrelated to panel intent

If a concern is not specifically about shared Deploy results-panel intent, it does not belong here.

---

## 5) What shared Deploy may delegate to the coordinator

Shared Deploy composition or shell integration may delegate the following narrow questions to the coordinator:
- should the shell auto-open the panel now?
- what title text should the panel show for the current Deploy lane?
- should the shared empty state be visible while Deploy Overview is active?
- how should shell-owned panel visibility and compact-unavailable state be applied to the participating Deploy lanes?
- what lane-local panel state should reset when Deploy loses right-panel ownership on capability switch?

This delegation is acceptable because it keeps panel-intent branching out of `MainWindow` without widening shared Deploy composition into a workflow owner.

What shared Deploy must not delegate here:
- workflow coordination
- helper coordination
- route ownership
- capability-wide refresh policy unrelated to the panel

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `DeployResultsPanelCoordinator` becoming the shared Deploy workflow owner
- `DeployResultsPanelCoordinator` becoming the place where new Deploy helpers accumulate by convenience
- `DeployResultsPanelCoordinator` owning shell panel container mechanics
- `DeployResultsPanelCoordinator` owning lane-local progress, readiness, or result semantics
- `DeployResultsPanelCoordinator` becoming the owner of Deploy Overview meaning beyond the narrow shared empty-state decision
- `DeployResultsPanelCoordinator` becoming the place where future lane-specific exceptions are hidden instead of documented

If new logic is proposed for this seam, the first question should be:
- is this truly shared Deploy panel-intent integration?

If the answer is no, it belongs somewhere else.

---

## 7) Behavior that remains unchanged

This contract preserves the current rules that:
- the shell owns right-panel infrastructure
- Deploy owns right-panel content contract in the current scope
- Quick Deploy and From Template remain the lanes that provide progress/results panel meaning
- Deploy Overview may still surface the shared empty state without becoming a panel workflow owner
- Quick Deploy and From Template continue to own their lane-local launcher behavior, result semantics, and panel-specific view-state application

This is a seam-ownership issue, not a results UX redesign issue.

---

## 8) Sequencing implication

Later runtime slices may:
- keep `DeployResultsPanelCoordinator` as a dedicated narrow type
- move it under a later typed Deploy runtime boundary
- or replace it with another narrow shared seam

But any later implementation must preserve the ownership boundary defined here:
- panel intent aggregation may stay shared
- shell infrastructure must stay shell-owned
- lane-local workflow semantics must stay lane-local

---

## 9) Traceability

- `FR-182`
  - `DeployResultsPanelCoordinator` is a narrow shared Deploy panel-intent seam
- `FR-183`
  - shell infrastructure, lane-local workflow semantics, and general shared-helper policy stay outside the coordinator
- `FR-184`
  - shared Deploy may delegate panel title, auto-open, empty-state, apply-state, and reset hooks to the coordinator without widening it into a workflow owner

Mapped acceptance criteria:
- `AC-045`

---

## 10) Open Questions / TBDs

- `TBD:` Whether a later typed `DeployCapabilityRuntime` should own the coordinator directly or whether the coordinator remains a dependency of shared Deploy composition.
- `TBD:` Whether Deploy Overview will ever need richer panel participation than the current shared empty-state decision, or whether that should remain out of scope permanently.
