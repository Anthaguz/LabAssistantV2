# GUI Action Map - Deploy (Draft)

**Purpose:** Document what happens behind each major user action on the Deploy workflow and what runs automatically in the background. This is a migration-safety artifact for future UI redesign/migration.

**Status:** Draft (Phase 3A of UI migration prep).

**Scope:** Current Deploy workflow surfaces (`DeployPage`, `VmDetailPage`, shared `VmConfigPanel`) and the connected ViewModel/Business/Services code paths.

**Related:**
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/current-ui-capability-audit.md`

---

## 1. Why Deploy Is Mapped First

Deploy is the highest-risk migration surface because it combines:
- user-driven configuration actions
- automatic readiness/preflight behavior
- runtime orchestration (deploy/cancel/cleanup)
- summary and diagnostics output
- guest-step controls and readiness completeness enforcement

A UI migration can easily preserve the layout but break the behavior. This map exists to prevent that.

---

## 2. Primary UI Surfaces in the Deploy Workflow

## 2.1 `DeployPage` (main workflow shell)

**File**
- `LabAssistant/Views/DeployPage.xaml`
- `LabAssistant/Views/DeployPage.xaml.cs`

**DataContext**
- `DeploymentViewModel`

**Main responsibilities**
- top-level actions (Add VM, Save as Template, Deploy, Cancel)
- readiness summary/details display
- deployment outcome summary display
- VM entry cards (open details / remove)
- grouped runtime log display

## 2.2 `VmDetailPage` (per-VM editing page)

**File**
- `LabAssistant/Views/VmDetailPage.xaml`
- `LabAssistant/Views/VmDetailPage.xaml.cs`

**DataContext**
- `DeployVmConfigContext` (wraps one `VmDeploymentContext`)

**Main responsibilities**
- host per-VM config editing via shared `VmConfigPanel`
- back navigation to Deploy page

## 2.3 `VmConfigPanel` (shared VM configuration UI)

**File**
- `LabAssistant/Views/Controls/VmConfigPanel.xaml`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml.cs`

**Used by**
- Deploy VM detail (`DeployVmConfigContext`)
- Template VM detail/editor (`TemplateVmConfigContext`) [different workflow]

**Deploy-specific importance**
- edits fields that trigger quick preflight refresh
- contains VHDX catalog interactions (embedded Assets capability action)
- exposes guest-step controls and placeholders

---

## 3. Core ViewModel / Orchestration Path (Deploy)

## 3.1 UI entry ViewModel
- `LabAssistant/ViewModels/DeploymentViewModel.cs`

This is the main behavior hub for Deploy. It owns:
- commands (`AddVm`, `SaveAsTemplate`, `DeployAll`, `CancelDeployment`, `OpenVmDetail`, `OpenVmOutcomeDetail`, `DeleteVm`)
- readiness state and quick/full preflight orchestration
- deploy UI interactivity state
- log aggregation / error feed integration
- deployment summary generation hookup

## 3.2 Business orchestration (deploy runtime)
- `LabAssistant.Business/Deployment/MultiVmDeploymentCoordinator.cs`
- `LabAssistant.Business/Deployment/DeploymentPipelineBuilder.cs`
- `LabAssistant.Business/Deployment/DeploymentStep.cs`
- `LabAssistant.Business/Deployment/VmCleanupOrchestrator.cs`
- `LabAssistant.Business/Deployment/DeploymentOutcomeSummaryBuilder.cs`

## 3.3 Business orchestration (readiness/preflight)
- `LabAssistant.Business/Deployment/DeploymentPreflightService.cs`
- `LabAssistant.Business/Deployment/BaseVhdxIntegrityPreflightCheck.cs`
- `LabAssistant.Business/Deployment/DestinationPathStoragePreflightCheck.cs`
- `LabAssistant.Business/Deployment/GuestStepConfigurationCompletenessPreflightCheck.cs`

## 3.4 Service-level side effects (selected)
- Hyper-V and PowerShell services (create VM, create VHD, networking, etc.)
- filesystem/copy/delete operations
- structured logging (`IStructuredLogger`)
- debug logging (`DebugLogger`)

---

## 4. User Actions -> Code Paths (DeployPage)

This section maps visible actions to code entry points and major downstream effects.

