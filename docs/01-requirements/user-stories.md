# User Stories — LabAssistantV2

**Purpose:** Describe desired behavior from the user’s perspective, in a way that is detailed enough for development, agent execution, and test planning.

**Status:** Live planning/traceability doc. It supports story framing and story-to-FR/AC/test mapping, but it does not override `srs.md` or `acceptance-criteria.md` on implementation behavior. Draft placeholders in this file do not become active implementation authority until they are mapped into the current requirement set or an active slice explicitly depends on them.

## How to fill this (advanced-stage workflow)
- Keep each story **end-to-end** and user-observable (UI + outcomes).
- Link each story to:
  - **FRs** in `srs.md`
  - **ACs** in `acceptance-criteria.md`
- Each story includes:
  - Scenarios (Happy path + Validation failures + Runtime failures)
  - Expected user feedback (UI)
  - Expected side effects (files/Hyper-V artifacts)
  - Notes/edge cases

## Conventions
- **Priority**: P0 = required for next-level release, P1 = important follow-up, P2 = optional/future.
- **Cleanup policy**: On runtime failure, the system **must cleanup** resources created by the operation (VMs, disks, etc.) and show a clear message that cleanup occurred (or if cleanup partially failed, show exactly what remains).

---

## Epic A — Prerequisites & Configuration

---

## Story US-001: First Run — Environment Check & Clear Guidance
**As a** support engineer  
**I want** the tool to check my environment on startup (Hyper-V, permissions, required components)  
**So that** I immediately know what I need to fix before trying to deploy a lab

**Links**
- FRs: (Prerequisite validation implied by FR-020/FR-022/FR-030 + error handling + UX)
- ACs: (Global rules in acceptance criteria: no silent failure)

### Acceptance Criteria (Scenarios)

1) **Happy path — Environment OK**
- **Context:** User opens LabAssistantV2 on a machine with Hyper-V enabled and required permissions.
- **Event:** App starts.
- **Result:** App shows “Ready” status and enables deployment actions.

**Given** Hyper-V is enabled and accessible  
**When** the app starts  
**Then** the app confirms readiness and enables relevant features

2) **Environment failure — Hyper-V disabled**
- **Context:** Hyper-V is not enabled on the host.
- **Event:** App starts.
- **Result:** App shows a clear message explaining Hyper-V is required and how to enable it.

**Given** Hyper-V is disabled  
**When** the app starts  
**Then** deployment features are disabled and guidance is shown

3) **Permission failure — Insufficient privileges**
- **Context:** User is not running with required permissions to manage Hyper-V.
- **Event:** App starts.
- **Result:** App explains elevation requirements and which features are impacted.

**Given** user lacks required permissions  
**When** the app starts  
**Then** the app explains what operations require elevation and prevents failures later

### Priority
- P0

### Notes / Edge Cases
- If only some features require admin, app should degrade gracefully (e.g., view templates but block deploy).
- Avoid “cryptic PowerShell error” showing directly to user; provide “what/why/how to fix”.

---

## Story US-002: Configure Default Storage Paths
**As a** power user / senior engineer  
**I want** to configure default storage paths for LabAssistant artifacts (VMs, differencing disks, templates, logs, base disk registry)  
**So that** I can control performance, disk usage, and organization

**Links**
- FRs: Configuration & storage (your section 4.1 proposal)
- ACs: (Should be added later; covered partially by global rules)

### Acceptance Criteria (Scenarios)

1) **Happy path — Set paths and persist**
- **Context:** User wants to keep differencing disks on fast SSD and logs/templates on a separate folder.
- **Event:** User edits settings and clicks Save.
- **Result:** Paths are validated, saved, and used for new operations.

**Given** user opens Settings → Storage Paths  
**When** user changes paths and saves  
**Then** the tool validates and persists these paths

2) **Validation failure — Path invalid or not writable**
- **Context:** User points differencing disk folder to a protected directory.
- **Event:** Save settings.
- **Result:** Tool blocks save and highlights the invalid path and reason.

**Given** a configured path is invalid or lacks permissions  
**When** user saves  
**Then** the tool blocks saving and shows actionable feedback

