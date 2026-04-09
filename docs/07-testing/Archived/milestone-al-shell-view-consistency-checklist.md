# Milestone AL Shell / View Consistency Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


## Purpose

Manual runtime verification checklist for Milestone AL (`WinUI Shell and View Consistency`).

Use this checklist after AL automation passes to confirm the cross-view shell/layout/navigation contract remains usable on a real Windows machine.

## Scope

- Shell title/description ownership
- Overview-first capability behavior
- Templates exception navigation model
- Deploy right-panel convergence
- Quick Deploy local issue signaling
- From Template review/remediation identity
- Action placement and icon-first command chrome
- Compact-mode and scroll-ownership behavior

## Out of Scope

- Domain workflow redesign
- Template Editor redesign
- WPF behavior
- New shell/navigation semantics beyond the approved AL contract
- Post-AL polish work

## Preconditions

- Build under test is from the target AL closure commit/branch
- WinUI app launches successfully
- At least one template exists
- At least one quick-deploy VM draft can be created
- At least one base disk and one switch exist if practical

## 1. Shell Title / Description Ownership

- Open `Assets`, `Deploy`, and `Diagnostics`
- Confirm the shell header owns the page-level title/description
- Confirm child surfaces do not reintroduce a second large page-title band
- Confirm local section headers still exist where needed:
  - inventory
  - details
  - workflow sections

## 2. No Duplicated Inner Page Header Bands

- Open:
  - `Assets > Base Disks`
  - `Assets > Switches`
  - `Deploy > Quick Deploy`
  - `Deploy > From Template`
- Confirm the working surface starts with the real operational panels, not a repeated inner page banner
- Confirm no child view reintroduces redundant page-level explanatory text already owned by the shell

## 3. Overview-First Behavior for Assets / Deploy / Diagnostics

- Click parent `Assets`
- Confirm it lands on `Overview`
- Verify local tabs exist in order:
  - `Overview`
  - `Base Disks`
  - `Switches`
- Click parent `Deploy`
- Confirm it lands on `Overview`
- Verify local tabs exist in order:
  - `Overview`
  - `Quick Deploy`
  - `From Template`
- Click parent `Diagnostics`
- Confirm it lands on `Overview`
- Verify local tabs exist in order:
  - `Overview`
  - `Logs`

## 4. Templates Exception Behavior

- Click parent `Templates`
- Confirm it lands in `Library`
- Confirm Templates does not expose the same peer-tab model used by Assets/Deploy/Diagnostics
- Open a template for editing
- Confirm the app enters `Editor` through the workflow action
- Use the existing return path back to library
- Confirm `Editor` still behaves as a stateful authoring workspace rather than a generic peer destination

## 5. Deploy Right-Panel Behavior

- Open `Deploy > Quick Deploy`
- Confirm the right-panel launcher is local to the workflow surface
- Confirm the panel is framed as progress/results, not as a generic issue inbox
- Open `Deploy > From Template`
- Confirm the same workflow-local panel model is present
- Start no deployment yet and confirm the panel is not acting as the primary pre-run issue surface

## 6. Quick Deploy Local Issue Signaling

- Add or select at least one Quick Deploy VM entry
- Confirm each VM row can show local warning/blocking state
- Confirm the selected VM editor shows local issue guidance inline
- Confirm pre-run issue review does not require opening the right panel
- Confirm row-local remove remains row-local

## 7. From Template Review / Remediation Identity

- Open `Deploy > From Template`
- Select a template
- Confirm the page reads as:
  - template review
  - remediation
  - deploy
- Confirm it does not read like a duplicate Quick Deploy editor
- Confirm shared issues, where available, are presented as shared in the main workspace
- Confirm remediation language clearly distinguishes:
  - fix here when supported
  - fix in Templates Editor for structural changes

## 8. Action Placement / Icon Consistency

- Review the migrated views:
  - `Machines`
  - `Assets > Base Disks`
  - `Assets > Switches`
  - `Deploy > Quick Deploy`
  - `Deploy > From Template`
- Confirm routine/local actions are near what they affect
- Confirm delete uses the trash-can icon where AL targeted it
- Confirm major workflow actions remain textual where clarity matters
- Confirm support actions do not visually compete with primary workflow actions

## 9. Compact Behavior and Workspace Preservation

- Resize the app to a narrow width
- Confirm shell nav behaves like drawer-style access rather than leaving a permanent compact mini-rail footprint
- Confirm workspace width is preserved as much as current structure allows
- For:
  - `Machines`
  - `Assets > Base Disks`
  - `Assets > Switches`
  - `Deploy > Quick Deploy`
  confirm the view degrades toward a stacked or focus-mode layout rather than forcing an unusable side-by-side split

## 10. Scroll Ownership Sanity

- Confirm the shell frame itself does not become a scrolling surface
- Confirm the right panel scrolls internally
- Confirm master/detail surfaces keep list/details scrolling bounded locally
- Confirm `Deploy > From Template` uses a bounded workspace scroll owner rather than growing into a giant page surface
- Confirm no obvious nested-scroll conflict makes the operational surfaces hard to use

## Result Record

- Commit tested:
- Environment:
  - Windows version:
  - App build type:
  - Screen resolution / scaling:
- Section 1 Shell Title / Description Ownership: Pass / Fail
- Section 2 No Duplicated Inner Page Header Bands: Pass / Fail
- Section 3 Overview-First Behavior: Pass / Fail
- Section 4 Templates Exception Behavior: Pass / Fail
- Section 5 Deploy Right-Panel Behavior: Pass / Fail
- Section 6 Quick Deploy Local Issue Signaling: Pass / Fail
- Section 7 From Template Review / Remediation Identity: Pass / Fail
- Section 8 Action Placement / Icon Consistency: Pass / Fail
- Section 9 Compact Behavior and Workspace Preservation: Pass / Fail
- Section 10 Scroll Ownership Sanity: Pass / Fail
- Findings / follow-up observations:

## Open Questions / TBDs

- Whether post-AL polish should further reduce remaining cross-view phrasing inconsistencies without changing AL contract structure
- Whether later milestones should add deeper compact-width focus behavior for authoring-heavy surfaces beyond the current conservative AL9 implementation