## 4.1 Action: `Add VM`

**UI control**
- `DeployPage.xaml` button -> `AddVmCommand`

**Entry point**
- `DeploymentViewModel.AddVm()` (`[RelayCommand]`)

**What it does**
- Creates a new `VmDeploymentContext` with default values:
  - generated `VmId`
  - default name (`VM<n>`)
  - default VM/VHD paths from settings
  - default memory/CPU
  - initial switch selection (first available switch if present)
- Applies deployment policy from settings (`PerVmFailFast`, non-blocking optional steps)
- Wraps context in `VmEntryViewModel`
- Adds to `VmEntries`
- Wires rename synchronization for log group display name
- Triggers quick preflight refresh (`RequestQuickPreflightRefresh()`)

**Side effects**
- None to Hyper-V/filesystem yet (in-memory only)
- Readiness state may change asynchronously after debounce

**Migration preservation notes**
- Default path generation and initial switch assignment are behavior, not just UI defaults
- Adding a VM must trigger quick preflight refresh

---

## 4.2 Action: `Delete VM` (from VM card trash icon)

**UI control**
- VM card trash icon -> `DeleteVmCommand` with `VmEntryViewModel`

**Entry point**
- `DeploymentViewModel.DeleteVm(VmEntryViewModel)`
- delegates to `RemoveVm(...)`

**What it does**
- Removes VM entry from `VmEntries`
- Removes corresponding log group (`LogGroups`) for that VM
- Triggers quick preflight refresh

**Current behavior notes**
- This removes the VM from the current Deploy workspace only
- It does **not** delete a Hyper-V VM on the host (that future capability belongs to `Machines`)

**Migration preservation notes**
- Keep this distinction explicit to avoid confusion with future Machines page delete actions

---

## 4.3 Action: `Open VM detail` (VM card body click or outcome "Open VM")

**UI controls**
- VM card main button -> `OpenVmDetailCommand`
- Outcome summary "Open VM" button -> `OpenVmOutcomeDetailCommand`

**Entry points**
- `DeploymentViewModel.OpenVmDetail(VmEntryViewModel)`
- `DeploymentViewModel.OpenVmOutcomeDetail(VmDeploymentOutcomeSummary)`

**What it does**
- Navigates `MainWindow.MainContentFrame` to `VmDetailPage`
- Creates `DeployVmConfigContext` for the selected `VmDeploymentContext`
- `VmConfigPanel` becomes the active editor UI for that VM

**Background behavior in VM detail**
- Field edits can trigger quick preflight refresh (see Section 6)
- VHDX selection/import in `VmConfigPanel` can update catalog and VM config

**Migration preservation notes**
- Outcome summary -> VM detail drilldown is a real behavior dependency
- Must preserve "return to Deploy" path/state restoration semantics (currently via callback navigation)

---

## 4.4 Action: `Save as Template`

**UI control**
- `DeployPage.xaml` button -> `SaveAsTemplateCommand`

**Entry point**
- `DeploymentViewModel.SaveAsTemplate()`

**Preconditions / blocking checks**
- Requires at least one VM entry
- Requires valid VHDX selections (`ValidateVhdxSelections("Save as Template")`)

**User interaction chain**
1. Template metadata dialog (`TemplateSaveDetailsDialog`)
2. Review dialog (`TemplateSaveReviewDialog`)
3. Save to template folder via `ILabTemplateStore.SaveToFolder(...)`

**What it builds**
- Creates canonical `LabTemplate`
- Creates per-VM `VmTemplate` entries from current deploy contexts
- Persists guest-step toggle configs (implemented step toggles) in template shape

**Side effects**
- Writes template JSON file to configured template folder
- Emits structured log event (`TemplateSaved`) with `operationId`
- Shows success/failure message box

**Migration preservation notes**
- This action is a major cross-capability bridge: `Deploy` -> `Templates`
- Preserve dialogs + review step + structured logging behavior (even if UI changes form)

---

## 4.5 Action: `Deploy`

**UI control**
- `DeployPage.xaml` button -> `DeployAllCommand`

**Entry point**
- `DeploymentViewModel.DeployAllAsync()`

