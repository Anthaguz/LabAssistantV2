# Test Plan

**Purpose:** Concrete test cases mapped to features and acceptance criteria.

This file is a practical baseline plan for recurring regression checks. It does not replace issue-specific tests.

---

## TC-001: Deploy Lab (1 VM) - Happy Path
- **Related AC:** `AC-001` (Scenario 1), `GR-04`
- **Type:** Manual (Hyper-V host)
- **Steps:**
  1. Open Deploy page and configure 1 VM with valid base VHDX and existing switch.
  2. Start deployment.
  3. Wait for completion.
- **Expected:**
  - VM deploys successfully.
  - Final status is `Completed`.
  - Per-VM and global summary are shown.
  - Structured log events are written with a shared `operationId`.

## TC-002: Deploy Failure Triggers Cleanup
- **Related AC:** `AC-001` (Scenario 4), `GR-02`, `GR-03`
- **Type:** Manual + automated coverage
- **Steps:**
  1. Configure a VM deployment that will fail at runtime (for example invalid/corrupt base VHDX).
  2. Start deployment.
  3. Observe terminal state and cleanup summary.
- **Expected:**
  - Deployment reaches failed terminal state (`Failed` or `FailedWithResiduals`).
  - Cleanup runs for created resources.
  - Residuals are clearly shown if cleanup cannot fully complete.
  - Structured logs include deploy/step/cleanup events for the same `operationId`.

## TC-003: Cancel Deployment at Safe Boundary
- **Related AC:** `AC-001` (extend cancellation behavior), `GR-02`, `GR-04`
- **Type:** Manual + automated coverage
- **Steps:**
  1. Start a deployment.
  2. Trigger Cancel while a step is in progress.
  3. Observe state transitions and final outcome.
- **Expected:**
  - UI shows cancelling state.
  - Operation stops at a safe boundary.
  - Cleanup runs if resources were created.
  - Terminal state is `Cancelled` or `CancelledWithResiduals`.

## TC-004: Template Save/Load Uses Canonical Schema
- **Related AC:** `AC-002`, `AC-003`
- **Type:** Manual + automated coverage
- **Steps:**
  1. Create or edit a template and save it.
  2. Inspect saved JSON.
  3. Load/import template again.
- **Expected:**
  - Template JSON includes canonical fields (`schemaVersion`, `templateRevision`, `createdWithAppVersion`, `templateType`, `vmId`).
  - Template loads successfully when within support window.
  - Validation errors are actionable if fields are missing/invalid.

## TC-005: Legacy Template Migration and Compatibility Gate
- **Related AC:** `AC-003` (compatibility scenario)
- **Type:** Automated + manual spot check
- **Steps:**
  1. Import/load a legacy `v0` template.
  2. Save/export with current app.
  3. Try unsupported major schema template.
- **Expected:**
  - Legacy template migrates to canonical in memory and saves in canonical schema.
  - Unsupported newer major blocks with update guidance.
  - Older-than-support-window template blocks with actionable guidance.

## TC-006: Structured Logging and Diagnostics Export
- **Related AC:** `GR-03`, `AC-001`, `AC-002`, `AC-003`
- **Type:** Manual + automated coverage
- **Steps:**
  1. Trigger deploy/template/catalog operations.
  2. Inspect `<LogFolder>\structured-events.jsonl`.
  3. Export diagnostics bundle (service path or UI path when available).
  4. Inspect ZIP contents.
- **Expected:**
  - Structured logs contain parseable JSONL entries with `ts`, `level`, `event`, `operationId`.
  - Diagnostics bundle includes manifest, runtime metadata, operation context metadata, and structured logs.
- Optional template artifact is included/excluded based on selected option.

## TC-007: Milestone U Readiness + Diagnostics Manual Hyper-V Verification
- **Related AC:** `AC-001` (readiness/preflight additions), `GR-03`, `GR-04`
- **Type:** Manual (Hyper-V host) + automated coverage
- **Related stabilization note:** Includes regression observations captured after `#219` (PersistentPowerShellSession + catalog validation path stabilization)
- **Steps:**
  1. Run the Milestone U checklist in `docs/07-testing/milestone-u-hyperv-verification-checklist.md`.
  2. Verify catalog-time VHDX validation (good + invalid VHDX).
  3. Verify quick/full preflight behavior and deploy gating on the Deploy page.
  4. Trigger a runtime deployment failure and inspect UI summary, structured logs, and debug logs.
- **Expected:**
  - Blocking readiness failures prevent deploy before Hyper-V actions begin.
  - Warning-only readiness results do not block deploy.
  - Runtime failure diagnostics include likely artifact/path context in UI summaries and structured logs.
  - Raw PowerShell diagnostics remain available in debug/supplemental logs.
  - No GUI hang/regression in VHDX validation paths and app shutdown sanity remains intact.
  - Catalog add/edit validation remains scoped to affected item(s) (no unrelated invalid catalog entries blocking single-item save/edit flows).

## Open Questions / TBDs
- Whether to split this file into smoke tests vs milestone regression suites as the product grows.
- Whether to add explicit pass/fail checklists for different Windows versions once compatibility targets are finalized.
