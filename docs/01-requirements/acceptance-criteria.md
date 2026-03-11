# Acceptance Criteria

**Purpose:** This document is the *implementation contract* for LabAssistantV2 features.
It defines required behavior, failure handling, logs, and side effects in a way that is:
- testable,
- regression-safe,
- and aligned with the SRS.

**Traceability Rule:** Every AC maps to one or more FRs from `srs.md`.

---

# Global Acceptance Rules (apply to ALL features)

## GR-01 — No Guessing / No Silent Failure
- Any invalid input or unmet prerequisite must be surfaced to the user with an actionable message.
- The system must not proceed with partial execution if required prerequisites are missing.

## GR-02 — Resource Safety (no orphaned resources)
- For failed operations, the system must either:
  - rollback created resources, **or**
  - present a clear “created resources” report with cleanup steps.
- The chosen strategy must be consistent and documented.
- Canonical policy reference: `docs/01-requirements/cleanup-cancellation-policy.md`.

## GR-03 — Observability Required
- Every major operation must log: start, key steps, completion/failure.
- Logs must be structured and include an operation identifier (`operationId`) per operation.
- Canonical logging contract reference: `docs/01-requirements/logging-contract.md`.

## GR-04 — UX Feedback
- Long operations must show progress (step-based or %) and the current stage.
- If cancellation is supported, it must be visible and responsive.

---

# AC-001 — Deploy Lab From Template (Multi-VM)

**Related FRs:** FR-022, FR-023, FR-024, FR-025, FR-026, FR-028, FR-031, FR-043, FR-044, FR-045, FR-046, FR-053, FR-055, FR-056, FR-057, FR-058, FR-059, FR-040, FR-041

## Scenarios

### 1) Happy Path — Successful Multi-VM Lab Deployment
**Given**
- Hyper-V is enabled
- All required base disks are available (or valid substitutes exist per base disk mapping rules)
- Required virtual switches exist
- A valid lab template is selected

**When**
- The user initiates **Deploy Lab**

**Then**
- Differencing disks are created as required
- All VMs are created and configured as defined
- All VMs are attached to the required virtual switches
- Progress is shown for overall deployment and per-VM
- Operation ends with success + a summary of created resources
- Logs exist for start → each step → completion

### 2) Validation Failure — Invalid Template
**Given**
- Template missing required fields (VM definitions, schema version, base disk reference, etc.)

**When**
- User attempts to deploy

**Then**
- Deployment does not start
- UI shows a clear validation report
- No Hyper-V resources are created
- A validation failure is logged

### 3) Environment Failure — Hyper-V Disabled / Missing Switch
**Given**
- Hyper-V is disabled OR a required switch is missing

**When**
- User attempts to deploy

**Then**
- Deployment is blocked with actionable guidance
- No partial resources are created
- Environment failure is logged

### 4) Runtime Failure — Partial Deployment
**Given**
- Deployment started and at least one resource was created

**When**
- A failure occurs during disk creation / VM creation / network attachment

**Then**
- Operation stops with failure state
- Resource handling follows GR-02 (rollback OR cleanup report)
- UI shows recovery guidance and concise likely artifact/path hints when known (for example base VHDX path, target VHD path, VM path)
- Logs include per-VM failure details and overall failure summary, including structured artifact/path context when known

### 5) Preflight Failure - Blocking Readiness Issues
**Given**
- The Deploy page configuration contains one or more blocking readiness failures (for example: missing required switch, invalid/corrupt base VHDX, or invalid/unwritable destination path)

**When**
- The user clicks **Deploy Lab**

**Then**
- A **full preflight** runs before any Hyper-V actions start
- Deployment is blocked (no Hyper-V resources are created)
- The Deploy page shows a readiness report that identifies blocking failures and affected VM(s) when applicable
- Messages include actionable guidance and likely cause/path hints
- Structured logs record the preflight outcome

### 6) Preflight Warning - Deploy Allowed With Warnings
**Given**
- The Deploy page configuration contains warnings only (for example: low or unknown free space in v1)

**When**
- The user clicks **Deploy Lab**

**Then**
- A **full preflight** runs before any Hyper-V actions start
- The readiness report shows warnings distinctly from blocking failures
- Deployment is allowed to proceed
- Structured logs record the warning outcome and deployment start

## Expected UI
- Template selection UI + Deploy action
- Readiness/preflight report that distinguishes blocking failures vs warnings
- Quick readiness feedback updates on relevant Deploy-page configuration changes
- Deployment progress UI:
  - overall status
  - per-VM status (Creating disk / Creating VM / Attaching network / Done)
- Completion summary:
  - list of VMs created
  - mapped base disks used
  - switches attached
- Clear error views:
  - validation report
  - environment/prerequisite failures
  - runtime failure with recovery steps

## Deploy Readiness / Preflight Contract (Milestone U)

### Readiness result semantics (contract-level)
Each readiness result shall include, at minimum:

- `status`: `Pass` | `Warn` | `Fail`
- `category`: check category
- `code`: machine-readable identifier suitable for logic/tests/logging
- `message`: user-facing summary
- `actionableGuidance`: what to do next
- `affectedVmNames` (optional): one or more impacted VM names when VM-specific
- `resourcePath` or `resourceName` (optional): path/resource hint when relevant

### Required readiness check categories (minimum v1)
- Environment
- Template/config
- VHDX/base disk
- Network/switch
- Destination path/storage

### Blocking vs warning policy (v1)
**Blocking (`Fail`) - deployment must not start**
- Missing/invalid base VHDX reference with no valid substitute
- Invalid/corrupt/unreadable base VHDX during deploy preflight
- Missing required virtual switch
- Invalid/unwritable destination path (VM path, differencing disk path, or other required output path)
- Any prerequisite failure that prevents safe Hyper-V actions

**Warning (`Warn`) - deployment may proceed**
- Low free space (estimated risk)
- Unknown/unverifiable free space
- Other non-blocking readiness concerns where deployment can still proceed safely in v1

**Policy note (v1)**
- Free-space readiness is warn-only in v1 (never a deploy blocker by itself), because differencing disk growth is time-dependent and exact required space cannot be predicted reliably.

### Quick vs full preflight behavior
**Quick preflight (automatic on relevant Deploy-page configuration changes)**
- Purpose: fast feedback while the user edits deployment configuration
- Runs automatically when relevant inputs change
- May run cheap/partial checks only
- Is not the authoritative deploy gate

**Full preflight (on Deploy click)**
- Runs when the user initiates deployment and completes before any Hyper-V actions begin
- Produces the authoritative readiness gating decision
- Deployment must not begin if any `Fail` results exist
- Deployment may proceed if results are only `Pass`/`Warn`

### User-facing readiness report behavior (contract)
- Deploy page shall show readiness/preflight results
- Blocking failures and warnings shall be clearly distinguished
- Deploy action shall be prevented when full preflight returns any blocking failures
- Deploy action shall remain available when results are warnings only
- Messages shall be actionable and include likely cause/path hints
- Raw PowerShell stderr is not required in the primary readiness UI and remains primarily in diagnostics/debug logs

## Guest-Step Execution Controls Contract (Milestone W)

### Guest-step taxonomy (contract)
- **Mandatory implemented steps**
  - Required by the current deployment pipeline behavior and executed when the runtime flow reaches them.
- **Optional implemented steps**
  - Executed only when the user enables them **and** required configuration is present/valid.
- **Visible placeholders (not implemented)**
  - Shown in the UI to communicate future capability.
  - Must be clearly labeled as not implemented.
  - Must not execute at runtime.

### Placeholder behavior (current contract)
- Placeholder visibility must not block deployment by itself.
- Placeholder steps may appear in mandatory or optional UI sections for future planning/consistency.
- Current deployment behavior shall allow placeholder steps to be skipped.

### Optional-step enablement and configuration completeness
- If an optional implemented step is **not enabled**, it shall be skipped.
- If an optional implemented step is enabled and required configuration is present/valid, it shall run.
- If an optional implemented step is enabled but required configuration is missing/incomplete, deployment shall be blocked before runtime execution with actionable configuration guidance.

### Network settings grouping (UI behavior contract)
- Hyper-V network attachment settings (for example switch selection / NIC attachment) and guest OS network configuration settings (for example IP, default gateway, DNS servers) shall remain in the same conceptual **Network** area of the UI.
- Milestone W does **not** change current v1 Hyper-V switch/NIC deployment requirements unless explicitly updated in a separate contract change.
- Guest OS network configuration may be shown as optional/placeholder until implemented.

### Execution result visibility (contract)
- Skipped guest-step outcomes shall be visible in the **per-VM deployment summary**.
- Skipped guest-step outcomes shall also be emitted in **structured logs**.
- Structured log entries for skipped placeholder steps shall use:
  - `result = skipped`
  - `skipReason = not_implemented` (when applicable)
- Global summary skipped counts are optional unless explicitly introduced by a later feature contract.

### Runtime semantics (future implementation expectation)
- Mandatory implemented steps execute according to pipeline order and current deployment semantics.
- Optional implemented steps execute only when enabled and configured.
- Placeholder steps are skipped with explicit status and must not silently attempt execution.
- These rules must remain compatible with GR-02 (resource safety) and GR-04 (clear user feedback).

## Expected Logs
**Minimum events**
- `DeployLabStarted`
- `TemplateValidated` / `TemplateValidationFailed`
- `VmDeployStarted` (per VM)
- `DifferencingDiskCreated` (per VM)
- `VmCreated` (per VM)
- `VmNetworkAttached` (per VM)
- `DeployLabCompleted` / `DeployLabFailed`

