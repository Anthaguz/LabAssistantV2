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
- Console and RDP actions remain grouped together as the dedicated remote-access action cluster
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
- WPF remains launchable as the legacy baseline/maintenance surface
- WinUI shell startup is functional without requiring feature-parity migration

### 2) Shell Navigation â€” Icon Rail + Hamburger Drawer
**Given**
- WinUI shell is running

**When**
- User interacts with left navigation

**Then**
- Normal desktop widths may show top-level capability icons in a persistent icon rail
- Hamburger opens a slide-out capability drawer from left
- Drawer shows scrim over remaining app content
- Drawer closes on outside click and on `Esc`
- Capability navigation does not rely on hover-only full-menu behavior
- Compact widths may replace the persistent rail with a hamburger-invoked drawer model

### 3) Startup Route and Persistence Policy
**Given**
- WinUI app starts from a fresh launch

**When**
- App initialization completes

**Then**
- Default landing capability is `Machines`
- Last selected capability is not restored from previous session

### 4) Shell Right Panel Infrastructure
**Given**
- WinUI shell is running

**When**
- User views shell chrome

**Then**
- Right panel infrastructure is collapsed by default
- Shell owns panel container/layout and generic visibility mechanics
- Approved capability or lane workflows may expose their own local triggers or counts when they use the panel

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
- [ ] Icon-rail + hamburger drawer behavior matches contract, including compact-width drawer fallback plus scrim and dismiss interactions
- [ ] WinUI defaults to Machines and does not persist last selected capability
- [ ] Right panel infrastructure is collapsed by default and capability-local trigger ownership remains explicit
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
- the canonical shell/layout rules are reviewed

**When**
- Contract references are reviewed

**Then**
- Diagnostics Logs and Machines are explicitly covered as first-class layout examples
- right-panel bounded-scroll behavior is explicit
- compact-width reachability rules remain explicit for dense action/filter surfaces

## Expected Artifacts
- Dedicated contract doc:
  - `docs/03-architecture/winui-shell-navigation-layout.md`
- SRS and architecture references remain aligned with the canonical shell/layout doc

## Definition of Done
- [ ] Canonical WinUI shell navigation/layout doc exists and is complete
- [ ] Contract defines testable rules for region sizing, scroll ownership, overflow, and resize behavior
- [ ] Diagnostics Logs and Machines are explicitly covered
- [ ] Right-panel bounded-scroll behavior is explicit
- [ ] Compact-width primary-action reachability rules are explicit

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
- Navigation remains keyboard- and pointer-accessible

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
- Top-level capabilities: `Machines`, `Deploy`, `Templates`, `Assets`, `Diagnostics`, and footer `Settings`
- Main entity list with hierarchical child entries
- Compact-mode child access affordance
- Footer `Settings` entry

## Definition of Done
- [ ] Global shell navigation contract is captured in `docs/03-architecture/winui-shell-navigation-layout.md`
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
- Shared dependency issues are grouped where that reduces redundant remediation work
- Safe one-to-many remapping is allowed when the issue is a shared environmental compatibility problem
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
- Readiness issues may surface at field, group, or VM-row granularity where that improves fixability
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

### 6) Quick Deploy editing and template-authoring handoff stay explicit
**Given**
- User edits one or more VM rows in the on-the-fly deploy workflow

**When**
- User updates draft values or chooses to preserve the current draft for template authoring

**Then**
- Draft editing remains live in memory rather than requiring per-VM apply actions
- Per-VM remove remains row-local rather than moving into global workflow chrome
- Any template-authoring handoff for the current draft uses explicit save-to-template wording rather than an ambiguous editor-launch label

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
- [ ] Quick Deploy editing and template-authoring handoff behavior remain explicit
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
- Selection state remains clear and stable across refresh/update actions

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
- Existing embedded asset shortcuts from Deploy/Templates remain preserved

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
- If remove/update/register flows fail after transient or in-memory state changes, visible state reconciles back to persisted truth
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
- Approved `Overview` surfaces are not decorative and provide at least two of route-entry value, useful summary, or attention/health signaling
- `Assets` local navigation model is `Overview`, `Base Disks`, `Switches`
- `Assets Overview` remains primarily a summary-and-navigation surface rather than the place that absorbs the full operational action set
- `Deploy` local navigation model is `Overview`, `Quick Deploy`, `From Template`, with `Deploy Overview` remaining the route-entry chooser/index surface, `Quick Deploy` remaining the direct configuration workflow, and `From Template` remaining a review/remediation/deploy workflow
- `Diagnostics` local navigation model is `Overview`, `Logs`, with `Overview` remaining a lightweight support dashboard
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
- Shell owns right-panel infrastructure:
  - panel container and layout host
  - generic visibility mechanics
  - compact fallback behavior
  - owner reset when active capability changes
  - internal panel scroll ownership
- Active capability or lane owns panel behavior:
  - whether the workflow uses the panel
  - what content appears there
  - what the panel means in that workflow
  - workflow-local open/close/update triggers
  - lane-specific titles, summaries, results, and contextual actions
- Right panel remains secondary context, not the primary editor surface
- `Deploy` uses right panel for progress/results-first behavior
- Current `Assets`, `Machines`, `Templates`, and `Diagnostics` scope do not require right-panel dependence by default
- Pre-run issue counts and validation ownership may live in the child workflow rather than shell-global chrome
- Mixed concerns that cross both buckets are treated as narrower shared-integration follow-ups rather than defaulting to shell ownership

### 5) Actions and iconography follow shared placement rules
**Given**
- A migrated capability surface exposes inventory, object, or workflow actions

**When**
- Actions are placed in the UI

**Then**
- Actions live nearest to the state they affect
- Inventory-level actions stay in inventory/header context
- Current-object actions stay in details/editor context
- Workflow actions may remain text-capable where clarity requires it
- Support actions stay secondary and do not compete with the primary action
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
- Overview/index surfaces may use more page-like scrolling when appropriate
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
- `MainWindow` owns shell chrome, route resolution, shell navigation behavior, shell header state, theme shell state, and right-panel host lifecycle/infrastructure
- `MainWindow` does not remain the long-term owner of capability-local inventory state, selection state, edit drafts, readiness state, or capability-specific workflow orchestration
- `MainWindow` does not centralize lane-specific right-panel meaning, content, or workflow-local update rules
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

---

# AC-026 - WinUI Capability Workspace Composition Refinement (AM33)

**Related FRs:** FR-125, FR-126, FR-127, FR-113, FR-114, FR-116, FR-117, FR-118, FR-122, FR-123, FR-124

## Scenarios

### 1) MainWindow remains shell composition root without becoming the long-term capability host hub
**Given**
- AM extraction has already moved Machines state and orchestration behind local seams

**When**
- the capability workspace composition contract is refined

**Then**
- `MainWindow` remains the shell composition root
- capability-local composition is explicitly expected to converge behind capability workspace objects
- capability-specific host interfaces implemented by `MainWindow` are treated as temporary migration bridges rather than a permanent scaling model

### 2) Views do not depend on MainWindow directly
**Given**
- capability views may need shell-owned or cross-capability help in future slices

**When**
- the refined composition contract is applied

**Then**
- views are explicitly prohibited from depending on or receiving `MainWindow` directly
- shell-owned behavior must be exposed through a narrow abstraction or service seam instead
- capability-local behavior remains local to that capability workspace unless explicitly documented otherwise

### 3) Capability workspaces remain long-lived by default
**Given**
- current WinUI capability hosts are long-lived during the app session

**When**
- workspace lifetime expectations are documented

**Then**
- capability workspaces are explicitly treated as long-lived while the app is open
- route activation refreshes or reconciles state rather than recreating the workspace by default
- any future shift toward per-navigation recreation remains a documented `TBD`, not an implicit refactor side effect

### 4) Machines becomes the proof point for refinement before broader rollout
**Given**
- Machines is the first AM capability already partially extracted

**When**
- the refined contract is reviewed

**Then**
- the docs explicitly call for re-evaluating Machines against the tighter workspace composition target
- later capability extraction slices (`Assets`, `Templates`, `Deploy`, `Diagnostics`) do not blindly repeat a shell-heavy pattern if Machines revealed a softer god-file risk

## Expected Boundary
- `MainWindow` hosts shell composition and capability workspace lifetime
- capability workspaces own capability-local composition, state, and workflow coordination
- direct view-to-`MainWindow` coupling is disallowed
- long-lived workspaces with route-activation refresh remain the current target

## Definition of Done
- [ ] capability-local workspace composition target is explicit and traceable
- [ ] temporary-host-bridge rule is explicit and traceable
- [ ] no-direct-MainWindow-injection rule is explicit and traceable
- [ ] long-lived-workspace lifetime rule is explicit and traceable
- [ ] Machines re-evaluation requirement before broader rollout is explicit and traceable

---

# AC-027 - WinUI Machines Workspace Composition Cleanup Target (AM34)

**Related FRs:** FR-128, FR-129, FR-130, FR-125, FR-126, FR-127, FR-122, FR-123, FR-124

## Scenarios

### 1) Machines gains a capability-local composition owner beyond the initial state/controller seams
**Given**
- Machines already has `MachinesWorkspaceViewModel` and `MachinesWorkspaceController`

**When**
- the post-AM33 cleanup target is defined

**Then**
- a Machines-local workspace composition owner is explicitly required as the long-term target
- that target is defined as the home for Machines-specific view/controller/state composition
- `MainWindow` is no longer treated as the intended long-term Machines composition hub

### 2) MainWindow host-bridge responsibilities are explicitly marked for reduction
**Given**
- current Machines extraction still leaves capability-local host responsibilities in `MainWindow`

**When**
- the cleanup target is reviewed

