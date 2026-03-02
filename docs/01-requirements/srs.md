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

- **FR-019:** The system shall persist only **implemented** guest-step execution settings/configuration in templates until the corresponding runtime behavior is supported.
  - **Acceptance details:** Placeholder-only UI affordances (visible but not implemented guest steps) must not force template schema churn or persisted placeholder payloads by themselves.
  - **Priority:** P1

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

- **FR-055:** The system shall classify deployment guest-step execution options into:
  - mandatory implemented steps,
  - optional implemented steps,
  - and visible placeholders (not implemented yet).
  - **Acceptance details:** Placeholder visibility must not imply runtime execution support.
  - **Priority:** P1

- **FR-056:** The system shall run an optional implemented guest step only when the user has enabled it **and** the required step configuration is present/valid.
  - **Acceptance details:** If an optional implemented step is enabled but required configuration is missing, deployment shall be blocked with actionable configuration guidance before runtime execution.
  - **Priority:** P1

- **FR-057:** The system shall keep placeholder guest steps visible in the Deploy UI with clear not-implemented labeling, and shall not execute them.
  - **Acceptance details:** Placeholder steps may appear in mandatory or optional UI sections, but current runtime behavior must allow deployment to proceed with placeholders skipped.
  - **Priority:** P1

- **FR-058:** The system shall record skipped guest-step outcomes in per-VM deployment summaries and structured logs, including a machine-readable skip reason (for example `not_implemented`).
  - **Acceptance details:** Per-VM visibility and structured logging are required; global skipped-step counts are optional unless introduced explicitly.
  - **Priority:** P1

- **FR-059:** The system shall keep Hyper-V network attachment settings and guest OS network configuration settings in the same network configuration area in the UI, while preserving current v1 deployment behavior for Hyper-V switch attachment.
  - **Acceptance details:** Milestone W must not silently change current deploy/runtime requirements for Hyper-V switch/NIC attachment; guest OS IP/DNS/gateway settings may remain optional/not implemented placeholders until supported.
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

### 3.6 Machines (Hyper-V VM Administration, v1)

- **FR-060:** The system shall provide a primary `Machines` capability area for host VM administration workflows.
  - **Acceptance details:** `Machines` is a first-class capability distinct from `Deploy`; `Deploy` remains provisioning-focused.
  - **Priority:** P1

- **FR-061:** The system shall list and manage all Hyper-V VMs on the current host, including VMs not created by LabAssistant.
  - **Acceptance details:** VM origin/status labeling strategy may be limited in v1 but must not hide external VMs.
  - **Priority:** P1

- **FR-062:** The system shall support v1 basic VM operations from `Machines`, including at minimum:
  - start VM
  - stop VM
  - delete VM (with safety prompts/options)
  - **Priority:** P1

- **FR-063:** The system shall support v1 basic VM edit operations from `Machines`, including at minimum:
  - memory
  - CPU
  - switch attachment
  - **Acceptance details:** Edit workflow is draft-then-apply with unsaved-change visibility; unapplied drafts are discarded on navigation away.
  - **Priority:** P1

- **FR-064:** The system shall expose separate connection actions from `Machines`:
  - **Open Hyper-V Console**
  - **Open RDP**
  - **Acceptance details:** `Open RDP` shall be disabled (grayed out) when RDP readiness is unknown/unmet.
  - **RDP readiness v1 criteria:**
    - VM is running
    - VM has at least one IPv4 address observable from host-side Hyper-V data
    - Host can reach guest TCP port 3389 within a short probe timeout (4000ms target)
  - **Execution details:** readiness checks must run asynchronously/background and must not block Machines UI interactions.
  - **Stability details:** transient probe timeout/cancellation/unreachable outcomes during background/manual checks are treated as non-fatal readiness results (for example `NotReady`/`Unknown`) and must not crash the app or produce repeated user-facing exception noise.
  - **Priority:** P1

- **FR-065:** The system shall support VM deletion options:
  - delete VM registration only
  - delete VM and associated disk/files
  - **Acceptance details:** v1 shall support persisted Machines deletion policy modes:
    - Ask every time (default)
    - Always delete disks
    - Always delete disks for LabAssistant-provisioned VMs
    - Always delete disks for differencing disks only
  - **Safety details:** policy-driven defaulting to delete-with-storage must be blocked when disk classification is known base/full or potential base/uncertain.
  - **Cleanup details:** delete-with-storage must attempt safe/owned VM folder cleanup and surface explicit actionable failure context when cleanup is partial.
  - **Priority:** P1

