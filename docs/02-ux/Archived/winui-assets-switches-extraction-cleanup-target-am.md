# WinUI Assets Switches Extraction Cleanup Target (AM49)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Assets Switches` cleanup target so Switches runtime extraction starts from an explicit ownership boundary inside the refined shared Assets composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Assets Switches` extraction cleanup target only.

**Out of scope:** Base Disks extraction details, Overview extraction details, runtime implementation, Switches behavior redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-143`..`FR-145`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-032`)
- `docs/02-ux/Archived/winui-assets-switches-capability-contract-ak.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-assets-workspace-extraction-seam-am.md`
- `docs/02-ux/Archived/winui-assets-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-assets-overview-extraction-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-assets-base-disks-extraction-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Assets Switches needs its own cleanup target

AM9, AM10, AM39, and AM43 already define:
- the broad Assets extraction seam
- the shared Assets composition cleanup target
- the narrow Overview cleanup target
- the narrow Base Disks cleanup target

That is still not narrow enough for Switches runtime work.

Without a Switches-specific cleanup target, later slices could drift into:
- leaving Switches-specific state and orchestration trapped in shared Assets composition
- reintroducing `MainWindow` as a Switches host hub
- letting the shared `AssetsWorkspaceComposition` become the de facto Switches workflow owner
- letting Switches absorb Overview or Base Disks responsibilities because it is an operational child surface

AM49 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Assets Switches

The target is:
- `Assets Switches` remains under shared `AssetsWorkspaceComposition`
- shared Assets composition remains responsible only for shared capability-level composition concerns
- a Switches-local seam becomes the long-term home for Switches-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Switches-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Switches-specific coordination should converge behind a Switches-local seam, not stay in shared Assets composition and not move back to `MainWindow`.

---

## 3) What stays in shared Assets composition

Shared `AssetsWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Assets`
- shared route activation handoff across `assets.overview`, `assets.base_disks`, and `assets.switches`
- shared capability composition and wiring that spans more than one Assets child surface
- shared lifetime participation for the long-lived Assets workspace

This issue does not redefine the shared Assets owner created by AM10.
It narrows what should not remain trapped there once Switches extraction begins.

---

## 4) What becomes Switches-local

Switches-local ownership should include:
- Switches list, selection, draft, and feedback state
- Switches-specific orchestration for refresh, create, edit, validate, and delete flows
- Switches-local composition and interaction coordination used by the `assets.switches` surface
- Switches-specific refresh or reconcile behavior triggered by `assets.switches` activation

This keeps the Switches slice narrow:
- operational Switches management surface
- Switches-local ownership cleanup
- route-activation refresh within the existing Assets workspace

It does not turn Switches into the shared owner for all Assets behavior.

---

## 5) Switches host cleanup expectation

AM49 is also the cleanup target for Switches-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Switches-specific host bridges remain migration scaffolding only
- Switches-specific control exposure or host-driven UI coordination should reduce behind the Switches-local seam
- shared `AssetsWorkspaceComposition` must not become a permanent host proxy for Switches-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Assets Switches` becoming a new shared Assets god object
- shared `AssetsWorkspaceComposition` becoming the Switches workflow owner
- Switches taking ownership of Overview summary/navigation semantics
- Switches taking ownership of Base Disks semantics
- direct `MainWindow` injection into Switches views
- shell-owned host growth that treats Switches as a shell surface instead of an Assets-local surface

Switches is an Assets child surface with operational ownership for Switches behavior.
It is not the semantic owner of Overview, Base Disks, or shared Assets composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `assets.switches` as the operational Switches management surface
- approved AK Switches behavior for list, refresh, create, edit, validate, and delete
- approved AK validation taxonomy, delete guardrails, and logging expectations
- approved AL shell/header/navigation/layout behavior around the Switches surface
- existing Overview-first Assets behavior and route structure outside Switches ownership cleanup

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM49 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Assets-local seams are the correct way to cross shell boundaries when needed

For Switches, this means:
- Switches views do not talk to `MainWindow` directly
- shared Assets composition may coordinate with shell-owned behavior only through narrow seams
- Switches-local interaction coordination should not widen shared Assets composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Assets Switches` continues to participate in the long-lived Assets workspace.

That means:
- Switches is not recreated on every navigation
- activation of `assets.switches` refreshes or reconciles Switches state within the existing Assets workspace
- route activation remains the trigger for Switches refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Switches follow-up runtime slices should do this in order:
1. keep Switches under shared `AssetsWorkspaceComposition`
2. introduce the Switches-local seam
3. move Switches-specific state, orchestration, composition, and interaction coordination behind that seam
4. reduce temporary Switches-specific host bridges or control exposure behind the seam
5. keep Overview and Base Disks extraction concerns in their own narrow slices

This sequencing keeps AM50-AM54 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM49 does **not** define:
- Base Disks extraction details
- Overview extraction details
- runtime implementation
- Switches behavior redesign
- performance redesign

---

## 12) Traceability

- `FR-143`
  - Switches stays under shared Assets composition while converging Switches-specific ownership behind a Switches-local seam
- `FR-144`
  - behavior-preserving Switches role with long-lived workspace participation and `assets.switches` route-activation refresh
- `FR-145`
  - no direct `MainWindow` view dependency, no shared Assets workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-032`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM10` defined the shared Assets composition cleanup target
- `AM39` defined the Overview cleanup target inside shared Assets composition
- `AM11` through `AM14` remain the shared Assets refinement context that this narrower Switches target must inherit and not override
- `AK` and `AL` remain the behavioral baseline for Switches routing, operations, validation taxonomy, delete guardrails, logging expectations, and shell/view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact Switches-local seam type name during implementation.
- `TBD:` Whether Switches route activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Switches-specific dialog or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.

