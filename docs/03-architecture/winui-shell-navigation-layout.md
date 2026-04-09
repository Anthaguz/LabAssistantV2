# WinUI Shell Navigation And Layout

**Purpose:** Define the current authoritative shell-surface rules for WinUI navigation, bounded layout, scroll ownership, and shell-level context presentation.

**Status:** Current authoritative WinUI architecture rule.

**Absorbs:**
- `docs/02-ux/Archived/winui-layout-constraints-contract.md`
- `docs/02-ux/Archived/winui-global-navigationview-contract-ac.md`
- surviving shell-surface/navigation/layout rules from `docs/02-ux/Archived/winui-shell-contract-aa.md`
- surviving shell-surface/navigation/layout rules from `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`

**Source basis:**
- `docs/01-requirements/srs.md` (`FR-074`..`FR-076`, `FR-097`, `FR-108`..`FR-112`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-009`, `AC-010`, `AC-017`, `AC-021`)

## 1) Core Rule

WinUI uses one bounded shell navigation and layout model.

That model must keep:
- shell navigation deterministic
- shell frame bounded
- capability workspaces responsible for their own primary content scroll
- right-panel use secondary and explicit

The shell surface must not drift into a mix of incompatible navigation patterns, unbounded page growth, or hover-only access paths.

## 2) Shell Surface Model

The shell surface consists of:
- top app/header region
- left navigation region
- main capability workspace host
- optional right panel owned as shell infrastructure

Required rules:
- the top header remains visible while capability content changes
- the main workspace is the primary bounded host for capability content
- left navigation and right-panel surfaces use explicit bounded widths while the main workspace remains fluid by default
- the shell frame itself is not the normal vertical scroll owner for dense capability content
- the right panel is collapsed by default unless an approved owner contract says otherwise

## 3) Global Navigation Rules

The shell uses a single global `NavigationView` in `LeftCompact` mode.

Required top-level capability set:
- `Machines`
- `Deploy`
- `Templates`
- `Assets`
- `Diagnostics`
- `Settings`

Required navigation behavior:
- route keys use canonical `capability.subview` format
- selecting a parent capability routes deterministically to the approved default child or workspace
- selecting a child route navigates directly to that child
- default startup route is `machines.overview`
- `Settings` is rendered as footer navigation, not mixed into the main capability list
- shell navigation remains keyboard- and pointer-accessible in expanded, collapsed, and compact forms

Expanded behavior:
- capability labels and hierarchy remain visible
- parent rows remain clickable

Collapsed and compact behavior:
- child-route access must not depend on hover-only behavior
- compact widths may replace a persistent rail with a hamburger-invoked drawer that shows the expanded capability tree
- when the compact drawer closes, workspace width returns to the active capability surface
- compact drawer dismissal remains explicit and discoverable, including close-on-dismiss interactions such as outside-click or `Esc`

## 4) Capability Navigation Support Rules

Shell navigation chooses capabilities.
Capability-local navigation chooses local child surfaces, overview tabs, editor states, and workflow-local destinations.

Approved Overview surfaces are not decorative.
When a capability uses an `Overview`, that surface should provide at least two of:
- route-entry choices
- useful summary
- attention, health, or issue signals

Current approved parent-click/default behavior:
- `Assets` routes to its approved `Overview` entry behavior
- `Deploy` routes to its approved `Overview` entry behavior
- `Diagnostics` routes to its approved `Overview` entry behavior
- `Machines` remains a single-surface capability for current scope
- `Templates` uses `templates.library` as the default primary surface

Current approved local-navigation exception:
- `templates.editor` is a canonical route, but it remains a workflow-state destination entered from explicit actions rather than a permanently exposed peer tab by default

Current approved local navigation models:
- `Assets` uses `Overview`, `Base Disks`, and `Switches` as the current local model; future `ISOs` remains an explicit later addition; Assets Overview remains primarily a summary-and-navigation surface rather than the place that absorbs the full operational action set
- `Deploy` uses `Overview`, `Quick Deploy`, and `From Template` in that order; Deploy Overview remains the route-entry chooser/index surface rather than a heavy deployment-history dashboard by default; `Quick Deploy` remains the direct configuration workflow and `From Template` remains a review/remediation/deploy workflow rather than a duplicate Quick Deploy editor
- `Diagnostics` uses `Overview` and `Logs`; Diagnostics Overview remains a lightweight support dashboard rather than a decorative analytics board
- `Machines` remains a single-surface capability and does not introduce local Overview or peer child tabs in the current scope
- `Templates` uses `Library` as the primary/default surface while `Editor` remains a workflow-state destination

## 5) Header And Context Presentation Rules

The shell header owns capability-level context presentation.

Required rules:
- shell header presents capability title and optional short capability description
- child views do not repeat page-level title/description bands by default
- child views may still use local section headers, tab labels, or workflow-state labels

Capability-local context should clarify the current child surface without duplicating shell-owned context chrome.

## 6) Bounded Layout And Scroll Ownership

The shell and migrated capability surfaces must follow explicit bounded-layout rules.

Required shell/layout rules:
- top-level capability hosts use bounded containers such as `Grid`, not unbounded root `StackPanel` layouts for dense screens
- primary content regions remain fluid by default; fixed heights are reserved for explicitly bounded utility surfaces
- one primary vertical scroll owner is defined per major surface
- nested scrolling is allowed only for bounded secondary regions such as long details or JSON payload areas
- the right panel owns its own internal vertical scroll when present
- long payload/detail content must scroll inside bounded child regions rather than stretching the parent surface indefinitely
- dense action/filter rows must preserve reachability through wrapping, grouping, or other bounded overflow handling instead of horizontal clipping
- status and feedback text must wrap or remain bounded without forcing uncontrolled parent growth

Rejected patterns:
- unbounded root layout for dense operational surfaces
- multiple competing sibling vertical scroll owners with no clear bounds
- horizontal clipping that hides primary actions
- uncontrolled full-page growth when the design intent is in-place scrolling

## 7) Compact, Normal, And Wide Behavior

Widths may vary, but the behavioral rule is stable:
- compact layouts preserve primary workflow reachability first
- secondary context collapses before primary workflow actions disappear
- normal layouts keep primary controls visible without relying on horizontal clipping
- wide layouts expose more simultaneous context but do not change routing or ownership rules
- compact and normal layouts keep primary action/filter controls keyboard-reachable

Compact behavior may use:
- wrapped action/filter rows
- focused master/detail drill-in behavior
- compact drawer navigation instead of a persistent narrow rail

## 8) First-Class Surface Coverage

The following surfaces remain explicit anchor examples for the shell/layout rule:

### Diagnostics Logs
- logs content owns the primary content scroll for the operational area
- long JSON/details payloads scroll inside bounded child regions
- filter and action controls must remain reachable at compact widths

### Machines
- machines workspace remains a bounded master/detail surface rather than an unbounded page
- list/details editing regions keep explicit scroll ownership
- compact behavior may prioritize list-first focus while preserving access to machine actions

### Templates
- template library/editor surfaces remain inside bounded capability layout
- library/details/editor content must not rely on unbounded vertical growth

### Deploy Right Panel
- deploy timeline/results content uses the shell right panel as bounded secondary context
- panel content scrolls in the panel rather than stretching the shell frame

## 9) Right Panel Support Rules

The right panel remains shell-owned infrastructure and secondary context only.

Required support rules:
- primary editing must remain possible in the main workspace when the panel is collapsed
- panel visibility and compact fallback remain shell-level infrastructure concerns
- panel meaning, titles, triggers, results, summaries, and issue context remain capability- or lane-owned
- deploy is the current approved capability that uses the panel as progress/results-first secondary context
- current `Assets`, `Machines`, `Templates`, and `Diagnostics` scope do not require right-panel dependence by default

## 10) Update Rule

If the durable WinUI shell navigation or layout rule changes, update this file in place.
Do not create a new milestone-coded shell/navigation/layout contract for a rule that should survive beyond the originating slice.

## Open Questions / TBDs

- `TBD:` Exact compact-width breakpoint values beyond the currently approved compact fallback behavior.
- `TBD:` Whether compact child-route reveal should remain entirely built-in `NavigationView` behavior or later adopt a stronger custom affordance without changing the non-hover rule.