3) **Runtime failure — Path becomes unavailable later**
- **Context:** External drive disconnected or folder deleted after configuration.
- **Event:** User deploys a lab later.
- **Result:** Tool blocks deployment with guidance to fix paths.

**Given** configured path is missing at runtime  
**When** a deployment starts  
**Then** deployment is blocked early with clear guidance

### Priority
- P0

### Notes / Edge Cases
- Clearly document what is stored where.
- If paths are changed, decide whether existing artifacts are moved (P2) or only future artifacts use new paths.

---

## Epic B — Base Disk (VHD/VHDX) Management

---

## Story US-010: Import/Register a Base Disk
**As a** support engineer  
**I want** to import (register) a base VHD/VHDX into the tool  
**So that** I can deploy VMs using differencing disks derived from it

**Links**
- FRs: FR-050, FR-040/041 (logs/status)
- ACs: AC-004

### Acceptance Criteria (Scenarios)

1) **Happy path — Register base disk**
- **Context:** User has `Win11_23H2_Base.vhdx` already sysprepped/configured.
- **Event:** User registers the file.
- **Result:** Base disk appears in base disk list and is selectable in deploy UI.

**Given** user selects a valid VHD/VHDX path  
**When** user clicks Register  
**Then** the base disk is registered and visible for selection

2) **Validation failure — File not found / wrong extension**
- **Context:** User selects a missing path or non-VHDX file.
- **Event:** Register.
- **Result:** Tool blocks with clear error and no registry entry is added.

3) **Validation failure — Disk locked/in use**
- **Context:** Base disk is in use or inaccessible.
- **Event:** Register.
- **Result:** Tool explains the disk cannot be accessed and suggests a fix.

### Priority
- P0

### Notes / Edge Cases
- Decide if tool supports both VHD and VHDX (likely yes, but confirm).
- If disk is huge, registration should be metadata-only (fast), not copying.

---

## Story US-011: Edit Base Disk Display Metadata
**As a** lead engineer / template author  
**I want** to set a friendly label and OS classification for each base disk  
**So that** templates can map across machines and humans can pick the right image

**Links**
- FRs: FR-051, FR-054
- ACs: AC-004

### Acceptance Criteria
1) **Happy path — Edit metadata**
**Given** a base disk is registered  
**When** user edits label/OS classification and saves  
**Then** new metadata is persisted and used in UI and matching rules

2) **Validation failure — invalid OS classification format (if standardized)**
**Given** classification must follow a defined scheme (TBD)  
**When** user saves invalid classification  
**Then** tool blocks and explains acceptable formats

### Priority
- P0

### Notes / Edge Cases
- OS classification scheme could start simple: “Windows 11”, “Windows Server 2022”, etc., then evolve later.
- Keep classification stable across machines (that’s the point of mapping).

---

## Story US-012: Remove Base Disk From Tool Registry
**As a** user  
**I want** to remove old base disks from the tool’s registry  
**So that** the UI stays clean and templates don’t reference obsolete images

**Links**
- FRs: FR-052
- ACs: AC-004

### Acceptance Criteria
1) **Happy path — Remove registry entry**
**Given** base disk is registered  
**When** user removes it and confirms  
**Then** it no longer appears in selection lists and registry updates

2) **Safety — Clarify whether file is deleted**
**Given** removal is initiated  
**When** confirmation dialog appears  
**Then** dialog clearly states whether the underlying VHDX will be deleted or not (policy TBD)

### Priority
- P0

### Notes / Edge Cases
- Strongly recommend: default action = remove from registry only (safer), optional “also delete file” later.

---

## Story US-013: Auto-map Missing Base Disk on Template Deploy
**As a** support engineer using a shared template  
**I want** the tool to map missing base disks to compatible ones on my machine  
**So that** templates remain portable across engineers

**Links**
- FRs: FR-054, FR-053
- ACs: AC-004

### Acceptance Criteria (Scenarios)

1) **Happy path — Compatible base disk exists**
- **Context:** Template references base disk ID “Win11-Base-A”, but my machine has “Win11-Base-B” with same OS classification.
- **Event:** Deploy lab.
- **Result:** Tool auto-maps and notifies me what it substituted.

**Given** template base disk is missing but a compatible disk exists  
**When** deployment starts  
**Then** tool substitutes the compatible disk and informs the user

