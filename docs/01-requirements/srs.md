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

- **FR-091:** WinUI Deploy migration Milestone AG shall implement `on-the-fly` workflow convergence while leaving WPF Deploy untouched.
  - **Acceptance details:** WinUI Deploy route model includes deterministic `deploy.on_the_fly` subview behavior for AG scope and preserves AF `deploy.from_template` behavior without contract drift.
  - **Priority:** P1

- **FR-092:** WinUI Deploy `on-the-fly` readiness shall classify input validation and environment checks as blocking or warning before start.
  - **Acceptance details:** Required unresolved inputs (for example missing required disk identity, invalid VM entry state, or invalid required switch selection) are blocking; non-critical mapping issues are warning-only with actionable guidance.
  - **Priority:** P1

- **FR-093:** WinUI Deploy `on-the-fly` shall provide correction affordances for blocking readiness issues and gate execution until blocking issues are resolved.
  - **Acceptance details:** Readiness output provides explicit correction actions and preserves existing deployment orchestration semantics once unblocked.
  - **Priority:** P1

- **FR-094:** WinUI Deploy `on-the-fly` results UX shall follow compact-first visibility parity with AF results patterns.
  - **Acceptance details:** sticky summary/progress remains visible; per-VM rows are concise by default with expandable details; global warnings/errors remain collapsed by default and discoverable.
  - **Priority:** P1

- **FR-095:** WinUI Deploy timelines (from-template and quick deploy) shall use a canonical step-state contract driven by explicit step-state data.
  - **Acceptance details:** canonical states are `Pending`, `Running`, `Succeeded`, `Failed`, and `Skipped`; timeline rows bind to state data instead of deriving completion from inferred text wherever state data is available.
  - **Priority:** P1

- **FR-096:** WinUI Deploy timeline rendering shall show one label per step with icon-state progression and deterministic transition behavior.
  - **Acceptance details:** step labels must not be duplicated as separate start/finish rows; running uses spinner state, terminal states use icon changes, skipped/non-applicable steps are hidden, and optional parent rows are hidden when no child step executes.
  - **Priority:** P1

- **FR-097:** WinUI shell right panel shall follow explicit ownership and lifecycle rules, with Deploy as the initial owning capability for timeline/results/issue context.
  - **Acceptance details:** active capability decides panel content owner; switching capabilities resets panel content state to the new owner contract; panel defaults collapsed unless owner marks active run context; compact-width fallback collapses panel; right panel owns internal vertical scroll.
  - **Priority:** P1

- **FR-098:** Deploy orchestration shall emit explicit per-VM step-state updates for UI timeline consumption in both from-template and quick deploy flows.
  - **Acceptance details:** each update includes `operationId`, `vmId`, `vmName`, `stepKey`, `stepLabel`, `state`, `timestampUtc`, and deterministic per-VM `sequence`; optional concise message is allowed.
  - **Priority:** P1

- **FR-099:** WinUI Deploy timeline projection shall consume explicit step-state updates as source-of-truth rather than relying on status-text inference.
  - **Acceptance details:** one step label transitions through state changes over time; per-VM ordering remains deterministic; failed steps are terminal; skipped representation follows AH timeline display policy.
  - **Priority:** P1

- **FR-100:** WinUI `Assets` shall expose a canonical `assets.base_disks` subview for Base Disk management within the existing shell route model.
  - **Acceptance details:** `assets.base_disks` is the AJ canonical Base Disks route; local `Assets` subview navigation may use tabs/segments bound to canonical route keys; AJ does not redefine shell-wide top-level `Assets` click behavior beyond requiring `assets.base_disks` to exist as the default Base Disks child route when Assets resolves to a child subview.
  - **Priority:** P1

- **FR-101:** WinUI `Assets` Base Disks shall provide an in-context management surface for registered base disks that preserves current base-disk domain behavior.
  - **Acceptance details:** the surface shall support list, refresh, import/register, metadata edit, validation/readiness visibility, and remove actions without requiring a separate edit route; metadata editing remains in selected-item details context and does not invent new schema or disk-domain semantics.
  - **Priority:** P1