**Then**
- Machines-specific host-bridge responsibilities are explicitly identified as temporary
- follow-up issues are expected to reduce `MainWindow` responsibility for:
  - Machines-specific view refresh
  - selection synchronization
  - edit-control coordination
  - controller-host bridging

### 3) Machines cleanup preserves all approved user-visible behavior
**Given**
- Machines is already functional under AM5-AM7

**When**
- the cleanup target is defined

**Then**
- no change is introduced to:
  - `machines.overview` route continuity
  - single-surface master/detail behavior
  - list-first compact behavior
  - draft/apply workflow
  - separate Console and RDP actions
  - delete safety behavior
  - current RDP disabled-state reasoning

### 4) Long-lived workspace lifetime remains the explicit target
**Given**
- AM33 established long-lived capability workspaces by default

**When**
- Machines-specific cleanup is defined

**Then**
- the cleanup target explicitly keeps Machines long-lived within the app session
- route activation continues to refresh/reconcile state rather than recreating the Machines workspace per navigation
- any future lifetime change remains a documented `TBD`

## Expected Boundary
- shell continues to host the Machines workspace lifetime and route visibility
- a Machines-local composition owner becomes the target for Machines-specific UI composition
- shell-local Machines bridge responsibilities reduce over the next follow-up slices
- user-visible Machines behavior remains unchanged while ownership improves

## Definition of Done
- [ ] Machines-local composition-owner target is explicit and traceable
- [ ] temporary shell-bridge reduction target is explicit and traceable
- [ ] preserved user-visible Machines behavior is explicit and traceable
- [ ] long-lived Machines workspace lifetime is explicit and traceable

---

# AC-028 - WinUI Assets Workspace Extraction Seam (AM9)

**Related FRs:** FR-131, FR-132, FR-133, FR-100, FR-101, FR-102, FR-103, FR-104, FR-105, FR-106, FR-107, FR-108, FR-110, FR-125, FR-126, FR-127

## Scenarios

### 1) Assets state and orchestration can move out of MainWindow without changing Overview-first behavior
**Given**
- `Assets` is the next capability selected for AM extraction after Machines

**When**
- the Assets workspace seam is defined

**Then**
- Assets-local ownership is explicitly identified for:
  - Overview summary/navigation state
  - Base Disks workspace state and orchestration
  - Switches workspace state and orchestration
- `assets.overview`, `assets.base_disks`, and `assets.switches` route continuity remains preserved
- `Assets` remains an Overview-first capability

### 2) Assets seam uses the refined capability-workspace target from the start
**Given**
- Machines exposed a softer god-file risk before AM33/AM34 refinement

**When**
- the Assets seam is reviewed

**Then**
- the seam explicitly targets capability-local workspace composition
- `MainWindow` is not treated as the intended long-term Assets composition hub
- shell-owned host bridges are explicitly treated as temporary if needed at all

### 3) Base Disks and Switches contracts remain preserved during extraction
**Given**
- Assets currently includes approved AJ and AK contracts

**When**
- the extraction seam is defined

**Then**
- Base Disks behavior remains preserved, including:
  - current validation/readiness contract
  - remove guardrails
  - in-context operational surface
- Switches behavior remains preserved, including:
  - current CRUD and validation contract
  - delete blocked when any VM is attached
  - current in-context operational surface

### 4) Assets workspace lifetime remains long-lived with route-activation refresh
**Given**
- AM33 established long-lived capability workspaces by default

**When**
- Assets-specific extraction is defined

**Then**
- Assets remains long-lived within the app session
- route activation refreshes/reconciles state rather than recreating the Assets workspace per navigation
- any future lifetime change remains a documented `TBD`

## Expected Boundary
- shell continues to host Assets workspace lifetime and route visibility
- Assets-local workspace composition becomes the target home for Overview/Base Disks/Switches local coordination
- Base Disks and Switches semantics remain unchanged while ownership improves
- long-lived Assets workspace lifetime remains the default model

## Definition of Done
- [ ] Assets-local state/orchestration ownership is explicit and traceable
- [ ] refined capability-composition target is explicit and traceable
- [ ] Base Disks and Switches behavior-preservation constraints are explicit and traceable
- [ ] long-lived Assets workspace lifetime is explicit and traceable

---

# AC-029 - WinUI Assets Shared Composition Cleanup Target (AM10)

**Related FRs:** FR-134, FR-135, FR-136, FR-131, FR-132, FR-133, FR-125, FR-126, FR-127

## Scenarios

### 1) MainWindow remains shell-only while shared Assets composition moves behind an Assets-local owner
**Given**
- `Assets` already has an approved extraction seam from AM9

**When**
- the shared Assets cleanup target is defined

**Then**
- `MainWindow` remains responsible only for:
  - shell route switching
  - shell title/description
  - shell compact/drawer behavior
  - shell host visibility
  - right-panel infrastructure
  - app-level workspace lifetime
- shared Assets-local composition does not terminate in `MainWindow`
- an Assets-local composition owner becomes the target home for shared Assets-local composition and wiring

### 2) Shared Assets responsibilities converge behind the Assets-local composition owner
**Given**
- `Assets` includes `assets.overview`, `assets.base_disks`, and `assets.switches`

**When**
- the shared ownership boundary is reviewed

**Then**
- the Assets-local composition owner is explicitly responsible for:
  - Assets-local composition and wiring
  - shared Assets route-activation handling
  - shared workspace lifetime participation
  - shared local interaction boundaries across Overview, Base Disks, and Switches
- Base Disks-specific, Switches-specific, and Overview-specific runtime extraction details remain deferred to later narrow issues

### 3) Temporary shell bridges and direct MainWindow view coupling are explicitly rejected as the final target
**Given**
- some capability-specific host interfaces may still exist temporarily during migration

**When**
- the cleanup target is applied

**Then**
- capability-specific host interfaces implemented by `MainWindow` are explicitly treated as temporary bridges only
- views must not depend on or receive `MainWindow` directly
- narrow abstractions or Assets-local seams are the required alternative for shell-owned behavior
- temporary host-bridge patterns are distinguished from unacceptable permanent shell-centric composition

### 4) Assets remains long-lived with route-activation refresh and no silent behavior redesign
**Given**
- AM33 established long-lived capability workspaces by default

**When**
- the Assets cleanup target is defined

**Then**
- Assets remains long-lived while the app session is open
- navigation between `assets.overview`, `assets.base_disks`, and `assets.switches` activates and reconciles shared state rather than recreating the workspace every route change
- no new Base Disks, Switches, or Overview behavior is introduced by this cleanup target
- runtime implementation and performance redesign remain out of scope

## Expected Boundary
- shell continues to host Assets workspace lifetime, route visibility, and shell infrastructure
- an Assets-local composition owner becomes the target home for shared Assets-local composition across Overview, Base Disks, and Switches
- temporary shell-host bridges are allowed only as migration scaffolding and are not the long-term architecture
- Assets remains long-lived with route-activation refresh rather than per-navigation recreation

## Definition of Done
- [ ] shell-vs-Assets ownership is explicit and traceable
- [ ] shared Assets-local composition-owner target is explicit and traceable
- [ ] temporary-bridge-vs-final-target rule is explicit and traceable
- [ ] no-direct-`MainWindow`-injection rule is explicit and traceable
- [ ] long-lived Assets workspace and route-activation refresh rule is explicit and traceable

---

# AC-030 - WinUI Assets Overview Extraction Cleanup Target (AM39)

**Related FRs:** FR-137, FR-138, FR-139, FR-134, FR-135, FR-136, FR-131, FR-132, FR-133, FR-125, FR-126, FR-127

## Scenarios

### 1) Assets Overview stays under shared Assets workspace composition but gains an Overview-local seam
**Given**
- the shared Assets composition cleanup target is already defined

**When**
- the narrow `Assets Overview` cleanup target is reviewed

**Then**
- `Assets Overview` remains under shared `AssetsWorkspaceComposition` rather than becoming a shell-owned surface
- shared Assets composition remains responsible only for shared capability-level composition concerns
- an Overview-local seam becomes the target home for Overview-specific state and UI coordination

### 2) Overview-local ownership is explicit without widening Overview into a new shared god object
**Given**
- `Assets Overview` is the route-entry and summary surface for the capability

**When**
- Overview-local ownership is defined

**Then**
- Overview-local ownership explicitly includes:
  - Overview summary state
  - Overview-local navigation coordination
  - Overview-local interaction boundaries needed by the Overview surface
- Overview does not become the owner of shared Base Disks or Switches composition concerns
- Overview does not become a new shared Assets god object

### 3) MainWindow and route-activation boundaries remain explicit
**Given**
- AM33 and AM10 keep `MainWindow` limited to shell ownership and keep Assets long-lived by default