- **FR-066:** All user-initiated `Machines` operations shall emit structured logs with operationId and action context (vmId/vmName/action/result/error details).
  - **Acceptance details:** Destructive operations (delete with or without disks) must log explicit action intent and result.
  - **Priority:** P1

- **FR-067:** The product shall support a parallel UI execution model during WinUI migration:
  - `LabAssistant` (WPF) remains available as production baseline
  - `LabAssistant.WinUI` is introduced as a separate application project
  - **Acceptance details:** both UI projects build in solution and are independently launchable.
  - **Priority:** P1

- **FR-068:** WinUI shell navigation shall use icon-rail + hamburger drawer interaction:
  - icon rail is always visible for top-level capabilities
  - hamburger opens a slide-out capability drawer with scrim
  - drawer dismisses on outside click or `Esc`
  - **Acceptance details:** full-menu navigation must not depend on hover-only behavior.
  - **Priority:** P1

- **FR-069:** WinUI shall default to `Machines` on startup and shall not persist last selected capability across restarts.
  - **Acceptance details:** app startup route is deterministic (`Machines`) unless explicitly changed by future approved requirements.
  - **Priority:** P1

- **FR-070:** WinUI shell shall include a right-side insights panel that is collapsed by default and opened via a warning/issue trigger.
  - **Acceptance details:** shell shows issue indicator/badge when issues exist; panel presence does not block normal workspace interaction when collapsed.
  - **Priority:** P1

- **FR-071:** Machines editor UX in WinUI shall use section-based details navigation (for example, Hardware grouping CPU + Memory) instead of legacy collapsible stacks.
  - **Acceptance details:** breadcrumb trail reflects details-pane context (`Machines > VM > Section`) and supports returning to parent context.
  - **Priority:** P1

- **FR-072:** WinUI foundation shall centralize semantic theme tokens with light/dark dictionaries and runtime switching support.
  - **Acceptance details:** page-level hardcoded foreground/background colors are disallowed for core shell surfaces.
  - **Priority:** P1

- **FR-073:** WinUI Diagnostics shall provide a read-only structured log viewer (Phase 1) using stable envelope fields with dynamic context inspection.
  - **Envelope fields:** `ts`, `level`, `event`, `operationId`, `result`
  - **Viewer behavior (Phase 1):**
    - render envelope columns
    - show selected entry dynamic context as raw/pretty JSON text
    - support filtering by operationId, level, event, free-text, and basic time range
    - read canonical `structured-events.jsonl` source
    - tolerate malformed JSONL lines by skipping and reporting parse error count
    - provide `Open raw JSONL` action for power users
  - **Out of scope (Phase 1):**
    - log editing/deletion
    - remote ingestion/upload
    - advanced visualizations/timelines
  - **Priority:** P1

- **FR-074:** WinUI shell and migrated capability surfaces shall follow a documented layout constraints contract that defines bounded region sizing, explicit scroll ownership, overflow handling rules, and resize behavior expectations for compact/normal/wide widths.
  - **Priority:** P1

- **FR-075:** WinUI shell shall use a global `NavigationView` (`LeftCompact`) for top-level capability navigation with hierarchical child actions/subviews per capability.
  - **Acceptance details:** Expanded mode shows entity labels and child actions; compact mode remains icon-first and must provide a non-hover path to child actions.
  - **Priority:** P1

- **FR-076:** WinUI shell navigation shall use canonical route keys in `capability.subview` format, with deterministic startup at `machines.overview`.
  - **Acceptance details:** Selecting a parent entity routes to its default child route; `Settings` is placed as footer navigation.
  - **Priority:** P1

- **FR-077:** WinUI `Templates` capability shall use canonical child routes with deterministic parent default routing:
  - `templates.library` (default child)
  - `templates.editor`
  - `templates.details` is deferred unless explicitly approved in a future milestone contract.
  - **Acceptance details:** Selecting parent `Templates` routes to `templates.library` and remains consistent with global navigation rules from FR-075/FR-076.
  - **Priority:** P1

- **FR-078:** WinUI `Templates` capability shall provide a unified workflow surface that keeps template library and template editing in one coherent capability context.
  - **Acceptance details:** Users can list/search/select templates, open selected template into editor, and perform create/edit/save flows without leaving `Templates` capability context.
  - **Priority:** P1

- **FR-079:** WinUI `Templates` capability shall keep import/export entry points discoverable within `Templates` capability context and shall not require file-hunt-first workflow as the primary edit path.
  - **Acceptance details:** Import/export remain available from `Templates` capability surfaces while existing template schema validation and compatibility semantics remain unchanged.
  - **Priority:** P1

