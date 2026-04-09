# Milestone U - Hyper-V Verification Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


Purpose: Manual verification checklist for Milestone U (Deployment Readiness & Root-Cause Diagnostics) on a real Windows Hyper-V host.

This checklist complements automated tests by validating real Hyper-V behavior, UI readiness feedback, and diagnostics output.

## Prerequisites

- Windows host with Hyper-V enabled
- .NET 8 runtime/SDK environment used by the app
- At least one valid base VHDX registered in the VHDX catalog
- One intentionally invalid/corrupt/fake `.vhdx` file for negative testing
- Access to the configured app log folder (for `structured-events.jsonl`)

## Regression context (important)

Milestone U depends on post-#210 stabilization work (#219) for VHDX validation and persistent PowerShell session behavior. While running the checklist, explicitly observe:

- no GUI hang during catalog VHDX validation
- app shuts down cleanly after validation/deploy interactions that use persistent PowerShell

## A. Catalog-time VHDX integrity validation (#210 / #219)

### A1. Valid VHDX can be added to catalog
- Open `VHDX Catalog` page
- Add a known-good VHDX entry
- Save
- Expected:
  - save succeeds
  - item remains in catalog
  - UI does not hang

### A2. Invalid/corrupt fake `.vhdx` is rejected
- Add/edit a catalog entry pointing to an invalid/corrupt file renamed `.vhdx`
- Save
- Expected:
  - save is blocked
  - validation message is summarized and path-aware
  - UI does not dump raw PowerShell stderr
  - UI remains responsive after validation finishes

### A3. Single-item edit does not revalidate unrelated catalog entries (regression)
- Keep one known-bad catalog entry already present
- Add/edit a different valid entry
- Save
- Expected:
  - only the edited/imported item validation affects the save outcome
  - unrelated existing bad entry does not block the edit/save of the new item

## B. Deploy page quick preflight visibility (#212)

### B1. Quick preflight updates on relevant config changes
- Open `Deploy` page
- Add a VM (or edit existing VM)
- Change a relevant deploy-impacting field:
  - base VHDX selection/path
  - destination path
  - switch selection
- Expected:
  - readiness panel updates automatically after a short debounce
  - readiness mode shows `Quick`
  - readiness results reflect the new configuration

## C. Full preflight gating before deploy (#212)

### C1. Blocking readiness failure prevents deploy start
- Create a blocking readiness issue (examples):
  - invalid base VHDX
  - invalid/unwritable destination path
- Click `Deploy`
- Expected:
  - full preflight runs first (panel shows `Full`)
  - deploy is blocked
  - no Hyper-V deployment actions start (no VM create/start activity)
  - readiness panel clearly shows blocking failures (`Fail`)

### C2. Warning-only readiness allows deploy
- Create a warning-only condition where practical:
  - low free space
  - unknown free-space result (environment/path dependent)
- Click `Deploy`
- Expected:
  - full preflight runs
  - warnings are shown
  - deploy is allowed to proceed

Note: low/unknown free-space reproduction may be environment-dependent. If a warning-only scenario cannot be reproduced on the test host, record the limitation and rely on automated tests for warn-only gating behavior.

## D. Destination path / storage readiness classification (#211)

### D1. Invalid/unwritable destination path reports blocking failure
- Configure VM path to an invalid or unwritable location
- Trigger quick/full preflight (full via `Deploy`)
- Expected:
  - readiness category indicates destination/storage issue
  - result is `Fail` (blocking)
  - message/guidance is actionable and path-aware

### D2. Low/unknown free space reports warning only
- Use a path/drive scenario that produces low or unknown free-space condition (if feasible)
- Trigger full preflight
- Expected:
  - category is destination/storage
  - result is `Warn`
  - deploy is not blocked by this warning alone

## E. Runtime failure diagnostics enrichment (#213)

Use a scenario that reaches runtime and fails after preflight (for example, a failure in differencing disk creation or VM creation path).

### E1. UI/runtime summary message is concise and path-aware
- Start deploy with a runtime-failing configuration
- Observe failure message in deploy/log/error summary paths
- Expected:
  - message includes likely artifact/path when known (e.g., `parentVhdPath`, `targetVhdPath`, `vmPath`)
  - message is concise/actionable
  - raw multi-line PowerShell stderr is not dumped directly into the UI summary

### E2. Structured logs contain enriched artifact/path context
- Inspect `<LogFolder>\\structured-events.jsonl`
- Locate failure events for the deployment operation (`operationId`)
- Expected relevant events include enriched context fields where applicable:
  - `StepFailed`
  - `VmDeployFailed`
  - `DeployLabFailed`
- Expected fields (as applicable):
  - `vmId`, `vmName`
  - `vmPath`
  - `targetVhdPath`
  - `parentVhdPath`
  - `failureStepKey`
  - `failedVmName` (deploy terminal event)

### E3. Raw diagnostics/debug logs remain available
- Inspect debug logs / raw PowerShell output logging path
- Expected:
  - raw PowerShell stderr/output is still available for deep diagnosis
  - structured summaries did not replace detailed diagnostics

## F. UI responsiveness and shutdown sanity (regression observation after #219)

### F1. No GUI hang during VHDX validation paths
- Perform catalog VHDX validation actions (good + bad VHDX)
- Expected:
  - validation completes
  - no indefinite UI hang
  - app remains usable after validation result is shown

### F2. App shutdown sanity after PowerShell-backed flows
- After catalog validation and/or deploy interactions, close the app normally
- Expected:
  - app exits cleanly
  - no stuck `LabAssistant.exe` / orphaned `powershell.exe` observed in Task Manager (spot-check)

## Recording results

For each run, record:
- app build/commit tested
- Windows version
- Hyper-V enabled state
- pass/fail per checklist section
- any environment-specific limitations (especially free-space warning reproduction)
- paths/artifacts used for negative testing (fake VHDX, invalid destination path)