- **FR-102:** WinUI `Assets` Base Disks shall classify registration and catalog validation outcomes as blocking or warning with actionable user guidance.
  - **Acceptance details:** blocking states include invalid path/file type, inaccessible or locked disk, failed required metadata extraction, and other conditions that prevent a catalog entry from being safely registered or validated; warning states may indicate non-blocking readiness concerns while keeping the item visible and actionable; empty/loading/error states must be explicit and non-silent.
  - **Priority:** P1

- **FR-103:** WinUI `Assets` Base Disks removal shall enforce explicit safety guardrails and operation-scoped diagnostics.
  - **Acceptance details:** AJ scope covers registry removal, not underlying file deletion; remove requires explicit confirmation, must surface whether the disk appears in use or referenced, blocks or warns per approved safety taxonomy, and emits structured logs with `operationId`, `baseDiskId`, action context, result, and error details; failed removal must not leave partial registry state.
  - **Priority:** P1

- **FR-104:** WinUI `Assets` shall expose a canonical `assets.switches` subview for virtual switch management within the existing shell route model.
  - **Acceptance details:** `assets.switches` is the AK canonical Switches route; local `Assets` subview navigation may use tabs/segments bound to canonical route keys; AK does not redefine shell-wide top-level `Assets` click behavior beyond requiring `assets.switches` to exist as an explicit child route.
  - **Priority:** P1

- **FR-105:** WinUI `Assets` Switches shall provide an in-context management surface for existing switch CRUD behavior.
  - **Acceptance details:** the surface shall support list, refresh, create, edit, validation/readiness visibility, and delete actions without requiring a separate editor route; create/edit remains in selected-item details context and does not invent new switch-domain semantics.
  - **Priority:** P1

- **FR-106:** WinUI `Assets` Switches shall classify switch validation and host-readiness outcomes as blocking or warning with actionable user guidance.
  - **Acceptance details:** blocking states include duplicate-name conflicts, invalid or unsupported switch configuration, unavailable required host state, and delete attempts that violate approved AK safety rules; warning states may indicate non-blocking host or inventory conditions while keeping the switch visible and actionable; empty/loading/error states must be explicit and non-silent.
  - **Priority:** P1

- **FR-107:** WinUI `Assets` Switches deletion shall require explicit confirmation, block deletion when any Hyper-V VM is attached to the switch regardless of VM power state, and emit structured logs with `operationId`.
  - **Acceptance details:** delete remains a switch-management action only and does not broaden into topology redesign; if any VM is attached, deletion is blocked with concrete feedback; if deletion proceeds, logs include action, `switchName`, `switchType`, result, and error details.
  - **Priority:** P1

- **FR-108:** WinUI shell capability navigation shall distinguish capability-level routing from capability-local navigation and shall use deterministic parent-click behavior across expanded, collapsed, and compact modes.
  - **Acceptance details:** capabilities with approved `Overview` surfaces shall route parent-click to their `Overview`; capabilities without approved `Overview` surfaces shall route parent-click to their default operational child or workspace; collapsed compact navigation shall not depend on hover/pop-up child choosers; compact shell mode may replace the persistent left rail with a hamburger-invoked navigation drawer.
  - **Priority:** P1

- **FR-109:** WinUI shell and migrated capability surfaces shall use explicit header-ownership rules so capability title/description context is owned by the shell and child views do not repeat page-level title bands by default.
  - **Acceptance details:** shell header presents capability-level title and optional capability-level description; child views use local section labels only unless a later contract explicitly justifies a child-owned page header; overview/tab selection or local workflow state shall provide child-view orientation rather than duplicating shell context.
  - **Priority:** P1

- **FR-110:** WinUI capability-local navigation shall support approved overview/index surfaces only where they add routing or status value, and shall preserve explicit workflow-state exceptions where a peer tab model would be misleading.
  - **Acceptance details:** `Assets`, `Deploy`, and `Diagnostics` use approved `Overview`-first local navigation; `Machines` remains single-surface for current scope; `Templates` keeps `Library` as the primary capability surface while `Editor` remains a workflow-state entered from explicit actions rather than a permanently exposed peer destination.
  - **Priority:** P1

