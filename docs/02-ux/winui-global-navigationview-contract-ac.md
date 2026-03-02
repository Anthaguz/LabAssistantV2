# WinUI Global NavigationView Contract (Milestone AC)

**Purpose:** Define the implementation contract for migrating WinUI shell navigation to a single global `NavigationView` with hierarchical entity/action routing.

**Status:** Approved contract for Milestone AC implementation.

**Related:**
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/winui-shell-contract-aa.md`
- `docs/02-ux/ui-migration-execution-plan.md`
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`

---

## 1. Contract Scope

This contract covers global shell navigation behavior only:
- top-level entity navigation
- entity child-action navigation
- routing keys and startup route
- compact/expanded menu behavior
- shell placement for `Settings`

This contract does not define feature-page implementations for Deploy/Templates/Assets/etc.

---

## 2. Navigation Control Model

WinUI shell shall use a single global `NavigationView` in `LeftCompact` mode.

Required behavior:
- compact state shows icons
- hamburger expands to show labels and hierarchy
- navigation remains keyboard and pointer accessible

The existing top app bar remains in place for this slice.

---

## 3. IA Shape and Routing Contract

### 3.1 Top-Level Entities

Required top-level entities:
- `Machines`
- `Deploy`
- `Templates`
- `Assets`
- `Diagnostics`
- `Settings` (footer placement)

### 3.2 Parent/Child Behavior

- Entity parents may contain child actions/subviews.
- Selecting a parent routes to that entity's default child.
- Selecting a child routes directly to that child.

### 3.3 Route Key Format

Canonical route key format is:
- `capability.subview`

Examples:
- `machines.overview`
- `deploy.on_the_fly`
- `templates.library`
- `assets.disks`

### 3.4 Startup Route

Default startup route is:
- `machines.overview`

Last-selected route persistence is not enabled in this slice.

---

## 4. Collapsed/Expanded Interaction Contract

### 4.1 Expanded

- Expanded navigation shows parent entities with visible child actions.
- Parent expand/collapse behavior shall be explicit and discoverable.

### 4.2 Collapsed

- In compact icon-only state, selecting an entity icon must expose that entity's child actions (flyout or equivalent compact affordance).
- Navigation must not require hover-only behavior for access to child actions.

---

## 5. Visual/Placement Contract

- `Settings` is rendered as a footer cog entry (not mixed into main entity list).
- Current top bar remains visible and is not replaced by this change.
- Breadcrumbs are not required in this slice; active nav highlighting is the required context signal.

---

## 6. Out of Scope (AC Navigation Contract)

- Settings inner page navigation model (for example top tabs)
- Final breadcrumbs strategy across all capabilities
- Capability-specific page redesign
- Feature parity completion for all child routes

---

## 7. Open Questions / TBDs

- `TBD:` Should compact child-action reveal use built-in `NavigationView` flyout behavior only, or custom flyout styling for stronger discoverability?
- `TBD:` Should route persistence be introduced in a later slice, and if yes, should it persist by capability only or full `capability.subview` route?