**When**
- the Overview cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Assets Overview` continues to participate in long-lived Assets workspace lifetime rather than per-navigation recreation
- route activation of `assets.overview` refreshes or reconciles Overview state within the existing Assets workspace

### 4) Overview remains behavior-preserving and does not absorb Base Disks or Switches semantics
**Given**
- `Assets` is an approved Overview-first capability with canonical child routes

**When**
- the Overview cleanup target is defined

**Then**
- `assets.overview` remains the route entry and index surface for `Assets`
- Overview remains primarily a summary and navigation surface
- no Base Disks or Switches semantic ownership is moved into Overview
- runtime implementation, overview redesign beyond ownership cleanup, and performance redesign remain out of scope

## Expected Boundary
- shared `AssetsWorkspaceComposition` remains the owner for shared capability-level composition only
- an Overview-local seam becomes the target home for Overview-specific state, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into Overview views
- `Assets Overview` remains long-lived with route-activation refresh inside the existing Assets workspace
- Base Disks and Switches keep their own future narrow extraction slices

## Definition of Done
- [ ] shared Assets vs Overview-local ownership is explicit and traceable
- [ ] Overview-local state/composition and interaction-boundary expectations are explicit and traceable
- [ ] long-lived Assets workspace participation and `assets.overview` route-activation refresh expectations are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

---

# AC-031 - WinUI Assets Base Disks Extraction Cleanup Target (AM43)

**Related FRs:** FR-140, FR-141, FR-142, FR-134, FR-135, FR-136, FR-131, FR-132, FR-133, FR-100, FR-101, FR-102, FR-103, FR-125, FR-126, FR-127

## Scenarios

### 1) Assets Base Disks stays under shared Assets workspace composition but gains a Base Disks-local seam
**Given**
- the shared Assets composition cleanup target and the Overview cleanup target are already defined

**When**
- the narrow `Assets Base Disks` cleanup target is reviewed

**Then**
- `Assets Base Disks` remains under shared `AssetsWorkspaceComposition` rather than becoming a shell-owned surface
- shared Assets composition remains responsible only for shared capability-level composition concerns
- a Base Disks-local seam becomes the target home for Base Disks-specific state, orchestration, composition, and UI coordination

### 2) Base Disks-local ownership is explicit without widening shared Assets composition into the workflow owner
**Given**
- `assets.base_disks` is the operational Base Disks management surface inside `Assets`

**When**
- Base Disks-local ownership is defined

**Then**
- Base Disks-local ownership explicitly includes:
  - Base Disks list, selection, draft, and feedback state
  - Base Disks-specific orchestration for refresh, import/register, edit/save, validate, and remove flows
  - Base Disks-local composition and interaction coordination for the `assets.base_disks` surface
  - cleanup or reduction of temporary Base Disks-specific host bridges or control exposure behind the Base Disks-local seam
- shared `AssetsWorkspaceComposition` does not become the Base Disks workflow owner
- shared Assets composition keeps only cross-surface capability concerns such as shared route activation and workspace participation

### 3) MainWindow and route-activation boundaries remain explicit for assets.base_disks
**Given**
- AM33 and AM10 keep `MainWindow` limited to shell ownership and keep Assets long-lived by default

**When**
- the Base Disks cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Assets Base Disks` continues to participate in long-lived Assets workspace lifetime rather than per-navigation recreation
- route activation of `assets.base_disks` refreshes or reconciles Base Disks state within the existing Assets workspace

### 4) Base Disks remains behavior-preserving and does not absorb Overview or Switches semantics
**Given**
- `Assets` is an approved Overview-first capability with canonical child routes

**When**
- the Base Disks cleanup target is defined

**Then**
- `assets.base_disks` remains the operational Base Disks management surface
- no Overview ownership is moved into Base Disks
- no Switches ownership is moved into Base Disks
- runtime implementation, Base Disks behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `AssetsWorkspaceComposition` remains the owner for shared capability-level composition only
- a Base Disks-local seam becomes the target home for Base Disks-specific state, orchestration, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into Base Disks views
- `Assets Base Disks` remains long-lived with route-activation refresh inside the existing Assets workspace
- Base Disks remains behavior-preserving and does not absorb shared Assets, Overview, or Switches concerns

## Definition of Done
- [ ] shared Assets vs Base Disks-local ownership is explicit and traceable
- [ ] Base Disks-local state/orchestration/composition and host-cleanup expectations are explicit and traceable
- [ ] long-lived Assets workspace participation and `assets.base_disks` route-activation refresh expectations are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

---

# AC-032 - WinUI Assets Switches Extraction Cleanup Target (AM49)

**Related FRs:** FR-143, FR-144, FR-145, FR-134, FR-135, FR-136, FR-131, FR-132, FR-133, FR-104, FR-105, FR-106, FR-107, FR-125, FR-126, FR-127

## Scenarios

### 1) Assets Switches stays under shared Assets workspace composition but gains a Switches-local seam
**Given**
- the shared Assets composition cleanup target, the Overview cleanup target, and the Base Disks cleanup target are already defined

**When**
- the narrow `Assets Switches` cleanup target is reviewed

**Then**
- `Assets Switches` remains under shared `AssetsWorkspaceComposition` rather than becoming a shell-owned surface
- shared Assets composition remains responsible only for shared capability-level composition concerns
- a Switches-local seam becomes the target home for Switches-specific state, orchestration, composition, and UI coordination

### 2) Switches-local ownership is explicit without widening shared Assets composition into the workflow owner
**Given**
- `assets.switches` is the operational Switches management surface inside `Assets`

**When**
- Switches-local ownership is defined

**Then**
- Switches-local ownership explicitly includes:
  - Switches list, selection, draft, and feedback state
  - Switches-specific orchestration for refresh, create, edit, validate, and delete flows
  - Switches-local composition and interaction coordination for the `assets.switches` surface
  - cleanup or reduction of temporary Switches-specific host bridges or control exposure behind the Switches-local seam
- shared `AssetsWorkspaceComposition` does not become the Switches workflow owner
- shared Assets composition keeps only cross-surface capability concerns such as shared route activation and workspace participation

### 3) MainWindow and route-activation boundaries remain explicit for assets.switches
**Given**
- AM33 and AM10 keep `MainWindow` limited to shell ownership and keep Assets long-lived by default

**When**
- the Switches cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Assets Switches` continues to participate in long-lived Assets workspace lifetime rather than per-navigation recreation
- route activation of `assets.switches` refreshes or reconciles Switches state within the existing Assets workspace

### 4) Switches remains behavior-preserving and does not absorb Overview or Base Disks semantics
**Given**
- `Assets` is an approved Overview-first capability with canonical child routes

**When**
- the Switches cleanup target is defined

**Then**
- `assets.switches` remains the operational Switches management surface
- no Overview ownership is moved into Switches
- no Base Disks ownership is moved into Switches
- runtime implementation, Switches behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `AssetsWorkspaceComposition` remains the owner for shared capability-level composition only
- a Switches-local seam becomes the target home for Switches-specific state, orchestration, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into Switches views
- `Assets Switches` remains long-lived with route-activation refresh inside the existing Assets workspace
- Switches remains behavior-preserving and does not absorb shared Assets, Overview, or Base Disks concerns

## Definition of Done
- [ ] shared Assets vs Switches-local ownership is explicit and traceable
- [ ] Switches-local state/orchestration/composition and host-cleanup expectations are explicit and traceable
- [ ] long-lived Assets workspace participation and `assets.switches` route-activation refresh expectations are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

---

# AC-033 - WinUI Templates Shared Composition Cleanup Target (AM56)

**Related FRs:** FR-146, FR-147, FR-148, FR-077, FR-078, FR-079, FR-125, FR-126, FR-127, FR-122, FR-123, FR-124

## Scenarios

### 1) MainWindow remains shell-only while shared Templates composition moves behind a Templates-local owner
**Given**
- `Templates` already has an approved capability routing/workflow contract from AD

**When**
- the shared Templates cleanup target is defined

**Then**
- `MainWindow` remains responsible only for:
  - shell route switching
  - shell title/description
  - shell compact/drawer behavior
  - shell host visibility
  - right-panel infrastructure
  - app-level workspace lifetime
- shared Templates-local composition does not terminate in `MainWindow`
- a Templates-local composition owner becomes the target home for shared Templates-local composition and wiring

### 2) Shared Templates responsibilities converge behind the Templates-local composition owner
**Given**
- `Templates` includes `templates.library` and `templates.editor`

**When**
- the shared ownership boundary is reviewed

**Then**
- the Templates-local composition owner is explicitly responsible for:
  - Templates-local composition and wiring
  - shared Templates route-activation handling
  - shared workspace lifetime participation
  - shared local interaction boundaries for Library and Editor
- Templates Library extraction details and Templates Editor extraction details remain deferred to later narrow issues

### 3) Templates preserves the Library-first navigation exception while rejecting shell-centric end-state patterns
**Given**
- AL defined `Templates` as a Library-first capability with Editor as workflow-state entry

**When**
- the cleanup target is applied

**Then**
- `templates.library` remains the stable/default Templates surface
- `templates.editor` remains a workflow-state destination entered from explicit actions
- shared Templates composition does not collapse back into a peer-tab or Overview-first model
- capability-specific host interfaces implemented by `MainWindow` are explicitly treated as temporary bridges only

### 4) Templates remains long-lived with route-activation refresh and no direct MainWindow view coupling
**Given**
- AM33 established long-lived capability workspaces by default

**When**
- the Templates cleanup target is defined

**Then**
- views must not depend on or receive `MainWindow` directly
- Templates remains long-lived while the app session is open
- navigation between `templates.library` and `templates.editor` activates and reconciles shared Templates state rather than recreating the workspace every route change
- runtime implementation, editor behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shell continues to host Templates workspace lifetime, route visibility, and shell infrastructure
- a Templates-local composition owner becomes the target home for shared Templates-local composition across Library and Editor
- temporary shell-host bridges are allowed only as migration scaffolding and are not the long-term architecture
- Templates remains long-lived with route-activation refresh rather than per-navigation recreation
- Templates preserves the Library-first / Editor workflow-state exception explicitly

## Definition of Done
- [ ] shell-vs-Templates ownership is explicit and traceable
- [ ] shared Templates-local composition-owner target is explicit and traceable
- [ ] Templates navigation exception preservation is explicit and traceable
- [ ] temporary-bridge-vs-final-target rule and no-direct-`MainWindow`-injection rule are explicit and traceable
- [ ] long-lived Templates workspace and route-activation refresh rule are explicit and traceable

---

# AC-034 - WinUI Templates Library Extraction Cleanup Target (AM61)

**Related FRs:** FR-149, FR-150, FR-151, FR-146, FR-147, FR-148, FR-077, FR-078, FR-079, FR-122, FR-123, FR-124, FR-125, FR-126, FR-127

## Scenarios

### 1) Templates Library stays under shared Templates workspace composition but gains a Library-local seam
**Given**
- the shared Templates composition cleanup target is already defined

**When**
- the narrow `Templates Library` cleanup target is reviewed

**Then**
- `Templates Library` remains under shared `TemplatesCapabilityRuntime` rather than becoming a shell-owned surface
- shared Templates composition remains responsible only for shared capability-level composition concerns
- a Library-local seam becomes the target home for Library-specific state, orchestration, composition, and UI coordination

### 2) Library-local ownership is explicit without widening shared Templates composition into the workflow owner
**Given**
- `templates.library` is the stable/default Templates surface inside `Templates`

**When**
- Library-local ownership is defined

**Then**
- Library-local ownership explicitly includes:
  - Library list, selection, filter, and feedback state
  - Library-specific orchestration for refresh, load/open, create-entry, import, export, delete, and library-surface selection flows
  - Library-local composition and interaction coordination for the `templates.library` surface
  - cleanup or reduction of temporary Library-specific host bridges or control exposure behind the Library-local seam
- shared `TemplatesCapabilityRuntime` does not become the Library workflow owner
- shared Templates composition keeps only cross-surface capability concerns such as shared route activation and workspace participation

### 3) MainWindow and route-activation boundaries remain explicit for templates.library
**Given**
- AM33 and AM56 keep `MainWindow` limited to shell ownership and keep Templates long-lived by default

**When**
- the Library cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Templates Library` continues to participate in long-lived Templates workspace lifetime rather than per-navigation recreation
- route activation of `templates.library` refreshes or reconciles Library state within the existing Templates workspace