**High-level flow**
1. Validate VHDX selections (catalog/load-based check)
2. Cancel pending quick preflight debounce
3. Run **full preflight** (`DeploymentPreflightMode.Full`)
4. If blocking readiness failures:
   - do not start deployment
   - keep readiness report visible
   - log/readiness status message updated
5. If preflight passes/warns:
   - reset deployment UI state/logs
   - reset VM runtime contexts for new operation
   - create `MultiVmDeploymentContext` with `operationId`
   - call `_coordinator.DeployAllAsync(...)`
6. On completion/failure:
   - build `DeploymentSummary`
   - refresh UI interactivity state
   - append per-VM logs into UI log stream

**Major downstream business behaviors**
- pipeline execution
- cleanup on failure/cancel (Milestone R)
- runtime states and cancellation handling
- per-VM and global summaries
- guest-step execution/skip reporting (Milestone W)
- structured logging and diagnostics context (Milestones S/U/V/W)

**Migration preservation notes**
- Full preflight gating before any Hyper-V work is non-negotiable behavior
- Deploy button enablement is derived from readiness + operation state
- UI migration must preserve "authoritative full preflight on click" even if quick preflight looks clean

---

## 4.6 Action: `Cancel`

**UI control**
- `DeployPage.xaml` button -> `CancelDeploymentCommand`
- Only visible/enabled when `CanCancelDeploymentOperation` / `ShowCancelDeploymentAction`

**Entry point**
- `DeploymentViewModel.CancelDeployment()`

**What it does**
- Requests user cancellation on active `MultiVmDeploymentContext`
- Updates operation state and UI logs ("Cancellation requested...")

**Downstream behavior**
- Cancellation occurs at safe boundaries in coordinator/pipeline flow
- Cleanup may run for created resources (Milestone R)
- Terminal summary/state reflects cancellation and residuals if present

**Migration preservation notes**
- Cancel is operation-scoped, not per-VM action
- UI must preserve "Cancelling..." style state transitions and disable/re-enable rules

---

## 5. Readiness Panel (Visible UI) -> Behavior Mapping

## 5.1 What the readiness panel shows

Bound to `DeploymentViewModel`:
- `ReadinessReport`
- `IsReadinessCheckInProgress`
- `ReadinessStatusMessage`
- counts (Pass/Warn/Fail)
- blocked state (`IsDeployBlockedByReadiness`)

Readiness details are now:
- compact summary visible
- detailed list in collapsible `Expander`
- scroll-constrained (`#242` usability fix)

## 5.2 What actually drives the readiness content

`DeploymentViewModel` -> `IDeploymentPreflightService` -> registered preflight checks:
- base VHDX integrity (`#210`)
- destination path/storage feasibility (`#211`)
- guest-step config completeness (`#234`)
- any future checks added via DI

## 5.3 Semantics that must be preserved
- Quick preflight updates on relevant edits
- Full preflight runs on Deploy click
- Full preflight is authoritative for deploy gating
- Blocking failures prevent deploy
- Warnings do not block deploy
- Placeholder visibility (guest network) does not block by itself

---

## 6. Automatic / Background Behaviors (Critical for Migration)

These are easy to lose in a UI rewrite because they are not tied to explicit buttons.

## 6.1 Automatic: Load available virtual switches on `DeploymentViewModel` construction

**Path**
- `DeploymentViewModel` constructor -> `LoadAvailableSwitches()`
- `VirtualSwitchProvider.GetVirtualSwitchesAsync()`

**Behavior**
- Populates `AvailableSwitches`
- Triggers quick preflight refresh after switch list load

**Migration note**
- Initial readiness state depends on switch discovery completing

---

## 6.2 Automatic: Quick preflight refresh on relevant deploy changes

**Trigger sources**
- `DeploymentViewModel.RequestQuickPreflightRefresh()` called from:
  - VM add/remove
  - selected VM property changes (`VmEntryViewModel` monitored fields)
  - `DeployVmConfigContext` property changes (name, switch, VHDX, base path, guest-step toggles)
  - startup switch load

**Behavior**
- Debounced async execution (`_quickPreflightDebounce`, default ~350ms)
- Cancels superseded requests
- Uses request versioning to discard stale results
- Updates readiness report/status on UI thread

**Migration note**
- This is a key "feels responsive" behavior and also a correctness behavior
- Do not replace with only manual validation buttons

