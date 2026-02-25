# Software Requirements Specification (SRS)

**Purpose:** This document defines the functional and non-functional requirements for LabAssistantV2 and serves as the primary implementation contract for development and testing.

**Terminology:**  
All domain terminology used in this document is defined in  
`/docs/00-overview/glossary.md`.

---

## 1. System Overview

- **Product name:** LabAssistantV2  
- **Primary users:**  
  - Support engineers requiring fast reproducible labs  
  - Senior engineers creating advanced troubleshooting environments  
  - Lead engineers designing reusable templates  

- **Operating environment:**  
  - Windows 10/11 with Hyper-V enabled  
  - .NET desktop runtime (exact version TBD)  
  - Local filesystem access for templates, logs, and virtual disks  
  - Administrative permissions required for Hyper-V operations  

- **Key integrations:**  
  - Microsoft Hyper-V  
  - Windows PowerShell / Hyper-V management APIs  
  - Local file system for configuration, templates, and logs  

---

## 2. System Context

### External systems/tools
- Hyper-V virtualization platform  
- Windows networking stack (virtual switches)  
- Local storage subsystem for VHD/VHDX files  

### Dependencies
- Hyper-V feature enabled on host machine  
- Valid base virtual disks available locally  
- PowerShell execution capability  
- Sufficient disk space and system resources  

## Priority Definitions

- **P0 — Critical (MVP Required):**  
  Functionality required for the first usable release.  
  Without these, the product cannot fulfill its core purpose.

- **P1 — Important (Post-MVP):**  
  Significant usability, reliability, or flexibility improvements  
  that can be delivered after core functionality works.

- **P2 — Optional / Future Enhancement:**  
  Nice-to-have capabilities that do not block real-world usage.

---

## 3. Functional Requirements

All requirements follow the format:

**FR-### — The system shall …**

Each requirement must be **testable** and mapped to acceptance criteria.

---

### 3.1 Template Management

- **FR-010:** The system shall support two template types:
  - **VM Template** (describes one or more VMs without full lab topology), and
  - **Lab Template** (describes a full deployment including multi-VM composition and networking).
  - **Priority:** P0

- **FR-011:** The system shall allow users to create VM templates and lab templates.
  - **Priority:** P0

- **FR-012:** The system shall allow users to edit existing templates.
  - **Priority:** P0

- **FR-013:** The system shall allow users to delete templates.
  - **Priority:** P0

- **FR-014:** The system shall validate template integrity before saving.
  - **Acceptance details:** Validation must include required fields, schema version, and referenced entities (e.g., base disk identifiers).
  - **Priority:** P0

- **FR-015:** The system shall present validation errors to the user in a clear and actionable manner.
  - **Priority:** P0

- **FR-016:** The system shall support importing VM templates and lab templates from JSON files.
  - **Priority:** P1

- **FR-017:** The system shall support exporting VM templates and lab templates to JSON files.
  - **Priority:** P1

- **FR-018:** The system shall include a template schema version identifier in each template to track format changes over time.
  - **Priority:** P0

---

### 3.2 Deployment

- **FR-020:** The system shall allow users to deploy a single VM with configuration selected on-the-fly (without requiring a pre-existing template).
  - **Priority:** P0

- **FR-021:** The system shall allow users to deploy multiple VMs configured on-the-fly.
  - **Priority:** P1

- **FR-022:** The system shall allow deployment of labs from a saved lab template.
  - **Priority:** P0

- **FR-023:** The system shall always create VMs using differencing disks derived from imported base disks.
  - **Priority:** P0

- **FR-024:** The system shall allow users to configure VM hardware settings at deployment time, including at minimum:
  - CPU
  - RAM
  - Base disk selection
  - Network / virtual switch selection
  - **Priority:** P0

- **FR-025:** The system shall attach deployed VMs to configured virtual switches.
  - **Priority:** P0

- **FR-026:** The system shall display deployment progress and status to the user.
  - **Priority:** P0

- **FR-027:** The system shall allow cancellation of an ongoing deployment.
  - **Priority:** P1

- **FR-028:** The system shall handle partial deployment failures with clear recovery guidance.
  - **Priority:** P1

- **FR-029:** If a user deploys a VM or multi-VM configuration on-the-fly, the system shall allow saving that configuration as a template.
  - **Priority:** P1

- **FR-043:** The system shall perform a deployment readiness (preflight) evaluation before starting Hyper-V deployment actions.
  - **Acceptance details:** The full preflight must complete before any VM/disk/network creation starts, and any blocking failures shall prevent deployment start.
  - **Priority:** P0

- **FR-044:** The system shall support two preflight modes for deployment readiness:
  - **Quick preflight** (automatic on relevant Deploy-page configuration changes; may run partial/cheap checks for fast feedback)
  - **Full preflight** (authoritative deploy gating check on Deploy click)
  - **Priority:** P1

- **FR-045:** The system shall classify deployment readiness check results as `Pass`, `Warn`, or `Fail`, and shall include a machine-readable code plus actionable user guidance for non-pass results.
  - **Acceptance details:** Warnings must not block deployment by themselves; failures must block deployment.
  - **Priority:** P0

- **FR-046:** The system shall present deployment readiness results in the Deploy UI with actionable summaries and likely cause/path hints, while detailed technical stderr remains primarily in diagnostics/debug logs.
  - **Priority:** P1

---

### 3.3 Networking

- **FR-030:** The system shall allow selection of an existing Hyper-V virtual switch for VM connection.
  - **Priority:** P0

- **FR-031:** The system shall validate the availability of required virtual switches before deployment.
  - **Priority:** P0

