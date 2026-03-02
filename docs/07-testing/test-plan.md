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

## TC-008: Milestone V Operational Hardening Verification (Wrapper + Logs + Diagnostics)
- **Related AC:** `GR-03`, `AC-001` (runtime diagnostics supportability aspects)
- **Type:** Manual (real machine) + automated coverage
- **Related milestone:** Milestone V (`#225`-`#229`)
- **Steps:**
  1. Run the Milestone V checklist in `docs/07-testing/milestone-v-operational-hardening-checklist.md`.
  2. Verify wrapper trace toggle off/on behavior (`LABASSISTANT_POWERSHELL_WRAPPER_TRACE`).
  3. Spot-check PowerShell-backed actions for no-hang/shutdown regressions.
  4. Verify structured/debug log rotation and retention behavior.
  5. Trigger a runtime failure and inspect structured + debug diagnostics metadata.
- **Expected:**
  - Wrapper trace protocol logs are low-noise by default and visible only when explicitly enabled.
  - PowerShell-backed flows remain functional and shutdown sanity is preserved.
  - Structured/debug logs rotate with stable active filenames and bounded retained history.
- Failure structured logs include normalized error metadata (when available) and path-context enrichment.
- Raw PowerShell stderr remains available in debug logs as supplemental diagnostics.

## TC-009: Milestone W Workflow Completion Verification (Guest Steps + Readiness)
- **Related AC:** `AC-001` (Milestone W guest-step execution controls contract + readiness behavior), `AC-002` (template persistence expectations)
- **Type:** Manual (real app/Hyper-V host where deploy runtime checks are exercised) + automated coverage
- **Related milestone:** Milestone W (`#230`-`#235`)
- **Steps:**
  1. Run the Milestone W checklist in `docs/07-testing/milestone-w-workflow-completion-checklist.md`.
  2. Verify guest-step controls UI grouping, placeholder visibility/labeling, and network grouping in `VmConfigPanel`.
  3. Verify template save/load JSON round-trip for implemented guest-step configs and placeholder-only guest network omission.
  4. Verify runtime guest-step skip/execution outcomes in per-VM summary and `structured-events.jsonl`.
  5. Verify guest-step completeness readiness failures block deploy until the step is disabled (or configured in future UI work).
- **Expected:**
  - Guest-step UI reflects mandatory/optional/placeholder contract without implying unsupported runtime behavior.
  - Implemented guest-step toggles persist through template save/load where applicable.
  - Guest-step runtime outcomes are explicit and observable (`executed` / `skipped` with machine-readable skip reasons).
- Enabled optional steps with missing config produce blocking readiness failures and prevent deploy before Hyper-V actions.
- Placeholder visibility alone (especially guest network placeholder) does not block deploy by itself.

## TC-010: Milestone AA WinUI Shell + Machines Verification
- **Related AC:** `AC-006` (Machines v1), `AC-007` (WinUI shell foundation)
- **Type:** Manual (real Windows machine / Hyper-V host) + automated coverage
- **Related milestone:** Milestone AA (`#265`, `#266`, `#274`, `#267`, `#269`)
- **Steps:**
  1. Run the Milestone AA checklist in `docs/07-testing/milestone-aa-winui-shell-machines-checklist.md`.
  2. Verify shell behavior (drawer motion/dismiss, top-bar visibility, theme readability, insights collapsed default).
  3. Verify capability-local navigation behavior (capability defaults, subview switching, breadcrumb/context updates).
  4. Verify Machines v1 behavior (inventory/selection, start-stop-restart-console, delete scope + confirmation, RDP disabled with reason).
  5. Verify structured machine-action logs include operation context and delete scope metadata.
- **Expected:**
  - WinUI shell behavior remains stable and contract-aligned.
  - Global capability navigation and local workspace subview navigation remain clearly separated.
  - Machines v1 actions are explicit, safe, and policy-gated where required.
  - RDP action remains visible but disabled until readiness policy work is delivered.
  - Structured logs include machine action operation context and delete scope fields.

## TC-011: Milestone AB Layout Hardening + View Decomposition Verification
- **Related AC:** `AC-007` (WinUI shell foundation behavior continuity), Milestone AB layout constraints contract (`#286`) and decomposition/hardening implementation (`#287`)
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AB (`#286`, `#287`, `#288`)
- **Steps:**
  1. Run automated AB matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneABScenarioMatrixTests.cs`.
  2. Run the Milestone AB checklist in `docs/07-testing/milestone-ab-layout-hardening-checklist.md`.
  3. Verify compact/normal/wide resize behavior across Machines and Diagnostics surfaces.
  4. Verify overflow/scroll ownership behavior, especially long Diagnostics payload details and filter/action reachability.
  5. Verify extracted host rendering remains stable while switching capabilities/subviews (no empty-host regressions).
- **Expected:**
  - MainWindow host-based decomposition contract remains intact for extracted views.
  - Visibility wiring remains valid and prevents empty surface regressions.
  - Dense surfaces remain usable under constrained widths without hidden primary controls.
  - Scroll ownership remains stable (bounded internal scroll where intended, no parent layout breakage from long content).
  - AB validation is covered by both deterministic structural tests and repeatable manual resize/interaction checks.

## Open Questions / TBDs
- Whether to split this file into smoke tests vs milestone regression suites as the product grows.
- Whether to add explicit pass/fail checklists for different Windows versions once compatibility targets are finalized.