2) **Failure — No compatible base disk**
**Given** template base disk missing and no compatible exists  
**When** user attempts deploy  
**Then** deployment is blocked with guidance: import disk or select compatible classification

### Priority
- P1

### Notes / Edge Cases
- Mapping should be deterministic (if multiple compatible disks exist, pick best match by policy: newest? user prompt? default? TBD).
- Always log mapping decision.

---

## Epic C — Template Management (VM templates + Lab templates)

---

## Story US-020: Create a VM Template
**As a** lead engineer  
**I want** to create a VM Template (one or more VM definitions without full lab topology)  
**So that** others can deploy standardized machines quickly

**Links**
- FRs: FR-010, FR-011, FR-014, FR-015, FR-018
- ACs: AC-002

### Acceptance Criteria (Scenarios)

1) **Happy path — Save valid VM template**
**Given** user defines VM count and VM settings (CPU/RAM/base disk/network)  
**When** user saves as VM Template  
**Then** template is validated and persisted and appears in template list

2) **Validation failure — Missing required fields**
**Given** required field missing (e.g., base disk not selected)  
**When** user saves  
**Then** tool blocks and highlights missing fields with actionable messages

3) **Versioning — Schema version included**
**Given** template is created  
**When** tool persists it  
**Then** template includes schema version field

### Priority
- P0

### Notes / Edge Cases
- Decide if a VM template supports 1 VM only or a “set of identical VMs” (TBD).
- Template names should be unique or collision-handled (prompt/auto-suffix).

---

## Story US-021: Create a Lab Template (Multi-VM + Networking)
**As a** lead engineer  
**I want** to create a Lab Template that defines multiple VMs and their networking  
**So that** engineers can recreate complex scenarios reliably

**Links**
- FRs: FR-010..FR-018, FR-032
- ACs: AC-002

### Acceptance Criteria
1) **Happy path — Save valid lab template**
**Given** user defines multiple VMs + network topology references  
**When** user saves as Lab Template  
**Then** template validates and persists and is deployable

2) **Validation failure — topology references missing switch**
**Given** template requires switch “X” but it’s not resolvable at creation-time (policy TBD)  
**When** user saves  
**Then** tool either:
- blocks save, OR
- allows save but marks “environment-dependent” (TBD)
(choose policy; prefer allow save + validate on deploy)

### Priority
- P0

### Notes / Edge Cases
- Prefer: template validation checks structure; environment validation happens on deploy.

---

## Story US-022: Edit Template (VM or Lab)
**As a** template author  
**I want** to edit a template  
**So that** I can evolve it as scenarios change without recreating from scratch

**Links**
- FRs: FR-012, FR-014, FR-018
- ACs: AC-002

### Acceptance Criteria
1) **Happy path — edit and save**
**Given** template exists  
**When** user edits and saves  
**Then** tool validates and updates the stored template

2) **Validation failure on edit**
**Given** user removes a required field  
**When** saving  
**Then** tool blocks save and explains what broke

### Priority
- P0

### Notes / Edge Cases
- Decide whether edits bump template version automatically (schema version vs template revision—TBD).

---

## Story US-023: Delete Template
**As a** user  
**I want** to delete templates I no longer need  
**So that** I keep the system clean and reduce mistakes

**Links**
- FRs: FR-013
- ACs: AC-002

### Acceptance Criteria
**Given** template exists  
**When** user deletes and confirms  
**Then** template is removed from tool and no longer selectable

### Priority
- P0

### Notes / Edge Cases
- Deleting a template should not delete deployed VMs.
- If deployment history exists later, keep record but mark template removed.

---

## Story US-024: Import Template from JSON
**As a** support engineer  
**I want** to import a template JSON file  
**So that** I can use templates shared by other engineers

**Links**
- FRs: FR-016, FR-014, FR-015, FR-018
- ACs: AC-003

### Acceptance Criteria
1) **Happy path**
**Given** JSON template is valid and supported  
**When** imported  
**Then** appears in template list and is deployable

2) **Invalid JSON/schema**
**Given** malformed or invalid schema  
**When** imported  
**Then** import fails with clear validation report, nothing is registered