- **FR-032:** The system shall support network topology configuration as defined in lab templates.
  - **Priority:** P1

- **FR-033:** The system shall allow creating new Hyper-V virtual switches from within the tool.
  - **Priority:** P2

- **FR-034:** The system shall allow modifying existing Hyper-V virtual switches from within the tool.
  - **Priority:** P2

- **FR-035:** The system shall allow deleting existing Hyper-V virtual switches from within the tool.
  - **Priority:** P2

---

### 3.4 Observability (Logs and Status)

- **FR-040:** The system shall generate structured logs for template operations, VM creation, disk operations, and networking.  
  - **Acceptance details:** Failure events should include known artifact/path context (for example base VHD path, target VHD path, VM path) when available to improve diagnosis speed.  
  - **Priority:** P0  

- **FR-041:** The system shall present user-friendly status and error messages during operations.  
  - **Acceptance details:** For readiness and runtime failures, the primary UI should favor concise actionable summaries with likely cause/path hints; raw technical stderr may remain in diagnostics/debug logs.  
  - **Priority:** P0  

- **FR-042:** The system shall allow exporting diagnostic information for troubleshooting.  
  - **Priority:** P1  

---

### 3.5 Base Disk Management (VHD/VHDX)

- **FR-050:** The system shall allow importing/registering a new base disk into the tool.
  - **Priority:** P0

- **FR-051:** The system shall allow editing display information for each base disk (e.g., name/label, OS type/version).
  - **Priority:** P0

- **FR-052:** The system shall allow removing a base disk from the tool’s registry.
  - **Note:** Removal from registry does not necessarily delete the underlying file unless explicitly chosen (TBD).
  - **Priority:** P0

- **FR-053:** The system shall prevent deployment when a required base disk is missing and no valid substitute exists.
  - **Priority:** P0

- **FR-054:** If a template references a base disk that is not imported, the system shall attempt to map it to another imported base disk with the same OS classification (if available), and notify the user of the substitution.
  - **Priority:** P1

---

## 4. Data Requirements

- Templates shall be stored as **structured local files** (JSON).  
- Template data shall include:
  - VM definitions  
  - Base disk references  
  - Network configuration  
- Logs shall be stored locally in a structured, readable format.  
- Template schema versioning and compatibility policy shall follow the canonical contract in `docs/01-requirements/template-schema.md` and migration rules in `docs/04-data/migrations.md`.
- Base disk registry shall store:
  - file path reference (or stable id)
  - OS classification metadata (TBD: how to classify)
  - display name/label
  
### 4.1 Local Configuration & Storage

The system shall manage LabAssistantV2 operational data using
configurable default storage locations, including at minimum:

- Virtual machine storage directory  
- Differencing disk storage directory  
- Imported base disk registry location  
- Template storage directory  
- Logs and diagnostic output directory  
- Local configuration and metadata files (JSON-based)

The system shall:

- Allow users to **view and modify** these default paths.
- Validate configured paths for:
  - Existence
  - Required permissions
  - Basic storage feasibility (v1 policy: low/unknown free space warns only and does not block deployment by itself).
- Use configured paths **consistently across all operations**.

Detailed schema contract:

- See `docs/01-requirements/template-schema.md` for canonical template fields, versioning behavior, and compatibility rules.
  
---

## 5. Error Handling Requirements

### Error categories
- **Validation errors** (invalid template, missing fields)  
- **Environment errors** (Hyper-V disabled, missing base disk, missing switch)  
- **Permission errors** (insufficient privileges)  
- **Runtime errors** (deployment failure, disk creation failure, VM creation failure, switch attach failure)

### Behavior
- Users shall receive **clear, actionable error messages**.  
- Detailed technical information shall be written to **logs**.  
- Preflight/readiness UI shall prioritize summarized actionable messages and likely cause/path hints before deployment starts.  
- Runtime failure summaries should include likely artifact/path hints when the operation inputs are known (for example parent base VHDX, target differencing VHD path, VM path).  
- Raw PowerShell stderr may remain primarily in diagnostics/debug logs rather than the primary readiness UI.  
- Failed deployments shall not leave **untracked or orphaned resources** where possible.

Detailed runtime policy:

- See `docs/01-requirements/cleanup-cancellation-policy.md` for cleanup order, cancellation boundaries, residual status rules, and expected terminal outcomes.
- Real-host regression observations for the persistent PowerShell wrapper and VHDX validation path are captured in `docs/07-testing/milestone-u-hyperv-verification-checklist.md` (post-`#219` stabilization).

---

## 6. Non-Functional Requirements (summary)

See:  
`/docs/01-requirements/non-functional-requirements.md`

Key areas include:

- Performance of lab deployment  
- Reliability and recovery  
- Security and safe file handling  
- Usability for support engineers  
- Observability and diagnostics  
- Compatibility with supported Windows environments  

Detailed logging contract:

- See `docs/01-requirements/logging-contract.md` for required event families, required fields, severity rules, redaction rules, and diagnostics export expectations.

---

## 7. Acceptance Criteria Mapping

Each functional requirement shall map to:

- A **user story**  
- A **feature acceptance criteria section**  
- One or more **test cases**  

Traceability must be maintained across:

**FR -> AC -> Test Case**

---

## Open Questions / TBDs

- Field-level template schema compatibility rules for future minor/patch evolution (see `docs/01-requirements/template-schema.md`)  
- Whether **post-deployment automation scripts** will be supported  
- Strategy for **resume/retry UX** after failed deployment beyond current cleanup-and-report policy  
- Long-term extensibility toward **multiple hypervisors**  
- Telemetry or usage metrics collection approach  
