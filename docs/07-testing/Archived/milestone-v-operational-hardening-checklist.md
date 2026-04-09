# Milestone V - Operational Hardening Verification Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


Purpose: Manual verification checklist for Milestone V operational hardening on a real machine, focusing on PowerShell wrapper behavior, trace verbosity control, log rotation/retention, and runtime diagnostics quality.

This checklist complements automated tests by validating real `powershell.exe` process behavior, log files on disk, and runtime troubleshooting workflows.

## Prerequisites

- Windows machine with the app build under test
- Access to the configured app log folder (`<LogFolder>`)
- Hyper-V enabled if validating real Hyper-V-backed flows (recommended for wrapper/runtime spot checks)
- At least one valid VHDX and one invalid/corrupt/fake `.vhdx` for negative testing (reuses Milestone U patterns)

## A. PowerShell wrapper trace verbosity control (#228)

### A1. Default low-noise behavior (trace off)
- Ensure environment variable `LABASSISTANT_POWERSHELL_WRAPPER_TRACE` is not set (or set to false/off)
- Launch the app
- Perform a PowerShell-backed action (examples):
  - VHDX catalog validation (add/edit item)
  - deploy flow action that queries Hyper-V (switch lookup / deploy preflight path)
- Inspect debug log (`<LogFolder>\log.txt`)
- Expected:
  - normal operational/debug lines may appear
  - no wrapper trace spam lines with prefix `[PowerShellWrapperTrace]`

### A2. Troubleshooting trace mode (trace on)
- Set environment variable before launching the app:
  - `LABASSISTANT_POWERSHELL_WRAPPER_TRACE=1`
- Launch the app
- Repeat the same PowerShell-backed action
- Inspect debug log (`<LogFolder>\log.txt`)
- Expected:
  - wrapper lifecycle/protocol trace lines appear with prefix `[PowerShellWrapperTrace]`
  - examples include execute/dispose lifecycle messages
  - functional behavior remains unchanged (no hang/regression introduced by enabling traces)

## B. PowerShell-backed action spot-check / no-hang regression (#219/#225)

### B1. Catalog VHDX validation remains responsive
- Open `VHDX Catalog`
- Add/edit:
  - one valid VHDX
  - one invalid/corrupt fake `.vhdx`
- Save/validate through normal catalog flow
- Expected:
  - validation completes (good accepted, bad rejected)
  - UI does not hang
  - app remains responsive after validation completes

### B2. Shutdown sanity after PowerShell-backed operations
- After running catalog validation and/or deploy-related PowerShell-backed actions
- Close the app normally
- Expected:
  - app process exits cleanly
  - no lingering `LabAssistant.exe` + `powershell.exe`/`conhost.exe` process pair attributable to the app (spot-check in Task Manager/Process Explorer)

## C. Log rotation and retention behavior (#226)

### C1. Structured log rotation (JSONL integrity)
- Generate many structured events (repeat deploy/template/catalog actions)
- Inspect `<LogFolder>` for:
  - active: `structured-events.jsonl`
  - rotated: `structured-events.1.jsonl`, `structured-events.2.jsonl`, ...
- Expected:
  - active file name remains stable (`structured-events.jsonl`)
  - rotated files exist after threshold is exceeded
  - each line in rotated structured files is valid JSON (JSONL)
  - active file continues receiving new events after rotation

### C2. Debug log rotation / retention
- Generate many debug log lines (wrapper traces can help if enabled, but not required)
- Inspect `<LogFolder>` for:
  - active: `log.txt`
  - rotated: `log.1.txt`, `log.2.txt`, ...
- Expected:
  - active file name remains stable (`log.txt`)
  - rotated files exist after threshold is exceeded
  - oldest rotated files are removed once retention cap is exceeded (bounded history)

Note: practical retention verification may require temporarily generating a large volume of logs. Record any environment constraints (e.g., time, disk speed, difficulty reproducing enough events quickly).

## D. Runtime diagnostics metadata quality (#227 + #213)

Use a runtime failure scenario that reaches deployment steps (not preflight-blocked), such as a configuration that fails during disk/VM creation.

### D1. Structured failure events include normalized metadata (when available)
- Trigger a representative runtime failure
- Inspect `<LogFolder>\structured-events.jsonl`
- Focus on failure events:
  - `StepFailed`
  - `VmDeployFailed`
  - `DeployLabFailed`
- Expected (when source data provides stable patterns):
  - `exceptionType` present
  - `hresult` present in normalized format (`0xXXXXXXXX`)
  - `errorCode` present when reliably extractable
- Also confirm path-context enrichment remains present:
  - `parentVhdPath`
  - `targetVhdPath`
  - `vmPath`

### D2. Debug logs still contain raw stderr / supplemental diagnostics
- Inspect debug log (`log.txt` / rotated debug logs)
- Expected:
  - raw PowerShell stderr remains available in debug logs (supplemental diagnostics path preserved)
  - structured log normalization does not replace raw stderr logging

## E. Recording results

For each section, record:
- Build/commit tested
- Environment notes (Windows version, Hyper-V state)
- Pass / Fail
- Any observed regressions
- Any limitations (e.g., unable to reproduce enough log volume for retention cap spot-check)

## Known manual-only areas (intentional)

- Real `powershell.exe` / `conhost.exe` process behavior under host-specific conditions
- Hyper-V cmdlet runtime behavior and host/environment differences
- Practical log-volume generation for rotation/retention spot checks on a given machine

Automated tests cover wrapper protocol/lifecycle semantics and rotation logic through seams; this checklist validates the real-machine integration behavior.