- **FR-080:** WinUI `Templates` editor shall display and manage the template VM entry list (`vmTemplates`) inside editor context.
  - **Acceptance details:** Users can view VM entries and select a VM entry for field editing without leaving `Templates` capability context.
  - **Priority:** P1

- **FR-081:** WinUI `Templates` editor shall support v1 VM-entry edit operations using existing schema/model fields only:
  - add VM entry
  - remove VM entry (with confirmation)
  - edit supported VM configuration fields already defined in template schema/contracts
  - **Acceptance details:** This requirement does not introduce new schema fields or new domain behavior.
  - **Priority:** P1

- **FR-082:** WinUI `Templates` VM-entry edits shall follow save/reload round-trip behavior through existing template persistence and validation semantics.
  - **Acceptance details:** Saved VM-entry edits persist and reload accurately from storage; invalid edits are blocked with actionable validation feedback.
  - **Priority:** P1

- **FR-083:** WinUI `Templates` library/editor continuity shall be preserved after VM-entry edits.
  - **Acceptance details:** After save/delete/add/edit operations, editor and library state remain coherent (selected template/context, counts, and reload behavior) without cross-capability navigation.
  - **Priority:** P1

- **FR-084:** WinUI `Templates` VM editor shall replace free-text switch entry with host-backed selector UX supporting optional multi-switch assignment.
  - **Acceptance details:** Switch selection is optional at VM level; if one or more switch rows are present, each row must resolve to a valid host switch value and duplicate switch values are disallowed. Selector UX shall support add/remove row interactions.
  - **Schema note:** `switchNames` is the canonical multi-switch list with compatibility fallback to legacy `switchName`.
  - **Priority:** P1

- **FR-085:** WinUI `Templates` VM editor shall use catalog-first VHDX selection while preserving backward compatibility for existing path-based templates.
  - **Acceptance details:** UI presents catalog selection as primary workflow; existing templates containing only path-based references remain loadable/editable and use documented fallback/ambiguity messaging when referenced catalog entries are missing or unavailable.
  - **Priority:** P1

- **FR-086:** WinUI `Templates` shall apply deterministic VHDX identity normalization and conflict messaging using existing fields.
  - **Acceptance details:** Effective identity precedence is `vhdxId` then `vhdxSignature` then `vhdPath`; unresolved conflicts/ambiguities block save until user selects a resolving catalog entry, and UI exposes actionable warning text.
  - **Priority:** P1

- **FR-087:** WinUI Deploy migration Milestone AF shall implement `from-template` workflow as the first Deploy slice and explicitly defer `on-the-fly` migration.
  - **Acceptance details:** WinUI Deploy parent/child routing remains canonical with deterministic route key `deploy.from_template` for AF scope.
  - **Priority:** P1

- **FR-088:** WinUI Deploy `from-template` shall consume Templates AE compatibility semantics for disk and switch references.
  - **Acceptance details:** Deploy compatibility handling prefers `switchNames` with fallback to legacy `switchName`; disk identity uses AE normalization semantics and required unresolved/ambiguous disk identity is blocking.
  - **Priority:** P1

- **FR-089:** WinUI Deploy readiness for `from-template` shall provide explicit correction affordances when compatibility checks fail.
  - **Acceptance details:** Blocking compatibility errors provide actionable correction paths, including auto-resolve suggestions and explicit `Open in Templates Editor` action.
  - **Priority:** P1

- **FR-090:** WinUI Deploy `from-template` results UX shall use compact-first visibility with progressive disclosure.
  - **Acceptance details:** sticky status/progress is always visible; per-VM rows are concise by default with expandable details; global warnings/errors are collapsed by default while remaining discoverable.
  - **Priority:** P1

Detailed capability contract:
- See `docs/01-requirements/machines-capability-contract.md` for v1 scope boundaries, safety constraints, and explicit TBDs.
- See `docs/02-ux/winui-shell-contract-aa.md` for Milestone AA shell-specific contract details.
- See `docs/02-ux/winui-global-navigationview-contract-ac.md` for Milestone AC global NavigationView behavior and routing contract.
- See `docs/02-ux/winui-templates-capability-contract-ad.md` for Milestone AD `Templates` routing and unified workflow contract.
- See `docs/02-ux/winui-deploy-from-template-contract-af.md` for Milestone AF `Deploy from-template` routing, readiness, and results visibility contract.

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
- RDP readiness detection policy beyond v1 host-observable checks (for example guest policy/NLA/firewall introspection).
