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

**Related FRs:** FR-022, FR-023, FR-024, FR-025, FR-026, FR-028, FR-040, FR-041

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
- UI shows recovery guidance
- Logs include per-VM failure details and overall failure summary

## Expected UI
- Template selection UI + Deploy action
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

## Expected Artifacts / Side Effects
- Differencing disk files created per VM
- Hyper-V VMs created per VM
- Hyper-V NIC connected to specified switch(es)
- Optional: local deployment record (TBD)

## Definition of Done
- [ ] All scenarios above pass
- [ ] Progress UI updates reliably for multi-VM
- [ ] Failure behavior conforms to GR-02
- [ ] Logs conform to GR-03 with operationId
- [ ] Tests exist for validation + deployment orchestration (mocks acceptable for Hyper-V)
- [ ] No orphaned resources without either rollback or explicit cleanup guidance

---

# AC-002 — Create/Edit/Delete Templates

**Related FRs:** FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-018

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

## Open Questions / TBDs
- Cleanup strategy is defined in `docs/01-requirements/cleanup-cancellation-policy.md`.
- VM/lab naming strategy and uniqueness rules
- Whether to store deployment history records locally