**Required fields (minimum)**
- operationId (canonical structured field; legacy `correlationId` may appear in transitional text logs)
- templateId/templateName
- vmName
- baseDiskId (and substituted baseDiskId if mapping occurs)
- switchName(s)
- result (success/failure)
- errorCode/errorMessage/exceptionType (when failed)
- failureStepKey (when failed and available)
- known artifact/path context when available (for example `parentVhdPath`, `targetVhdPath`, `vmPath`)

## Expected Artifacts / Side Effects
- Differencing disk files created per VM
- Hyper-V VMs created per VM
- Hyper-V NIC connected to specified switch(es)
- Optional: local deployment record (TBD)

## Definition of Done
- [ ] All scenarios above pass
- [ ] Quick preflight runs automatically on relevant Deploy-page configuration changes
- [ ] Full preflight runs on Deploy click before any Hyper-V actions start
- [ ] Blocking readiness failures prevent deployment start
- [ ] Warning-only readiness results do not block deployment
- [ ] Readiness report distinguishes failures vs warnings with actionable messages
- [ ] Progress UI updates reliably for multi-VM
- [ ] Failure behavior conforms to GR-02
- [ ] Logs conform to GR-03 with operationId
- [ ] Tests exist for validation + deployment orchestration (mocks acceptable for Hyper-V)
- [ ] No orphaned resources without either rollback or explicit cleanup guidance

---

# AC-002 — Create/Edit/Delete Templates

**Related FRs:** FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-018, FR-019

## Scenarios

### 1) Happy Path — Create Template
**Given**
- User provides template fields required by schema (type, name, VM definitions, base disk refs, etc.)

**When**
- User clicks **Save Template**

**Then**
- Template is validated
- Template is persisted locally
- Template appears in template list
- Success feedback is shown
- Structured logs recorded

### 2) Happy Path — Edit Template
**Given**
- Existing template selected

**When**
- User edits and saves

**Then**
- Validation runs
- Existing template is updated
- Change is reflected in list/details
- Logs recorded

### 3) Happy Path — Delete Template
**Given**
- Existing template selected

**When**
- User deletes and confirms

**Then**
- Template is removed from tool
- Template file (or record) removed/marked deleted (TBD: retention)
- Logs recorded

### 4) Validation Failure — Missing Required Fields
**Given**
- Template has missing or invalid fields

**When**
- User attempts to save

**Then**
- Save is blocked
- UI highlights missing/invalid fields
- No template artifact is created/updated
- Validation failure logged

## Expected UI
- Template list + template details/editor
- Create / Save / Delete actions
- Inline validation messages pointing to fields (or a validation summary panel)
- Confirmation on delete

## Expected Logs
- `TemplateCreateStarted` / `TemplateCreated`
- `TemplateEditStarted` / `TemplateUpdated`
- `TemplateDeleteStarted` / `TemplateDeleted`
- `TemplateValidationFailed`
- Fields: operationId, templateId/name, templateType, schemaVersion, result, error details

## Expected Artifacts / Side Effects
- Template JSON file created/updated/deleted (or record in local store)
- Template schema version included in artifact
- Implemented guest-step execution selections/configuration may be persisted when supported.
- Placeholder-only guest-step UI affordances (visible but not implemented) do not require persisted template payloads by themselves.

## Definition of Done
- [ ] CRUD works for VM templates and Lab templates
- [ ] Validation errors are actionable and field-specific (or clearly mapped)
- [ ] Schema version always present
- [ ] Tests cover validation + persistence behavior
- [ ] Logs present for create/edit/delete and validation failure

---

# AC-003 — Import/Export Template (JSON)

**Related FRs:** FR-016, FR-017, FR-014, FR-015, FR-018

## Scenarios

### 1) Happy Path — Import Valid Template
**Given**
- User selects a valid JSON template with supported schema version

**When**
- User imports

**Then**
- Template validates successfully
- Template is added to template list
- UI confirms success
- Logs recorded

### 2) Failure — Malformed JSON / Invalid Schema
**Given**
- JSON is malformed OR missing required schema fields/version

**When**
- User imports

**Then**
- Import fails
- UI shows clear reason and what’s missing
- Nothing is registered
- Failure logged

### 3) Compatibility — Unsupported Schema Version
**Given**
- Schema version not supported

**When**
- User imports

**Then**
- If template major version is newer than supported by the app, import is blocked and user is told to update LabAssistant
- If template major version is older than support window (`N-1`), import is blocked with actionable migration guidance
- If template is within support window (`N` or `N-1`), import proceeds (with warning for compatible newer minor/patch when fields are understood)
- User is clearly informed
- Event logged

### 4) Happy Path — Export Template
**Given**
- Existing template selected

**When**
- User exports

**Then**
- JSON is produced with schema version
- User chooses destination
- Logs recorded

## Expected UI
- Import action (file picker) + validation result display
- Export action (save dialog)
- Clear warnings/errors for invalid or unsupported templates

## Expected Logs
- `TemplateImportStarted` / `TemplateImported` / `TemplateImportFailed`
- `TemplateExportStarted` / `TemplateExported` / `TemplateExportFailed`
- Fields: operationId, templateId/name, schemaVersion, filePath (optional), result, error details

## Expected Artifacts / Side Effects
- Imported template persisted locally
- Export produces a JSON file including schema version

## Definition of Done
- [ ] Import blocks invalid templates reliably
- [ ] Export always includes schema version
- [ ] Tests cover schema validation behavior
- [ ] Logs generated for all import/export actions

---

# AC-004 — Base Disk Import/Edit/Delete + Missing Base Disk Mapping

**Related FRs:** FR-050, FR-051, FR-052, FR-053, FR-054

## Scenarios

### 1) Happy Path — Register Base Disk
**Given**
- User selects a valid VHD/VHDX file path

**When**
- User registers/imports base disk

**Then**
- Base disk is added to base disk list
- User can edit display metadata (label, OS classification)
- Logs recorded

### 2) Happy Path — Remove Base Disk From Tool
**Given**
- Base disk is registered

**When**
- User removes it from registry

**Then**
- Base disk is removed from tool list
- Underlying file deletion behavior is explicit (TBD policy)
- Logs recorded

### 3) Deploy Template With Missing Base Disk → Mapping Works
**Given**
- Template references a base disk not registered
- Another base disk exists with same OS classification

**When**
- User deploys template

**Then**
- System maps to the compatible base disk
- UI notifies user of substitution
- Mapping decision logged

### 4) Deploy Template With Missing Base Disk → No Substitute
**Given**
- Template references missing base disk
- No compatible base disk exists

**When**
- User attempts deploy

**Then**
- Deploy blocked with actionable guidance (import disk / choose compatible)
- Logs recorded

## Expected UI
- Base disk list + register action
- Metadata edit UI (label, OS classification)
- Remove action with confirmation
- Missing disk mapping prompt/notification during deployment

## Expected Logs
- `BaseDiskRegisterStarted` / `BaseDiskRegistered`
- `BaseDiskMetadataUpdated`
- `BaseDiskRemoved`
- `BaseDiskMissingDetected`
- `BaseDiskMappedSubstitutionApplied` / `BaseDiskMappingFailed`
- Fields: operationId, baseDiskId, filePath (optional), osClassification, result, error details

## Expected Artifacts / Side Effects
- Base disk registry updated (local configuration/store)
- Optional: cached metadata records updated

## Definition of Done
- [ ] Base disk register/edit/remove works reliably
- [ ] Missing disk mapping behavior matches FR-054
- [ ] User always informed when substitution occurs
- [ ] Tests cover mapping decisions (pure logic) separate from Hyper-V calls
- [ ] Logs emitted for all operations

---

# AC-005 — Virtual Switch Management (if implemented)

**Related FRs:** FR-033, FR-034, FR-035

## Scenarios

### 1) Create New Switch
**Given**
- User provides required parameters (type, name, etc.)

**When**
- User creates switch

**Then**
- Switch exists in Hyper-V
- Switch appears in tool list
- Logs recorded

### 2) Modify Existing Switch
**Given**
- Existing switch selected

**When**
- User modifies it

**Then**
- Changes apply successfully
- Logs recorded

### 3) Delete Existing Switch
**Given**
- Existing switch selected

**When**
- User deletes and confirms

**Then**
- Switch removed from Hyper-V
- Tool list refreshes
- Logs recorded

## Expected UI
- Switch list + create/edit/delete actions
- Confirmation dialogs for destructive actions
- Clear permission/environment error messaging if Hyper-V denies action

## Expected Logs
- `SwitchCreateStarted` / `SwitchCreated` / `SwitchCreateFailed`
- `SwitchUpdateStarted` / `SwitchUpdated` / `SwitchUpdateFailed`
- `SwitchDeleteStarted` / `SwitchDeleted` / `SwitchDeleteFailed`
- Fields: operationId, switchName, switchType, result, error details

## Expected Artifacts / Side Effects
- Hyper-V virtual switch created/updated/deleted

## Definition of Done
- [ ] Switch CRUD works end-to-end (if feature is in scope)
- [ ] Permission errors are actionable
- [ ] Logs emitted for all operations

---

# AC-006 — Machines (Hyper-V VM Administration, v1)

**Related FRs:** FR-060, FR-061, FR-062, FR-063, FR-064, FR-065, FR-066, FR-040, FR-041

