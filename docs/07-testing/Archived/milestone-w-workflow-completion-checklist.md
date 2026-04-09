# Milestone W - Workflow Completion Verification Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


Purpose: Manual verification checklist for Milestone W guest-step controls/persistence/execution/readiness behavior on a real app build.

This checklist complements automated tests by validating end-to-end UI behavior, template JSON persistence, runtime guest-step outcome reporting, and Deploy-page readiness/gating behavior.

## Prerequisites

- Windows machine with the app build under test
- Access to the configured log folder (`<LogFolder>`)
- At least one valid base VHDX and a working Hyper-V switch (for deploy/runtime spot checks)
- Ability to inspect saved template JSON files

## A. Guest-step controls UI contract (#232)

### A1. Mandatory / Optional grouping visibility
- Open `Deploy` page and open a VM configuration panel (`VmConfigPanel`)
- Expand **Deployment Steps**
- Expected:
  - `Mandatory` section is visible
  - `Optional` section is visible
  - optional implemented controls are shown as interactive toggles:
    - Set Time Zone
    - Install Software
    - Install Role

### A2. Placeholder visibility and labeling
- In **Deployment Steps**, inspect placeholder subsection
- In **Network**, inspect guest OS network placeholder area
- Expected:
  - placeholder controls are visible
  - placeholder controls are disabled / non-runnable
  - text clearly indicates “not implemented” / “coming soon” behavior

### A3. Network grouping preserved
- In the VM config UI, open **Network**
- Expected:
  - Hyper-V switch/NIC control remains present
  - guest OS network placeholder controls are in the same Network area (not split into another unrelated section)

## B. Template persistence behavior (#231 + #232)

### B1. Save/load template with guest-step toggles
- In Template Editor, toggle implemented optional guest steps on/off for a VM
- Save template
- Reload the template
- Expected:
  - toggle states round-trip correctly

### B2. Inspect template JSON for persisted guest-step config shape
- Open the saved template JSON
- Expected (when toggled/enabled as applicable):
  - optional guest-step config objects can appear:
    - `timeZoneConfig`
    - `softwareConfig`
    - `roleConfig`

### B3. Placeholder-only guest network payload omission
- Save a template without configuring guest network payload values (placeholder-only state)
- Inspect JSON
- Expected:
  - `guestNetworkConfig` is omitted by default when disabled/empty placeholder state only

## C. Runtime guest-step execution/skip reporting (#233)

Use a deploy run that reaches guest-step processing (same machine/environment can be used repeatedly).

### C1. Disabled optional implemented step => explicit skip (not_selected)
- In VM config, disable one implemented optional step (example: Set Time Zone or Install Software)
- Run deployment
- Inspect per-VM summary/details on the Deploy page
- Expected:
  - guest-step outcome shows `skipped`
  - skip reason is `not_selected`

### C2. Placeholder guest network step => explicit skip (not_implemented)
- Ensure guest network placeholder remains visible (no runtime implementation expected)
- Run deployment
- Inspect per-VM summary/details
- Expected:
  - guest network guest-step outcome shows `skipped`
  - skip reason is `not_implemented`

### C3. Structured logs for guest-step skip outcomes
- Inspect `<LogFolder>\\structured-events.jsonl`
- Expected `StepSkipped` entries include:
  - `result = "skipped"`
  - `stepKey`
  - `skipReason` (`not_selected`, `not_implemented`)

## D. Guest-step completeness readiness + deploy gating (#234)

### D1. Enabled optional step + missing config => blocking readiness failure
- In Deploy VM config, enable an optional implemented guest step (example: Install Software)
- Leave config incomplete (current UI has toggles but not full payload editors yet)
- Observe Deploy-page readiness panel
- Expected:
  - readiness failure appears (`Fail`)
  - category is template/config-related (`TemplateConfig`)
  - message + actionable guidance explain that config is missing/incomplete

### D2. Deploy click is blocked before Hyper-V actions
- With the readiness failure still present, click `Deploy`
- Expected:
  - full preflight runs
  - deployment is blocked before Hyper-V actions start
  - readiness panel shows blocking issue

### D3. Disable step => readiness failure clears
- Disable the previously enabled optional step
- Expected:
  - quick preflight refresh updates readiness panel
  - blocking guest-step completeness failure clears

### D4. Placeholder visibility alone does not block deploy
- Leave guest network placeholder visible (default UI state)
- Ensure implemented optional steps are disabled (or validly configured if available)
- Expected:
  - placeholder visibility alone does not create a blocking readiness failure

## E. Expected limitation (current Milestone W state)

- Payload editors for optional guest-step configuration are not fully implemented yet.
- Because of that, enabling optional implemented steps will commonly produce readiness failures until future work adds config editors.
- This is expected and contract-aligned for Milestone W (`#234` enforces enabled + missing config as blocking pre-deploy issue).

## F. Recording results

For each section, record:
- Build/commit tested
- Environment notes (Windows version, Hyper-V state)
- Pass / Fail
- Observed regressions (if any)
- Notes on current limitations (especially guest-step payload editor availability)

## Known manual-only areas (intentional)

- Real Hyper-V runtime behavior while guest-step pipeline runs
- UI interaction flow and wording clarity
- Per-VM summary/details rendering behavior in the live app
- Full readiness panel behavior under real interactive edits and deploy attempts

Automated tests cover milestone-level logic and integration paths; this checklist validates the real app behavior and expected user experience for Milestone W.

