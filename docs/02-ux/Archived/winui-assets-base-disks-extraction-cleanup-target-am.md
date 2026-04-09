# WinUI Assets Base Disks Extraction Cleanup Target (AM43)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Assets Base Disks` cleanup target so Base Disks runtime extraction starts from an explicit ownership boundary inside the refined shared Assets composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Assets Base Disks` extraction cleanup target only.

**Out of scope:** Switches extraction details, Overview extraction details, runtime implementation, Base Disks behavior redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-140`..`FR-142`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-031`)
- `docs/02-ux/Archived/winui-assets-base-disks-capability-contract-aj.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-assets-workspace-extraction-seam-am.md`
- `docs/02-ux/Archived/winui-assets-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-assets-overview-extraction-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Assets Base Disks needs its own cleanup target

AM9, AM10, and AM39 already define:
- the broad Assets extraction seam
- the shared Assets composition cleanup target
- the narrow Overview cleanup target

That is still not narrow enough for Base Disks runtime work.

Without a Base Disks-specific cleanup target, later slices could drift into:
- leaving Base Disks-specific state and orchestration trapped in shared Assets composition
- reintroducing `MainWindow` as a Base Disks host hub
- letting the shared `AssetsWorkspaceComposition` become the de facto Base Disks workflow owner
- letting Base Disks absorb Overview or Switches responsibilities because it is the operational child surface

AM43 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Assets Base Disks

The target is:
- `Assets Base Disks` remains under shared `AssetsWorkspaceComposition`
- shared Assets composition remains responsible only for shared capability-level composition concerns
- a Base Disks-local seam becomes the long-term home for Base Disks-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Base Disks-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Base Disks-specific coordination should converge behind a Base Disks-local seam, not stay in shared Assets composition and not move back to `MainWindow`.

---

## 3) What stays in shared Assets composition

Shared `AssetsWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Assets`
- shared route activation handoff across `assets.overview`, `assets.base_disks`, and `assets.switches`
- shared capability composition and wiring that spans more than one Assets child surface
- shared lifetime participation for the long-lived Assets workspace

This issue does not redefine the shared Assets owner created by AM10.
It narrows what should not remain trapped there once Base Disks extraction begins.

---

## 4) What becomes Base Disks-local

Base Disks-local ownership should include:
- Base Disks list, selection, draft, and feedback state
- Base Disks-specific orchestration for refresh, import/register, edit/save, validate, and remove flows
- Base Disks-local composition and interaction coordination used by the `assets.base_disks` surface
- Base Disks-specific refresh or reconcile behavior triggered by `assets.base_disks` activation

This keeps the Base Disks slice narrow:
- operational Base Disks management surface
- Base Disks-local ownership cleanup
- route-activation refresh within the existing Assets workspace

It does not turn Base Disks into the shared owner for all Assets behavior.

---

## 5) Base Disks host cleanup expectation

AM43 is also the cleanup target for Base Disks-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Base Disks-specific host bridges remain migration scaffolding only
- Base Disks-specific control exposure or host-driven UI coordination should reduce behind the Base Disks-local seam
- shared `AssetsWorkspaceComposition` must not become a permanent host proxy for Base Disks-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Assets Base Disks` becoming a new shared Assets god object
- shared `AssetsWorkspaceComposition` becoming the Base Disks workflow owner
- Base Disks taking ownership of Overview summary/navigation semantics
- Base Disks taking ownership of Switches semantics
- direct `MainWindow` injection into Base Disks views
- shell-owned host growth that treats Base Disks as a shell surface instead of an Assets-local surface

Base Disks is an Assets child surface with operational ownership for Base Disks behavior.
It is not the semantic owner of Overview, Switches, or shared Assets composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `assets.base_disks` as the operational Base Disks management surface
- approved AJ Base Disks behavior for list, refresh, import/register, edit/save, validate, and registry-only remove
- approved AJ validation and removal guardrail semantics
- approved AL shell/header/navigation/layout behavior around the Base Disks surface
- existing Overview-first Assets behavior and route structure outside Base Disks ownership cleanup

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM43 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Assets-local seams are the correct way to cross shell boundaries when needed

For Base Disks, this means:
- Base Disks views do not talk to `MainWindow` directly
- shared Assets composition may coordinate with shell-owned behavior only through narrow seams
- Base Disks-local interaction coordination should not widen shared Assets composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Assets Base Disks` continues to participate in the long-lived Assets workspace.

That means:
- Base Disks is not recreated on every navigation
- activation of `assets.base_disks` refreshes or reconciles Base Disks state within the existing Assets workspace
- route activation remains the trigger for Base Disks refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Base Disks follow-up runtime slices should do this in order:
1. keep Base Disks under shared `AssetsWorkspaceComposition`
2. introduce the Base Disks-local seam
3. move Base Disks-specific state, orchestration, composition, and interaction coordination behind that seam
4. reduce temporary Base Disks-specific host bridges or control exposure behind the seam
5. keep Overview and Switches extraction concerns in their own narrow slices

This sequencing keeps AM44-AM48 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM43 does **not** define:
- Switches extraction details
- Overview extraction details
- runtime implementation
- Base Disks behavior redesign
- performance redesign

---

## 12) Traceability

- `FR-140`
  - Base Disks stays under shared Assets composition while converging Base Disks-specific ownership behind a Base Disks-local seam
- `FR-141`
  - behavior-preserving Base Disks role with long-lived workspace participation and `assets.base_disks` route-activation refresh
- `FR-142`
  - no direct `MainWindow` view dependency, no shared Assets workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-031`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM10` defined the shared Assets composition cleanup target
- `AM39` defined the Overview cleanup target inside shared Assets composition
- `AM11` through `AM14` remain the shared Assets refinement context that this narrower Base Disks target must inherit and not override
- `AJ` and `AL` remain the behavioral baseline for Base Disks routing, operations, safety guardrails, and shell/view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact Base Disks-local seam type name during implementation.
- `TBD:` Whether Base Disks route activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Base Disks-specific dialog or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.