## Scenarios

### 1) Inventory — List All Host VMs
**Given**
- Hyper-V is enabled on the host

**When**
- User opens `Machines`

**Then**
- All Hyper-V VMs on the host are listed (including non-LabAssistant-created VMs)
- The list supports selecting a VM for operations
- Structured logs capture inventory load result

### 2) Basic Actions — Start/Stop VM
**Given**
- A VM is selected in `Machines`

**When**
- User clicks `Start` or `Stop`

**Then**
- Action is executed, or blocked with a concise actionable error
- UI reflects updated VM state or error state
- Structured logs include operationId, vm identity, action, and result

### 3) Basic Edit — CPU/Memory/Switch
**Given**
- A VM is selected in `Machines`

**When**
- User updates supported v1 settings (CPU, memory, switch) and applies

**Then**
- Requested edits are applied or rejected with explicit feedback
- No silent partial update is reported as success
- Structured logs capture attempted edits and result
- Edit workflow is draft-based:
  - edits remain local until `Apply`
  - unsaved-changes indicator is visible when draft differs from loaded host values
  - no dedicated `Reset` button is required in v1
  - navigating away (capability/subview/VM selection) discards unapplied draft state
- Network switch editing is adapter-aware for VMs with multiple network adapters

### 4) Connection Actions — Console and RDP
**Given**
- A VM is selected in `Machines`

**When**
- User views available connection actions

**Then**
- `Open Hyper-V Console` is available as a dedicated action
- `Open RDP` is a separate dedicated action
- `Open RDP` is disabled (grayed out) when readiness is unknown/unmet
- Disabled RDP state includes user-facing reason text/guidance
- Action attempts and results are logged

### 4a) RDP Readiness v1 — Host-Observable Checks and Recheck Behavior
**Given**
- A VM is selected in `Machines`

**When**
- RDP readiness evaluation runs (background poll and/or manual recheck)

**Then**
- Readiness state is represented as `Ready`, `NotReady`, `Unknown`, or `Checking`
- `Ready` requires all v1 criteria:
  - VM is running
  - VM has IPv4
- TCP 3389 is reachable from host
- `Ready` => `Open RDP` enabled
- `NotReady` or `Unknown` => `Open RDP` disabled with concise reason text/tool tip
- A manual recheck action is available and does not block UI interaction
- Transient probe timeout/cancellation/unreachable outcomes during background or manual checks are handled as non-fatal readiness outcomes and must not crash the app

### 5) Delete VM — Scope Selection and Confirmation
**Given**
- A VM is selected in `Machines`

**When**
- User initiates delete

**Then**
- UI offers delete scope options:
  - VM registration only
  - VM + associated disks/files
- Destructive action requires explicit confirmation
- Result is clearly reported
- Structured logs include selected delete scope and result

### 6) Delete Default Policy — Machines Deletion Policy Modes + Safety Guards
**Given**
- User configures Machines deletion policy in `Settings > Machines`

**When**
- User initiates delete in `Machines`

**Then**
- Supported persisted modes are:
  - Ask every time (default)
  - Always delete disks
  - Always delete disks for LabAssistant-provisioned VMs
  - Always delete disks for differencing disks only
- Default delete scope follows policy only when disk safety classification is safe
- Safety guardrails apply before auto-selecting delete-with-storage:
  - known base/full disks are never auto-selected for storage deletion
  - potential base/uncertain classification is never auto-selected for storage deletion
- Confirmation dialog still shows effective scope before destructive action

### 6a) Delete Cleanup Completeness — VM Folder and Disk Cleanup
**Given**
- User confirms delete with storage scope

**When**
- Delete completes

**Then**
- VM registration is removed
- Intended disk/file targets are removed where possible
- VM folder artifacts are removed when path is safe/owned
- Cleanup failures are not silent:
  - UI shows actionable status
  - failure context includes affected folder/disk paths
  - structured logs include cleanup failure details

### 7) Failure Handling — Actionable, Non-Silent
**Given**
- A Machines operation fails (permission/state/runtime/dependency)

**When**
- Operation completes

**Then**
- UI shows concise actionable error
- UI does not report false success
- Structured logs include operationId and failure context

## Expected UI
- `Machines` capability entry and VM inventory/list
- Selection model for VM operations
- Dedicated actions:
  - Start/Stop
  - Apply basic edits (CPU/memory/switch)
  - Open Hyper-V Console
  - Open RDP
  - Delete
- Explicit delete scope selection and confirmation
- Explicit disabled RDP state with reason text

## Expected Logs
- `MachineInventoryLoadStarted` / `MachineInventoryLoadCompleted`
- `MachineActionStarted` / `MachineActionCompleted` / `MachineActionFailed`
- `MachineDeleteStarted` / `MachineDeleteCompleted` / `MachineDeleteFailed`
- `MachineConnectionActionStarted` / `MachineConnectionActionCompleted` / `MachineConnectionActionFailed`
- Required fields:
  - operationId
  - vmId/vmName (when VM-scoped)
  - action name
  - policy mode and disk classification context (for delete actions)
  - selected delete scope (for delete actions)
  - result and error details on failure

## Definition of Done
- [ ] Machines inventory lists all host Hyper-V VMs
- [ ] Start/Stop operations are actionable and logged
- [ ] Basic edit operations (CPU/memory/switch) are actionable and logged
- [ ] Console and RDP are separate actions; RDP disabled-state behavior is explicit when unavailable
- [ ] RDP readiness v1 criteria (running + IPv4 + TCP 3389 reachability) gate button enablement
- [ ] Readiness checks are asynchronous/non-blocking and support manual recheck
- [ ] Transient probe timeout/cancellation outcomes do not crash Machines UI and are surfaced as readiness state updates (not exception-driven user errors)
- [ ] Delete flow supports VM-only vs VM+disks scopes with confirmation
- [ ] Machines deletion policy modes and safety guard behavior are defined and testable
- [ ] Delete-with-storage cleanup removes safe/owned VM folder artifacts or reports explicit actionable failure
- [ ] Failure behavior is actionable and non-silent
- [ ] Structured logging includes operationId and action context for Machines actions

---

# AC-007 â€” WinUI Parallel Shell Foundation (Milestone AA)

**Related FRs:** FR-067, FR-068, FR-069, FR-070, FR-071, FR-072

## Scenarios

### 1) Parallel UI Projects â€” Build and Launch
**Given**
- The solution includes WPF and WinUI UI projects

**When**
- User/developer builds and launches each UI project independently

**Then**
- Both UIs launch successfully
- WPF remains available for production usage
- WinUI shell startup is functional without requiring feature-parity migration

### 2) Shell Navigation â€” Icon Rail + Hamburger Drawer
**Given**
- WinUI shell is running

**When**
- User interacts with left navigation

**Then**
- Top-level capability icons are visible in icon rail
- Hamburger opens a slide-out capability drawer from left
- Drawer shows scrim over remaining app content
- Drawer closes on outside click and on `Esc`
- Capability navigation does not rely on hover-only full-menu behavior

### 3) Startup Route and Persistence Policy
**Given**
- WinUI app starts from a fresh launch

**When**
- App initialization completes

**Then**
- Default landing capability is `Machines`
- Last selected capability is not restored from previous session

### 4) Shell Issue Insights Panel
**Given**
- WinUI shell is running

**When**
- User views shell chrome

**Then**
- Insights panel is collapsed by default
- Warning/issue trigger can open insights panel
- Trigger shows visible count/badge when issues exist

### 5) Machines Details Navigation Pattern
**Given**
- User selects a VM in WinUI Machines page

**When**
- User navigates details sections

**Then**
- Details are organized by sections (for example Hardware, Storage, Network)
- CPU and Memory are grouped under Hardware-oriented context
- Breadcrumb reflects details-pane context (`Machines > VM > Section`)

### 6) Theme Foundation
**Given**
- WinUI shell is running

**When**
- User toggles light/dark theme

**Then**
- Theme switches through centralized semantic token dictionaries
- Core shell surfaces do not rely on hardcoded page-level foreground/background color values

## Expected UI
- WinUI shell with:
  - top app bar
  - left icon rail
  - hamburger drawer + scrim behavior
  - main content host
  - right insights panel (collapsed by default)
- `Machines` as initial capability route
- section-based details surface for Machines
- breadcrumb reflecting selected VM details context

## Expected Logs
- Startup and navigation interactions continue to honor canonical structured logging conventions where events exist
- Machines actions continue emitting operation-scoped structured events per AC-006/FR-066

## Definition of Done
- [ ] Parallel WPF + WinUI projects are present and launchable
- [ ] Icon-rail + hamburger drawer behavior matches contract (including scrim and dismiss interactions)
- [ ] WinUI defaults to Machines and does not persist last selected capability
- [ ] Insights panel is collapsed by default and warning trigger/badge behavior is present
- [ ] Machines details UX uses section navigation with breadcrumb context
- [ ] Theme token foundation (light/dark + semantic brushes) is implemented

---

# AC-008 — Diagnostics Structured Log Viewer (Phase 1)

**Related FRs:** FR-073

## Scenarios

### 1) Diagnostics > Logs subview exists (WinUI)
**Given**
- User is in WinUI shell

**When**
- User navigates to `Diagnostics > Logs`

**Then**
- A Logs subview is available
- Viewer loads from canonical structured log source (`structured-events.jsonl`)

