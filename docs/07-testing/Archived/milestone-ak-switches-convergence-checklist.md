# Milestone AK Switches Convergence Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


## Purpose

Manual runtime verification checklist for Milestone AK (`Assets > Switches` WinUI convergence).

Use this checklist after AK automation passes to confirm the real surface remains usable on a real Windows / Hyper-V machine.

## Scope

- `assets.switches` route and shell hosting
- Switches scaffold continuity from AK2
- Switches operational behavior from AK3
- UX/data-binding hardening from AK4

## Out of Scope

- Topology-management redesign
- Caching/performance redesign
- WPF behavior
- Cross-capability shell standardization beyond observing inconsistencies

## Preconditions

- Build under test is from the target commit/branch for AK closure
- Hyper-V is available
- At least one host switch exists, or the tester is prepared to create one
- At least one VM attached to a switch is available for blocked-delete verification

## 1. Route / Surface Sanity

- Navigate to `Assets > Virtual Switches`
- Confirm the route resolves to the WinUI Switches surface
- Confirm the shell title reads `Virtual Switches`
- Confirm the main working surface starts with inventory/details panels rather than a redundant nested header card
- Confirm the inventory and details panels are both visible and usable

## 2. Loading / Empty / Error Behavior

- Open the view with normal host state
- Confirm switch inventory loads without placeholder-only scaffold copy
- If inventory is empty, confirm the empty-state panel is explicit and actionable
- Force or simulate a switch load failure if practical
- Confirm the error panel appears explicitly and the message is copyable
- Confirm the page does not dump raw multi-line PowerShell output into the normal details area

## 3. Refresh and Selection Continuity

- Select an existing switch
- Click refresh
- Confirm selection is preserved by switch name where possible
- If the selected switch disappears between refreshes, confirm the UI reconciles explicitly and does not keep stale details silently
- Confirm refresh does not visibly reset the full page unnecessarily

## 4. New-Switch Draft Continuity

- Click `New`
- Enter a draft name and type
- Trigger refresh before applying
- Confirm the unsaved draft is preserved
- Confirm refresh does not force selection back onto an existing switch while the draft is active
- Confirm attached-VM display for a new draft remains neutral and does not imply delete assessment

## 5. Create Workflow

- Create a new switch draft
- Verify live validation updates while editing
- For `External`, confirm adapter/target is required
- Apply the draft
- Confirm success messaging is understandable
- Confirm the newly created switch appears in the inventory and can be selected

## 6. Rename / Update Workflow

- Select an existing switch
- Change only the name
- Confirm live validation reflects whether the change is allowed
- Apply the rename
- Confirm success behavior is explicit and the updated name is reflected in inventory/details

## 7. Blocked Update Mutation Messaging

- Select an existing switch
- Attempt to change the switch type
- Confirm the UI blocks this with clear messaging that a new switch must be created instead
- If the switch is `External`, attempt to rebind the adapter/target
- Confirm the UI blocks this with clear messaging that creating a new switch is required

## 8. Validation / Readiness Presentation

- Confirm there is no separate `Validate` button
- Confirm validation updates inline while editing
- Trigger:
  - missing-name validation
  - duplicate-name validation
  - missing-adapter validation for `External`
- Confirm blocking text is understandable and actionable
- Confirm pass-state text does not redundantly repeat obvious type/adapter summary text

## 9. Attached-VM Visibility Behavior

- Select a switch with attached VMs
- Confirm attached VM names load automatically
- Confirm they are displayed as neutral host-state information, not as delete-only guidance
- Select a switch with no attached VMs
- Confirm the UI shows `No attached VMs.`
- Confirm the attached-VM area is not a freeform textbox and does not look editable

## 10. Delete Confirmation and Blocked-Delete Messaging

- Select a switch with attached VMs
- Click `Delete`
- Confirm delete is blocked explicitly
- Confirm attached VM names are visible and the reason is understandable
- Confirm the UI does not pretend delete is allowed
- Select a switch with no attached VMs
- Click `Delete`
- Confirm confirmation is required before delete proceeds
- Confirm delete messaging remains explicit and does not imply file/system-topology semantics outside current scope

## 11. Layout / Overflow / Scroll Sanity

- Verify the view at compact height
- Verify the view at normal window size
- Verify the view at wide / maximized size
- Confirm the inventory and details panels gain useful space when the window grows
- Confirm no redundant nested header band reappears
- Confirm scroll ownership remains inside the intended panels rather than causing whole-page instability

## 12. Cross-View Consistency Observation Notes

This is not a pass/fail requirement for AK, but testers should note any consistency problems relative to:

- `Machines`
- `Assets > Base Disks`
- `Deploy > Quick Deploy`

Capture whether:
- shell titles and child-surface titles feel duplicated
- nested header cards waste vertical space
- panel hierarchy differs between capabilities in non-intentional ways

These notes should be carried to the PM for a separate cross-view standardization issue.

## Result Record

- Commit tested:
- Environment:
  - Windows version:
  - Hyper-V available:
  - Host switch count:
  - VM attachment scenario available:
- Section 1 Route / Surface Sanity: Pass / Fail
- Section 2 Loading / Empty / Error Behavior: Pass / Fail
- Section 3 Refresh and Selection Continuity: Pass / Fail
- Section 4 New-Switch Draft Continuity: Pass / Fail
- Section 5 Create Workflow: Pass / Fail
- Section 6 Rename / Update Workflow: Pass / Fail
- Section 7 Blocked Update Mutation Messaging: Pass / Fail
- Section 8 Validation / Readiness Presentation: Pass / Fail
- Section 9 Attached-VM Visibility Behavior: Pass / Fail
- Section 10 Delete Confirmation and Blocked-Delete Messaging: Pass / Fail
- Section 11 Layout / Overflow / Scroll Sanity: Pass / Fail
- Section 12 Cross-View Consistency Observation Notes recorded: Yes / No
- Findings / limitations:

## Open Questions / TBDs

- Whether future shell/title standardization should remove redundant nested view headers across multiple capabilities
- Whether attached-VM visibility should eventually expand into richer host-state inspection beyond current AK scope

