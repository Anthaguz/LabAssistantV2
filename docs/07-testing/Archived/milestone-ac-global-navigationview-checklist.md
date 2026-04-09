# Milestone AC - Global NavigationView Convergence Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


## Purpose

Manual verification checklist for Milestone AC (#292/#293/#294) to confirm global NavigationView behavior matches AC-010 and remains stable on real machines.

## Scope

- WinUI global NavigationView shell behavior
- Parent/child routing and compact behavior
- Startup route determinism
- Active selection context signaling
- Basic stability sanity after repeated navigation changes

Out of scope:

- Feature-depth migration under Deploy/Templates/Assets
- Breadcrumb rollout (deferred)
- RDP readiness feature validation beyond non-blocking sanity

## Preconditions

- Build is from latest master including #293 and #294.
- Run path: `.\build-run-winui.ps1`
- Test machine: Windows host where WinUI app launches successfully.

## Verification Steps

### 1) Navigation structure

- [ ] Open WinUI app and confirm shell uses a single global left navigation surface.
- [ ] Verify main entities are present: Machines, Deploy, Templates, Assets, Diagnostics.
- [ ] Verify Settings is shown in the footer region (cog/footer nav placement), not mixed into top entity list.

### 2) Expanded mode behavior

- [ ] Expand navigation pane.
- [ ] For each parent entity with children, verify child options are visible/expandable.
- [ ] Click parent label and confirm route lands on that entity's default child.
- [ ] Click non-default child and confirm route updates correctly.

### 3) Compact mode behavior

- [ ] Collapse navigation pane (LeftCompact state).
- [ ] Click parent icon for entity with children.
- [ ] Confirm parent click does **not** accidentally jump to default child if compact behavior is configured to expose child options first.
- [ ] Confirm child actions remain reachable by click affordance (no hover-only dependency).

### 4) Startup and routing determinism

- [ ] Restart app.
- [ ] Confirm fresh launch route is deterministic at `machines.overview`.
- [ ] Navigate to another route, close app, reopen app.
- [ ] Confirm app does not restore last route for this milestone (still starts at `machines.overview`).

### 5) Context signaling

- [ ] While switching parent and child routes, verify active navigation selection/highlight remains correct.
- [ ] Confirm no ambiguous selected state (e.g., wrong item highlighted after route change).
- [ ] Confirm no breadcrumb dependency exists for understanding current location in this slice.

### 6) Stability sanity checks

- [ ] Repeatedly switch between Machines, Deploy, Diagnostics, and Settings.
- [ ] Confirm navigation does not crash/fail-fast during route changes.
- [ ] Confirm Machines view remains usable after repeated nav switches.
- [ ] Spot-check RDP readiness status updates remain non-blocking/non-crashing while app is idle.

## Result Recording Template

- Build/commit tested:
- Environment (OS version, runtime notes):
- Section 1 (Navigation structure): Pass/Fail + notes
- Section 2 (Expanded mode): Pass/Fail + notes
- Section 3 (Compact mode): Pass/Fail + notes
- Section 4 (Startup/routing): Pass/Fail + notes
- Section 5 (Context signaling): Pass/Fail + notes
- Section 6 (Stability sanity): Pass/Fail + notes
- Known limitations or deferred items:


