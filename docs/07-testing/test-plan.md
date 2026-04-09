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
  1. Run the Milestone U checklist in `docs/07-testing/Archived/milestone-u-hyperv-verification-checklist.md`.
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
  1. Run the Milestone V checklist in `docs/07-testing/Archived/milestone-v-operational-hardening-checklist.md`.
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
  1. Run the Milestone W checklist in `docs/07-testing/Archived/milestone-w-workflow-completion-checklist.md`.
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
  1. Run the Milestone AA checklist in `docs/07-testing/Archived/milestone-aa-winui-shell-machines-checklist.md`.
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
  2. Run the Milestone AB checklist in `docs/07-testing/Archived/milestone-ab-layout-hardening-checklist.md`.
  3. Verify compact/normal/wide resize behavior across Machines and Diagnostics surfaces.
  4. Verify overflow/scroll ownership behavior, especially long Diagnostics payload details and filter/action reachability.
  5. Verify extracted host rendering remains stable while switching capabilities/subviews (no empty-host regressions).
- **Expected:**
  - MainWindow host-based decomposition contract remains intact for extracted views.
  - Visibility wiring remains valid and prevents empty surface regressions.
  - Dense surfaces remain usable under constrained widths without hidden primary controls.
  - Scroll ownership remains stable (bounded internal scroll where intended, no parent layout breakage from long content).
- AB validation is covered by both deterministic structural tests and repeatable manual resize/interaction checks.

