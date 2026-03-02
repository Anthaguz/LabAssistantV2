# Milestone AB - WinUI Layout Hardening + View Decomposition Checklist

Purpose: Manual verification checklist for Milestone AB closure, focused on layout stability, overflow behavior, scroll ownership, and shell-host decomposition reliability.

This checklist complements automated AB matrix tests by validating real resize and interaction behavior.

## Prerequisites

- Windows machine capable of running `LabAssistant.WinUI`
- Build that includes merged AB work (`#286`, `#287`, `#288`)
- Access to sample structured logs with at least one long context payload
- Multiple VMs present for Machines workspace density checks

## A. Resize Bands (compact / normal / wide)

### A1. Compact width sanity
- Launch WinUI: `.\build-run-winui.ps1`
- Reduce window width to compact range.
- Verify:
  - primary actions remain visible/reachable
  - no clipped/hard-hidden primary controls
  - Diagnostics Logs filter/actions remain usable without horizontal guessing

### A2. Normal width sanity
- Return window to normal working width.
- Verify:
  - list/detail surfaces remain visible and usable
  - no abrupt layout jumps or overlapping controls

### A3. Wide width sanity
- Expand window to wide mode.
- Verify:
  - full expected layout is shown
  - controls do not collapse unexpectedly
  - no regressions in shell alignment

## B. Overflow and Scroll Ownership

### B1. Diagnostics Logs filter/action reachability
- Navigate to `Diagnostics > Logs`.
- Verify:
  - filter/action area remains reachable at compact and normal widths
  - `Apply filters`, `Clear filters`, `Reload`, and `Open raw JSONL` remain visible

### B2. Long payload behavior in Diagnostics details
- Select a log event with long context JSON.
- Verify:
  - details pane scrolls internally
  - long content does not expand parent panel off-screen
  - shell/workspace remains stable while scrolling details

### B3. Machines panel behavior under constrained width
- Navigate to `Machines > Overview`.
- Verify:
  - VM list/details/actions remain operable
  - details sections do not force unrelated shell regions off-screen
  - primary action row remains reachable

## C. Decomposition and Host Rendering

### C1. Host-based rendering sanity
- Switch between capabilities/subviews:
  - `Machines > Overview`
  - `Diagnostics > Logs`
  - another capability
  - back again
- Verify:
  - extracted views render consistently via shell hosts
  - no empty-host regressions

### C2. Capability/subview switching stability
- Repeatedly switch between Machines and Diagnostics.
- Verify:
  - no lost panel visibility states
  - no stale/blank host after switching

## D. Interaction and Accessibility Sanity

### D1. Keyboard reachability
- Use keyboard navigation for primary controls in Machines and Diagnostics.
- Verify:
  - tab sequence reaches primary controls
  - focused controls remain visible in compact/normal widths

### D2. Pointer affordance discoverability
- Confirm icon-first shell controls still expose tooltip/discoverability cues.
- Verify no critical action depends on hidden hover-only affordances.

## E. Record Template

For each run, record:
- commit/build tested
- environment (Windows version, display scale, resolution)
- pass/fail per section (`A`/`B`/`C`/`D`)
- limitations or defects found
- reproduction notes for failures

## Explicit Deferred Items (AB)

- No new feature behavior is validated in this checklist.
- This checklist does not cover business-policy changes (for example RDP policy semantics).
- This checklist is layout/decomposition hardening only.