### 2) Envelope columns + dynamic context rendering
**Given**
- Structured log entries are loaded

**When**
- User views the log list and selects an entry

**Then**
- Envelope columns render:
  - Timestamp
  - Level
  - Event
  - OperationId
  - Result
- Selected entry details render dynamic context as readable JSON/text
- Mixed event schemas do not break viewer rendering

### 3) Filtering/search
**Given**
- Viewer has loaded entries

**When**
- User applies filters/search

**Then**
- Viewer supports:
  - operationId filter
  - level filter
  - event filter
  - free-text search (envelope + serialized context)
  - basic start/end time filters

### 4) Malformed-line tolerance
**Given**
- Source JSONL contains malformed lines

**When**
- Viewer loads logs

**Then**
- Malformed lines are skipped
- Valid lines still render
- Parse error count/status is shown
- Viewer does not crash/blank

### 5) Power-user raw access
**Given**
- User needs direct file-level investigation

**When**
- User invokes `Open raw JSONL`

**Then**
- App opens file/folder for `structured-events.jsonl`
- Viewer remains read-only

## Out of Scope (Phase 1)
- Advanced timelines/visual analytics
- In-app log editing/deletion
- Remote ingestion/upload
- Unified legacy `log.txt` viewer

## Definition of Done
- [ ] Diagnostics Logs subview exists in WinUI
- [ ] Envelope columns render from structured JSONL
- [ ] Dynamic context inspection works for mixed schemas
- [ ] Filters/search operate as specified
- [ ] Malformed-line tolerance and parse-error reporting work
- [ ] Open raw JSONL action is available

---

# AC-009 — WinUI Layout Constraints and Scroll Ownership (AB1)

**Related FRs:** FR-074

## Scenarios

### 1) Contracted shell sizing model
**Given**
- WinUI shell is used as migration host

**When**
- A capability workspace is rendered

**Then**
- Top bar remains fixed/visible
- Main workspace uses bounded (`*`) sizing
- Drawer/rail/insights sizing follows documented fixed/explicit constraints
- Content host does not rely on unbounded top-level layout containers for complex surfaces

### 2) Explicit scroll ownership
**Given**
- A dense capability surface (for example Diagnostics Logs, Machines)

**When**
- Content exceeds visible space

**Then**
- One primary scroll owner is defined for the surface
- Nested scrolling appears only in bounded secondary regions
- Surface does not create ambiguous competing scroll regions

### 3) Overflow-safe interaction controls
**Given**
- User resizes window to compact widths

**When**
- Filter/action controls are shown

**Then**
- Primary actions remain visible/reachable (no hidden off-screen controls)
- Control rows wrap/collapse/scroll according to contract rules
- Long payload content scrolls in-place instead of stretching parent panels off-screen

### 4) First-class surface coverage
**Given**
- AB2 implementation planning is prepared

**When**
- Contract references are reviewed

**Then**
- Diagnostics Logs and Machines are explicitly covered as first-class layout examples
- AB2 checklist includes decomposition order, overflow hardening criteria, and regression checks

## Expected Artifacts
- Dedicated contract doc:
  - `docs/02-ux/winui-layout-constraints-contract.md`
- Migration plan links and gating references updated
- IA references updated for layout ownership and constraints

## Definition of Done
- [ ] WinUI layout constraints contract doc exists and is complete
- [ ] Contract defines testable rules for region sizing, scroll ownership, overflow, and resize behavior
- [ ] Diagnostics Logs and Machines are explicitly covered
- [ ] Migration plan references layout contract as AB gate
- [ ] AB2 can execute without ambiguous layout decisions

---

# AC-010 â€” WinUI Global NavigationView Convergence (AC1)

**Related FRs:** FR-075, FR-076

## Scenarios

### 1) Global NavigationView model
**Given**
- WinUI shell is running

**When**
- User views shell navigation

**Then**
- Shell uses a single global `NavigationView` in `LeftCompact` mode
- Compact state is icon-first
- Expanded state shows labels and hierarchical entity/action entries

### 2) Entity and child-action routing
**Given**
- Top-level entities have child actions/subviews

**When**
- User selects navigation items

**Then**
- Selecting a parent entity routes to that entity's default child route
- Selecting a child routes directly to that child
- Canonical route keys use `capability.subview` format

### 3) Compact-mode child access (non-hover)
**Given**
- Navigation is in compact icon-only mode

**When**
- User selects an entity icon

**Then**
- Child actions become accessible via flyout/compact affordance
- Child access does not depend on hover-only behavior

### 4) Startup and context defaults
**Given**
- App starts from a fresh launch

**When**
- Startup navigation resolves

**Then**
- Default startup route is `machines.overview`
- Last selected capability/route is not restored in this slice
- Active navigation highlight provides context (breadcrumbs not required in this slice)

### 5) Settings placement
**Given**
- User inspects global navigation

**When**
- Navigation is rendered

**Then**
- `Settings` appears as footer navigation (cog entry), separate from main entity list

## Expected UI
- One global shell `NavigationView` (LeftCompact)
- Main entity list with hierarchical child entries
- Compact-mode child access affordance
- Footer `Settings` entry

## Definition of Done
- [ ] Navigation model matches `docs/02-ux/winui-global-navigationview-contract-ac.md`
- [ ] Route keys use canonical `capability.subview`
- [ ] Parent-select-to-default-child behavior is implemented
- [ ] Compact mode exposes child actions without hover dependency
- [ ] Startup route is `machines.overview`
- [ ] Settings is rendered as footer entry

---

# AC-011 — WinUI Templates Capability Convergence (AD1)

**Related FRs:** FR-077, FR-078, FR-079, FR-075, FR-076

## Scenarios

### 1) Parent Templates routes to default child
**Given**
- WinUI global navigation is active

**When**
- User selects parent `Templates` capability

**Then**
- Navigation resolves to `templates.library`
- Routing remains in canonical `capability.subview` format
- Behavior remains consistent with global NavigationView contract (AC-010)

### 2) Library to Editor transition continuity
**Given**
- User is in `templates.library`
- A template is selected from the library list

**When**
- User chooses to open or edit the selected template

**Then**
- Navigation transitions to `templates.editor`
- Selected template context is preserved through the transition
- User remains in `Templates` capability context (no cross-capability jump)

### 3) Unified Templates action discoverability
**Given**
- User is operating within `Templates` capability

**When**
- User looks for core template operations

**Then**
- Library/search/select actions are discoverable in `Templates`
- Create/edit/save actions are discoverable in `Templates`
- Import/export entry points are discoverable in `Templates`
- Primary edit workflow does not require filesystem-first file hunting

### 4) No regression against global navigation behavior
**Given**
- Templates routes are enabled under global NavigationView

**When**
- User switches among capabilities and Templates subviews

**Then**
- Global nav contract remains intact (entity hierarchy, compact/expanded behavior, settings footer)
- Startup route remains `machines.overview`
- No implicit breadcrumb dependency is introduced by Templates convergence contract

## Expected UI
- Parent capability: `Templates`
- Canonical child routes:
  - `templates.library` (default)
  - `templates.editor`
- `templates.details` deferred unless approved by future milestone contract
- Unified Templates capability context for library + editor + import/export entry points

## Definition of Done
- [ ] Parent `Templates` routes deterministically to `templates.library`
- [ ] `templates.library` and `templates.editor` routes are explicit in contract/docs
- [ ] Unified library-to-editor workflow continuity is documented and testable
- [ ] Import/export discoverability is defined within Templates capability context
- [ ] No AC-010 global navigation behavior is contradicted

---

# AC-012 — WinUI Templates VM Entry Editing Parity (AD5)

**Related FRs:** FR-080, FR-081, FR-082, FR-083, FR-012, FR-014, FR-015

## Scenarios

### 1) VM list visible in editor context
**Given**
- User is in `templates.editor`
- A template document is loaded (existing or draft)

**When**
- User views editor content

**Then**
- Template VM entries (`vmTemplates`) are visible as an editor-local list
- User can select a VM entry to edit its fields within Templates editor context

### 2) Add VM entry
**Given**
- User is editing a template in `templates.editor`

**When**
- User chooses to add a VM entry

**Then**
- A new VM entry is added to editor draft state
- Required fields are surfaced for completion/validation
- Save remains blocked until required validation conditions are met

### 3) Remove VM entry with confirmation
**Given**
- User has selected an existing VM entry in editor

**When**
- User chooses remove and confirms

**Then**
- Selected VM entry is removed from editor draft state
- Removal is not committed until template save
- User receives clear confirmation/result feedback

### 4) Edit VM-entry fields using existing schema
**Given**
- User has selected a VM entry in editor

**When**
- User edits supported VM-entry fields already defined in template schema/contracts

**Then**
- Editor updates draft state without introducing new schema fields
- Validation behavior remains consistent with existing template rules
- Invalid state is surfaced with actionable guidance

### 5) Save/reload round-trip for VM-entry edits
**Given**
- User has changed VM-entry data in editor

**When**
- User saves and later reloads/reopens the template

**Then**
- Saved VM-entry changes persist and reload accurately
- Validation failures prevent invalid persistence
- No silent data loss occurs for valid saved VM-entry changes

### 6) Library <-> Editor continuity after VM edits
**Given**
- User performs add/remove/edit operations and saves (or receives validation failure)

**When**
- User returns to `templates.library` and/or reopens the same template in editor