- **FR-111:** WinUI shell right panel shall remain shell-owned infrastructure, but capability views shall own panel meaning, trigger placement, and issue/progress scoping according to the active workflow contract.
  - **Acceptance details:** workflow-local panel toggles and issue counts may live inside child views; `Deploy` right panel is progress/results-first, while pre-run validation issues move inline in the main workspace; right-panel content remains secondary context and must not be the primary editor surface.
  - **Priority:** P1

- **FR-112:** WinUI migrated capability surfaces shall follow shared action-placement, iconography, and compact-layout rules derived from the cross-view audit.
  - **Acceptance details:** actions live nearest to the state they affect; `New` defaults to an inventory-level action that clears the current details/editor into draft state; icon-first command chrome with tooltips is preferred, including trash-can delete affordances and icon-based save/apply where clarity remains sufficient; compact layouts prioritize the primary workflow region and use bounded scroll owners rather than unbounded page growth.
  - **Priority:** P1

- **FR-113:** WinUI `MainWindow` shall act as the shell composition root only and shall limit its direct ownership to shell chrome, route resolution, shell navigation behavior, shell header state, and shell right-panel host lifecycle.
  - **Acceptance details:** `MainWindow` may continue to own shell-level state such as active route, active capability, shell theme state, compact drawer behavior, and right-panel container lifecycle, but it shall not remain the long-term owner of capability-local inventory state, selection state, edit drafts, workflow-specific readiness state, or capability-specific action orchestration once AM extraction slices land.
  - **Priority:** P1

- **FR-114:** WinUI migrated capabilities shall expose explicit workspace seams so capability-local state and workflow orchestration can move out of `MainWindow` without changing approved shell contracts or user-facing capability behavior.
  - **Acceptance details:** each migrated capability may introduce a capability-scoped workspace owner (for example controller, presenter, or viewmodel) as long as shell-level behavior remains preserved; shell-to-capability integration must stay narrow and capability-specific state must no longer require broad direct control mutation from `MainWindow`.
  - **Priority:** P1

- **FR-115:** WinUI shell composition refactors shall preserve existing capability contracts and route behavior while reducing shell-level coupling to child view controls.
  - **Acceptance details:** extraction work must preserve AL header/navigation/right-panel behavior, Templates Library-first exception behavior, and existing capability route keys; shell refactors must not silently redesign deploy, assets, templates, diagnostics, or machines semantics while introducing composition seams.
  - **Priority:** P1

- **FR-116:** WinUI migrated views shall use bindings and commands as the default interaction model, while limiting code-behind to a small explicit surface for genuinely view-local interactions.
  - **Acceptance details:** bindings and commands are the default pattern for state presentation and routine actions; view-local events remain acceptable only when they are narrow, explicit, and do not centralize capability orchestration in the shell; extraction work must not require shell-level direct mutation of routine child control state.
  - **Priority:** P1

- **FR-117:** WinUI migrated views shall not act as broad typed control bags for shell-level orchestration.
  - **Acceptance details:** views may expose a narrow interaction seam or limited stateful surface needed by the current extraction step, but they shall not continue to expose dozens of raw controls for routine capability updates; child-control exposure must reduce over time as capability-local workspace seams are introduced.
  - **Priority:** P1

- **FR-118:** WinUI view interaction refactors shall preserve current capability behavior while enabling capability-local state owners to update UI through bindings, commands, and narrow interaction seams instead of broad `MainWindow` control proxies.
  - **Acceptance details:** the target interaction contract must remain pragmatic rather than framework-dogmatic; AM refactors may use viewmodels, controllers, presenters, or mixed patterns as long as they reduce shell-to-view coupling and preserve existing capability semantics.
  - **Priority:** P1

- **FR-119:** WinUI UI tests shall preserve approved shell and capability contracts while reducing brittle dependence on exact source-shape or literal-string implementation details when extraction work changes internal structure without changing behavior.
  - **Acceptance details:** milestone tests may continue to protect contract anchors that must remain explicit, but extraction work should move tests toward state, seam, route, and behavior-oriented assertions where practical; test refactors must happen in the same issue as the corresponding runtime extraction when existing tests become too coupled to superseded structure.
  - **Priority:** P1

