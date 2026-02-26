# Code Path Index (Draft)

**Purpose:** Provide a practical "where to read first" index for understanding what code paths are taken by key GUI actions and automatic behaviors. This is a migration/onboarding aid, not a replacement for the detailed GUI action maps.

**Status:** Draft (Phase 5A of UI migration prep).

**Scope (current draft):**
- Deploy workflows (highest-risk behavior surface)
- Templates workflows (currently fragmented across multiple pages)

**Related:**
- `docs/03-architecture/gui-action-map.deploy.md`
- `docs/03-architecture/gui-action-map.templates.md`
- `docs/02-ux/current-ui-capability-audit.md`
- `docs/02-ux/migration-preservation-matrix.md`

---

## 1. How to Use This Index

Use this when you want to answer:
- "What happens when I click this button?"
- "Where does this UI state come from?"
- "What runs automatically in the background?"

Read in this order:
1. **UI surface** (XAML + code-behind) to identify the user action and binding/event
2. **ViewModel/context** to see command/state/orchestration entry point
3. **Business** for workflow/preflight/summary orchestration
4. **Services/Data** for external actions and persistence
5. **Models** for shared runtime/persistence state

For exact action semantics and side effects, cross-reference the GUI Action Maps.

---

## 2. Deploy Code Path Index

### 2.1 Start Here (Deploy workspace)

Primary files to read first:
- `LabAssistant/Views/DeployPage.xaml`
- `LabAssistant/Views/DeployPage.xaml.cs`
- `LabAssistant/ViewModels/DeploymentViewModel.cs`