**Then**
- Template context remains coherent across library/editor transitions
- Library reflects saved VM-entry state (for example item metadata/count consistency where shown)
- Navigation remains inside `Templates` capability context

## Scope boundary for AD6 implementation
- In scope:
  - VM entry list rendering in editor
  - add/remove/edit behaviors for existing schema fields
  - save/reload continuity and validation feedback
- Out of scope:
  - new template schema fields
  - new template domain semantics unrelated to existing schema
  - Deploy/Assets/global-nav redesign work

## Definition of Done
- [ ] VM entry list is available in `templates.editor`
- [ ] Add/remove/edit VM-entry flows are implemented against existing schema fields
- [ ] Remove requires explicit confirmation
- [ ] Save/reload round-trip preserves valid VM-entry edits
- [ ] Validation/error behavior is explicit and actionable
- [ ] Library/editor continuity remains stable after VM-entry edits
- [ ] No schema/domain scope expansion is introduced

---

# AC-013 - WinUI Templates Selector and Normalization Hardening (AE1)

**Related FRs:** FR-084, FR-085, FR-086, FR-014, FR-015

## Scenarios

### 1) Switch selector population and empty state
**Given**
- User opens VM settings in `templates.editor`
- Host switch discovery returns available switches or none

**When**
- User interacts with switch assignment controls

**Then**
- Switch controls render as selector rows populated from host switch discovery
- If no host switches are available, UI shows explicit non-silent guidance
- VM-level switch assignment remains optional unless rows are present

### 2) Multi-switch row rules and persistence
**Given**
- User adds one or more switch rows for a VM entry

**When**
- User selects switch values and saves template

**Then**
- Each present row must have a selected switch value
- Duplicate switch values are rejected with actionable validation feedback
- Persisted output uses `switchNames` as canonical list
- Legacy `switchName` is dual-written from first `switchNames` entry for compatibility
- Reload preserves selected switch rows/values

### 3) Catalog-first VHDX selection
**Given**
- User edits VM VHDX references in `templates.editor`

**When**
- User selects a base disk

**Then**
- Catalog-backed selection is the primary interaction path
- Manual path-first editing is not the primary interaction path

### 4) Backward-compatible path-only templates
**Given**
- Template VM entry has only legacy path-based VHDX reference

**When**
- User opens and edits the template

**Then**
- Template remains loadable/editable without schema migration failure
- UI displays fallback status when no matching catalog entry is currently available
- Save behavior remains explicit and non-silent

### 5) Deterministic VHDX normalization precedence
**Given**
- VM entry contains any combination of `vhdxId`, `vhdxSignature`, and `vhdPath`

**When**
- Editor resolves effective disk identity for display/validation

**Then**
- Precedence is deterministic: `vhdxId` -> `vhdxSignature` -> `vhdPath`
- Effective source-of-truth is visible to user in editor context

### 6) Conflict and ambiguity messaging
**Given**
- VHDX references are mixed/conflicting/ambiguous

**When**
- User attempts save

**Then**
- User receives actionable warning message describing ambiguity
- Save is blocked until user resolves ambiguity by selecting a catalog entry
- No silent conflict resolution occurs

### 7) Scope boundary guard
**Given**
- AE selector/normalization hardening work

**When**
- Team validates contract compliance

**Then**
- No template-domain expansion beyond selector UX/data-binding hardening is introduced
- No unrelated Deploy/Assets/global-nav behavior is changed
- Schema change scope is limited to `switchNames` plus compatibility fallback rules

## Definition of Done
- [ ] Switch selector behavior is documented with host-backed data source and empty-state guidance
- [ ] Multi-switch add/remove and duplicate validation rules are documented
- [ ] `switchNames` canonical + `switchName` fallback dual-write rule is documented
- [ ] Catalog-first VHDX selector behavior is documented with backward compatibility handling
- [ ] Deterministic normalization precedence is documented
- [ ] Conflict/ambiguity save blocking rule is documented
- [ ] Scope boundary for AE2/AE3/AE4 is explicit and testable

---

# AC-014 - WinUI Deploy From-Template Convergence (AF1)

**Related FRs:** FR-087, FR-088, FR-089, FR-090, FR-014, FR-015

## Scenarios

### 1) Deploy routing and template-first entry
**Given**
- User is in WinUI global navigation

**When**
- User selects Deploy capability

**Then**
- AF route resolves to `deploy.from_template`
- From-template workflow is the active Deploy slice for AF
- On-the-fly migration remains explicitly deferred in AF scope

### 2) Required disk identity unresolved/ambiguous blocks deploy
**Given**
- A selected template contains VM disk references that cannot be resolved deterministically using AE rules

**When**
- User runs readiness or attempts deploy

**Then**
- Readiness returns blocking state for required unresolved/ambiguous disk identity
- Deploy start is blocked until user resolves required disk identity
- Blocking status is explicit and actionable

### 3) Switch mapping compatibility with partial warnings
**Given**
- Template VM switch data contains canonical `switchNames`, legacy `switchName`, or mixed compatibility state

**When**
- Deploy readiness evaluates switch mappings

**Then**
- Mapping prefers `switchNames` and falls back to `switchName` when needed
- Missing/partial switch mapping surfaces warning-level guidance when deploy can continue
- Warning text identifies affected VM rows and correction path

### 4) Correction affordances for readiness failures
**Given**
- Deploy readiness reports blocking compatibility issues

**When**
- User inspects readiness output

**Then**
- UI shows auto-resolve suggestions where deterministic repair is possible
- UI provides explicit `Open in Templates Editor` correction action
- Correction flow is non-silent and does not require guesswork

### 5) Compact-first deploy result visibility
**Given**
- User starts a from-template deployment

**When**
- Progress and outcomes are rendered

**Then**
- Sticky status/progress remains visible
- Per-VM result rows are concise by default
- Per-VM details are expandable on demand
- Global warnings/errors are collapsed by default but remain discoverable

## Scope boundary for AF implementation
- In scope:
  - Deploy `from-template` route and workflow in WinUI
  - AE compatibility checks and readiness correction affordances
  - compact-first results presentation model
- Out of scope:
  - Deploy `on-the-fly` migration
  - WPF Deploy changes
  - new deployment semantics
  - schema/model changes unrelated to approved compatibility behavior

## Definition of Done
- [ ] `deploy.from_template` route behavior is explicit and testable for AF
- [ ] Required unresolved/ambiguous disk identity is blocking and actionable
- [ ] Switch mapping compatibility behavior (`switchNames` preferred, `switchName` fallback) is explicit
- [ ] Auto-resolve suggestions and `Open in Templates Editor` correction action are defined
- [ ] Compact-first results visibility with expandable details is defined
- [ ] AF scope boundaries are explicit and enforceable

---

# AC-015 - WinUI Deploy On-the-Fly Convergence (AG1)

**Related FRs:** FR-091, FR-092, FR-093, FR-094, FR-014, FR-015, FR-020, FR-021, FR-024, FR-025, FR-043, FR-045

## Scenarios

### 1) Deploy routing and on-the-fly entry
**Given**
- User is in WinUI global navigation

**When**
- User selects Deploy on-the-fly path (user-facing label may be `Quick Deploy`)

**Then**
- Route resolves to `deploy.on_the_fly`
- On-the-fly workflow is active for AG scope
- AF from-template behavior remains available and unchanged

### 2) On-the-fly input model and required readiness checks
**Given**
- User configures one or more VM entries on-the-fly

**When**
- Readiness evaluation runs

**Then**
- Required inputs are validated before start (VM identity/config completeness, required disk identity, required switch selections)
- Blocking vs warning outcomes are explicit
- Blocking outcomes prevent deploy start

### 3) Correction affordances for blocking readiness issues
**Given**
- Readiness returns blocking results for one or more VM entries

**When**
- User inspects readiness output

**Then**
- UI provides actionable correction affordances for blocking issues
- Correction flow is explicit and non-silent
- Deploy start remains gated until blocking issues are resolved

### 4) Execution boundary and orchestration semantics
**Given**
- Readiness is unblocked

**When**
- User starts on-the-fly deploy

**Then**
- Execution uses existing deployment orchestration semantics
- No AG-only behavior invents new deploy runtime semantics
- Existing structured operation logging contract remains preserved

### 5) Compact-first results visibility parity
**Given**
- On-the-fly deploy is running or completed

**When**
- Results are shown

**Then**
- Sticky summary/progress remains visible
- Per-VM rows are concise by default with expandable details
- Global warnings/errors are collapsed by default and discoverable

## Scope boundary for AG implementation
- In scope:
  - WinUI `deploy.on_the_fly` route and workspace convergence
  - On-the-fly readiness taxonomy and correction affordances
  - Compact-first results UX parity with AF pattern
- Out of scope:
  - WPF Deploy changes
  - From-template contract semantics changes
  - schema/model changes not required by approved requirements
  - deployment domain behavior redesign

## Definition of Done
- [ ] `deploy.on_the_fly` route behavior is explicit and testable for AG
- [ ] Blocking vs warning readiness taxonomy is explicit for on-the-fly inputs
- [ ] Correction affordances for blocking readiness issues are defined
- [ ] Execution boundary preserves existing deployment orchestration semantics
- [ ] Compact-first results parity contract is explicit and testable
- [ ] AG scope boundaries are explicit and enforceable

---

# AC-016 - WinUI Deploy Timeline Canonical Step-State Contract (AH1)

**Related FRs:** FR-095, FR-096, FR-090, FR-094

## Scenarios

