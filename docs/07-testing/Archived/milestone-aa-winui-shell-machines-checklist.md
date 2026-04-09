# Milestone AA - WinUI Shell + Machines Verification Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


Purpose: Manual verification checklist for Milestone AA closure, covering WinUI shell foundation behavior, capability-local navigation scaffolding, and Machines v1 action/policy behavior.

This checklist complements automated tests by validating real WinUI interaction behavior on a host machine.

## Prerequisites

- Windows machine with Hyper-V enabled
- At least one running VM and one stopped VM visible in Hyper-V
- App build that includes merged Milestone AA work (`#265`, `#266`, `#274`, `#267`)
- Access to logs folder (`<LogFolder>\structured-events.jsonl`)

## A. Shell behavior (AA shell contract)

### A1. Top bar visibility and drawer behavior
- Launch WinUI app: `.\build-run-winui.ps1`
- Open capability drawer via hamburger
- Verify:
  - drawer slides in (no abrupt pop)
  - top bar stays visible while drawer is open
  - hamburger remains visible and usable while drawer is open
  - drawer width appears fixed and does not cover the full window width

### A2. Drawer dismiss channels
- With drawer open:
  - click outside on scrim
  - press `Esc`
- Verify both dismiss the drawer cleanly.

### A3. Theme readability sanity
- In Light theme and Dark theme:
  - verify top bar icons/text remain readable
  - verify left rail icons are visible
  - verify primary action text is readable on hover/focus states

### A4. Insights default state
- Launch app fresh.
- Verify insights panel is collapsed by default.
- If issue badge count is present, verify badge appears without opening panel automatically.

## B. Navigation model (global capability vs local subview)

### B1. Capability vs subview separation
- Verify left rail + drawer only switch capabilities (`Machines`, `Deploy`, `Templates`, `Assets`, `Diagnostics`, `Settings`).
- Verify subview selector is shown inside workspace (not in global rail/top bar).

### B2. Capability/subview context transitions
- Move to `Deploy`, switch between `On-the-fly` and `From Template`.
- Move away to another capability, then back to `Deploy`.
- Verify:
  - capability switch loads that capability default subview
  - breadcrumb/context text updates to `Capability > Subview`
  - local toolbar placeholder actions update with active subview

### B3. Startup routing
- Close and relaunch app.
- Verify startup lands on `Machines` default context.
- Verify app does not restore last selected capability/subview across restarts.

## C. Machines v1 behavior

### C1. Inventory load + selection details
- Navigate to `Machines > Overview`.
- Verify host VM list loads (LabAssistant + external VMs where present).
- Select different VMs.
- Verify details panel updates (name/state/origin/vmId/path).

### C2. Action behavior
- For a selected VM:
  - `Start`
  - `Stop`
  - `Restart`
  - `Open Console`
- Verify each action gives explicit status feedback and no silent failure path.

### C3. Delete scope + confirmation policy gate
- Select a VM and click `Delete`.
- Verify dialog requires:
  - delete scope selection:
    - `VM registration only`
    - `VM + associated disks/files`
  - explicit confirmation before Delete is enabled
- Cancel once to verify safe cancellation path.
- Execute once to verify scope is honored and result feedback is shown.

### C4. RDP policy gate
- Verify `Open RDP` action is visible.
- Verify it is disabled.
- Verify explanatory reason text indicates readiness/policy is not implemented yet.

## D. Structured diagnostics/logging verification

### D1. Machines action logging
- Perform at least one machine action (start/stop/restart/open console/delete).
- Inspect `<LogFolder>\structured-events.jsonl`.
- Verify events include operation context:
  - `operationId`
  - VM context fields (`vmName`, `vmId` when available)
  - action name
  - result (`started` / `success` / `failed`)

### D2. Delete scope logging
- Perform delete action with each scope at least once.
- Verify relevant delete events include `deleteScope`:
  - `vm_registration_only`
  - `vm_and_storage`

## E. Explicit deferred items (expected for AA)

- RDP readiness detection/enablement is deferred (`#261`).
- Deploy/Templates/Assets feature migration is not part of AA closure.
- No packaging/MSIX validation is required for AA.
- Logs UI viewer (`#215`) is deferred.

## Recording results

For each section:
- Commit/build tested
- Environment notes (Windows version, Hyper-V state)
- Pass / Fail
- Any regression details and reproduction steps


