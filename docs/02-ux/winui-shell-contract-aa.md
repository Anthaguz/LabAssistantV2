# WinUI Shell Contract (Milestone AA)

**Purpose:** Define the implementation contract for the first production-grade WinUI shell slice after Milestone Y decisions.

**Status:** Approved contract for Milestone AA implementation.

**Related:**
- `docs/02-ux/ui-framework-decision-record-y3.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/ui-migration-execution-plan.md`
- `docs/01-requirements/machines-capability-contract.md`

---

## 1. Execution Model (Approved)

- Keep current WPF app (`LabAssistant`) as production baseline.
- Add WinUI app as a parallel project (`LabAssistant.WinUI`).
- WPF policy during AA: bugfix-only.
- WinUI policy during AA: unpackaged implementation path.
- AA migration sequence:
  1. Shell foundation
  2. Machines page v1

---

## 2. Shell Layout Contract

The shell must provide these regions:

1. Top app bar
2. Left icon rail
3. Slide-out navigation drawer (hamburger-triggered)
4. Main content host
5. Right insights panel (collapsed by default)

---

## 3. Navigation Contract

### 3.1 Left Navigation

- Left rail shows capability icons.
- Primary navigation expansion is hamburger-triggered drawer (not hover-triggered full menu).
- Hover is allowed for lightweight tooltips only.
- Drawer behavior:
  - slide in from left
  - show scrim over remaining shell
  - close on outside click
  - close on `Esc`

### 3.2 Capability Set

- Machines
- Deploy
- Templates
- Assets
- Diagnostics
- Settings

### 3.3 Startup and Persistence

- Default landing capability: `Machines`.
- Do not persist last selected capability across app restarts.

---

## 4. Content Interaction Contract

### 4.1 Machines Page (first real page after shell)

- Default layout: side-by-side list and details.
- Left content subpanel: VM list with key status characteristics.
- Main details subpanel: active editor/workspace for selected VM.

### 4.2 Section-Based Editing (No Legacy Collapsible Menus)

Use section navigation in details area, not old collapsible stacks.

Example sections:
- Overview
- Hardware (CPU + Memory combined)
- Storage
- Network
- Guest OS

### 4.3 Breadcrumb Contract

Breadcrumb reflects details-pane context:
- `Machines`
- `Machines > <VM Name>`
- `Machines > <VM Name> > <Section>`

When no VM is selected:
- `Machines > Overview`

---

## 5. Right Insights Panel Contract

- Right panel is closed by default.
- Triggered by warning/issue icon in shell.
- If there are active issues, icon displays a visible count badge.
- AA scope includes shell placement + toggle behavior, not full diagnostics feature parity.

---

## 6. Responsive Density Contract

Target behavior:

- Primary desktop target: `1920x1080`.
- Support compact behavior at reduced widths without breaking workflow.

Recommended shell breakpoints:

- `>=1600`: 3-column capable layouts (content + optional insights panel)
- `1200-1599`: 2-column layouts (insights collapsed by default)
- `<1200`: single-focus mode with panel toggles/overlays

---

## 7. Theme Foundation Contract

AA must include centralized theme tokens in WinUI:

- light and dark dictionaries
- semantic color/brush tokens (no hardcoded page-level foreground/background colors)
- runtime theme switching hook

Provisional palette direction for AA:

- Slate + Blue

Final brand palette is explicitly deferred.

---

## 8. Explicit Out of Scope (AA)

- Full Deploy/Templates/Assets page migrations
- Final visual brand system
- RDP readiness detection logic (RDP button remains visible but disabled in Machines v1)
- Advanced Hyper-V settings editor/parity with all MMC dialogs

---

## 9. Open Questions / TBDs

- `TBD:` Templates capability inner layout contract (library/details/editor arrangement in WinUI shell).
- `TBD:` Assets capability inner layout contract (catalog/switches composition in WinUI shell).
- `TBD:` Diagnostics capability final right-panel vs page-level ownership split once Logs UI scope is resumed.