### 1) Canonical step-state model is explicit
**Given**
- Deploy timeline rendering for from-template or quick deploy

**When**
- Timeline state is projected for UI rows

**Then**
- Canonical states are available: `Pending`, `Running`, `Succeeded`, `Failed`, `Skipped`
- UI row rendering reads explicit state values (not text-only inference as the primary source)

### 2) Single-label step rendering and icon-state progression
**Given**
- A VM deploy run with multiple steps

**When**
- Step state transitions from pending to running to terminal state

**Then**
- Each step label appears only once in the timeline
- Running state uses spinner indicator
- Terminal states use deterministic icon-state output (`Succeeded`, `Failed`, or `Skipped`)
- Duplicate label patterns such as separate “start row” and “completed row” are not used

### 3) Optional/skipped step visibility rules
**Given**
- Optional or nested step groups with non-applicable steps

**When**
- Timeline rows are produced

**Then**
- Skipped/non-applicable rows are hidden
- Parent row is hidden when no child step executes
- Timeline includes only steps that will run, are running, or have completed

### 4) AF/AG parity and non-regression
**Given**
- Timeline rendering exists in both deploy flows

**When**
- User runs from-template and quick deploy flows

**Then**
- Both flows follow the same canonical icon-state timeline rules
- Existing readiness gating and deployment execution semantics remain unchanged
- Route contracts remain intact (`deploy.from_template`, `deploy.on_the_fly`)

## Definition of Done
- [ ] Canonical timeline states are explicit and testable
- [ ] Single-label step rendering with state-driven icons is explicit and testable
- [ ] Skipped/non-applicable visibility rules are explicit and testable
- [ ] AF and quick deploy timeline parity is explicit without semantics drift

---

# AC-017 - WinUI Shell Right-Panel Ownership Contract (AH2)

**Related FRs:** FR-097, FR-090, FR-094, FR-074

## Scenarios

### 1) Ownership precedence follows active capability
**Given**
- Shell right panel region is available

**When**
- User navigates between capabilities

**Then**
- Active capability determines right-panel owner
- Deploy capability owns right-panel timeline/results/issue content in AH2 initial slice
- Non-owning capabilities do not retain stale Deploy panel content

### 2) Lifecycle reset on capability switch
**Given**
- Right panel has expanded state/content while in Deploy

**When**
- User switches to another capability and later returns

**Then**
- Panel content state resets according to owner contract
- Default entry state is collapsed unless owner marks active-run context

### 3) Compact fallback behavior
**Given**
- Window width crosses compact threshold

**When**
- Right panel is open

**Then**
- Right panel collapses in compact fallback mode for AH2
- Main capability workspace remains usable without horizontal clipping

### 4) Scroll ownership in right panel
**Given**
- Right panel contains long timeline/results content

**When**
- Content exceeds available panel height

**Then**
- Right panel owns vertical scrolling
- Parent shell regions remain bounded and avoid unbounded growth

### 5) Deploy non-regression guard
**Given**
- Deploy from-template and quick deploy flows are available

**When**
- User runs readiness and deployment lifecycle actions

**Then**
- Existing readiness/gating/execution behavior remains unchanged
- Deploy timeline/results/issue context is visible through shell-owned right panel

## Definition of Done
- [ ] Right-panel ownership precedence and lifecycle reset rules are explicit and testable
- [ ] Deploy right-panel ownership initial slice is explicit and testable
- [ ] Compact fallback behavior is explicit and testable
- [ ] Right-panel scroll ownership rule is explicit and testable
- [ ] Deploy behavior non-regression is explicit and testable

---

# AC-018 - Deploy Backend-to-UI Step-State Event Contract (AH3)

**Related FRs:** FR-098, FR-099, FR-095, FR-096

## Scenarios

### 1) Explicit per-step event payload contract
**Given**
- Deploy orchestration executes per-VM steps in from-template or quick deploy

**When**
- A step changes state

**Then**
- Backend emits explicit step-state update payload with required fields:
  - `operationId`
  - `vmId`
  - `vmName`
  - `stepKey`
  - `stepLabel`
  - `state` (`Pending`, `Running`, `Succeeded`, `Failed`, `Skipped`)
  - `timestampUtc`
  - deterministic per-VM `sequence`
- Optional concise `message` may be included

### 2) Deterministic ordering and terminal semantics
**Given**
- Step-state updates are emitted for a VM

**When**
- Timeline projection consumes updates

**Then**
- Step ordering is deterministic by per-VM sequence
- One step label transitions through states over time without duplicate start/complete rows
- Failed step transitions are terminal for that step
- Skipped optional steps are represented consistently in event stream

### 3) WinUI source-of-truth integration
**Given**
- Deploy right-panel timeline is visible

**When**
- Deployment progresses

**Then**
- Timeline state updates are driven by backend step-state stream
- Text/status messages may supplement summaries but do not drive state inference
- AF/AG route behavior and right-panel ownership behavior remain unchanged

## Definition of Done
- [ ] Backend step-state payload contract is explicit and testable
- [ ] Deterministic per-VM sequence and terminal semantics are explicit and testable
- [ ] WinUI timeline uses explicit step-state updates as source-of-truth
- [ ] AF/AG route + AH2 panel ownership non-regression is explicit and testable

---

# AC-019 - WinUI Assets Base Disks Capability Convergence (AJ1)

**Related FRs:** FR-100, FR-101, FR-102, FR-103, FR-050, FR-051, FR-052, FR-053, FR-054

## Scenarios

### 1) Route and local navigation contract
**Given**
- User enters `Assets` in WinUI

**When**
- Base Disks subview is selected or the Assets capability resolves to its Base Disks child view

**Then**
- Route resolves to `assets.base_disks`
- Base Disks is the canonical AJ subview
- Local Assets navigation may use tabs/segments bound to canonical child routes
- AJ1 does not redefine shell-wide top-level `Assets` click behavior beyond the Base Disks child-route contract

### 2) Base disk library surface and state behavior
**Given**
- User is in `assets.base_disks`

**When**
- The view loads, refreshes, or returns no items

**Then**
- A base disk list is shown when items exist
- Empty state is explicit and includes import/register guidance
- Loading state is explicit
- Load/refresh failure state is explicit and actionable
- Selected-disk details context is available for metadata visibility/editing without a separate edit route

### 3) Import/register and metadata edit contract
**Given**
- User selects a VHD/VHDX path to register or edits metadata on an existing entry

**When**
- The action is submitted

**Then**
- Blocking validation prevents invalid or inaccessible disks from being registered
- Successful registration adds the item to the list and details context
- Metadata edit remains in-context on the selected-disk surface
- Save/update feedback is actionable and non-silent
- Existing base disk semantics from AC-004 remain preserved

### 4) Validation and readiness taxonomy
**Given**
- A base disk entry is loaded or being changed

**When**
- Validation/readiness is evaluated

**Then**
- Blocking conditions are presented as blocking and prevent unsafe completion of the relevant action
- Warning conditions remain visible without being misrepresented as blocking
- Guidance explains what the user must fix vs what the user may review later
- Validation does not silently hide empty/error/loading states

### 5) Remove with safety guardrails
**Given**
- User attempts to remove a registered base disk

**When**
- The remove action is invoked

**Then**
- Confirmation is required
- AJ1 removal scope is registry removal only; underlying file deletion is not part of this contract
- The UI surfaces whether the disk appears referenced/in-use and classifies the condition as block or warning per contract
- Removal failure produces actionable feedback and does not leave partial registry state
- Successful removal removes the entry from selection surfaces

### 6) Logging and diagnostics contract
**Given**
- User performs list/refresh/import/edit/validate/remove actions in `assets.base_disks`

**When**
- The action starts, completes, or fails

**Then**
- Structured logs are emitted with `operationId`
- Logged context includes action, `baseDiskId` when available, file path when relevant, result, and error details
- Diagnostics semantics remain consistent with existing base-disk domain behavior

## Expected UI
- `assets.base_disks` route-backed Base Disks surface inside `Assets`
- Local Assets subview navigation pattern suitable for future `Base Disks` / `Virtual Switches` / `ISOs` growth
- Base disk list
- Refresh/import/register actions
- Selected base disk details with in-context metadata edit
- Validation/readiness status visibility
- Remove action with confirmation and safety messaging
- Explicit empty/loading/error states

## Expected Logs
- `BaseDiskListRequested` / `BaseDiskListLoaded` / `BaseDiskListFailed`
- `BaseDiskRefreshRequested` / `BaseDiskRefreshCompleted` / `BaseDiskRefreshFailed`
- `BaseDiskRegisterStarted` / `BaseDiskRegistered` / `BaseDiskRegisterFailed`
- `BaseDiskMetadataUpdateStarted` / `BaseDiskMetadataUpdated` / `BaseDiskMetadataUpdateFailed`
- `BaseDiskValidationEvaluated`
- `BaseDiskRemoveStarted` / `BaseDiskRemoved` / `BaseDiskRemoveBlocked` / `BaseDiskRemoveFailed`
- Fields: `operationId`, action, `baseDiskId` (when available), filePath (when relevant), readiness/result classification, result, error details

## Expected Artifacts / Side Effects
- Base disk registry list loads through existing catalog/store behavior
- Registration and metadata edit preserve current persistence semantics
- Remove updates registry state only in AJ scope
- Existing missing-disk mapping behavior for Deploy/Templates remains unchanged

