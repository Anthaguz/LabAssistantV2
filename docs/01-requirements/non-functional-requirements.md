# Non-Functional Requirements (NFRs)

**Purpose:** Define quality attributes so improvements are measurable and regressions are caught early.

**How this is used:**
- Release gates: a feature is not "done" if it breaks an NFR.
- Test expectations: NFRs should have at least one verification method (manual or automated).
- Agent guardrails: prevents scope drift and random improvements that harm stability.

---

## Performance

### P-01 Single VM deployment speed
- **Target:** Deploy a single VM in **<= 2 minutes** on baseline hardware.
- **Baseline:** TBD (measure current)
- **Verify:** Manual timing test + log timestamps (`VmDeployStarted` -> `VmDeployCompleted`)

> This aligns with product KPIs. (See Product Vision)

### P-02 Lab deployment speed
- **Target:** Deploy a lab with **N VMs** in **<= TBD minutes** on baseline hardware
  OR improve by **>= 80%** vs a measured manual baseline.
- **N:** TBD (pick a representative number like 3, 5, 10)
- **Verify:** Manual timing test + deployment summary

### P-03 UI responsiveness
- **Target:** UI remains responsive during long operations:
  - progress updates visible
  - no "application not responding"
  - automatic quick preflight/readiness feedback does not block normal Deploy-page editing interactions
- **Verify:** Manual test + optional automated UI test later

### P-04 Cancellation responsiveness (if supported)
- **Target:** Cancel request acknowledged in **<= 2 seconds** and operation stops in **<= 10 seconds** (TBD if needed)
- **Verify:** Manual test (cancel mid-deploy) + logs show cancellation path

### P-05 Hyper-V backend timing visibility
- **Target:** Diagnostics distinguish:
  - workflow-session creation cost
  - query-session creation cost
  - one-shot administrative session creation cost
  - Hyper-V command/query execution cost
  - key read-flow duration such as Machines inventory load and edit snapshot load
- **Verify:** Inspect diagnostics/debug timing entries after Machines and Deploy flows

---

## Reliability / Resilience

### R-01 Partial failure behavior is consistent
- **Target:** If a deployment fails mid-way, the tool shall automatically cleanup any resources it created (cleanup mode).
- **Policy:** Cleanup (selected)
- **Verify:** Failure injection test (for example missing switch / denied permission mid-way) and confirm no orphaned VMs/disks remain, or residuals are explicitly reported.

### R-02 No "unknown state"
- **Target:** After any operation completes/fails/cancels, the user can see:
  - final status
  - what was created
  - what can be retried safely
- **Verify:** Manual test across success/failure/cancel

### R-03 Idempotency / retry safety
- **Target:** Retrying an operation must not silently corrupt state:
  - name collisions handled predictably (block, prompt, or auto-suffix) - TBD policy
- **Verify:** Attempt two deployments with same template/name

---

## Security

### S-01 No secrets in plaintext
- **Target:** No secrets stored in templates, config files, or logs.
- **Verify:** Spot-check generated artifacts; grep logs/templates for common secret patterns.

### S-02 Input validation for file paths and JSON
- **Target:** All user-provided file paths and JSON imports are validated.
  - No path traversal
  - No writing outside configured directories unless explicitly chosen by the user
- **Verify:** Negative tests (bad JSON, invalid paths, missing fields)

### S-03 Least-privilege clarity
- **Target:** If admin privileges are required, the tool must clearly communicate:
  - what needs elevation
  - why it is needed
- **Verify:** Run without admin and confirm message clarity

---

## Usability

### U-01 First-time user success
- **Target:** A new user can complete their first lab deployment in **<= TBD minutes**.
- **Verify:** Fresh machine dry run checklist

### U-02 Actionable errors
- **Target:** Error messages provide:
  - what failed
  - why it failed (likely cause)
  - what to do next (fix steps)
  - for deploy readiness/preflight, whether the issue is blocking or warning-only
- **Verify:** Trigger top 5 failure modes and review messaging

---

## Observability

### O-01 Structured logs with operation id
- **Target:** Every major operation emits structured logs:
  - start -> steps -> completion/failure
  - includes **operationId** per operation (canonical structured field)
- **Verify:** Inspect `structured-events.jsonl` for required fields and event families

> This matches the Acceptance Criteria logging expectations and the canonical logging contract.

### O-02 Diagnostic bundle export
- **Target:** Tool can export a diagnostics bundle containing at minimum:
  - structured logs (`logs/structured-events.jsonl`)
  - runtime metadata
  - operation context metadata
  - relevant template metadata (not secrets)
- **Verify:** Manual export and inspect ZIP contents and JSONL parseability

---

## Compatibility

### C-01 Supported Windows + Hyper-V requirements
- **Target:** Runs on supported Windows versions with Hyper-V enabled.
- **Supported Windows versions:** TBD
- **Verify:** Documented prerequisites + smoke test on each supported OS

### C-02 Runtime + dependencies
- **Target:** .NET runtime requirement documented and validated at startup.
- **Version:** .NET 8 desktop runtime (Windows)
- **Verify:** Run on a machine missing runtime and confirm helpful guidance

---

## Maintainability (optional but recommended for next level)

### M-01 Logging and error standards
- **Target:** All operations use consistent logging and error handling patterns
- **Verify:** PR checklist + code review gate

### M-02 Minimum automated testing
- **Target:** Core logic covered with unit tests (especially mapping/validation).
- **Verify:** CI test execution required to merge

---

## Baseline Hardware (for performance targets)
Define once so timing numbers mean something.

- CPU: TBD
- RAM: TBD
- Storage: TBD
- OS: TBD

---

## Open Questions / TBDs
- Pick N for the lab performance test case.
- Decide naming collision strategy (prompt vs auto-suffix vs block).
- Decide retention policy for logs and diagnostic bundles.