### 4) Templates Library remains behavior-preserving and does not absorb Editor semantics
**Given**
- `Templates` is an approved Library-first capability with Editor as workflow-state entry

**When**
- the Library cleanup target is defined

**Then**
- `templates.library` remains the stable/default Templates surface
- no Editor-specific ownership is moved into Library
- no shared Templates ownership is moved into Library
- runtime implementation, Templates Editor extraction details, Library behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `TemplatesCapabilityRuntime` remains the owner for shared capability-level composition only
- a Library-local seam becomes the target home for Library-specific state, orchestration, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into Library views
- `Templates Library` remains long-lived with route-activation refresh inside the existing Templates workspace
- Templates Library remains behavior-preserving and does not absorb shared Templates or Editor concerns

## Definition of Done
- [ ] shared Templates vs Library-local ownership is explicit and traceable
- [ ] Library-local state/orchestration/composition and host-cleanup expectations are explicit and traceable
- [ ] long-lived Templates workspace participation and `templates.library` route-activation refresh expectations are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

---

# AC-035 - WinUI Templates Editor Extraction Cleanup Target (AM68)

**Related FRs:** FR-152, FR-153, FR-154, FR-146, FR-147, FR-148, FR-077, FR-078, FR-079, FR-080, FR-081, FR-082, FR-083, FR-122, FR-123, FR-124, FR-125, FR-126, FR-127

## Scenarios

### 1) Templates Editor stays under shared Templates workspace composition but gains an Editor-local seam
**Given**
- the shared Templates composition cleanup target and Templates Library cleanup target are already defined

**When**
- the narrow `Templates Editor` cleanup target is reviewed

**Then**
- `Templates Editor` remains under shared `TemplatesCapabilityRuntime` rather than becoming a shell-owned surface
- shared Templates composition remains responsible only for shared capability-level composition concerns
- an Editor-local seam becomes the target home for Editor-specific state, orchestration, composition, and UI coordination

### 2) Editor-local ownership is explicit without widening shared Templates composition into the workflow owner
**Given**
- `templates.editor` is the workflow-state editing surface inside `Templates`

**When**
- Editor-local ownership is defined

**Then**
- Editor-local ownership explicitly includes:
  - selected template editing context, draft, validation, and feedback state for the editor surface
  - Editor-specific orchestration for open/load, create-new, edit, save, save-as-needed, discard/reset, and editor-surface VM-entry editing flows
  - Editor-local composition and interaction coordination for the `templates.editor` surface
  - cleanup or reduction of temporary Editor-specific host bridges or control exposure behind the Editor-local seam
- shared `TemplatesCapabilityRuntime` does not become the Editor workflow owner
- shared Templates composition keeps only cross-surface capability concerns such as shared route activation, shared workspace participation, and shared coordination genuinely spanning Library and Editor

### 3) MainWindow and route-activation boundaries remain explicit for templates.editor
**Given**
- AM33 and AM56 keep `MainWindow` limited to shell ownership and keep Templates long-lived by default

**When**
- the Editor cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Templates Editor` continues to participate in long-lived Templates workspace lifetime rather than per-navigation recreation
- route activation of `templates.editor` refreshes or reconciles Editor state within the existing Templates workspace
- `templates.editor` remains a workflow-state destination entered from explicit actions rather than becoming the default Templates route

### 4) Templates Editor remains behavior-preserving and does not absorb Library semantics
**Given**
- `Templates` is an approved Library-first capability with Editor as workflow-state entry

**When**
- the Editor cleanup target is defined

**Then**
- no Library-specific ownership is moved into Editor
- no shared Templates ownership is moved into Editor
- `templates.library` remains the stable/default Templates surface
- runtime implementation, Templates Library extraction details, Editor behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `TemplatesCapabilityRuntime` remains the owner for shared capability-level composition only
- an Editor-local seam becomes the target home for Editor-specific state, orchestration, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into Editor views
- `Templates Editor` remains long-lived with route-activation refresh inside the existing Templates workspace
- Templates Editor remains behavior-preserving and does not absorb shared Templates or Library concerns

## Definition of Done
- [ ] shared Templates vs Editor-local ownership is explicit and traceable
- [ ] Editor-local state/orchestration/composition and host-cleanup expectations are explicit and traceable
- [ ] long-lived Templates workspace participation and `templates.editor` route-activation refresh expectations are explicit and traceable
- [ ] Templates Library-first / Editor workflow-state boundary is explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

---

# AC-036 - WinUI Deploy Shared Composition Cleanup Target (AM78)

**Related FRs:** FR-155, FR-156, FR-157, FR-087, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-100, FR-101, FR-102, FR-103, FR-125, FR-126, FR-127

## Scenarios

### 1) MainWindow remains shell-only while shared Deploy composition moves behind a Deploy-local owner
**Given**
- `Deploy` already has approved AF, AG, and AL routing/workflow contracts

**When**
- the shared Deploy cleanup target is defined

**Then**
- `MainWindow` remains responsible only for:
  - shell route switching
  - shell title/description
  - shell compact/drawer behavior
  - shell host visibility
  - right-panel infrastructure
  - app-level workspace lifetime
- shared Deploy-local composition does not terminate in `MainWindow`
- a Deploy-local composition owner becomes the target home for shared Deploy-local composition and wiring

### 2) Shared Deploy responsibilities converge behind the Deploy-local composition owner
**Given**
- `Deploy` includes `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template`

**When**
- the shared ownership boundary is reviewed

**Then**
- the Deploy-local composition owner is explicitly responsible for:
  - Deploy-local composition and wiring
  - shared Deploy route-activation handling
  - shared workspace lifetime participation
  - shared local interaction boundaries for:
    - Deploy Overview
    - Quick Deploy
    - From Template
- Deploy Overview extraction details, From Template extraction details, and Quick Deploy extraction details remain deferred to later narrow issues

### 3) Deploy preserves its approved route model while rejecting shell-centric end-state patterns
**Given**
- AL defined `Deploy` as an Overview-first capability with `Quick Deploy` and `From Template` child routes

**When**
- the cleanup target is applied

**Then**
- `deploy.overview` remains the route-entry and index surface for `Deploy`
- `deploy.on_the_fly` remains the deep editor-oriented `Quick Deploy` workflow
- `deploy.from_template` remains a review/remediation/deploy workflow and does not collapse into the Quick Deploy editor surface
- capability-specific host interfaces implemented by `MainWindow` are explicitly treated as temporary bridges only

### 4) Deploy remains long-lived with route-activation refresh and no direct MainWindow view coupling
**Given**
- AM33 established long-lived capability workspaces by default

**When**
- the Deploy cleanup target is defined

**Then**
- views must not depend on or receive `MainWindow` directly
- Deploy remains long-lived while the app session is open
- navigation between `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template` activates and reconciles shared Deploy state rather than recreating the workspace every route change
- runtime implementation, Deploy workflow redesign, and performance redesign remain out of scope

## Expected Boundary
- shell continues to host Deploy workspace lifetime, route visibility, and shell infrastructure
- a Deploy-local composition owner becomes the target home for shared Deploy-local composition across Overview, Quick Deploy, and From Template
- temporary shell-host bridges are allowed only as migration scaffolding and are not the long-term architecture
- Deploy remains long-lived with route-activation refresh rather than per-navigation recreation
- Deploy preserves the Overview-first route model plus the distinct Quick Deploy and From Template workflow boundaries explicitly

## Definition of Done
- [ ] shell-vs-Deploy ownership is explicit and traceable
- [ ] shared Deploy-local composition-owner target is explicit and traceable
- [ ] Deploy route-model preservation is explicit and traceable
- [ ] temporary-bridge-vs-final-target rule and no-direct-`MainWindow`-injection rule are explicit and traceable
- [ ] long-lived Deploy workspace and route-activation refresh rule are explicit and traceable

---

# AC-037 - WinUI Deploy Overview Extraction Cleanup Target (AM83)

**Related FRs:** FR-158, FR-159, FR-160, FR-155, FR-156, FR-157, FR-087, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-100, FR-101, FR-102, FR-103, FR-125, FR-126, FR-127

## Scenarios

### 1) Deploy Overview stays under shared Deploy workspace composition but gains an Overview-local seam
**Given**
- the shared Deploy composition cleanup target is already defined

**When**
- the narrow `Deploy Overview` cleanup target is reviewed

**Then**
- `Deploy Overview` remains under shared `DeployWorkspaceComposition` rather than becoming a shell-owned surface
- shared Deploy composition remains responsible only for shared capability-level composition concerns
- an Overview-local seam becomes the target home for Overview-specific state and UI coordination

### 2) Overview-local ownership is explicit without widening Overview into a new shared god object
**Given**
- `Deploy Overview` is the route-entry and summary/navigation surface for the capability

**When**
- Overview-local ownership is defined

**Then**
- Overview-local ownership explicitly includes:
  - Overview summary state
  - Overview-local navigation coordination
  - Overview-local interaction boundaries used by the Overview surface
  - Overview-specific refresh or reconcile behavior triggered by `deploy.overview` activation
- shared `DeployWorkspaceComposition` does not become the Overview workflow owner
- shared Deploy composition keeps only cross-surface capability concerns such as shared route activation and workspace participation

### 3) MainWindow and route-activation boundaries remain explicit for deploy.overview
**Given**
- AM33 and AM78 keep `MainWindow` limited to shell ownership and keep Deploy long-lived by default

**When**
- the Overview cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Deploy Overview` continues to participate in long-lived Deploy workspace lifetime rather than per-navigation recreation
- route activation of `deploy.overview` refreshes or reconciles Overview state within the existing Deploy workspace