## Definition of Done
- [ ] `assets.base_disks` route and local Assets navigation contract are explicit and testable
- [ ] Base disk list/refresh/import/edit/validate/remove behavior is explicit for AJ2/AJ3
- [ ] Blocking vs warning semantics are explicit and actionable
- [ ] Remove safety guardrails are explicit, including registry-only scope and in-use/reference messaging
- [ ] Logging expectations with `operationId` are explicit and traceable
- [ ] No shell-wide `Assets` click behavior changes are introduced by AJ1

---

# AC-020 - WinUI Assets Switches Capability Convergence (AK1)

**Related FRs:** FR-104, FR-105, FR-106, FR-107, FR-030, FR-031, FR-033, FR-034, FR-035

## Scenarios

### 1) Route and local navigation contract
**Given**
- User enters `Assets` in WinUI

**When**
- Switches subview is selected or the Assets capability resolves to its Switches child view

**Then**
- Route resolves to `assets.switches`
- Switches is the canonical AK child route under `Assets`
- Local Assets navigation may use tabs/segments bound to canonical child routes
- AK1 does not redefine shell-wide top-level `Assets` click behavior beyond the Switches child-route contract

### 2) Switches surface and state behavior
**Given**
- User is in `assets.switches`

**When**
- The view loads, refreshes, or returns no items

**Then**
- A switch list is shown when items exist
- Selected-switch details context is available for inspection and in-context editing
- Loading, empty, and error states are explicit and actionable
- Actions and status/feedback regions remain visible without requiring a separate route

### 3) Create and edit workflow contract
**Given**
- User creates a new switch or edits an existing switch in the selected-switch details context

**When**
- The action is submitted

**Then**
- Existing switch CRUD semantics from FR-033 and FR-034 are preserved
- Create/edit occurs in-context on the Switches surface rather than a separate editor route
- Blocking validation prevents invalid or unsupported switch configuration from being applied
- Successful create/update reconciles list selection and details state without silent failure
- Feedback is actionable and non-silent

### 4) Validation and readiness taxonomy
**Given**
- A switch create/edit/delete action is evaluated

**When**
- Validation/readiness is performed

**Then**
- Blocking conditions are presented explicitly as blocking
- Warning conditions remain visible without being misrepresented as blocking
- Duplicate-name conflicts, invalid configuration, unavailable host state, and unsupported updates are surfaced with concrete guidance
- Validation does not silently hide load/empty/error states

### 5) Delete with safety guardrails
**Given**
- User attempts to delete an existing virtual switch

**When**
- The delete action is invoked

**Then**
- Confirmation is required
- Deletion is blocked if any Hyper-V VM is attached to the switch, regardless of VM power state
- Blocking feedback is concrete and actionable
- Successful delete removes the switch from the list and details context
- AK1 does not invent broader topology-management semantics beyond current switch CRUD scope

### 6) Logging and diagnostics contract
**Given**
- User performs load/refresh/create/edit/delete actions in `assets.switches`

**When**
- The action starts, completes, or fails

**Then**
- Structured logs are emitted with `operationId`
- Logged context includes action, `switchName`, `switchType`, result, and error details
- Diagnostics semantics remain consistent with existing switch CRUD behavior

## Expected UI
- `assets.switches` route-backed Switches surface inside `Assets`
- Local Assets subview navigation pattern suitable for Base Disks / Switches / future ISOs growth
- Switch list
- Selected-switch details with in-context create/edit workflow
- Load/refresh/create/update/delete actions
- Validation/readiness status visibility
- Explicit loading/empty/error states

## Expected Logs
- `SwitchListRequested` / `SwitchListLoaded` / `SwitchListFailed`
- `SwitchRefreshRequested` / `SwitchRefreshCompleted` / `SwitchRefreshFailed`
- `SwitchCreateStarted` / `SwitchCreated` / `SwitchCreateFailed`
- `SwitchUpdateStarted` / `SwitchUpdated` / `SwitchUpdateFailed`
- `SwitchValidationEvaluated`
- `SwitchDeleteStarted` / `SwitchDeleteBlocked` / `SwitchDeleted` / `SwitchDeleteFailed`
- Fields: `operationId`, action, `switchName`, `switchType`, result, error details

## Expected Artifacts / Side Effects
- Hyper-V virtual switch inventory loads through existing switch-management behavior
- Create/update/delete preserve existing switch CRUD semantics
- Delete remains blocked whenever any Hyper-V VM is attached to the switch
- Existing deploy/template switch selection semantics remain unchanged

## Definition of Done
- [ ] `assets.switches` route and local Assets navigation contract are explicit and testable
- [ ] Switch list/load/refresh/create/edit/delete behavior is explicit for AK2/AK3
- [ ] Blocking vs warning semantics are explicit and actionable
- [ ] Delete safety guardrails are explicit, including “block if any VM is attached”
- [ ] Logging expectations with `operationId` are explicit and traceable
- [ ] No shell-wide `Assets` click behavior changes are introduced by AK1

---

# AC-021 - WinUI Shell and View Consistency Contract (AL1)

**Related FRs:** FR-108, FR-109, FR-110, FR-111, FR-112, FR-074, FR-075, FR-097

## Scenarios

### 1) Parent capability navigation remains deterministic in expanded, collapsed, and compact modes
**Given**
- User navigates with the WinUI shell navigation

**When**
- User selects a parent capability in expanded, collapsed, or compact drawer mode

**Then**
- Parent-click routes deterministically to the approved capability default
- Capabilities with approved `Overview` surfaces route parent-click to `Overview`
- Capabilities without approved `Overview` surfaces route parent-click to their default operational child or workspace
- Collapsed/compact behavior does not depend on hover/pop-up child-route choosers

### 2) Capability-local overview policy is explicit and capability-specific
**Given**
- User enters a capability that contains multiple child surfaces

**When**
- The capability resolves its local navigation model

**Then**
- `Assets`, `Deploy`, and `Diagnostics` expose approved `Overview`-first local navigation
- `Machines` remains single-surface for current scope
- `Templates` keeps `Library` as the primary capability surface and does not expose `Editor` as a misleading always-peer tab
- Child-route ordering and labeling remain explicit and testable

### 3) Shell header owns capability context by default
**Given**
- A migrated capability surface is rendered inside the shell content host

**When**
- The user enters the capability or switches local child views

**Then**
- Shell header presents capability-level title and optional short capability-level description
- Child views do not repeat page-level title/description bands by default
- Child views may use local section labels, local tab labels, or workflow-state labels without duplicating shell context

### 4) Right panel stays secondary and capability-scoped
**Given**
- A capability uses the shell right panel

**When**
- The capability renders right-panel content or triggers the panel

**Then**
- Shell owns the panel container and lifecycle, but active capability owns panel meaning and trigger placement
- Right panel remains secondary context, not the primary editor surface
- `Deploy` uses right panel for progress/results-first behavior
- Pre-run issue counts and validation ownership may live in the child workflow rather than shell-global chrome

### 5) Actions and iconography follow shared placement rules
**Given**
- A migrated capability surface exposes inventory, object, or workflow actions

**When**
- Actions are placed in the UI

**Then**
- Actions live nearest to the state they affect
- Inventory-level actions stay in inventory/header context
- Current-object actions stay in details/editor context
- `New` defaults to an inventory-level action that clears the current details/editor into a draft state
- Icon-first command chrome is preferred with tooltip labels
- Delete actions may use trash-can iconography with confirmation as the safety layer

### 6) Compact and bounded layout behavior follows explicit scroll-ownership priorities
**Given**
- A migrated capability surface is used at wide, medium, or compact size

**When**
- Available width or height is reduced

**Then**
- Primary workflow region remains prioritized over secondary context
- Shell frame remains bounded and does not become an unbounded page-scroll surface
- Right panel owns its own internal scroll
- Operational master/detail or workflow surfaces keep bounded scroll owners rather than uncontrolled full-page growth
- Compact mode may shift to focus-mode or hamburger-invoked navigation to preserve workspace economy

## Expected UI
- Capability-level shell header with child-view orientation handled by local tabs, section labels, or workflow-state labels
- Approved overview/index surfaces for `Assets`, `Deploy`, and `Diagnostics`
- Deterministic parent capability navigation in expanded, collapsed, and compact shell modes
- Right-panel behavior that remains capability-scoped and secondary
- Icon-first action chrome with predictable inventory/object/workflow placement
- Compact-mode behavior that preserves primary workflow reachability

## Definition of Done
- [ ] Parent-click and compact navigation behavior are explicit and testable
- [ ] Overview-vs-non-overview capability policy is explicit and traceable
- [ ] Shell header ownership and child-header suppression rules are explicit and testable
- [ ] Right-panel capability ownership and secondary-context role are explicit and testable
- [ ] Shared action-placement and iconography rules are explicit and testable
- [ ] Compact-mode and scroll-ownership priorities are explicit and testable

---

## Open Questions / TBDs
- Cleanup strategy is defined in `docs/01-requirements/cleanup-cancellation-policy.md`.
- VM/lab naming strategy and uniqueness rules
- Whether to store deployment history records locally
- RDP readiness policy beyond v1 host-observable checks (for example guest policy/NLA/firewall introspection).
- Deferred Assets inner layout details beyond `assets.base_disks` (for example `assets.switches` / `assets.isos`)
- Include rotated structured logs in Phase 1 viewer (`structured-events.1.jsonl`, etc.) or defer.

---

# AC-022 - WinUI Shell Composition Boundary Contract (AM1)