3) **Unsupported schema version**
**Given** schema version unsupported  
**When** imported  
**Then** import blocked or warned (policy TBD) with clear messaging

### Priority
- P1

### Notes / Edge Cases
- Import collisions (same template name/id) must follow a policy: overwrite / duplicate / prompt (TBD).

---

## Story US-025: Export Template to JSON
**As a** lead engineer  
**I want** to export a template to JSON  
**So that** I can share it with other engineers or version it in source control

**Links**
- FRs: FR-017
- ACs: AC-003

### Acceptance Criteria
**Given** template exists  
**When** exported  
**Then** JSON file is written and includes schema version and all required fields

### Priority
- P1

### Notes / Edge Cases
- Avoid exporting secrets (should be none).
- Export should be deterministic (same output order/format to reduce diffs—optional).

---

## Epic D — Deployment (On-the-fly + From Template)

---

## Story US-030: Deploy a Single VM On-the-Fly
**As a** support engineer  
**I want** to deploy a single VM without creating a template  
**So that** I can quickly reproduce an issue with minimal setup time

**Links**
- FRs: FR-020, FR-023, FR-024, FR-025, FR-026
- ACs: (Should exist, but your advanced stage can treat this as subset of AC-001)

### Acceptance Criteria (Scenarios)
1) **Happy path**
**Given** base disk and switch exist  
**When** user configures CPU/RAM/base disk/network and clicks Deploy  
**Then** VM is created using a differencing disk and attached to switch with progress shown

2) **Validation failure**
**Given** missing base disk selection  
**When** deploy clicked  
**Then** tool blocks and shows clear validation message

3) **Runtime failure with cleanup**
**Given** disk created but VM creation fails  
**When** error occurs  
**Then** tool cleans up created disk and any partial VM and reports cleanup result

### Priority
- P0

### Notes / Edge Cases
- VM name collisions: prompt/auto-suffix/block policy TBD (but must be deterministic and testable).

---

## Story US-031: Deploy Multiple VMs On-the-Fly
**As a** senior engineer  
**I want** to deploy multiple VMs configured on-the-fly  
**So that** I can quickly spin up ad-hoc scenarios without authoring a template first

**Links**
- FRs: FR-021, FR-023, FR-024..FR-028
- ACs: AC-001 (multi-VM behavior is similar)

### Acceptance Criteria
- Must show per-VM progress and perform cleanup on partial failures.

### Priority
- P1

### Notes / Edge Cases
- UI must allow defining multiple VM entries cleanly (bulk edit or repeated form).

---

## Story US-032: Deploy Lab From Template (Multi-VM)
**As a** support engineer  
**I want** to deploy a lab from a lab template  
**So that** I can reproduce complex scenarios reliably with one action

**Links**
- FRs: FR-022..FR-028
- ACs: AC-001

### Acceptance Criteria
- See AC-001 for full details.
- Key must-haves: per-VM status + cleanup on failure + summary on completion.

### Priority
- P0

### Notes / Edge Cases
- Template may reference base disk missing → mapping rules apply.
- If one VM fails late, cleanup must include *all* created VMs/disks from that operation.

---

## Story US-033: Save On-the-Fly Configuration as a Template
**As a** engineer  
**I want** to save an ad-hoc configuration as a VM template or lab template  
**So that** I can reuse it later and share it

**Links**
- FRs: FR-029
- ACs: AC-002

### Acceptance Criteria
- Saving must run the same validation rules as normal template creation.
- Saved template must include schema version.

### Priority
- P1

### Notes / Edge Cases
- Decide whether to allow saving after deployment only, or before deploying (both are useful).

---

## Story US-034: Cancel Deployment (and Cleanup)
**As a** user  
**I want** to cancel a deployment in progress  
**So that** I can stop mistakes quickly and free resources

**Links**
- FRs: FR-027, FR-028 + cleanup policy
- ACs: AC-001 (extend with cancellation scenario)

### Acceptance Criteria
1) **Happy path**
**Given** deployment is running  
**When** user clicks Cancel  
**Then** deployment stops and all created resources from the operation are cleaned up

2) **Cancel not possible in a critical step**
**Given** tool is in an atomic step (TBD)  
**When** cancel requested  
**Then** tool acknowledges cancel and stops at safe boundary, then cleans up

