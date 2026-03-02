# WinUI Layout Constraints and Scroll Ownership Contract (AB1)

**Purpose:** Define enforceable layout constraints for WinUI shell and capability surfaces so overflow and scrolling behavior remain predictable as migration work continues.

**Status:** Approved implementation contract for Milestone AB.

**Scope:** WinUI shell and first migrated capability surfaces (`Machines`, `Diagnostics > Logs`, `Templates`).

**Out of scope:** Feature behavior changes, runtime orchestration changes, visual theme redesign.

**Related:**
- `docs/02-ux/winui-shell-contract-aa.md`
- `docs/02-ux/ui-migration-execution-plan.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/01-requirements/acceptance-criteria.md` (`AC-006`, `AC-007`, `AC-008`, `AC-009`)

---

## 1) Shell Region Sizing Strategy

All WinUI shell pages shall follow the same top-level region sizing model:

1. **Top bar** (`Auto` height)
- Fixed visible header row.
- Must remain visible when drawers, panels, or content panes are open.

2. **Main workspace row** (`*` height)
- Owns all scrollable feature content.
- Must not be replaced with unbounded parent containers (for example, root `StackPanel`).

3. **Left icon rail** (fixed width)
- Fixed width; never scrolls independently.

4. **Capability drawer** (fixed width)
- Fixed width (`280px` standard unless explicitly changed by contract).
- Opens inside workspace row; does not cover top bar.

5. **Right insights panel** (fixed width, collapsed by default)
- Width bounded; internal content may scroll when needed.

6. **Content host**
- Must use bounded layout containers (`Grid` with `*` rows/columns preferred).
- Surface-level children cannot rely on infinite-height measurement.
- Primary content regions are fluid by default (`*` sizing). Fixed heights are allowed only for explicitly bounded utility surfaces and must be documented per-view.

---

## 2) Scroll Ownership Rules

Each major surface must define a single primary scroll owner.

1. **Primary scroll owner**
- One container per surface owns vertical scroll for the main content area.
- Typical owner: `ScrollViewer` around content host or a control with built-in scrolling (`ListView`, `TreeView`, etc.).

2. **Allowed nested scroll**
- Nested scroll is allowed only for bounded secondary content:
  - long JSON/details text panes
  - long status/history sections
- Nested scroll region must have explicit bounds (`MaxHeight`, fixed row, or `*` row inside bounded parent).

3. **Disallowed patterns**
- Unbounded `StackPanel` as top-level content host for complex screens.
- Multiple sibling vertical scroll owners competing for wheel focus in same region without explicit bounds.
- Controls growing parent height to reveal all content when the design intent is in-place scrolling.

4. **Ownership declaration requirement**
- New complex surface PRs must state:
  - primary scroll owner
  - any nested scroll owners and their bounds

---

## 3) Overflow Handling Rules

1. **Actionable controls must remain reachable**
- Buttons/inputs used to apply user actions cannot be hidden by horizontal overflow.
- For dense rows (filters/actions), use wrapping, multi-row grid grouping, or overflow menu pattern.

2. **Filter/action rows**
- Must support compact widths without hiding primary actions.
- Use explicit min widths and wrapping strategy; do not rely on horizontal clipping.

3. **Long payload rendering**
- Long text/JSON/error payloads must scroll in bounded child regions.
- Parent panel must remain dimensionally stable.

4. **Status text behavior**
- Status/error banners should wrap within bounded region and not force uncontrolled parent growth.

---

## 4) Dense Form and Panel Guidelines

1. **Grouping**
- Group related fields into rows/sections with clear labels.
- Keep action controls near the state they affect.

2. **Action placement**
- Primary actions (`Apply`, `Reload`, `Run`) must be visible without horizontal scrolling.
- Secondary actions can move to overflow/menu only when explicitly designed.

3. **Status placement**
- Place status near related actions or section header.
- Status should not displace critical controls out of viewport.

4. **Details panes**
- Envelope/summary areas can be capped with `MaxHeight`.
- Full details payload area should use bounded, scrollable region.

---

## 5) Resize Behavior Bands

Define expected behavior by width bands (exact pixel values may be tuned in implementation):

1. **Compact**
- Filter/action bars wrap to multiple rows.
- Secondary metadata may collapse to summary text.
- No hidden primary controls.

2. **Normal**
- Primary controls visible in 1-2 rows.
- Content list + details panes remain simultaneously usable.

3. **Wide**
- Full list/details layout visible without collapsing.
- Scroll appears only for content volume, not layout defects.

---

## 6) Accessibility and Interaction Sanity

1. **Keyboard reachability**
- All filter/action controls must be reachable by tab order in compact and normal widths.

2. **Discoverability**
- Icon-first controls require tooltip or text label fallback.
- Disabled critical actions should expose reason text/tooltips where relevant.

3. **No hidden state actions**
- Users should not need horizontal scrolling to find required commands.

---

## 7) First-Class Surface Rules (AB baseline)

## 7.1 Diagnostics > Logs
- Primary scroll owner: logs content region.
- Log list and details area must stay bounded in viewport.
- Long context JSON must scroll in-place, not stretch parent panel.
- Filter actions (`Apply`, `Clear`, `Reload`, `Open raw`) must remain visible in compact widths via wrapping/multi-row layout.

## 7.2 Machines
- Primary scroll owner: machines workspace content region.
- VM list/details/actions area must remain visible and operable under compact and normal widths.
- Dense edit sections (CPU/memory/network) must use bounded sections and avoid pushing action row out of viewport.

## 7.3 Templates
- Primary scroll owner: template library list region.
- Library content region must expand with available workspace height (no fixed list-height containers).
- Template list must keep actions visible while list/details metadata remain readable in compact and wide layouts.

---

## 8) Anti-Patterns Checklist (Do Not Merge)

- Root surface built with unbounded vertical `StackPanel` for complex layouts.
- Any bug where primary action buttons are off-screen due to horizontal overflow.
- Long payload causing parent panel to exceed viewport without internal scroll.
- Ambiguous or undocumented scroll ownership in new complex surface.

---

## 9) AB2 Implementation Checklist

AB2 must implement all of the following:

1. Decompose shell-hosted capability content out of `MainWindow.xaml`:
- Start with:
  - Diagnostics Logs view
  - Machines overview/details view

2. Apply explicit scroll ownership:
- Document primary and nested scroll owners in PR notes.

3. Enforce bounded layout:
- Replace unbounded parent containers with bounded `Grid`-based composition where needed.

4. Validate resize behavior:
- Manual checks in compact/normal/wide bands.

5. Regression-test interactions:
- Ensure no loss of:
  - filter/action reachability
  - keyboard navigation
  - status visibility
  - existing feature semantics

---

## 10) Known Limitations / Deferred Items

- Exact pixel breakpoints may evolve after AB2 visual review.
- This contract does not define final visual style/theme tokens.
- This contract does not change feature behavior or business logic semantics.