- **FR-120:** WinUI extraction work shall treat UI test updates as part of the implementation contract rather than as deferred cleanup after runtime refactors land.
  - **Acceptance details:** when AM extraction work changes ownership boundaries, interaction seams, or capability-local state structure, directly impacted tests must be updated in the same slice; milestone evidence must continue to prove shell/capability contract preservation even if implementation shape changes.
  - **Priority:** P1

- **FR-121:** WinUI UI test strategy shall distinguish between tests that protect stable product contracts and tests that only protect temporary migration scaffolding or interim source shape.
  - **Acceptance details:** stable contract tests should remain explicit and intentional; temporary scaffolding assertions should be reduced when they block legitimate boundary cleanup; the test suite should preserve migration safety without freezing the codebase into one shell-centric implementation shape.
  - **Priority:** P1

- **FR-122:** WinUI `Machines` shall expose an explicit workspace extraction seam so inventory state, selected-VM state, edit-draft state, RDP readiness state, and action enablement/orchestration can move out of `MainWindow` without changing approved Machines behavior.
  - **Acceptance details:** the Machines workspace seam must preserve the current single-surface master/detail model, `machines.overview` route continuity, draft-based apply workflow, separate Console and RDP actions, delete safety behavior, and current shell-owned capability framing from AL.
  - **Priority:** P1

- **FR-123:** WinUI `Machines` extraction shall preserve asynchronous and non-blocking readiness and inventory behavior while reducing shell-owned direct mutation of Machines controls.
  - **Acceptance details:** RDP readiness refresh, inventory refresh, and selected-VM state updates must remain non-blocking and must not require `MainWindow` to remain the persistent owner of Machines-specific collections, drafts, or readiness flags; shell may still host capability views and dialogs without reclaiming Machines workflow ownership.
  - **Priority:** P1

- **FR-124:** WinUI `Machines` extraction shall preserve current user-visible interaction contracts while introducing a narrower Machines workspace owner boundary.
  - **Acceptance details:** no silent redesign of Machines layout model, action grouping, delete policy behavior, RDP disabled-state behavior, or edit/apply semantics is allowed under AM4; the seam definition must be narrow enough to support AM5 through AM8 without guesswork.
  - **Priority:** P1

- **FR-125:** WinUI capability extraction shall converge on capability-local workspace composition so `MainWindow` remains the shell composition root while capability-specific view, state, and workflow composition no longer terminate in shell-owned host interfaces as a long-term architecture.
  - **Acceptance details:** `MainWindow` may still instantiate and host capability workspaces, but capability-local UI coordination shall move behind capability workspace composition objects rather than scaling `MainWindow` into a permanent multi-capability host-interface implementation hub.
  - **Priority:** P1

- **FR-126:** WinUI views shall not depend on or receive `MainWindow` directly in order to access global or shell-owned behavior.
  - **Acceptance details:** if cross-capability or shell-owned behavior is needed, it shall be exposed through a narrow shell/workspace abstraction or service seam rather than direct `MainWindow` injection into views; capability-local behavior should remain local to that capability workspace unless explicitly documented otherwise.
  - **Priority:** P1

- **FR-127:** WinUI capability workspaces shall be treated as long-lived while the app session is open, with route activation triggering refresh/reconciliation rather than full workspace recreation by default.
  - **Acceptance details:** the current AM target preserves long-lived capability workspaces and explicit route-activation refresh rules; recreating capability workspaces on every navigation is out of scope unless later re-contracted, and any future lifetime change remains a `TBD`.
  - **Priority:** P1

- **FR-128:** WinUI `Machines` shall be reworked toward a capability-local workspace composition object so the shell no longer remains the practical final destination for Machines-specific UI composition after the initial extraction slices.
  - **Acceptance details:** Machines follow-up cleanup must preserve the existing `MachinesWorkspaceViewModel` and `MachinesWorkspaceController` seams while introducing a capability-local composition owner that becomes the long-term home for Machines-specific view/controller/state coordination.
  - **Priority:** P1

- **FR-129:** WinUI `Machines` host-bridge responsibilities currently implemented by `MainWindow` shall be treated as transitional only and reduced behind the capability-local workspace composition boundary.
  - **Acceptance details:** the post-AM33 Machines cleanup target must explicitly reduce `MainWindow` responsibility for Machines-specific view refresh, selection syncing, edit-control coordination, and controller-host bridging without changing approved Machines behavior.
  - **Priority:** P1