**Related FRs:** FR-113, FR-114, FR-115, FR-108, FR-109, FR-110, FR-111, FR-112

## Scenarios

### 1) MainWindow remains shell composition root only
**Given**
- WinUI shell is hosting migrated capability surfaces

**When**
- Runtime composition responsibilities are assigned

**Then**
- `MainWindow` owns shell chrome, route resolution, shell navigation behavior, shell header state, theme shell state, and right-panel host lifecycle
- `MainWindow` does not remain the long-term owner of capability-local inventory state, selection state, edit drafts, readiness state, or capability-specific workflow orchestration
- Shell ownership boundaries remain explicit and testable

### 2) Capability-local state can move behind explicit workspace seams
**Given**
- A migrated capability must preserve current user-facing behavior while reducing shell entanglement

**When**
- Capability-local extraction work is introduced

**Then**
- The capability may introduce a capability-scoped workspace owner
- Shell-to-capability integration remains narrow
- Capability-local state no longer depends on broad direct child-control mutation from `MainWindow`
- The capability remains reachable through the approved canonical route model

### 3) Shell composition refactors preserve approved AL behavior
**Given**
- AM refactors are applied after AL shell/view consistency convergence

**When**
- Shell composition boundaries are implemented

**Then**
- Shell header ownership rules remain intact
- Approved Overview-first capabilities remain intact
- Templates remains Library-first with Editor as workflow-state entry
- Deploy right-panel ownership and workflow-local trigger behavior remain intact
- Compact navigation and bounded scroll rules remain intact unless a later contract explicitly changes them

### 4) Capability workflows are not silently redesigned during extraction
**Given**
- A capability is being extracted away from shell-owned state

**When**
- The extraction changes internal ownership boundaries

**Then**
- Existing capability semantics, route behavior, and current user-visible contract remain preserved
- Extraction does not silently redesign deploy, assets, templates, diagnostics, or machines workflows
- Any new behavior change requires a separate docs-first issue

## Expected UI / Runtime Boundary
- `MainWindow` remains responsible for shell-level composition only
- Capability-local workspace owners become the seam for state and orchestration
- Shell continues to host capability views and route between them without becoming the persistent owner of capability-local workflow state

## Definition of Done
- [ ] `MainWindow` shell-only ownership is explicit and traceable
- [ ] capability-local workspace seam expectations are explicit and traceable
- [ ] AL shell/view behavior preservation requirements are explicit and traceable
- [ ] extraction is explicitly constrained from becoming silent workflow redesign

---

# AC-023 - WinUI View Interaction Contract (AM2)

**Related FRs:** FR-116, FR-117, FR-118, FR-113, FR-114, FR-115

## Scenarios

### 1) Bindings and commands are the default interaction model
**Given**
- A migrated WinUI capability surface presents state and routine actions

**When**
- The interaction contract is implemented or refactored

**Then**
- bindings and commands are the default mechanism for presenting capability-local state and routine actions
- routine UI state updates do not depend on broad shell-level direct control mutation
- capability-local workspace owners can drive UI state without `MainWindow` acting as the normal control-updater

### 2) Limited view-local events remain allowed when truly local
**Given**
- A view contains interactions that are genuinely local to the view surface

**When**
- The interaction seam is defined

**Then**
- a small explicit code-behind event surface may remain
- those events stay narrow and view-local
- those events do not re-centralize capability workflow orchestration in `MainWindow`
- the contract remains pragmatic rather than enforcing framework purity for its own sake

### 3) Views do not remain broad typed control bags
**Given**
- A migrated view is hosted by the shell

**When**
- The view interaction boundary is evaluated

**Then**
- the view does not continue to expose dozens of raw controls for routine capability updates
- any exposed interaction seam remains narrow and intentional
- child-control exposure reduces over time as capability-local extraction proceeds

### 4) Interaction refactors preserve current capability behavior
**Given**
- Capability-local interaction ownership is being moved away from shell-level control orchestration

**When**
- bindings, commands, narrow view events, or capability-local workspace owners are introduced

**Then**
- current user-visible capability behavior remains preserved
- extraction does not silently redesign workflow semantics
- the chosen interaction pattern may be viewmodel-, controller-, presenter-, or mixed-based as long as the shell/view coupling is reduced and capability behavior remains intact

## Expected Interaction Boundary
- bindings and commands are the first-choice interaction model
- limited view-local events are allowed where they are the simpler and more defensible choice
- `MainWindow` should stop acting as a broad child-control mutation layer for routine capability updates

## Definition of Done
- [ ] bindings/commands-first rule is explicit and traceable
- [ ] limited view-local event allowance is explicit and constrained
- [ ] typed-control-bag pattern is explicitly rejected
- [ ] pragmatic, non-dogmatic extraction rule is explicit and traceable

---

# AC-024 - WinUI UI Test Convergence Contract (AM3)

**Related FRs:** FR-119, FR-120, FR-121, FR-113, FR-114, FR-115, FR-116, FR-117, FR-118

## Scenarios

### 1) Stable shell/capability contracts remain protected during extraction
**Given**
- AM extraction work changes internal ownership boundaries

**When**
- UI tests are evaluated or updated

**Then**
- tests continue to protect approved shell and capability contracts
- route continuity, shell ownership rules, and approved capability behaviors remain covered
- extraction work does not remove contract protection just because implementation structure changes

### 2) Tests reduce brittle source-shape coupling where behavior is unchanged
**Given**
- A runtime refactor changes internal structure without changing approved behavior

**When**
- directly impacted UI tests are updated

**Then**
- tests may move away from exact source-string or source-shape assertions where those no longer represent the stable contract
- tests prefer seam-, state-, route-, or behavior-oriented assertions where practical
- the suite does not freeze the codebase into one shell-centric implementation shape

### 3) Test updates happen in the same slice as runtime extraction
**Given**
- An AM extraction slice changes a capability’s ownership boundary or interaction seam

**When**
- The runtime change is implemented

**Then**
- directly impacted UI tests are updated in the same issue/PR
- test convergence is not deferred as cleanup after the refactor lands
- milestone evidence remains coherent at each step

### 4) Temporary migration-scaffold tests are distinguished from stable contract tests
**Given**
- The UI test suite contains assertions created during the migration phase

**When**
- Those tests are reviewed during AM extraction

**Then**
- stable product-contract tests remain explicit and intentional
- temporary scaffolding assertions may be reduced when they block legitimate boundary cleanup
- the suite remains a safety mechanism rather than a structural straitjacket

## Expected Test Strategy Boundary
- stable shell/capability contracts remain explicitly covered
- extraction-friendly tests move toward seam/state/route/behavior coverage where practical
- runtime extraction and test convergence happen together

## Definition of Done
- [ ] stable contract protection is explicit and traceable
- [ ] brittle source-shape coupling reduction is explicit and traceable
- [ ] same-slice test update rule is explicit and traceable
- [ ] temporary-scaffold-vs-stable-contract distinction is explicit and traceable

---

# AC-025 - WinUI Machines Workspace Extraction Seam (AM4)

**Related FRs:** FR-122, FR-123, FR-124, FR-060, FR-061, FR-062, FR-063, FR-064, FR-065, FR-066, FR-071, FR-113, FR-114, FR-116, FR-117

## Scenarios

### 1) Machines state ownership can move out of MainWindow without changing route or shell framing
**Given**
- `Machines` is the first capability selected for AM extraction

**When**
- The Machines workspace seam is defined

**Then**
- Machines inventory state, selected-VM state, edit-draft state, readiness state, and action enablement/orchestration are explicitly identified as capability-local ownership
- `machines.overview` route continuity remains preserved
- shell continues to own capability framing while Machines-local ownership moves behind the seam

### 2) Machines extraction preserves current master/detail and action behavior
**Given**
- Machines is currently a single-surface master/detail capability

**When**
- The extraction seam is defined

**Then**
- the single-surface master/detail behavior remains preserved
- draft-based edit/apply behavior remains preserved
- Console and RDP remain separate actions
- delete behavior and delete policy integration remain preserved
- no silent layout or workflow redesign is introduced by the seam definition

### 3) Machines readiness and refresh behavior remains asynchronous and non-blocking
**Given**
- Machines inventory and RDP readiness currently update asynchronously

**When**
- The capability-local seam is introduced

**Then**
- inventory refresh remains non-blocking
- RDP readiness evaluation remains non-blocking
- shell does not remain the persistent owner of Machines-specific readiness flags or collections just to preserve refresh behavior

### 4) Machines seam is narrow enough for the next extraction slices
**Given**
- AM5 through AM8 will extract Machines state, interactions, view exposure, and tests

**When**
- The seam contract is reviewed

**Then**
- ownership boundaries are explicit enough that later slices do not need to guess where Machines state ends and shell ownership begins
- the seam supports pragmatic bindings/commands-first interaction rules from AM2
- the seam supports same-slice UI test convergence from AM3

## Expected Boundary
- shell continues to host the Machines surface
- Machines-specific state and orchestration move behind a capability-local workspace seam
- current Machines user-visible behavior remains preserved while shell-owned direct control mutation is reduced over later AM slices

## Definition of Done
- [ ] Machines-local state/orchestration ownership is explicit and traceable
- [ ] preserved Machines behavior constraints are explicit and traceable
- [ ] async/non-blocking refresh/readiness preservation is explicit and traceable
- [ ] seam guidance is narrow enough for AM5 through AM8
