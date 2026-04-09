# WinUI Assets Overview Extraction Cleanup Target (AM39)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Assets Overview` cleanup target so Overview runtime extraction starts from an explicit ownership boundary inside the refined shared Assets composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Assets Overview` extraction cleanup target only.

**Out of scope:** Base Disks extraction details, Switches extraction details, runtime implementation, Overview redesign beyond ownership cleanup, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-137`..`FR-139`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-030`)
- `docs/02-ux/Archived/winui-assets-workspace-extraction-seam-am.md`
- `docs/02-ux/Archived/winui-assets-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Assets Overview needs its own cleanup target

AM9 and AM10 already define:
- the broad Assets extraction seam
- the shared Assets composition cleanup target

That is still not narrow enough for Overview runtime work.

Without an Overview-specific cleanup target, later slices could drift into:
- leaving Overview-specific coordination in shared Assets composition
- reintroducing `MainWindow` as an Overview host hub
- letting Overview absorb Base Disks or Switches concerns because it is the route-entry surface

AM39 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Assets Overview

The target is:
- `Assets Overview` remains under shared `AssetsWorkspaceComposition`
- shared Assets composition remains responsible only for shared capability-level composition concerns
- an Overview-local seam becomes the long-term home for Overview-specific state and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Overview-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Overview-specific coordination should converge behind an Overview-local seam, not stay in shared Assets composition and not move back to `MainWindow`.

---

## 3) What stays in shared Assets composition

Shared `AssetsWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Assets`
- shared route activation handoff across `assets.overview`, `assets.base_disks`, and `assets.switches`
- shared capability composition and wiring that spans more than one Assets child surface
- shared lifetime participation for the long-lived Assets workspace

This issue does not redefine the shared Assets owner created by AM10.
It narrows what should not remain trapped there once Overview extraction begins.

---

## 4) What becomes Overview-local

Overview-local ownership should include:
- Overview summary state
- Overview-local navigation coordination
- Overview-local interaction boundaries used by the Overview surface
- Overview-specific refresh/reconcile behavior triggered by `assets.overview` activation

This keeps the Overview slice narrow:
- summary
- entry/index behavior
- local interaction coordination

It does not turn Overview into the shared owner for all Assets behavior.

---

## 5) What must not happen

The following outcomes are explicitly rejected:
- `Assets Overview` becoming a new shared Assets god object
- Base Disks behavior or Switches behavior being re-homed under Overview
- direct `MainWindow` injection into Overview views
- shell-owned host growth that treats Overview as a shell surface instead of an Assets-local surface

Overview is an Assets child surface with a summary/navigation role.
It is not the semantic owner of Base Disks or Switches.

---

## 6) Behavior that must remain unchanged

The cleanup target must preserve:
- `assets.overview` as the route-entry and index surface for `Assets`
- Overview as primarily a summary and navigation surface
- approved Overview-first Assets behavior
- existing Base Disks and Switches semantic ownership outside Overview

This remains an ownership cleanup issue, not a redesign issue.

---

## 7) MainWindow and interaction boundary rule

AM39 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Assets-local seams are the correct way to cross shell boundaries when needed

For Overview, this means:
- Overview views do not talk to `MainWindow` directly
- shared Assets composition may coordinate with shell-owned behavior only through narrow seams
- Overview-local interaction coordination should not widen shared Assets composition or shell host responsibilities

---

## 8) Workspace lifetime and route activation rule

`Assets Overview` continues to participate in the long-lived Assets workspace.

That means:
- Overview is not recreated on every navigation
- activation of `assets.overview` refreshes or reconciles Overview state within the existing Assets workspace
- route activation remains the trigger for Overview refresh/reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 9) Sequencing implication

Overview follow-up runtime slices should do this in order:
1. keep Overview under shared `AssetsWorkspaceComposition`
2. introduce the Overview-local seam
3. move Overview-specific state and UI coordination behind that seam
4. keep Base Disks and Switches extraction concerns in their own later narrow slices

This sequencing keeps AM40-AM42 narrow and avoids reintroducing shared-owner ambiguity.

---

## 10) Non-goals

AM39 does **not** define:
- Base Disks extraction details
- Switches extraction details
- runtime implementation
- Overview redesign beyond ownership cleanup
- performance redesign

---

## 11) Traceability

- `FR-137`
  - Overview stays under shared Assets composition while converging Overview-specific ownership behind an Overview-local seam
- `FR-138`
  - behavior-preserving Overview role with long-lived workspace participation and route-activation refresh
- `FR-139`
  - no direct `MainWindow` view dependency, no Overview god object growth, and explicit non-goals

Mapped acceptance criteria:
- `AC-030`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM10` defined the shared Assets composition cleanup target
- `AM11` through `AM14` remain the shared Assets refinement context that this narrower Overview target must not override

---

## 12) Open Questions / TBDs

- `TBD:` Exact Overview-local seam type name during implementation.
- `TBD:` Whether Overview summary refresh remains entirely route-activation-driven or later needs a more explicit local refresh policy.
- `TBD:` Whether any Overview-local action affordance needs a narrower shell-facing abstraction during runtime extraction.