- **FR-130:** WinUI `Machines` follow-up cleanup shall preserve the current long-lived workspace/session model while moving Machines-specific route-activation refresh and UI coordination behind the Machines-local composition boundary.
  - **Acceptance details:** the refined Machines target must keep `machines.overview` long-lived and route-activated without reverting to per-navigation recreation, and must preserve current compact/layout/action semantics while reducing shell-local Machines coordination.
  - **Priority:** P1

- **FR-131:** WinUI `Assets` shall expose an explicit workspace extraction seam so `Assets Overview`, `Base Disks`, and `Switches` state and orchestration can move out of `MainWindow` without changing approved Assets behavior.
  - **Acceptance details:** the Assets extraction seam must preserve `Assets` as an Overview-first capability with canonical child routes `assets.overview`, `assets.base_disks`, and `assets.switches`, while keeping Base Disks and Switches semantics from AJ/AK intact.
  - **Priority:** P1

- **FR-132:** WinUI `Assets` extraction shall target capability-local workspace composition from the start, so `MainWindow` does not become the long-term composition hub for Assets local navigation, overview summaries, Base Disks, and Switches coordination.
  - **Acceptance details:** Assets follow-up extraction must treat shell-owned host bridges as temporary only, must keep `MainWindow` limited to shell composition and workspace lifetime, and must use the AM33 composition model rather than repeating the pre-refinement Machines pattern.
  - **Priority:** P1

- **FR-133:** WinUI `Assets` extraction shall preserve the current long-lived workspace/session model with route-activation refresh and must not silently redesign Overview-first navigation, Base Disks behavior, or Switches behavior during seam definition.
  - **Acceptance details:** Assets cleanup must keep long-lived workspace lifetime, route-bound local navigation, Base Disks validation/remove guardrails, Switches delete guardrails, and the approved Assets Overview-first behavior while reducing shell-local ownership over time.
  - **Priority:** P1

- **FR-134:** WinUI `Assets` shared composition cleanup shall converge shared `Assets Overview`, `Base Disks`, and `Switches` composition into an Assets-local composition owner instead of leaving shared capability composition responsibilities in `MainWindow`.
  - **Acceptance details:** `MainWindow` remains the shell composition root and keeps only shell route switching, shell title/description, shell compact or drawer behavior, shell host visibility, right-panel infrastructure, and app-level workspace lifetime; the Assets-local composition owner becomes the long-term home for shared Assets-local composition and interaction boundaries.
  - **Priority:** P1

- **FR-135:** WinUI `Assets` shared composition cleanup shall treat capability-specific host interfaces implemented by `MainWindow` as temporary migration bridges only, and views shall not depend on or receive `MainWindow` directly.
  - **Acceptance details:** shared Assets route activation handling, shared workspace lifetime participation, and shared local interaction boundaries must converge behind the Assets-local composition owner or narrow abstractions rather than direct `MainWindow` injection or permanent shell-host interface accumulation.
  - **Priority:** P1

- **FR-136:** WinUI `Assets` shared composition cleanup shall preserve the long-lived Assets workspace/session model so navigation activates and reconciles shared Assets state rather than recreating the Assets workspace on every route change.
  - **Acceptance details:** cleanup-target definition must preserve `assets.overview`, `assets.base_disks`, and `assets.switches` route continuity and must remain explicit that Base Disks-specific extraction details, Switches-specific extraction details, Overview-specific extraction details, runtime implementation, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-137:** WinUI `Assets Overview` cleanup shall remain under shared `AssetsWorkspaceComposition` rather than becoming a shell-owned surface, while converging Overview-specific state and UI coordination behind an Overview-local seam.
  - **Acceptance details:** the Overview cleanup target must explicitly keep shared Assets composition responsible only for capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Overview-local seam the target owner for Overview summary/navigation state and Overview-local interaction coordination.
  - **Priority:** P1