## TC-012: Milestone AC Global NavigationView Convergence Verification
- **Related AC:** `AC-010`, `FR-075`, `FR-076`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AC (`#292`, `#293`, `#294`)
- **Steps:**
  1. Run automated AC matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneACScenarioMatrixTests.cs`.
  2. Run the Milestone AC checklist in `docs/07-testing/Archived/milestone-ac-global-navigationview-checklist.md`.
  3. Verify global entity set + settings footer placement and startup route determinism (`machines.overview`).
  4. Verify expanded parent->default child behavior and direct child route selection.
  5. Verify compact mode child accessibility by click (no hover-only dependency) and accepted compact parent-click behavior.
- **Expected:**
  - Global NavigationView model remains stable in `LeftCompact` mode.
  - Entity hierarchy and settings footer placement remain consistent with AC contract.
  - Canonical route usage and startup route remain deterministic across restarts.
  - Compact and expanded interactions remain discoverable and non-ambiguous.
  - Active navigation highlight remains the context signal, with no breadcrumb dependency introduced in this slice.

## TC-013: Milestone AD Templates Convergence Verification
- **Related AC:** `AC-011`, `AC-012`, `FR-077`, `FR-078`, `FR-079`, `FR-080`, `FR-081`, `FR-082`, `FR-083`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AD (`#300`, `#301`, `#302`, `#307`, `#308`, `#303`)
- **Steps:**
  1. Run automated AD matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneADScenarioMatrixTests.cs`.
  2. Run the Milestone AD checklist in `docs/07-testing/Archived/milestone-ad-templates-convergence-checklist.md`.
  3. Verify Templates routing (`templates.library` default from parent, local navigation to `templates.editor`).
  4. Verify unified workflow continuity (Library selection -> Editor context -> back to Library with coherent selection state).
  5. Verify AD3 operation entry points and AC-012 VM-edit parity flows (add/remove/edit/apply + save/reload round-trip).
  6. Verify layout/overflow sanity for Templates surfaces across compact/normal/wide window sizes.
- **Expected:**
  - Templates capability follows canonical routing and global-nav compatibility contract.
  - Library and Editor operate as one coherent capability workflow.
  - AD3 operation entry paths are available and actionable with explicit status/error messaging.
  - AC-012 VM-entry parity behavior is observable and stable through save/reload.
  - Templates layout remains scroll-safe and does not regress to overflow/infinite-growth behavior.
  - AD closure evidence includes both automated structural checks and manual runtime verification.

## TC-014: Milestone AE Templates Selector and Normalization Verification
- **Related AC:** `AC-013`, `FR-084`, `FR-085`, `FR-086`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AE (`#312`, `#313`, `#314`, `#315`, `#316`)
- **Steps:**
  1. Run automated AE matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAEScenarioMatrixTests.cs`.
  2. Run the Milestone AE checklist in `docs/07-testing/Archived/milestone-ae-templates-selector-normalization-checklist.md`.
  3. Verify switch selector rows (`add/remove`, `zero-row valid`, duplicate/empty-row guards, empty-host guidance).
  4. Verify catalog-first VHDX selector behavior and path-first legacy compatibility guidance.
  5. Verify normalization conflict policy blocks save until catalog resolution when identities conflict.
  6. Verify save/reload determinism for switch compatibility and effective VHD identity.
- **Expected:**
  - Templates routing/context continuity remains intact while executing selector workflows.
  - Switch selector behavior and compatibility rules remain explicit and guard-railed.
  - VHDX catalog-first selection is discoverable, with actionable guidance for legacy/missing catalog states.
  - Deterministic normalization precedence is enforced and save-blocking conflict behavior is observable.
  - AE closure evidence includes both automated structural checks and manual runtime verification.

## TC-015: Milestone AF Deploy From-Template Convergence Verification
- **Related AC:** `AC-014`, `FR-087`, `FR-088`, `FR-089`, `FR-090`
- **Type:** Manual (real Windows machine / Hyper-V host) + automated coverage
- **Related milestone:** Milestone AF (`#322`, `#323`, `#324`, `#325`, `#326`)
- **Steps:**
  1. Run automated AF matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAFScenarioMatrixTests.cs`.
  2. Run the Milestone AF checklist in `docs/07-testing/Archived/milestone-af-deploy-from-template-checklist.md`.
  3. Verify Deploy parent scope defaults to `deploy.from_template` and on-the-fly remains deferred for AF.
  4. Verify readiness/gating behavior (blocking disk identity conflicts, warning switch mapping states, gated deploy start).
  5. Verify correction actions (`Resolve Suggestions`, `Open in Templates Editor`) and route/context handoff.
  6. Verify AF4 compact-first results UX (sticky compact strip, concise per-VM rows, collapsed-by-default details and global issues drawer, badge/count updates).
  7. Verify layout sanity across compact/normal/wide window sizes with stable scroll ownership.
- **Expected:**
  - AF route/scope behavior remains deterministic and aligned with from-template-first contract.
  - Readiness classification and deploy gating enforce blocking vs warning conditions without silent fallback.
  - Correction actions are discoverable and preserve template context during handoff flows.
- Compact-first results UX remains usable, dense-by-default, and expandable on demand.
- AF closure evidence includes both automated structural checks and repeatable manual runtime verification.

## TC-016: Milestone AG Deploy On-the-Fly Convergence Verification
- **Related AC:** `AC-015`, `FR-091`, `FR-092`, `FR-093`, `FR-094`
- **Type:** Manual (real Windows machine / Hyper-V host) + automated coverage
- **Related milestone:** Milestone AG (`#332`, `#333`, `#334`, `#335`, `#336`)
- **Steps:**
  1. Run automated AG matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAGScenarioMatrixTests.cs`.
  2. Run the Milestone AG checklist in `docs/07-testing/Archived/milestone-ag-deploy-on-the-fly-checklist.md`.
  3. Verify route/scope behavior (`deploy.on_the_fly`) and Deploy subview continuity.
  4. Verify readiness gating (blocking vs warning) and deploy-start enablement rules.
  5. Verify correction actions (`Resolve Suggestions`, `Open in Templates Editor`) and context handoff.
  6. Verify compact results behavior (sticky summary strip, per-VM rows, collapsed-by-default details, issue summary badge updates).
  7. Verify layout/overflow/scroll behavior across compact/normal/wide windows.
- **Expected:**
  - On-the-fly route and scaffold remain stable and contract-aligned.
  - Readiness classification enforces blocking conditions and preserves warning-only flow.
  - Correction actions are discoverable and functional with predictable context transfer.
- Compact-first results remain readable with details available on demand.
  - AG closure evidence is supported by automated structural checks plus manual runtime verification.

## TC-017: Milestone AJ Base Disks Convergence Verification
- **Related AC:** `AC-019`, `FR-100`, `FR-101`, `FR-102`, `FR-103`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AJ (`#355`, `#356`, `#357`, `#358`, `#359`)
- **Steps:**
  1. Run automated AJ matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAJScenarioMatrixTests.cs`.
  2. Run the Milestone AJ checklist in `docs/07-testing/Archived/milestone-aj-base-disks-convergence-checklist.md`.
  3. Verify `assets.base_disks` route and shell hosting remain canonical and stable.
  4. Verify scaffold regions and explicit loading/empty/error states remain present.
  5. Verify operational Base Disks behavior: list, refresh, import/register, metadata edit/save, validate, and registry-only remove.
  6. Verify AJ4 hardening behavior: pending-draft continuity, readable validation summaries, actionable failure messaging, and stable selection reconciliation.
  7. Verify known template reference warnings remain explicit and active runtime consumer detection is still called out as not implemented.
- **Expected:**
  - Base Disks remains a first-class WinUI capability at `assets.base_disks`.
  - AJ2 scaffold contract remains structurally intact.
  - AJ3 operational hooks remain present and usable.
- AJ4 UX/data-binding hardening remains intact without semantic/domain drift.
- Registry-only remove wording and current reference-warning limitation remain explicit.
- AJ closure evidence includes both automated structural checks and repeatable manual runtime verification.

## TC-018: Milestone AK Switches Convergence Verification
- **Related AC:** `AC-020`, `FR-104`, `FR-105`, `FR-106`, `FR-107`
- **Type:** Manual (real Windows machine / Hyper-V host) + automated coverage
- **Related milestone:** Milestone AK (`#365`, `#366`, `#367`, `#368`, `#369`)
- **Steps:**
  1. Run automated AK matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAKScenarioMatrixTests.cs`.
  2. Run the Milestone AK checklist in `docs/07-testing/Archived/milestone-ak-switches-convergence-checklist.md`.
  3. Verify `assets.switches` route and shell hosting remain canonical and stable.
  4. Verify AK2 scaffold continuity for list/details/edit/actions and explicit loading/empty/error state containers.
  5. Verify AK3 operational continuity for load/refresh/create/update/delete wiring, conservative update rules, and delete blocking when any VM is attached.
  6. Verify AK4 hardening continuity for draft preservation, live inline validation, attached-VM visibility, reduced delete-overemphasis, and stable route/title behavior.
  7. Verify delete guardrails remain explicit while attached-VM information is presented as normal host-state context.
- **Expected:**
  - Switches remains a first-class WinUI capability at `assets.switches`.
  - AK2 scaffold structure remains intact and visible.
  - AK3 operational behavior remains present without topology/domain drift.
- AK4 hardening remains intact, especially around draft continuity, inline validation, and attached-VM visibility.
- Delete remains blocked if any VM is attached, regardless of VM power state.
- AK closure evidence includes both automated structural checks and repeatable manual runtime verification.

## TC-019: Milestone AL Shell and View Consistency Verification
- **Related AC:** `AC-021`, `FR-108`, `FR-109`, `FR-110`, `FR-111`, `FR-112`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AL (`#378`, `#379`, `#380`, `#381`, `#382`, `#383`, `#384`, `#385`, `#386`, `#387`)
- **Steps:**
  1. Run automated AL matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneALScenarioMatrixTests.cs`.
  2. Run the Milestone AL checklist in `docs/07-testing/Archived/milestone-al-shell-view-consistency-checklist.md`.
  3. Verify shell title/description ownership and confirm targeted child views do not reintroduce duplicated page-level header bands.
  4. Verify Overview-first behavior for `Assets`, `Deploy`, and `Diagnostics`, and verify `Templates` still uses the Library-first exception model.
  5. Verify Deploy right-panel behavior remains workflow-local and progress/results-first, with Quick Deploy issue signaling kept in the workflow/editor surface.
  6. Verify From Template still reads as review/remediation/deploy rather than a duplicate editor.
  7. Verify action placement/iconography consistency across the migrated views reviewed in AL8.
  8. Verify compact-mode and scroll-ownership behavior across the migrated operational views from AL9.
- **Expected:**
  - Shell remains the owner of capability-level title/description context.
  - Assets/Deploy/Diagnostics remain Overview-first and route-bound, while Templates remains Library-first and editor-state driven.
  - Deploy right-panel behavior remains workflow-local and progress/results-oriented.
  - Quick Deploy issue guidance remains local to VM rows/editor, and From Template remains review/remediation oriented.
  - Action placement and icon-first command chrome remain consistent with the approved AL rules.
  - Compact-mode and scroll ownership remain bounded and workspace-preserving without domain/workflow redesign.
  - AL closure evidence includes both automated structural checks and repeatable manual runtime verification.

## TC-020: Milestone AM Assets Extraction Closure Verification
- **Related AC:** `AC-021`, `FR-108`, `FR-109`, `FR-110`, `FR-111`, `FR-112`
- **Type:** Manual (real Windows machine / Hyper-V host where Switches flows are exercised) + automated coverage
- **Related milestone:** Milestone AM (`#399`, `#400`, `#401`, `#407`, `#408`, `#409`, `#410`, `#411`, `#412`, `#439`, `#441`, `#453`, `#454`, `#455`, `#456`, `#457`, `#458`, `#459`, `#460`, `#461`, `#462`, `#463`, `#464`, `#465`, `#466`, `#467`, `#468`, `#469`)
- **Steps:**
  1. Run automated AM matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAMScenarioMatrixTests.cs`.
  2. Run the Milestone AM checklist in `docs/07-testing/Archived/milestone-am-assets-extraction-checklist.md`.
  3. Verify the shared Assets foundation remains overview-first, route-stable, and host/delegation only.
  4. Verify Overview lane summary/navigation sanity plus shared-host non-ownership.
  5. Verify Base Disks route, selection, edit, import/register, validation, and remove flow sanity.
  6. Verify Switches route, selection, create/edit/delete, attached-VM display, validation, and blocked-delete sanity.
  7. Verify route switching preserves the long-lived Assets workspace model and refresh/reconcile behavior rather than per-navigation recreation.
- **Expected:**
  - Shared `AssetsWorkspaceComposition` remains the shared capability composition owner, while `MainWindow` remains out of local Assets workflow/state ownership.
  - `AssetsOverviewWorkspaceComposition` and `AssetsOverviewWorkspaceViewModel` continue to own Overview-local summary behavior.
  - `AssetsBaseDisksWorkspaceViewModel`, `AssetsBaseDisksWorkspaceController`, and `AssetsBaseDisksWorkspaceComposition` remain the Base Disks-local seams, with the narrowed Base Disks view surface still represented.
  - `AssetsSwitchesWorkspaceViewModel`, `AssetsSwitchesWorkspaceController`, and `AssetsSwitchesWorkspaceComposition` remain the Switches-local seams, with the narrowed Switches presentation/view surface still represented.
  - Assets remains a long-lived workspace whose route activation refreshes/reconciles the active lane instead of recreating the capability surface.
  - AM closure evidence links both deterministic automated seam protection and repeatable manual runtime verification without introducing runtime Assets changes.

## TC-021: Milestone AM Templates Extraction Closure Verification
- **Related AC:** `AC-021`, `FR-108`, `FR-109`, `FR-110`, `FR-111`, `FR-112`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AM (`#399`, `#400`, `#401`, `#413`, `#414`, `#415`, `#416`, `#417`, `#418`, `#419`, `#420`, `#421`, `#422`, `#423`, `#424`, `#425`, `#426`, `#427`, `#428`, `#429`, `#430`, `#439`, `#441`, `#497`, `#498`, `#499`, `#500`)
- **Steps:**
  1. Run automated AM matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAMScenarioMatrixTests.cs`.
  2. Run the Milestone AM checklist in `docs/07-testing/Archived/milestone-am-templates-extraction-checklist.md`.
  3. Verify the shared Templates foundation remains Library-first, route-stable, and host/delegation only.
  4. Verify Library search/filter/list/selection/action sanity plus shared-host non-ownership.
  5. Verify Editor explicit-entry behavior, document/header state, VM list/selection/draft/apply, save/save-as/validate, and VM-entry add/remove sanity.
  6. Verify route switching preserves the long-lived Templates workspace model and refresh/reconcile behavior rather than per-navigation recreation.
- **Expected:**
  - Shared `TemplatesWorkspaceComposition` remains the shared capability composition owner, while `MainWindow` remains out of Templates-local workflow/state ownership.
  - `templates.library` remains the stable/default Templates surface, while `templates.editor` remains an explicit workflow-state destination.
- `TemplatesLibraryWorkspaceViewModel`, `TemplatesLibraryWorkspaceController`, and `TemplatesLibraryWorkspaceComposition` remain the Library-local seams, with the narrowed Library interaction/view surface still represented.
- `TemplatesEditorWorkspaceViewModel`, `TemplatesEditorWorkspaceController`, and `TemplatesEditorWorkspaceComposition` remain the Editor-local seams, with the narrowed Editor interaction/view surface still represented.
  - Templates remains a long-lived workspace whose route activation refreshes/reconciles the active lane instead of recreating the capability surface.
  - AM closure evidence links both deterministic automated seam protection and repeatable manual runtime verification without introducing runtime Templates changes.

## TC-022: Milestone AM Deploy Extraction Closure Verification
- **Related AC:** `AC-021`, `FR-108`, `FR-109`, `FR-110`, `FR-111`, `FR-112`
- **Type:** Manual (real Windows machine / Hyper-V host where deploy execution is exercised) + automated coverage
- **Related milestone:** Milestone AM (`#399`, `#400`, `#401`, `#439`, `#441`, `#501`, `#502`, `#503`, `#504`, `#505`, `#506`, `#507`, `#508`, `#509`, `#510`, `#511`, `#512`, `#513`, `#514`, `#515`, `#516`, `#517`, `#518`, `#519`, `#520`, `#521`, `#522`, `#523`, `#524`, `#525`, `#526`, `#527`)
- **Steps:**
  1. Run automated AM matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAMScenarioMatrixTests.cs`.
  2. Run the Milestone AM checklist in `docs/07-testing/Archived/milestone-am-deploy-extraction-checklist.md`.
  3. Verify the shared Deploy foundation remains overview-first, route-stable, and host/delegation only.
  4. Verify Overview lane summary/navigation sanity plus shared-host non-ownership.
  5. Verify From Template selection/review/grouped-issue/deploy/results sanity.
  6. Verify Quick Deploy collection/editor/readiness/deploy/results sanity.
  7. Verify route switching preserves the long-lived Deploy workspace model and refresh/reconcile behavior rather than per-navigation recreation.
- **Expected:**
  - Shared `DeployWorkspaceComposition` remains the shared capability composition owner, while `MainWindow` remains out of shared Deploy composition ownership.
  - `deploy.overview` remains the stable/default Deploy surface, while `deploy.on_the_fly` and `deploy.from_template` remain distinct workflow surfaces.
  - `DeployOverviewWorkspaceViewModel` and `DeployOverviewWorkspaceComposition` remain the Overview-local seams, with the narrowed Overview interaction/view surface still represented.
  - `DeployFromTemplateWorkspaceViewModel`, `DeployFromTemplateWorkspaceController`, and `DeployFromTemplateWorkspaceComposition` remain the From Template-local seams, with the narrowed main/right-panel view surfaces still represented.
  - `DeployOnTheFlyWorkspaceViewModel`, `DeployOnTheFlyWorkspaceHost`, `DeployOnTheFlyWorkspaceController`, and `DeployOnTheFlyWorkspaceComposition` remain the Quick Deploy-local seams, with controller-host ownership terminating in the local host rather than `MainWindow` and the narrowed main/right-panel view surfaces still represented.
  - Deploy remains a long-lived workspace whose route activation refreshes/reconciles the active lane instead of recreating the capability surface.
  - AM closure evidence links both deterministic automated seam protection and repeatable manual runtime verification without introducing runtime Deploy changes.

## TC-023: Milestone AM Diagnostics Extraction Closure Verification
- **Related AC:** `AC-021`, `FR-108`, `FR-109`, `FR-110`, `FR-111`, `FR-112`
- **Type:** Manual (real Windows machine) + automated coverage
- **Related milestone:** Milestone AM (`#399`, `#400`, `#401`, `#439`, `#441`, `#528`, `#529`, `#530`, `#531`, `#532`, `#533`, `#534`, `#535`, `#536`, `#537`, `#538`, `#539`, `#540`, `#541`, `#542`, `#543`, `#544`)
- **Steps:**
  1. Run automated AM matrix tests in `LabAssistant.UI.Tests/Tests/MilestoneAMScenarioMatrixTests.cs`.
  2. Run the Milestone AM checklist in `docs/07-testing/Archived/milestone-am-diagnostics-extraction-checklist.md`.
  3. Verify the shared Diagnostics foundation remains Overview-first, route-stable, and host/delegation only.
  4. Verify Overview lane summary/actions sanity plus shared-host non-ownership.
  5. Verify Logs filter/query, selection/detail, reload/clear/open-location sanity.
  6. Verify route switching preserves the long-lived Diagnostics workspace model and refresh/reconcile behavior rather than per-navigation recreation.
- **Expected:**
  - Shared `DiagnosticsWorkspaceComposition` remains the shared capability composition owner, while `MainWindow` remains out of Diagnostics-local workflow/state ownership.
  - `diagnostics.overview` remains the stable/default Diagnostics surface, while `diagnostics.logs` remains a distinct Diagnostics surface.
  - `DiagnosticsOverviewWorkspaceViewModel` and `DiagnosticsOverviewWorkspaceComposition` remain the Overview-local seams, with the narrowed Overview interaction/view surface still represented.
  - `DiagnosticsLogsWorkspaceViewModel`, `DiagnosticsLogsWorkspaceController`, and `DiagnosticsLogsWorkspaceComposition` remain the Logs-local seams, with the narrowed Logs interaction/view surface still represented.
  - Diagnostics remains a long-lived workspace whose route activation refreshes/reconciles the active lane instead of recreating the capability surface.
  - AM closure evidence links both deterministic automated seam protection and repeatable manual runtime verification without introducing runtime Diagnostics changes.

## Open Questions / TBDs
- Whether to split this file into smoke tests vs milestone regression suites as the product grows.
- Whether to add explicit pass/fail checklists for different Windows versions once compatibility targets are finalized.
