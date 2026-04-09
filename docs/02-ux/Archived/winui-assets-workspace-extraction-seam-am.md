# WinUI Assets Workspace Extraction Seam (AM9)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the Assets-specific extraction seam for AM so `Assets Overview`, `Base Disks`, and `Switches` can move out of `MainWindow` ownership without repeating the earlier shell-heavy pattern discovered in Machines.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Assets` extraction seam only.

**Out of scope:** Runtime extraction implementation, Assets workflow redesign, per-navigation workspace recreation, domain-semantic redesign, and performance tuning.

**Related:**
- `docs/01-requirements/srs.md` (`FR-131`..`FR-133`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-028`)
- `docs/02-ux/Archived/winui-assets-base-disks-capability-contract-aj.md`
- `docs/02-ux/Archived/winui-assets-switches-capability-contract-ak.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Assets is next

`Assets` is the next extraction target because it now has:
- a stable Overview-first navigation model
- two meaningful operational child surfaces
  - `Base Disks`
  - `Switches`
- enough complexity to prove the AM33 composition model beyond the single-surface Machines case

Unlike Machines, Assets should start from the refined target:
- capability-local workspace composition from the beginning
- not a temporary shell-heavy pattern first and a cleanup later

---

## 2) What should move behind the Assets seam

Assets-local ownership should include:
- local Assets workspace composition
- Overview summary and navigation state
- Base Disks state and orchestration
- Switches state and orchestration
- Assets-local refresh/reconcile rules for route activation
- Assets-local status and feedback state

These should stop being long-term shell-owned concerns in `MainWindow`.

---

## 3) What remains shell-owned

Shell still owns:
- route and capability resolution
- shell header/title/description
- shell compact navigation mode
- right-panel infrastructure
- shell host visibility
- app-level workspace lifetime

The Assets seam does not move shell composition responsibilities away from `MainWindow`.

---

## 4) Assets-specific behavior that must not change

Assets extraction must preserve:
- `Assets` as an Overview-first capability
- canonical child routes:
  - `assets.overview`
  - `assets.base_disks`
  - `assets.switches`
- current route-bound local tab/navigation behavior
- `Assets Overview` as mostly summary + navigation

### Base Disks preservation
- current in-context operational surface
- current validation/readiness presentation
- current remove guardrails and registry/catalog semantics

### Switches preservation
- current in-context operational surface
- current CRUD and validation behavior
- delete blocked when any VM is attached, regardless of power state

AM9 is not an Assets redesign issue.

---

## 5) Composition target for Assets

Assets should adopt the refined AM33 model from the start:
- `MainWindow` hosts the Assets workspace lifetime and route visibility
- an Assets-local composition owner becomes the long-term home for:
  - Overview
  - Base Disks
  - Switches
  - local navigation coordination
  - view/controller/state composition

Shell-owned host bridges remain temporary only if needed.

This is the explicit correction learned from Machines.

---

## 6) Workspace lifetime and route activation

Assets remains long-lived during the app session.

That means:
- the Assets workspace is not recreated on every navigation
- route activation refreshes or reconciles state when needed
- switching between Overview, Base Disks, and Switches should continue to use route-bound activation rather than full workspace reconstruction

Future lifetime changes remain a `TBD`.

---

## 7) Interaction boundary implication

AM9 inherits the AM2 and AM33 rules:
- bindings/commands first
- limited narrow view-local events allowed where justified
- views must not receive `MainWindow` directly
- shell must not become the long-term Assets composition hub

For Assets, this means:
- local navigation/view coordination should converge behind the Assets-local workspace composition
- not through widening `MainWindow` capability-specific host responsibilities

---

## 8) Sequencing from this seam

This seam exists to support:
1. `AM10` - extract Assets Overview and shared workspace state from `MainWindow`
2. `AM11` - extract Base Disks workspace state and orchestration
3. `AM12` - extract Switches workspace state and orchestration
4. `AM13` - reduce Assets view control exposure
5. `AM14` - converge Assets UI tests to the extracted seams

If AM9 is not explicit, the later slices will guess or drift back toward shell-heavy composition.

---

## 9) Non-goals

AM9 does **not** introduce:
- new Base Disks semantics
- new Switches semantics
- new Assets Overview product behavior beyond the approved AL contract
- per-navigation workspace recreation
- performance tuning as a separate concern

---

## 10) Traceability

- `FR-131`
  - Assets-local state/orchestration extraction seam
- `FR-132`
  - refined capability-local composition target from the start
- `FR-133`
  - preserved Assets/Overview/Base Disks/Switches behavior with long-lived workspace lifetime

Mapped acceptance criteria:
- `AC-028`

---

## 11) Open Questions / TBDs

- `TBD:` Exact Assets-local composition-owner type during implementation.
- `TBD:` Whether Assets Overview summary data should remain lazy/current-state-driven during extraction or gain a more explicit refresh contract later.
- `TBD:` Whether any shell-hosted dialog helpers remain temporarily necessary during Base Disks or Switches extraction.