### Priority
- P1

### Notes / Edge Cases
- Clearly show “cancelling…” state and final result.

---

## Epic E — Networking

---

## Story US-040: Select Existing Virtual Switch for VM/Lab
**As a** user  
**I want** to select an existing Hyper-V virtual switch for deployment  
**So that** deployed VMs are connected correctly

**Links**
- FRs: FR-030, FR-031
- ACs: (covered by AC-001/AC-030 rules)

### Acceptance Criteria
- Switch list is loaded.
- Missing switch blocks deployment with clear message.

### Priority
- P0

### Notes / Edge Cases
- If switch disappears between selection and deployment, tool must detect before creation starts.

---

## Story US-041: Template-defined Network Topology
**As a** lead engineer  
**I want** lab templates to define networking topology  
**So that** complex environments can be recreated reliably

**Links**
- FRs: FR-032
- ACs: AC-001

### Acceptance Criteria
- Lab deploy attaches each VM to correct switch/network configuration per template.
- Validation checks structural correctness of topology.

### Priority
- P1

### Notes / Edge Cases
- Keep v1 realistic: topology might just mean “which switch each VM is attached to” (not full routing simulation).

---

## Story US-042: Manage Virtual Switches From the Tool (Optional)
**As a** power user  
**I want** to create/modify/delete virtual switches from the tool  
**So that** I can manage networking without switching tools

**Links**
- FRs: FR-033, FR-034, FR-035
- ACs: AC-005

### Acceptance Criteria
- CRUD operations must be validated, logged, and permission-aware.

### Priority
- P2

### Notes / Edge Cases
- This is powerful and can break user networking; keep behind confirmations and strong warnings.

---

## Epic F — Observability & Diagnostics

---

## Story US-050: Deployment Progress UI + Summary
**As a** user  
**I want** clear progress and a final summary  
**So that** I know what’s happening and what was created

**Links**
- FRs: FR-026, FR-041
- ACs: AC-001

### Acceptance Criteria
- Show overall progress + per-VM stage.
- Final summary includes: VM names, base disks used, switch attachments.
- Failures show actionable guidance + cleanup result.

### Priority
- P0

### Notes / Edge Cases
- Must remain responsive during long operations.

---

## Story US-051: Structured Logging With Correlation ID
**As a** support engineer troubleshooting a failed deploy  
**I want** structured logs that correlate steps in a single operation  
**So that** I can quickly identify what failed and why

**Links**
- FRs: FR-040
- ACs: AC-001 + global rules

### Acceptance Criteria
- Each deployment has a correlation id.
- Logs include start/step/end events with key fields.

### Priority
- P0

### Notes / Edge Cases
- Never log secrets (should not exist, but still).

---

## Story US-052: Export Diagnostic Bundle
**As a** support engineer or developer  
**I want** to export a diagnostic bundle  
**So that** I can share troubleshooting data with another engineer without manually collecting files

**Links**
- FRs: FR-042
- ACs: (should exist—add later)

### Acceptance Criteria
- Bundle contains: recent logs, config paths/settings, template metadata involved in last operation.
- Bundle must exclude secrets and large unnecessary data (e.g., not entire VHDX files).

### Priority
- P1

### Notes / Edge Cases
- Bundle size limits or caps may be needed.

---

## Epic G — “Next Level” Quality (Internal personas)

---

## Story US-060: Developer/Maintainer — Prevent Regressions Via Automated Tests
**As a** maintainer  
**I want** core logic to have automated tests (validation, mapping, naming rules)  
**So that** new changes don’t break existing behavior

### Acceptance Criteria
- Validation rules are unit-tested.
- Base disk mapping decisions are unit-tested.
- Deploy orchestration is tested with mocks around Hyper-V integration.

### Priority
- P1

### Notes / Edge Cases
- This story is intentionally internal; it protects your “next level” goals.

---

## Open Questions / TBDs
- Mapping when multiple compatible base disks exist remains deferred to the current TBD inventory in `docs/00-overview/tbd-register.md`.
- Collision policies remain deferred to the current naming-policy TBD inventory in `docs/00-overview/tbd-register.md`.
