# WinUI Shell and View Consistency Contract (AL1)

**Purpose:** Define the cross-view shell/content consistency rules approved after the PM audit of migrated WinUI surfaces.

**Status:** Approved implementation contract for Milestone AL.

**Scope:** WinUI shell plus migrated or actively migrating capability surfaces, especially `Machines`, `Deploy`, `Templates`, `Assets`, and `Diagnostics`.

**Out of scope:** Domain-semantic redesign, logging-signal redesign, new Hyper-V behavior, and full Settings product definition.

**Related:**
- `docs/01-requirements/srs.md` (`FR-108`..`FR-112`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-021`)
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/winui-shell-contract-aa.md`
- `docs/02-ux/winui-layout-constraints-contract.md`

---

## 1) Core shell/view rules

1. Shell owns capability-level context.
- Shell header presents capability title and optional short capability-level description.
- Child views do not repeat page-level title/description bands by default.
- Child views may use local section headers, tab labels, workflow-state labels, or editor-local breadcrumbs.

2. Navigation has two scopes.
- Shell navigation selects capabilities.
- Capability-local navigation handles overview tabs, child tabs, lists, focused editors, and workflow-state transitions inside the capability workspace.

3. Right panel is shell-owned infrastructure.
- Shell owns panel container, collapse lifecycle, compact fallback, and reset behavior.
- Active capability owns panel meaning, trigger placement, and count scoping when it uses the panel.

4. Compact mode protects workspace economy.
- Persistent narrow icon rail is not required in compact mode.
- Compact shell mode may use a hamburger-invoked navigation drawer instead of leaving the rail visible full-time.

---

## 2) Parent-click and shell navigation behavior

### 2.1 Expanded shell navigation
- Parent capability rows remain clickable.
- Child routes are visible beneath the parent when the shell shows expanded hierarchical navigation.
- If a capability has approved `Overview`, parent-click routes to `Overview`.
- If a capability has no approved `Overview`, parent-click routes to the default operational child/workspace.

### 2.2 Collapsed shell navigation
- Parent capability click remains deterministic.
- Collapsed behavior does not rely on hover/pop-up child-route choosers.
- If a capability has approved `Overview`, collapsed parent-click routes to `Overview`.
- Otherwise collapsed parent-click routes to the default operational child/workspace.

### 2.3 Compact shell navigation
- Compact widths may replace persistent left rail with a hamburger-invoked full navigation drawer.
- Drawer shows expanded capability navigation rather than tiny-rail hover affordances.
- When compact drawer closes, workspace width returns to the active capability.

---

## 3) Capability-local overview policy

Overview is not decorative. If present, it must provide at least two of:
- route entry points
- useful summary
- attention/health signals

### 3.1 Capabilities that use Overview now
- `Assets`
- `Deploy`
- `Diagnostics`

### 3.2 Capabilities that do not use Overview now
- `Machines`
- `Settings` (deferred as separate product-definition track)

### 3.3 Templates exception
- `Templates` does not use Overview in current scope.
- `Library` is the primary/default capability surface.
- `Editor` remains a workflow-state entered from explicit actions such as `New Template` or `Edit Template`.
- `templates.editor` may remain a canonical route for deep-linking and restore, but it is not treated as a permanently exposed peer tab by default.

---

## 4) Capability-local navigation contract

### 4.1 Assets
- Shell title: `Assets`
- Local tabs: `Overview`, `Base Disks`, `Switches`
- Future child tabs may include `ISOs`
- Parent `Assets` row acts as Overview entrypoint rather than adding a separate `Overview` child row in the shell

### 4.2 Deploy
- Shell title: `Deploy`
- Local tabs are ordered: `Overview`, `Quick Deploy`, `From Template`
- `Quick Deploy` is the direct configuration workflow
- `From Template` is a template-review/remediation/deploy workflow and shall not duplicate the Quick Deploy editor

### 4.3 Diagnostics
- Shell title: `Diagnostics`
- Local tabs: `Overview`, `Logs`
- Overview is a lightweight support dashboard, not a decorative analytics board

### 4.4 Machines
- Shell title: `Machines`
- Single-surface capability for current scope
- No local Overview or peer child tabs are introduced by AL

### 4.5 Templates
- Shell title: `Templates`
- `Library` is the primary/default surface
- `Editor` is workflow-state entry, not normal peer-tab navigation

---

## 5) Right panel contract refinement

### 5.1 General rule
- Right panel is for secondary context only.
- Primary editing must remain possible in the main workspace if the panel is collapsed.

### 5.2 Deploy-specific rule
- Deploy right panel is progress/results-first.
- Pre-run validation, warnings, and blocking issues should move inline into the main workflow surface.
- Workflow-local counts and panel triggers may live inside the active deploy child view instead of shell-global chrome.

### 5.3 Non-Deploy capabilities
- `Assets`, `Machines`, and current `Diagnostics`/`Templates` scope do not require right-panel dependence by default.
- Future capability-specific panel use must be justified by a real secondary-context need.

---

## 6) Action placement and iconography

### 6.1 Placement
- Actions live nearest to the state they affect.
- Inventory actions belong in inventory/list header context.
- Current-object actions belong in details/editor context.
- Workflow actions belong in the workflow control area and may remain text-capable if clarity requires it.
- Support actions stay secondary and do not compete with the primary action.

### 6.2 `New`
- `New` defaults to an inventory-level action.
- Triggering `New` clears the current details/editor region into a draft/create state.

### 6.3 Iconography
- Icon-first command chrome is preferred.
- Tooltips provide descriptive labels.
- Delete defaults to trash-can iconography with confirmation dialog as safety layer.
- Save/apply may use icon-first affordances where clarity remains sufficient.

---

## 7) Compact layout and scroll priorities

### 7.1 Scroll ownership
- Shell frame does not scroll.
- Right panel owns its own internal scroll.
- Operational master/detail and workflow surfaces use bounded list/editor/details scroll owners rather than unbounded full-page growth.
- Overview pages may use more page-like scrolling when appropriate.

### 7.2 Compact focus behavior
- Compact layouts should preserve the primary workflow region first.
- Secondary context collapses before core workflow actions disappear.
- `Machines` uses list-first compact behavior and drills into VM detail focus.
- `Quick Deploy` may use focused VM editor drill-through in compact mode.
- Focused category editing should replace the editor region, not the entire capability surface, when width allows the primary list/context to remain visible.

---

## 8) Capability-specific consistency notes

### 8.1 Assets Overview
- Mostly summary + navigation.
- Inventory counts and asset-health/attention summary are appropriate.
- Do not overload Overview with the full operational action set.

### 8.2 Deploy Overview
- Primarily chooser/index between deploy modes.
- May include lightweight recent/temporary-draft shortcuts.
- Should not become a heavy deployment-history dashboard by default.

### 8.3 Diagnostics Overview
- Lightweight recent issue/support dashboard.
- May include compact per-capability issue breakdown when useful.
- Full analytics/graph expansion is deferred unless it provides real operational value.

### 8.4 Quick Deploy issue signaling
- Pre-run issues should move toward field-level, group-level, and VM-row-level signaling.
- VM rows may show warning/blocking icons with hover detail.
- Shared workflow summary near the primary deploy action may still exist as a compact readiness line.

### 8.5 From Template remediation model
- Shared dependency issues should be grouped where possible.
- Safe one-to-many remapping is allowed when the issue is a shared environmental compatibility problem.
- Broader structural fixes route to Template Editor.
- Future switch-creation-from-template semantics remain deferred to a separate docs-first contract.

---

## 9) Open Questions / TBDs

- `TBD:` Whether the shell breadcrumb/current-route text should be removed, minimized, or repurposed once capability-local tabs and workflow-local breadcrumbs are implemented.
- `TBD:` Exact compact-width breakpoint values beyond already approved panel-threshold decisions.
- `TBD:` Final Diagnostics Logs filter UX shape once logging-signal quality and category taxonomy are revisited.
- `TBD:` Logging signal depth/verbosity expectations for Diagnostics Overview and Logs once a separate diagnostics-signal workstream is approved.
- `TBD:` Whether template editor-local structure should expose explicit `Metadata` / `Machines` local sections or a different editor-local navigation model.