### 4) Deploy Overview remains behavior-preserving and does not absorb Quick Deploy or From Template semantics
**Given**
- `Deploy` is an approved Overview-first capability with distinct Quick Deploy and From Template child routes

**When**
- the Overview cleanup target is defined

**Then**
- `deploy.overview` remains the route-entry and index surface for `Deploy`
- Overview remains primarily a summary and navigation surface
- no Quick Deploy ownership is moved into Overview
- no From Template ownership is moved into Overview
- runtime implementation, Quick Deploy extraction details, From Template extraction details, Deploy behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `DeployWorkspaceComposition` remains the owner for shared capability-level composition only
- an Overview-local seam becomes the target home for Overview-specific state and interaction coordination
- `MainWindow` remains shell-only and is not injected into Overview views
- `Deploy Overview` remains long-lived with route-activation refresh inside the existing Deploy workspace
- Deploy Overview remains behavior-preserving and does not absorb shared Deploy, Quick Deploy, or From Template concerns

## Definition of Done
- [ ] shared Deploy vs Overview-local ownership is explicit and traceable
- [ ] Overview-local state/interaction and host-cleanup expectations are explicit and traceable
- [ ] long-lived Deploy workspace participation and `deploy.overview` route-activation refresh expectations are explicit and traceable
- [ ] Quick Deploy / From Template non-goals are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

# AC-038 - WinUI Deploy From Template Extraction Cleanup Target (AM87)

**Related FRs:** FR-161, FR-162, FR-163, FR-155, FR-156, FR-157, FR-087, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-100, FR-101, FR-102, FR-103, FR-125, FR-126, FR-127

## Scenarios

### 1) Deploy From Template stays under shared Deploy workspace composition but gains a From Template-local seam
**Given**
- the shared Deploy composition cleanup target and Deploy Overview cleanup target are already defined

**When**
- the narrow `Deploy From Template` cleanup target is reviewed

**Then**
- `Deploy From Template` remains under shared `DeployWorkspaceComposition` rather than becoming a shell-owned surface
- shared Deploy composition remains responsible only for shared capability-level composition concerns
- a From Template-local seam becomes the target home for From Template-specific state, orchestration, composition, and UI coordination

### 2) From Template-local ownership is explicit without widening shared Deploy composition into the workflow owner
**Given**
- `deploy.from_template` is the template-driven review/remediation/deploy workflow surface for the capability

**When**
- From Template-local ownership is defined

**Then**
- From Template-local ownership explicitly includes:
  - From Template-specific state
  - From Template-specific orchestration
  - From Template-specific interaction boundaries used by the From Template surface
  - From Template-specific composition or host cleanup expectations
  - From Template-specific refresh or reconcile behavior triggered by `deploy.from_template` activation
- shared `DeployWorkspaceComposition` does not become the From Template workflow owner
- shared Deploy composition keeps only cross-surface capability concerns such as shared route activation and workspace participation

### 3) MainWindow and route-activation boundaries remain explicit for deploy.from_template
**Given**
- AM33 and AM78 keep `MainWindow` limited to shell ownership and keep Deploy long-lived by default

