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

## Open Questions / TBDs
- Cleanup strategy is defined in `docs/01-requirements/cleanup-cancellation-policy.md`.
- VM/lab naming strategy and uniqueness rules
- Whether to store deployment history records locally
- RDP readiness policy beyond v1 host-observable checks (for example guest policy/NLA/firewall introspection).
- Templates capability inner layout contract in WinUI (`TBD`)
- Assets capability inner layout contract in WinUI (`TBD`)
- Include rotated structured logs in Phase 1 viewer (`structured-events.1.jsonl`, etc.) or defer.
