# Milestone AM Assets Extraction Checklist

## Purpose

Manual runtime verification checklist for Milestone AM (`Assets extraction closure`).

Use this checklist after AM automation passes to confirm the final shared Assets workspace model, Overview lane, Base Disks lane, and Switches lane remain usable on a real Windows machine.

## Scope

- Shared Assets workspace ownership and Overview-first routing
- Overview summary and navigation sanity
- Base Disks operational sanity
- Switches operational sanity
- Shell/title ownership sanity for Assets
- Long-lived Assets workspace and route-switching sanity

## Out of Scope

- Runtime redesign
- Business/domain semantic changes
- WPF behavior
- New CI/workflow behavior
- Logging redesign

## Preconditions

- Build under test is from the target AM55 closure commit/branch
- WinUI app launches successfully
- At least one base disk exists if Base Disks operational flows are to be exercised fully
- At least one virtual switch exists, or the tester is prepared to create one
- At least one VM attached to a switch is available for blocked-delete verification if practical

## 1. Assets Overview-First Behavior

- Open parent `Assets` from the global navigation
- Confirm it resolves to `assets.overview`
- Confirm the shell title/description represent the capability context rather than a child view owning a duplicate page banner
- Confirm local navigation exposes:
  - `Overview`
  - `Base Disks`
  - `Switches`
- Confirm `Overview` is selected first when entering Assets from outside the capability

## 2. Overview Summary / Navigation Surface Sanity

- Stay on `Assets > Overview`
- Confirm the summary surface shows Base Disks and Switches status/count information
- If Base Disks or Switches are loading, confirm the summary text reflects loading rather than stale zero-state wording
- Click the Base Disks overview action
- Confirm navigation moves to `assets.base_disks`
- Return to `Assets > Overview`
- Click the Switches overview action
- Confirm navigation moves to `assets.switches`

## 3. Base Disks Route / Selection / Edit / Import / Validate / Remove Sanity

- Navigate to `Assets > Base Disks`
- Confirm the route resolves to `assets.base_disks`
- Confirm inventory, details, and action surfaces are present without a redundant nested page banner
- Select an existing base disk if available and confirm details load coherently
- Edit metadata fields and confirm save/readiness affordances update coherently
- Trigger `Import / Register` and confirm the file-pick path is reachable
- Trigger validation and confirm validation status is explicit and readable
- If a removable row is available, confirm remove flow requires confirmation and remains registry-scoped
- Switch away to `Overview` or `Switches`, then return to `Base Disks`
- Confirm the Assets workspace stays alive and the route activation behaves like a refresh/reconcile pass rather than a full workspace recreation

## 4. Switches Route / Selection / Create / Edit / Delete / Attached-VM / Validation Sanity

- Navigate to `Assets > Virtual Switches`
- Confirm the route resolves to `assets.switches`
- Confirm inventory, details, and action surfaces are present without a redundant nested page banner
- Select an existing switch and confirm details plus attached-VM information load coherently
- Click `New`, enter a draft, and confirm inline validation updates while editing
- If practical, create a new switch and confirm it appears in the inventory after apply
- Select an existing switch and confirm edit/update behavior remains coherent and constrained to the approved workflow
- If a switch with attached VMs is available, confirm delete is blocked explicitly and attached VM names are visible
- If a switch with no attached VMs is available, confirm delete requires confirmation before proceeding
- Switch away to `Overview` or `Base Disks`, then return to `Switches`
- Confirm the Assets workspace stays alive and the route activation behaves like a refresh/reconcile pass rather than a full workspace recreation

## 5. Shell / Title Ownership Sanity for Assets

- Navigate across:
  - `Assets > Overview`
  - `Assets > Base Disks`
  - `Assets > Switches`
- Confirm the shell remains the owner of page-level title/description context
- Confirm child views do not reintroduce large duplicate page-title bands
- Confirm local operational section headings still exist where needed for list/details/actions clarity

## 6. Long-Lived Assets Workspace / Route Switching Sanity

- From `Assets > Base Disks`, select a row or prepare a visible editor state if practical
- Move to `Assets > Overview`
- Move back to `Assets > Base Disks`
- Confirm the capability behaves like one long-lived Assets workspace rather than a newly constructed surface each time
- Repeat the same route-switching sanity pass for `Assets > Switches`
- Confirm route activation visibly refreshes/reconciles the active lane without collapsing the capability back into shell-owned local workflow state

## Result Record

- Commit tested:
- Environment:
  - Windows version:
  - App build type:
  - Hyper-V available:
  - Base disk scenario available:
  - Switch / attached-VM scenario available:
- Section 1 Assets Overview-First Behavior: Pass / Fail
- Section 2 Overview Summary / Navigation Surface Sanity: Pass / Fail
- Section 3 Base Disks Route / Selection / Edit / Import / Validate / Remove Sanity: Pass / Fail
- Section 4 Switches Route / Selection / Create / Edit / Delete / Attached-VM / Validation Sanity: Pass / Fail
- Section 5 Shell / Title Ownership Sanity for Assets: Pass / Fail
- Section 6 Long-Lived Assets Workspace / Route Switching Sanity: Pass / Fail
- Findings / follow-up observations:

## Open Questions / TBDs

- Whether a future closure pass should add a dedicated Assets-only exploratory checklist for compact-width layout behavior beyond the current extraction-closure scope
- Whether later milestones should add a single cross-capability workspace-lifetime checklist once additional extracted workspaces adopt the same long-lived pattern