**When**
- the From Template cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Deploy From Template` continues to participate in long-lived Deploy workspace lifetime rather than per-navigation recreation
- route activation of `deploy.from_template` refreshes or reconciles From Template state within the existing Deploy workspace

### 4) Deploy From Template remains behavior-preserving and does not absorb Quick Deploy or Overview semantics
**Given**
- `Deploy` is an approved Overview-first capability with distinct Quick Deploy and From Template child routes

**When**
- the From Template cleanup target is defined

**Then**
- `deploy.from_template` remains a distinct template-driven review/remediation/deploy workflow surface and not the route-entry Deploy surface
- no Quick Deploy ownership is moved into From Template
- no Deploy Overview ownership is moved into From Template
- runtime implementation, Quick Deploy extraction details, Deploy Overview extraction details, Deploy behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `DeployWorkspaceComposition` remains the owner for shared capability-level composition only
- a From Template-local seam becomes the target home for From Template-specific state, orchestration, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into From Template views
- `Deploy From Template` remains long-lived with route-activation refresh inside the existing Deploy workspace
- Deploy From Template remains behavior-preserving and does not absorb shared Deploy, Quick Deploy, or Overview concerns

## Definition of Done
- [ ] shared Deploy vs From Template-local ownership is explicit and traceable
- [ ] From Template-local state/orchestration/composition and host-cleanup expectations are explicit and traceable
- [ ] long-lived Deploy workspace participation and `deploy.from_template` route-activation refresh expectations are explicit and traceable
- [ ] Quick Deploy / Overview non-goals are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

# AC-039 - WinUI Deploy Quick Deploy Extraction Cleanup Target (AM95)

**Related FRs:** FR-164, FR-165, FR-166, FR-155, FR-156, FR-157, FR-087, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-100, FR-101, FR-102, FR-103, FR-125, FR-126, FR-127

## Scenarios

### 1) Deploy Quick Deploy stays under shared Deploy workspace composition but gains a Quick Deploy-local seam
**Given**
- the shared Deploy composition cleanup target, Deploy Overview cleanup target, and Deploy From Template cleanup target are already defined

**When**
- the narrow `Deploy Quick Deploy` cleanup target is reviewed

**Then**
- `Deploy Quick Deploy` remains under shared `DeployWorkspaceComposition` rather than becoming a shell-owned surface
- shared Deploy composition remains responsible only for shared capability-level composition concerns
- a Quick Deploy-local seam becomes the target home for Quick Deploy-specific state, orchestration, composition, and UI coordination

### 2) Quick Deploy-local ownership is explicit without widening shared Deploy composition into the workflow owner
**Given**
- `deploy.on_the_fly` is the on-the-fly deploy workflow surface for the capability

**When**
- Quick Deploy-local ownership is defined

**Then**
- Quick Deploy-local ownership explicitly includes:
  - Quick Deploy-specific state
  - Quick Deploy-specific orchestration
  - Quick Deploy-specific interaction boundaries used by the Quick Deploy surface
  - Quick Deploy-specific composition or host cleanup expectations
  - Quick Deploy-specific refresh or reconcile behavior triggered by `deploy.on_the_fly` activation
- shared `DeployWorkspaceComposition` does not become the Quick Deploy workflow owner
- shared Deploy composition keeps only cross-surface capability concerns such as shared route activation and workspace participation

### 3) MainWindow and route-activation boundaries remain explicit for deploy.on_the_fly
**Given**
- AM33 and AM78 keep `MainWindow` limited to shell ownership and keep Deploy long-lived by default

**When**
- the Quick Deploy cleanup target is applied

**Then**
- views must not depend on or receive `MainWindow` directly
- `Deploy Quick Deploy` continues to participate in long-lived Deploy workspace lifetime rather than per-navigation recreation
- route activation of `deploy.on_the_fly` refreshes or reconciles Quick Deploy state within the existing Deploy workspace

### 4) Deploy Quick Deploy remains behavior-preserving and does not absorb From Template or Overview semantics
**Given**
- `Deploy` is an approved Overview-first capability with distinct Quick Deploy and From Template child routes

**When**
- the Quick Deploy cleanup target is defined

**Then**
- `deploy.on_the_fly` remains a distinct on-the-fly deploy workflow surface and not the route-entry Deploy surface
- no From Template ownership is moved into Quick Deploy
- no Deploy Overview ownership is moved into Quick Deploy
- runtime implementation, From Template extraction details, Deploy Overview extraction details, Deploy behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `DeployWorkspaceComposition` remains the owner for shared capability-level composition only
- a Quick Deploy-local seam becomes the target home for Quick Deploy-specific state, orchestration, composition, and interaction coordination
- `MainWindow` remains shell-only and is not injected into Quick Deploy views
- `Deploy Quick Deploy` remains long-lived with route-activation refresh inside the existing Deploy workspace
- Deploy Quick Deploy remains behavior-preserving and does not absorb shared Deploy, From Template, or Overview concerns

## Definition of Done
- [ ] shared Deploy vs Quick Deploy-local ownership is explicit and traceable
- [ ] Quick Deploy-local state/orchestration/composition and host-cleanup expectations are explicit and traceable
- [ ] long-lived Deploy workspace participation and `deploy.on_the_fly` route-activation refresh expectations are explicit and traceable
- [ ] From Template / Overview non-goals are explicit and traceable
- [ ] behavior-preservation and non-goals are explicit and traceable

---

# AC-040 - WinUI Diagnostics Shared Composition Cleanup Target (AM105)

**Related FRs:** FR-167, FR-168, FR-169, FR-104, FR-105, FR-106, FR-107, FR-108, FR-109, FR-125, FR-126, FR-127

## Scenarios

### 1) MainWindow remains shell-only while shared Diagnostics composition moves behind a Diagnostics-local owner
**Given**
- `Diagnostics` already has approved AL routing/header/navigation behavior

**When**
- the shared Diagnostics cleanup target is defined

**Then**
- `MainWindow` remains responsible only for:
  - shell route switching
  - shell title/description
  - shell compact/drawer behavior
  - shell host visibility
  - right-panel infrastructure
  - app-level workspace lifetime
- shared Diagnostics-local composition does not terminate in `MainWindow`
- a Diagnostics-local composition owner becomes the target home for shared Diagnostics-local composition and wiring

### 2) Shared Diagnostics responsibilities converge behind the Diagnostics-local composition owner
**Given**
- `Diagnostics` includes shared capability concerns across `Diagnostics Overview` and `Diagnostics Logs`

**When**
- the shared ownership boundary is reviewed

**Then**
- the Diagnostics-local composition owner is explicitly responsible for:
  - Diagnostics-local composition and wiring
  - shared Diagnostics route-activation handling
  - shared workspace lifetime participation
  - shared local interaction boundaries for:
    - Diagnostics Overview
    - Diagnostics Logs
- Diagnostics Overview extraction details and Diagnostics Logs extraction details remain deferred to later narrow issues

### 3) Diagnostics preserves its approved route-entry model while rejecting shell-centric end-state patterns
**Given**
- AL defined `Diagnostics` as an Overview-first capability with Logs as a child troubleshooting surface

**When**
- the cleanup target is applied

**Then**
- Diagnostics Overview remains the route-entry and index surface for `Diagnostics`
- Diagnostics Logs remains a child troubleshooting surface rather than a top-level shell destination
- capability-specific host interfaces implemented by `MainWindow` are explicitly treated as temporary bridges only
- shared Diagnostics composition does not redesign existing Diagnostics routes or route-entry semantics

### 4) Diagnostics remains long-lived with route-activation refresh and no direct MainWindow view coupling
**Given**
- AM33 established long-lived capability workspaces by default

**When**
- the Diagnostics cleanup target is defined

**Then**
- views must not depend on or receive `MainWindow` directly
- Diagnostics remains long-lived while the app session is open unless a later approved contract explicitly changes that rule
- navigation between Diagnostics Overview and Diagnostics Logs activates and reconciles shared Diagnostics state rather than recreating the workspace every route change
- runtime implementation, Diagnostics workflow redesign, and performance redesign remain out of scope

## Expected Boundary
- shell continues to host Diagnostics workspace lifetime, route visibility, and shell infrastructure
- a Diagnostics-local composition owner becomes the target home for shared Diagnostics-local composition across Diagnostics Overview and Diagnostics Logs
- temporary shell-host bridges are allowed only as migration scaffolding and are not the long-term architecture
- Diagnostics remains long-lived with route-activation refresh rather than per-navigation recreation
- Diagnostics preserves the Overview-first route-entry model and current shell-contained capability semantics explicitly

## Definition of Done
- [ ] shell-vs-Diagnostics ownership is explicit and traceable
- [ ] shared Diagnostics-local composition-owner target is explicit and traceable
- [ ] Diagnostics route-entry preservation is explicit and traceable
- [ ] temporary-bridge-vs-final-target rule and no-direct-`MainWindow`-injection rule are explicit and traceable
- [ ] long-lived Diagnostics workspace and route-activation refresh rule are explicit and traceable

---

# AC-041 - WinUI Diagnostics Overview Extraction Cleanup Target (AM110)

**Related FRs:** FR-170, FR-171, FR-172, FR-167, FR-168, FR-169, FR-104, FR-105, FR-106, FR-107, FR-108, FR-109, FR-125, FR-126, FR-127

## Scenarios

### 1) Diagnostics Overview stays under shared Diagnostics composition while converging Overview-local ownership
**Given**
- `Diagnostics` already has an approved shared composition boundary under `DiagnosticsWorkspaceComposition`

**When**
- the `Diagnostics Overview` cleanup target is defined

**Then**
- `Diagnostics Overview` remains under shared `DiagnosticsWorkspaceComposition`
- shared Diagnostics composition remains responsible only for shared capability-level concerns
- an Overview-local seam becomes the target home for Overview-specific state, composition, and UI coordination
- `MainWindow` remains responsible only for shell ownership and app-level workspace lifetime

### 2) Overview-local ownership is explicit without widening shared Diagnostics composition
**Given**
- `diagnostics.overview` is the Diagnostics route-entry and summary surface

**When**
- the Overview-local ownership boundary is reviewed

**Then**
- Overview-local ownership is explicitly responsible for:
  - Overview summary or entry-surface state
  - Overview-local navigation or interaction coordination
  - Overview-local composition and host cleanup expectations
  - Overview-specific refresh or reconcile behavior triggered by `diagnostics.overview` activation
- shared Diagnostics composition does not become the Overview workflow owner

### 3) Diagnostics Overview preserves approved route-entry behavior and long-lived workspace participation
**Given**
- Diagnostics remains an Overview-first capability and Logs remains a child troubleshooting surface

**When**
- the narrow Overview cleanup target is applied

**Then**
- `diagnostics.overview` remains the distinct route-entry and index surface for `Diagnostics`
- Diagnostics Overview continues to participate in the long-lived Diagnostics workspace rather than per-navigation workspace recreation
- route activation refreshes or reconciles Overview state within the existing Diagnostics workspace
- Diagnostics Logs ownership and semantics remain outside Overview ownership

### 4) Diagnostics Overview rejects shell-centric and Logs-owning end-state patterns
**Given**
- shell-only ownership and shared Diagnostics ownership are already defined elsewhere

**When**
- the Overview cleanup target is reviewed for non-goals

**Then**
- views must not depend on or receive `MainWindow` directly
- shared Diagnostics composition must not widen into an Overview workflow owner
- Overview does not absorb Diagnostics Logs ownership or semantics
- Diagnostics Logs extraction details, runtime implementation, Diagnostics behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `DiagnosticsWorkspaceComposition` continues to own shared Diagnostics capability composition, shared route participation, shared workspace hosting, and shared cross-surface coordination only where it is genuinely capability-level
- an Overview-local seam becomes the target home for Overview-specific state, composition, interaction boundaries, UI coordination, and `diagnostics.overview` refresh or reconcile handling
- `MainWindow` remains shell-only and is not injected into Overview views
- Diagnostics Overview remains the route-entry surface inside the long-lived Diagnostics workspace
- Diagnostics Logs ownership remains outside Overview and outside this issue's seam definition

## Definition of Done
- [ ] shared Diagnostics vs Overview-local ownership is explicit and traceable
- [ ] Overview-local state/composition/UI-coordination expectations are explicit and traceable
- [ ] `diagnostics.overview` route-entry and long-lived workspace participation are explicit and traceable
- [ ] route-activation refresh or reconcile expectations for `diagnostics.overview` are explicit and traceable
- [ ] Logs non-goals and shell-only boundary preservation are explicit and traceable

---

# AC-042 - WinUI Diagnostics Logs Extraction Cleanup Target (AM114)

**Related FRs:** FR-173, FR-174, FR-175, FR-167, FR-168, FR-169, FR-170, FR-171, FR-172, FR-104, FR-105, FR-106, FR-107, FR-108, FR-109, FR-125, FR-126, FR-127

## Scenarios

### 1) Diagnostics Logs stays under shared Diagnostics composition while converging Logs-local ownership
**Given**
- `Diagnostics` already has an approved shared composition boundary under `DiagnosticsWorkspaceComposition`

**When**
- the `Diagnostics Logs` cleanup target is defined

**Then**
- `Diagnostics Logs` remains under shared `DiagnosticsWorkspaceComposition`
- shared Diagnostics composition remains responsible only for shared capability-level concerns
- a Logs-local seam becomes the target home for Logs-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains responsible only for shell ownership and app-level workspace lifetime

### 2) Logs-local ownership is explicit without widening shared Diagnostics composition
**Given**
- `diagnostics.logs` is the Diagnostics troubleshooting and log-exploration surface

**When**
- the Logs-local ownership boundary is reviewed

**Then**
- Logs-local ownership is explicitly responsible for:
  - Logs troubleshooting or log-exploration state
  - Logs-local filtering, selection, refresh, and workflow coordination
  - Logs-local composition and host cleanup expectations
  - Logs-specific refresh or reconcile behavior triggered by `diagnostics.logs` activation
- shared Diagnostics composition does not become the Logs workflow owner

### 3) Diagnostics Logs preserves approved troubleshooting behavior and long-lived workspace participation
**Given**
- Diagnostics remains an Overview-first capability and Logs remains a child troubleshooting surface

**When**
- the narrow Logs cleanup target is applied

**Then**
- `diagnostics.logs` remains the distinct troubleshooting and log-exploration surface for `Diagnostics`
- Diagnostics Logs continues to participate in the long-lived Diagnostics workspace rather than per-navigation workspace recreation
- route activation refreshes or reconciles Logs state within the existing Diagnostics workspace
- Diagnostics Overview route-entry and index semantics remain outside Logs ownership

### 4) Diagnostics Logs rejects shell-centric and Overview-owning end-state patterns
**Given**
- shell-only ownership, shared Diagnostics ownership, and Overview ownership are already defined elsewhere

**When**
- the Logs cleanup target is reviewed for non-goals

**Then**
- views must not depend on or receive `MainWindow` directly
- shared Diagnostics composition must not widen into a Logs workflow owner
- Logs does not absorb Diagnostics Overview route-entry or index semantics
- Diagnostics Overview extraction details, runtime implementation, Diagnostics behavior redesign, and performance redesign remain out of scope

## Expected Boundary
- shared `DiagnosticsWorkspaceComposition` continues to own shared Diagnostics capability composition, shared route participation, shared workspace hosting, and shared cross-surface coordination only where it is genuinely capability-level
- a Logs-local seam becomes the target home for Logs-specific state, orchestration, interaction boundaries, composition, UI coordination, and `diagnostics.logs` refresh or reconcile handling
- `MainWindow` remains shell-only and is not injected into Logs views
- Diagnostics Logs remains the child troubleshooting surface inside the long-lived Diagnostics workspace
- Diagnostics Overview ownership remains outside Logs and outside this issue's seam definition

## Definition of Done
- [ ] shared Diagnostics vs Logs-local ownership is explicit and traceable
- [ ] Logs-local state/orchestration/composition/UI-coordination expectations are explicit and traceable
- [ ] `diagnostics.logs` troubleshooting role and long-lived workspace participation are explicit and traceable
- [ ] route-activation refresh or reconcile expectations for `diagnostics.logs` are explicit and traceable
- [ ] Overview non-goals and shell-only boundary preservation are explicit and traceable

---

# AC-043 - WinUI Lane Architecture Standard (AN1)

**Related FRs:** FR-176, FR-177, FR-178, FR-113, FR-114, FR-115, FR-125, FR-126, FR-127

## Scenarios

### 1) Non-trivial lanes use an explicit owner/controller/composition/shell-bridge split
**Given**
- shell ownership and capability composition ownership are already defined elsewhere
- some child WinUI lanes own enough local workflow or lifecycle behavior that lane-local ownership can no longer stay implicit

**When**
- the canonical lane architecture standard is defined

**Then**
- a non-trivial lane is explicitly expected to converge behind:
  - `WorkspaceOwner`
  - `WorkspaceController`
  - `WorkspaceComposition`
  - `WorkspaceShellBridge`
- `WorkspaceOwner` is explicitly responsible for:
  - lane-local lifetime and top-level wiring
  - controller/composition/shell-bridge lifetime
  - lane-specific route-activation entry points
  - lane-local refresh or reconcile sequencing
  - lane-local helper coordination where needed
- `WorkspaceController` is explicitly responsible for:
  - lane-local workflow orchestration
  - async sequencing or action flow
  - narrow host-driven cooperation without direct view or shell ownership
- `WorkspaceComposition` is explicitly responsible for:
  - lane-local view composition
  - view-state application
  - interaction-state capture
- `WorkspaceShellBridge` is explicitly responsible only for:
  - genuine shell-owned interaction seams such as dispatcher marshalling, dialogs, route-active queries, or explicit panel toggles

### 2) Each role has explicit non-goals so ownership does not drift by convenience
**Given**
- a non-trivial lane uses the canonical role split

**When**
- role ownership is reviewed

**Then**
- `WorkspaceOwner` does not become:
  - the shell route owner
  - the shell container owner
  - the detailed workflow sequencer
  - the detailed view-state applier
- `WorkspaceController` does not become:
  - a direct view owner
  - a direct `MainWindow` consumer
  - a shared-capability composition owner
- `WorkspaceComposition` does not become:
  - the workflow owner
  - the lane-helper coordinator
  - the shared-capability owner
- `WorkspaceShellBridge` does not become:
  - a general-purpose shell escape hatch
  - a holder for lane workflow state
  - a disguised direct `MainWindow` dependency

### 3) Known drift patterns are explicitly rejected
**Given**
- later runtime cleanup will continue adding or refining lane-local seams

**When**
- the standard defines banned patterns

**Then**
- backpack hosts are explicitly rejected
- attach cycles are explicitly rejected
- lane-specific shell lambdas in `MainWindow` are explicitly rejected as the integration pattern
- lane-specific logic inside shared capability composition is explicitly rejected
- a narrow dedicated shell bridge remains acceptable, but ad hoc shell wiring is not treated as equivalent

### 4) The simple-lane exception is explicit and bounded
**Given**
- some lanes may be too small to justify the full four-part split

**When**
- the standard defines when a slimmer form is allowed

**Then**
- the full split may be avoided only when a lane:
  - lacks independent lane-local workflow orchestration
  - lacks lane-local helper coordination
  - lacks a dedicated auxiliary surface such as a lane-local side panel or second bound view
  - lacks a genuine shell-owned interaction seam beyond already-approved visibility handling
- even under the slimmer form:
  - `MainWindow` remains shell-only
  - shared capability composition remains capability-shared only
  - the banned-pattern list still applies
- the simple-lane exception is treated as an explicit fit rule, not as permission to leave ownership ambiguous

## Expected Boundary
- non-trivial lanes use an explicit owner/controller/composition/shell-bridge split
- each lane role has explicit may-own and must-not-own boundaries
- shell-only and capability-shared-only ownership rules remain preserved
- backpack hosts, attach cycles, lane-specific shell lambdas in `MainWindow`, and lane-specific logic in shared capability composition are rejected as end-state patterns
- a simple-lane exception exists, but only for genuinely narrow lanes and without weakening the existing shell or capability boundaries

## Definition of Done
- [ ] the non-trivial lane role split is explicit and traceable
- [ ] each role's non-goals are explicit and traceable
- [ ] the banned-pattern list is explicit and traceable
- [ ] the simple-lane exception rule is explicit and traceable
- [ ] shell-only and shared-capability-only boundary preservation remains explicit and traceable

---

# AC-044 - WinUI Typed Capability Bootstrap and Runtime Contract (AN2)

**Related FRs:** FR-179, FR-180, FR-181, FR-113, FR-114, FR-115, FR-125, FR-126, FR-127, FR-176, FR-177, FR-178

## Scenarios

### 1) Capability bootstrap is explicit and distinct from the long-lived runtime boundary
**Given**
- `MainWindow` remains the shell composition root
- later runtime cleanup needs a clearer shell-to-capability boundary than direct lane/helper construction in `MainWindow`

**When**
- the typed capability bootstrap/runtime contract is defined

**Then**
- capability bootstrap is explicitly defined as the shell-owned construction step for a capability runtime
- capability bootstrap may:
  - resolve app-level services
  - bind fixed shell-owned hosts or view roots
  - create shell bridges or adapters needed by the capability runtime
  - instantiate one typed runtime boundary for the capability
- capability bootstrap does not remain the capability's long-lived owner after construction

### 2) The typed capability runtime owns the capability-local runtime boundary without replacing lower seams
**Given**
- a capability may already have shared capability composition and lane-local seams beneath it

**When**
- the typed runtime boundary is defined

**Then**
- the typed capability runtime is explicitly responsible for:
  - shared capability composition lifetime
  - capability-shared helper lifetime
  - lane-local seam lifetime
  - capability-local route-activation handoff
  - capability-level refresh or reconcile entry points
- the typed capability runtime does not become:
  - the shell route owner
  - the shell navigation or shell header owner
  - the shell container owner
  - the lane-local workflow owner when that responsibility belongs in lane-local seams

### 3) The relationship between runtime, shared capability composition, and lane seams remains explicit
**Given**
- shared capability composition and lane-local seams are already part of the approved direction

**When**
- the runtime hierarchy is defined

**Then**
- the intended hierarchy is explicit:
  - `MainWindow`
  - capability bootstrap
  - typed capability runtime
  - shared capability composition and capability-shared helpers where needed
  - lane-local seams under the capability boundary
- shared capability composition remains limited to genuinely shared capability-level concerns
- lane-local seams remain responsible for lane-local workflow and lane-local view-state application
- typed capability runtime does not replace the lane architecture standard from AN1

### 4) MainWindow remains shell-only while converging toward one typed runtime per capability
**Given**
- `MainWindow` already owns shell route switching, shell header state, shell panel infrastructure, and app-level workspace lifetime

**When**
- the typed capability runtime target is defined

**Then**
- `MainWindow` remains explicitly responsible for:
  - shell route switching and route resolution
  - shell navigation configuration and selection
  - shell header/title/description
  - shell theme and compact/drawer behavior
  - shell-owned panel/container infrastructure
  - shell host visibility
  - app-level capability runtime lifetime
  - capability bootstrap entry points
- `MainWindow` is not treated as the long-term direct owner of:
  - multiple lane-local composition fields for the same capability
  - multiple lane-local owner fields for the same capability
  - capability-local helper fields whose meaning is local to one capability
  - scattered capability-local route-activation wiring for the same capability
- the target is one typed runtime field per capability rather than one shell field per capability sub-piece

### 5) Implementation policy and bootstrap timing remain explicit non-goals
**Given**
- the issue is docs-only

**When**
- the runtime contract is reviewed for scope

**Then**
- runtime implementation remains out of scope
- eager-vs-lazy bootstrap timing remains out of scope
- generic runtime-framework design remains out of scope
- behavior redesign remains out of scope

## Expected Boundary
- capability bootstrap is a shell-owned construction phase distinct from the long-lived typed capability runtime
- each capability can converge toward one typed runtime boundary owned by `MainWindow` at the app level
- typed capability runtime sits above shared capability composition and lane-local seams without replacing either
- `MainWindow` remains shell-only while converging away from lane/helper field sprawl
- AN1 lane-local rules remain in force underneath the typed runtime boundary

## Definition of Done
- [ ] capability bootstrap vs runtime ownership is explicit and traceable
- [ ] typed capability runtime ownership is explicit and traceable
- [ ] runtime vs shared-capability vs lane-local hierarchy is explicit and traceable
- [ ] `MainWindow` residual shell ownership is explicit and traceable
- [ ] runtime implementation and bootstrap-timing non-goals are explicit and traceable

---

# AC-045 - WinUI Deploy Results Panel Coordinator Contract (AN3)

**Related FRs:** FR-182, FR-183, FR-184, FR-090, FR-097, FR-113, FR-155, FR-161, FR-162, FR-163, FR-164, FR-165, FR-166

## Scenarios

### 1) The coordinator remains a narrow shared Deploy panel-intent seam
**Given**
- the shell owns right-panel infrastructure
- Quick Deploy and From Template both participate in the shared Deploy results panel

**When**
- the `DeployResultsPanelCoordinator` seam is defined

**Then**
- the coordinator is explicitly allowed to own:
  - active-lane-based panel title selection
  - active-lane-based auto-open recommendation
  - Overview empty-state participation for the shared Deploy panel region
  - panel-state delegation into the participating lane-local seams
  - capability-switch reset hooks that are specifically about Deploy panel presentation
- the coordinator is explicitly treated as a shared Deploy panel-intent seam rather than a general shared Deploy owner

### 2) Shell right-panel infrastructure remains outside the coordinator
**Given**
- `MainWindow` remains the shell composition root

**When**
- coordinator ownership is reviewed

**Then**
- the coordinator does not own:
  - shell panel open state
  - shell panel width/layout or compact fallback
  - shell owner-capability precedence
  - shell container visibility
  - shell host lifecycle
- those concerns remain explicitly shell-owned

### 3) Lane-local workflow and result semantics remain outside the coordinator
**Given**
- Quick Deploy and From Template already expose lane-local panel behavior through their own seams

**When**
- coordinator non-goals are reviewed

**Then**
- the coordinator does not own:
  - lane-local readiness or progress state
  - lane-local workflow sequencing
  - lane-local result-row or issue-row construction
  - lane-local launcher semantics beyond reading already-exposed lane intent
  - general shared Deploy helper coordination such as reference-data refresh or template-editor launch policy
- lane-local seams remain responsible for their own panel meaning, content, and workflow-local state application

### 4) Shared Deploy may delegate narrow panel-integration questions without widening the seam
**Given**
- shared Deploy composition still needs one place to answer shared panel-integration questions

**When**
- later runtime cleanup uses the coordinator seam

**Then**
- acceptable delegation is limited to:
  - panel title selection
  - auto-open recommendation
  - Overview empty-state visibility
  - panel-state application into the participating lanes
  - capability-switch reset hooks specifically tied to Deploy panel presentation
- unacceptable delegation includes:
  - route resolution
  - general Deploy refresh policy
  - general shared-helper policy
  - capability-wide workflow ownership

## Expected Boundary
- `DeployResultsPanelCoordinator` remains a narrow shared Deploy panel-intent seam
- shell panel infrastructure remains shell-owned
- Quick Deploy and From Template remain responsible for lane-local panel meaning and lane-local workflow semantics
- shared Deploy may use the coordinator for narrow panel-integration decisions only
- the coordinator's non-goals are explicit enough that future refactors do not treat it as a convenience landfill

## Definition of Done
- [ ] the coordinator's allowed ownership is explicit and traceable
- [ ] shell-owned panel infrastructure stays explicit and traceable outside the coordinator
- [ ] lane-local workflow and result-semantics non-goals are explicit and traceable
- [ ] allowed shared Deploy delegation is explicit and traceable
- [ ] the coordinator remains a narrow seam rather than a shared Deploy workflow owner

---

# AC-046 - WinUI Deploy Shared Helper Ownership Contract (AN4)

**Related FRs:** FR-185, FR-186, FR-187, FR-188, FR-155, FR-161, FR-162, FR-163, FR-164, FR-165, FR-166, FR-176, FR-177, FR-179, FR-180

## Scenarios

### 1) Shared Deploy helpers remain narrow capability-shared, lane-triggered seams
**Given**
- shared Deploy already has lane-local owners and compositions beneath the current shell-owned construction path
- later cleanup may reuse helper seams across more than one Deploy lane

**When**
- shared Deploy helper ownership is defined

**Then**
- shared Deploy helpers are explicitly classified as capability-shared, lane-triggered seams
- helper reuse does not make the helper the owner of lane-local workflow meaning
- lane-local owners, lane-local hosts, or later capability-runtime seams remain responsible for:
  - invocation timing
  - status and remediation messaging
  - post-helper workflow sequencing
  - route-activation-driven refresh or reconcile behavior

### 2) `DeployReferenceDataService` remains a shared reference-data seam only
**Given**
- Quick Deploy and From Template both depend on shared Deploy-side reference inputs

**When**
- `DeployReferenceDataService` ownership is reviewed

**Then**
- the helper is explicitly allowed to own:
  - shared Deploy reference-data loading
  - shared settings/switch/catalog snapshot exposure
  - shared cached snapshot behavior where current callers do not require stronger freshness guarantees
  - a narrow `EnsureAsync(forceRefresh)` boundary
- the helper does not own:
  - route-activation refresh policy
  - lane-local refresh/reconcile sequencing
  - readiness re-evaluation
  - lane-local default selection
  - user-facing remediation or status messaging
  - shell or panel coordination

### 3) `DeployResolveSuggestionsService` remains a shared deterministic suggestion seam only
**Given**
- multiple Deploy lanes may need the same switch/disk auto-resolve rules

**When**
- `DeployResolveSuggestionsService` ownership is reviewed

**Then**
- the helper is explicitly allowed to own:
  - deterministic shared Deploy-side normalization and suggestion rules
  - applying those rules to a caller-supplied template using caller-supplied reference data
  - returning an applied-count summary
- the helper does not own:
  - loading or refreshing reference data
  - deciding when suggestions run
  - deciding whether suggestions are automatic or user-invoked in a given lane
  - readiness re-evaluation after suggestions apply
  - lane-local status text or correction messaging
  - template-editor launch behavior

### 4) `DeployTemplateEditorLauncher` remains a narrow cross-capability launch seam only
**Given**
- Deploy may need to hand off a caller-prepared template document into the Templates editor workspace

**When**
- `DeployTemplateEditorLauncher` ownership is reviewed

**Then**
- the helper is explicitly allowed to own:
  - forwarding a caller-supplied `TemplateEditorDocument`
  - forwarding a caller-supplied status text
  - the narrow launch boundary into the existing Templates editor seam
- the helper does not own:
  - deciding when a lane should open the editor
  - building the template snapshot or choosing the document contents
  - lane-local validation or correction policy
  - template-library selection or reconciliation policy
  - broader Deploy-to-Templates workflow coordination

### 5) `DeployReferenceDataService` invalidation remains explicit and deferred
**Given**
- current shared Deploy reference data uses caller-invoked refresh rather than a broader invalidation system

**When**
- invalidation scope is reviewed in this docs-only issue

**Then**
- the contract explicitly allows caller-owned `EnsureAsync(forceRefresh: true)` triggers
- the contract does not silently promise auto-invalidation across Assets, Templates, or other app-state changes
- stronger same-session freshness guarantees remain deferred until a later narrow issue establishes the trigger, scope, and owner

## Expected Boundary
- shared Deploy helpers are narrow reusable seams under lane-local or later runtime ownership, not replacements for those owners
- `DeployReferenceDataService` owns shared reference-data loading/caching only
- `DeployResolveSuggestionsService` owns deterministic shared suggestion application only
- `DeployTemplateEditorLauncher` owns the narrow Deploy-to-Templates launch handoff only
- invalidation remains an explicit caller-owned/deferred rule rather than an assumed background guarantee

## Definition of Done
- [ ] shared Deploy helper classification is explicit and traceable
- [ ] `DeployReferenceDataService` ownership and non-goals are explicit and traceable
- [ ] `DeployResolveSuggestionsService` ownership and non-goals are explicit and traceable
- [ ] `DeployTemplateEditorLauncher` ownership and non-goals are explicit and traceable
- [ ] `DeployReferenceDataService` invalidation is either contracted or explicitly deferred with rationale

---

# AC-047 - WinUI Accessibility Baseline

**Related FRs:** FR-189, FR-041

## Scenarios

### 1) Primary workflows remain keyboard reachable with logical focus flow
**Given**
- a user is navigating a primary WinUI workflow without relying on the mouse

**When**
- focus moves through the active surface using keyboard navigation

**Then**
- the primary controls for the workflow are reachable by keyboard
- focus order is logical for the visible workflow
- keyboard-only users are not blocked by hover-only affordances

### 2) Focus visibility remains explicit
**Given**
- focus is moved across interactive controls in a WinUI surface

**When**
- the user tabs, arrow-navigates, or otherwise changes focus

**Then**
- focused controls remain visibly distinguishable
- compact or dense layouts do not remove the user's ability to identify the active control

### 3) Supported text scaling and high-contrast modes do not break primary interaction
**Given**
- the shell or a capability surface is shown under supported text scaling or high-contrast presentation

**When**
- the user loads or interacts with a primary workflow

**Then**
- layout adaptation does not hide the workflow's primary controls
- text remains readable enough to complete the workflow
- focus flow and primary action access remain intact

### 4) Errors are not communicated by color alone
**Given**
- a WinUI workflow shows validation, readiness, or runtime error state

**When**
- the issue is presented to the user

**Then**
- the message includes clear text or equivalent non-color cues
- users are not required to infer the problem from color alone
- the error remains actionable rather than purely decorative

## Expected Boundary
- the accessibility baseline is a cross-cutting WinUI quality contract, not a capability-specific redesign mandate
- slices may satisfy the baseline using the existing shell/view patterns as long as keyboard access, focus clarity, readable presentation, and non-color-only errors remain intact

## Definition of Done
- [ ] keyboard reachability is explicit for primary WinUI workflows
- [ ] logical focus order and visible focus indication are explicit
- [ ] supported text scaling and high-contrast resilience are explicit
- [ ] non-color-only error communication is explicit