Shared VM editor surface used by Deploy:
- `LabAssistant/Views/VmDetailPage.xaml`
- `LabAssistant/Views/VmDetailPage.xaml.cs`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml.cs`
- `LabAssistant/ViewModels/DeployVmConfigContext.cs`

If you only read one file first for Deploy behavior, read:
- `LabAssistant/ViewModels/DeploymentViewModel.cs`

It is the main UI orchestration point for:
- VM list/workspace state
- readiness/preflight calls
- deploy/cancel commands
- summary/log display state
- error feed publication

---

### 2.2 Deploy: "Add VM" / VM list editing

Read in order:
1. `LabAssistant/Views/DeployPage.xaml`
   - identify the add/remove/open-detail bindings
2. `LabAssistant/ViewModels/DeploymentViewModel.cs`
   - VM entry collection management
   - default VM path/name generation
   - command enablement and UI state refresh
3. `LabAssistant/ViewModels/VmEntryViewModel.cs`
   - card/list row projection and per-VM UI state
4. `LabAssistant/ViewModels/DeployVmConfigContext.cs`
   - per-VM editable config mapping (including guest-step toggles)

Watch for automatic behaviors tied to edits:
- quick preflight refresh triggers
- switch-loading interactions
- readiness panel state updates

---

### 2.3 Deploy: Readiness / Preflight (automatic + Deploy click gating)

Read in order:
1. `LabAssistant/ViewModels/DeploymentViewModel.cs`
   - quick preflight debounce/versioning
   - full preflight on Deploy click
   - blocked/not-blocked UI state
2. `LabAssistant.Business/Deployment/DeploymentPreflightService.cs`
   - check ordering and report aggregation
3. Preflight checks (current implemented set)
   - `LabAssistant.Business/Deployment/VhdxIntegrityPreflightCheck.cs` (if present in current codebase)
   - `LabAssistant.Business/Deployment/DestinationPathStoragePreflightCheck.cs`
   - `LabAssistant.Business/Deployment/GuestStepConfigurationCompletenessPreflightCheck.cs`
4. Readiness models
   - `LabAssistant.Models/Deployment/DeploymentReadinessReport.cs` (or combined readiness model file)
   - `LabAssistant.Models/Deployment/DeploymentReadinessCheckResult` definitions (same file or adjacent)

Key background behavior to preserve:
- quick preflight is advisory/refresh-driven
- full preflight is authoritative before deployment starts
- readiness failures block before Hyper-V actions begin

---

### 2.4 Deploy: "Deploy" button -> runtime orchestration

Read in order:
1. `LabAssistant/ViewModels/DeploymentViewModel.cs`
   - `DeployAll` command path
   - preflight gating
   - `MultiVmDeploymentContext` construction
2. `LabAssistant.Business/Deployment/IDeploymentCoordinator.cs`
3. `LabAssistant.Business/Deployment/MultiVmDeploymentCoordinator.cs`
   - main runtime orchestration
   - cancellation handling
   - cleanup invocation
   - summary building
   - structured event emission
4. `LabAssistant.Business/Deployment/DeploymentPipelineBuilder.cs`
   - step sequence assembly (including guest steps)
5. `LabAssistant.Business/Deployment/DeploymentStep.cs`
   - step lifecycle events (`StepStarted`, `StepCompleted`, `StepFailed`, `StepSkipped`)

Then read step implementations as needed:
- core VM provisioning steps in `LabAssistant.Business/Deployment/VmSteps/*`
- guest-step path in:
  - `LabAssistant.Business/Deployment/VmSteps/GuestOsConfigurationStep.cs`
  - `LabAssistant.Business/Deployment/GuestSteps/*`

---

### 2.5 Deploy: "Cancel" button -> cancellation + cleanup path

Read in order:
1. `LabAssistant/ViewModels/DeploymentViewModel.cs`
   - cancel command availability and UI interactivity behavior
2. `LabAssistant.Business/Deployment/MultiVmDeploymentCoordinator.cs`
   - user cancellation request handling
   - operation state transitions
3. Cleanup orchestration
   - `LabAssistant.Business/Deployment/VmCleanupOrchestrator.cs`
   - cleanup result models in `LabAssistant.Models/Deployment/*Cleanup*`
4. Outcome summary mapping
   - `LabAssistant.Business/Deployment/DeploymentOutcomeSummaryBuilder.cs`
   - `LabAssistant.Models/Deployment/DeploymentOutcomeSummary.cs`

Important migration note:
- cancellation and cleanup are not just button actions; they are stateful background workflows with terminal-state UI recovery requirements.

---

### 2.6 Deploy: "Save as Template" bridge (cross-capability path)

Read in order:
1. `LabAssistant/ViewModels/DeploymentViewModel.cs`
   - save-as-template command path
   - mapping from runtime VM state -> template model
2. Template persistence path
   - `LabAssistant.Data/Templates/LabTemplateStore.cs`
3. Template models
   - `LabAssistant.Models/Templates/LabTemplate.cs`
   - `LabAssistant.Models/Templates/VmTemplate.cs`
   - `LabAssistant.Models/Templates/GuestStepConfigs.cs`

Why this matters:
- Deploy is already a producer of Templates.
- UI migration must preserve this cross-capability bridge.

---

### 2.7 Deploy: Where logging / diagnostics / summaries are emitted

Read in order:
1. `LabAssistant.Business/Deployment/MultiVmDeploymentCoordinator.cs`
   - canonical structured workflow events
2. `LabAssistant.Models/Deployment/VmDeploymentContext.cs`
   - runtime state, failures, guest-step outcomes
3. `LabAssistant.Business/Deployment/DeploymentOutcomeSummaryBuilder.cs`
4. Structured logging services
   - `LabAssistant.Services/Logging/*`

For runtime failure diagnostics enrichment:
- `LabAssistant.Business/Deployment/VmSteps/CreateVhdStep.cs`
- `LabAssistant.Business/Deployment/VmSteps/CreateVmStep.cs`
- `LabAssistant.Business/Deployment/VmSteps/CreateVmfolderStep.cs`

---

## 3. Templates Code Path Index

### 3.1 Start Here (current fragmented Templates workflow)

Current surfaces are split. Read these first:
- `LabAssistant/Views/TemplatesPage.xaml.cs` (library/list entry point)
- `LabAssistant/Views/TemplateDetailsPage.xaml.cs` (details + VHDX mapping path)
- `LabAssistant/Views/TemplateEditorPage.xaml.cs` (editor workflow shell)
- `LabAssistant/ViewModels/TemplateEditorViewModel.cs`

Shared template VM config context:
- `LabAssistant/ViewModels/TemplateVmConfigContext.cs`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml.cs`

Template persistence + normalization:
- `LabAssistant.Data/Templates/LabTemplateStore.cs`

---

### 3.2 Templates: list / reload / open details

Read in order:
1. `LabAssistant/Views/TemplatesPage.xaml`
2. `LabAssistant/Views/TemplatesPage.xaml.cs`
   - `LoadTemplates()`
   - reload button
   - double-click navigation to details
3. Template stores/services used in page code-behind
   - `ILabTemplateStore` / `LabTemplateStore`
   - `IVhdxCatalogStore`
   - `IErrorFeedService` publishing compatibility warnings/load errors

Key behavior to understand:
- template list loading is coupled to catalog load (for reference resolution context)
- compatibility warnings/errors may publish to global shell error feed

---

### 3.3 Templates: details page actions (view + VHDX resolution/mapping)

Read in order:
1. `LabAssistant/Views/TemplateDetailsPage.xaml`
2. `LabAssistant/Views/TemplateDetailsPage.xaml.cs`
3. `LabAssistant/ViewModels/TemplateDetailsViewModel.cs`
4. Template-related business helpers (as referenced by the ViewModel/page)
   - missing VHDX resolution services
   - template selection/mapping services

Also cross-reference asset dialogs:
- `LabAssistant/Views/VhdxSelectorDialog.xaml.cs`
- `LabAssistant/Views/MissingVhdxResolutionDialog.xaml.cs`

Why this matters:
- Templates details workflow is not "read-only"; it already owns part of the asset reference repair path.

---

### 3.4 Templates: editor open / save / save-as / validation

Read in order:
1. `LabAssistant/Views/TemplateEditorPage.xaml`
2. `LabAssistant/Views/TemplateEditorPage.xaml.cs`
   - open/save/save-as
   - validation panel interactions
   - VM list <-> detail navigation
   - missing VHDX resolution integration
3. `LabAssistant/ViewModels/TemplateEditorViewModel.cs`
   - template state mutation
   - VM add/remove
   - load/save calls
   - validation state building
4. `LabAssistant.Data/Templates/LabTemplateStore.cs`
   - canonical save normalization
   - vmId generation/backfill behavior
   - guest-step payload persistence/placeholder omission behavior
5. `LabAssistant.Models/Validation/LabTemplateValidator.cs`

Key migration note:
- editor is currently the most complete template workflow, but it is disconnected from the template library entry path.

---

### 3.5 Templates: per-VM editing inside template editor

Read in order:
1. `LabAssistant/Views/TemplateVmDetailPage.xaml`
2. `LabAssistant/Views/TemplateVmDetailPage.xaml.cs`
3. `LabAssistant/ViewModels/TemplateVmConfigContext.cs`
4. Shared panel:
   - `LabAssistant/Views/Controls/VmConfigPanel.xaml`
   - `LabAssistant/Views/Controls/VmConfigPanel.xaml.cs`

Focus areas:
- mapping guest-step toggle UI state to `VmTemplate` guest-step configs
- VHDX catalog selection/import shortcuts in template context
- placeholder visibility vs persisted state behavior

---

### 3.6 Templates: where logging/error feed is triggered

Read in order:
1. `LabAssistant/Views/TemplatesPage.xaml.cs`
   - compatibility warnings and load errors -> global error feed
2. `LabAssistant/Views/TemplateEditorPage.xaml.cs`
   - compatibility warnings -> global error feed
3. Template persistence path for structured logs
   - `LabAssistant.Data/Templates/LabTemplateStore.cs`
   - any template operation service/bridge emitting `TemplateSaved` / `TemplateImported` structured events

Migration note:
- Template workflows currently combine local page message boxes and shell error feed publication.
- Future UI can improve presentation, but the distinction between blocking local errors and non-blocking warnings should be preserved.

---

## 4. Reading Order by Question (Quick Lookup)

### "Why did Deploy get blocked before starting?"
Read:
1. `LabAssistant/ViewModels/DeploymentViewModel.cs`
2. `LabAssistant.Business/Deployment/DeploymentPreflightService.cs`
3. preflight checks (`*PreflightCheck.cs`)

### "Why did a guest step show skipped/not_selected or not_implemented?"
Read:
1. `LabAssistant.Business/Deployment/VmSteps/GuestOsConfigurationStep.cs`
2. `LabAssistant.Business/Deployment/GuestSteps/*`
3. `LabAssistant.Models/Deployment/GuestStepExecutionOutcome.cs`

### "Why do template edits require file browsing today?"
Read:
1. `LabAssistant/Views/TemplatesPage.xaml.cs`
2. `LabAssistant/Views/TemplateEditorPage.xaml.cs`
3. `docs/02-ux/current-ui-capability-audit.md` (Templates fragmentation section)

### "Where is template JSON normalized and why do fields appear/disappear?"
Read:
1. `LabAssistant.Data/Templates/LabTemplateStore.cs`
2. `LabAssistant.Models/Templates/*`
3. `LabAssistant.Models/Validation/LabTemplateValidator.cs`

---

## 5. Next Planned Expansions (Phase 5B+)

Add code-path index sections for:
- Assets (`VHDX Catalog`, missing VHDX resolution, embedded asset actions)
- Settings / Diagnostics / Shell (settings side effects, error feed, transitional `LogsPage`)
- Future `Machines` capability (once contract + implementation exists)

These should align with the existing GUI action maps so migration planning stays behavior-first and navigable.