- **FR-138:** WinUI `Assets Overview` cleanup shall preserve current Overview behavior and boundaries while making Overview participation in the long-lived Assets workspace explicit.
  - **Acceptance details:** the Overview cleanup target must preserve `assets.overview` as the route-entry and index surface for `Assets`, must keep the Overview summary/navigation role intact, must avoid taking semantic ownership of Base Disks or Switches behavior, and must keep route activation refresh within the long-lived Assets workspace rather than per-navigation recreation.
  - **Priority:** P1

- **FR-139:** WinUI `Assets Overview` cleanup shall reject direct `MainWindow` view dependency, Overview-owned shared god-object growth, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Assets composition must not be widened into an Overview-centric shared owner; and the cleanup target must stay explicit that Base Disks extraction details, Switches extraction details, runtime implementation, overview redesign beyond ownership cleanup, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-140:** WinUI `Assets Base Disks` cleanup shall remain under shared `AssetsWorkspaceComposition` rather than becoming a shell-owned surface, while converging Base Disks-specific state, orchestration, and UI coordination behind a Base Disks-local seam.
  - **Acceptance details:** the Base Disks cleanup target must explicitly keep shared Assets composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Base Disks-local seam the target owner for Base Disks-specific state, orchestration, composition, and interaction coordination.
  - **Priority:** P1

- **FR-141:** WinUI `Assets Base Disks` cleanup shall preserve current `assets.base_disks` behavior and boundaries while making Base Disks participation in the long-lived Assets workspace explicit.
  - **Acceptance details:** the Base Disks cleanup target must preserve `assets.base_disks` as the operational Base Disks management surface, must keep Base Disks outside Overview and Switches semantic ownership, must keep route activation refresh within the long-lived Assets workspace rather than per-navigation recreation, and must preserve AJ and AL Base Disks behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-142:** WinUI `Assets Base Disks` cleanup shall reject direct `MainWindow` view dependency, shared Assets composition widening into the Base Disks workflow owner, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Assets composition must not become the Base Disks workflow owner; Base Disks-specific host bridges or control exposure remain temporary migration cleanup targets behind the Base Disks-local seam; and the cleanup target must stay explicit that Switches extraction details, Overview extraction details, runtime implementation, Base Disks behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-143:** WinUI `Assets Switches` cleanup shall remain under shared `AssetsWorkspaceComposition` rather than becoming a shell-owned surface, while converging Switches-specific state, orchestration, and UI coordination behind a Switches-local seam.
  - **Acceptance details:** the Switches cleanup target must explicitly keep shared Assets composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Switches-local seam the target owner for Switches-specific state, orchestration, composition, and interaction coordination.
  - **Priority:** P1

- **FR-144:** WinUI `Assets Switches` cleanup shall preserve current `assets.switches` behavior and boundaries while making Switches participation in the long-lived Assets workspace explicit.
  - **Acceptance details:** the Switches cleanup target must preserve `assets.switches` as the operational Switches management surface, must keep Switches outside Overview and Base Disks semantic ownership, must keep route activation refresh within the long-lived Assets workspace rather than per-navigation recreation, and must preserve AK and AL Switches behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-145:** WinUI `Assets Switches` cleanup shall reject direct `MainWindow` view dependency, shared Assets composition widening into the Switches workflow owner, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Assets composition must not become the Switches workflow owner; Switches-specific host bridges or control exposure remain temporary migration cleanup targets behind the Switches-local seam; and the cleanup target must stay explicit that Base Disks extraction details, Overview extraction details, runtime implementation, Switches behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-146:** WinUI `Templates` shared composition cleanup shall converge shared `Templates Library` and `Templates Editor` composition into a Templates-local composition owner instead of leaving shared capability composition responsibilities in `MainWindow`.
  - **Acceptance details:** `MainWindow` remains the shell composition root and keeps only shell route switching, shell title/description, shell compact or drawer behavior, shell host visibility, right-panel infrastructure, and app-level workspace lifetime; the Templates-local composition owner becomes the long-term home for shared Templates-local composition and interaction boundaries across `templates.library` and `templates.editor`.
  - **Priority:** P1