---

## 6.3 Automatic: UI interactivity state changes

`DeploymentViewModel` computes command/button enablement based on:
- `IsDeploying`
- active operation context presence
- `OperationState`
- deploy-start preflight in progress
- readiness blocking state

This drives:
- editability of config areas
- Add VM / Save as Template / Deploy / Cancel actions

**Migration note**
- These are state rules, not purely visual button states

---

## 6.4 Automatic: Runtime log grouping and error-feed publication

**Path**
- `VmDeploymentContext.LogCallback` -> `DeploymentViewModel.AddLog(...)`

**Behavior**
- Maintains per-VM `LogGroups`
- Updates UI log panel
- Publishes global error feed items for runtime error-like log messages

**Migration note**
- This cross-cuts shell UI and Deploy UI
- Error feed + Deploy logs relationship should be preserved or intentionally redesigned

---

## 6.5 Automatic: Deployment summary generation after operation completion

**Path**
- `DeployAllAsync()` finally block -> `_outcomeSummaryBuilder.Build(multiContext)`

**Behavior**
- Builds global + per-VM summary including:
  - cleanup outcomes
  - residuals
  - guest-step outcomes
- Drives summary UI in `DeployPage`

**Migration note**
- Summary is data-driven and should remain so in new UI

---

## 7. Embedded Cross-Capability Actions Inside Deploy (Important IA Note)

Deploy contains actions that belong conceptually to other future capabilities but are valid in-flow shortcuts.

## 7.1 Embedded Assets actions in `VmConfigPanel`

**Actions**
- `Select VHDX`
- `Import VHDX`

**Path**
- `VmConfigPanel.xaml.cs` (`SelectVhdx_Click`, `AddVhdx_Click`)
- `CatalogService` load/save
- `VhdxSelectorDialog` / `VhdxCatalogEditDialog`

**Behavior**
- Reads/updates VHDX catalog
- Applies selected catalog item to the VM config context
- Can persist catalog changes from inside Deploy

**Migration implication**
- Even after Assets is consolidated, keep fast in-flow asset selection/import shortcuts

---

## 7.2 Embedded Template bridge in Deploy

**Action**
- `Save as Template`

**Capability crossing**
- `Deploy` -> `Templates`

**Migration implication**
- Keep this as an in-flow shortcut even if Templates becomes a separate top-level area

---

## 8. Known Current-State UX/Behavior Limitations (Deploy)

These are not all bugs; some are expected current-state constraints.

### 8.1 Guest-step payload editors are missing (post-Milestone W current state)
- Optional guest-step toggles exist
- Readiness correctly blocks when enabled but config is incomplete
- This is expected until payload editors are implemented

### 8.2 Deploy page remains dense despite `#242`
- `#242` fixed the readiness panel domination issue
- The page is still a dense workflow surface by design
- Future redesign should improve structure without changing behavior

### 8.3 `LogsPage` exists as a separate surface
- Deploy logs also appear in the Deploy page
- Logs/Diagnostics IA remains transitional

---

## 9. Migration Preservation Checklist (Deploy)

When redesigning/migrating the Deploy UI, preserve these behaviors explicitly:

- `Add VM` creates default VM context and triggers quick preflight
- VM remove deletes UI VM entry and log group (workspace-only, not host VM delete)
- VM detail editing uses shared VM config behavior and triggers quick preflight on relevant edits
- Full preflight runs before deploy and blocks Hyper-V work on failures
- Quick preflight remains automatic and debounced
- Cancel remains operation-scoped and safe-boundary based
- Deployment summary is generated from runtime context/summaries, not UI recomputation
- Guest-step outcomes (executed/skipped + `skipReason`) remain visible in per-VM outcomes and structured logs
- Embedded VHDX catalog actions remain available from deploy configuration flow
- `Save as Template` bridge remains available from Deploy

---

## 10. Open Questions / TBDs (Deploy Action Map)

- Should post-deploy VM actions (Open console / RDP) appear in Deploy outcomes first, Machines page first, or both?
- Should Deploy page keep the current split (VM cards + log panel) in the future UI, or move logs/summaries into a secondary pane/tab model?
- How much of the global error feed should remain visible in the app shell while also showing rich Deploy summaries?