- **FR-147:** WinUI `Templates` shared composition cleanup shall treat capability-specific host interfaces implemented by `MainWindow` as temporary migration bridges only, and views shall not depend on or receive `MainWindow` directly.
  - **Acceptance details:** shared Templates route activation handling, shared workspace lifetime participation, and shared local interaction boundaries for Library and Editor must converge behind the Templates-local composition owner or narrow abstractions rather than direct `MainWindow` injection or permanent shell-host interface accumulation.
  - **Priority:** P1

- **FR-148:** WinUI `Templates` shared composition cleanup shall preserve the long-lived Templates workspace/session model and the existing Library-first navigation exception so route activation reconciles shared Templates state rather than recreating the Templates workspace on every route change.
  - **Acceptance details:** cleanup-target definition must preserve `templates.library` as the stable/default Templates surface, keep `templates.editor` as a workflow-state destination entered from explicit actions rather than a peer tab, and remain explicit that Templates Library extraction details, Templates Editor extraction details, runtime implementation, editor behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

Detailed capability contract:
- See `docs/01-requirements/machines-capability-contract.md` for v1 scope boundaries, safety constraints, and explicit TBDs.
- See `docs/02-ux/winui-shell-contract-aa.md` for Milestone AA shell-specific contract details.
- See `docs/02-ux/winui-global-navigationview-contract-ac.md` for Milestone AC global NavigationView behavior and routing contract.
- See `docs/02-ux/winui-templates-capability-contract-ad.md` for Milestone AD `Templates` routing and unified workflow contract.
- See `docs/02-ux/winui-deploy-from-template-contract-af.md` for Milestone AF `Deploy from-template` routing, readiness, and results visibility contract.
- See `docs/02-ux/winui-deploy-on-the-fly-contract-ag.md` for Milestone AG `Deploy on-the-fly` routing, readiness, correction affordances, and results visibility parity contract.
- See `docs/02-ux/winui-assets-base-disks-capability-contract-aj.md` for Milestone AJ `Assets > Base Disks` routing, operations, validation taxonomy, and removal safety contract.
- See `docs/02-ux/winui-assets-switches-capability-contract-ak.md` for Milestone AK `Assets > Switches` routing, CRUD surface, validation taxonomy, and deletion guardrail contract.
- See `docs/02-ux/winui-shell-view-consistency-contract-al.md` for Milestone AL cross-view shell/header/navigation/right-panel/action/compact-layout consistency rules.
- See `docs/02-ux/winui-shell-composition-boundary-contract-am.md` for Milestone AM shell composition ownership and workspace extraction boundary rules.
- See `docs/02-ux/winui-view-interaction-contract-am.md` for Milestone AM view interaction rules replacing broad child-control exposure patterns.
- See `docs/02-ux/winui-ui-test-convergence-contract-am.md` for Milestone AM UI test strategy rules during shell/workspace extraction.
- See `docs/02-ux/winui-machines-workspace-extraction-seam-am.md` for Milestone AM Machines-specific extraction seam rules.
- See `docs/02-ux/winui-capability-workspace-composition-contract-am.md` for Milestone AM capability-local workspace composition refinement after the first Machines extraction slices.
- See `docs/02-ux/winui-machines-composition-cleanup-target-am.md` for the post-AM33 Machines-specific cleanup target before broader capability rollout continues.
- See `docs/02-ux/winui-assets-workspace-extraction-seam-am.md` for the AM Assets-specific extraction seam and composition target after the Machines proof point.
- See `docs/02-ux/winui-assets-composition-cleanup-target-am.md` for the AM shared Assets composition cleanup target that narrows shell-vs-Assets ownership before Assets runtime extraction proceeds.
- See `docs/02-ux/winui-templates-composition-cleanup-target-am.md` for the AM shared Templates composition cleanup target that narrows shell-vs-Templates ownership before Templates runtime extraction proceeds.
- See `docs/02-ux/winui-assets-overview-extraction-cleanup-target-am.md` for the AM Assets Overview cleanup target inside the shared Assets composition boundary.
- See `docs/02-ux/winui-assets-base-disks-extraction-cleanup-target-am.md` for the AM Assets Base Disks cleanup target inside the shared Assets composition boundary.
- See `docs/02-ux/winui-assets-switches-extraction-cleanup-target-am.md` for the AM Assets Switches cleanup target inside the shared Assets composition boundary.

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
