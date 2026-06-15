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

- **FR-047:** The system shall execute Hyper-V Deploy workflow commands for a given VM through one persistent PowerShell session owned by that VM workflow.
  - **Acceptance details:** The same workflow-owned session shall be reused across ordered create/configure/start steps and cleanup for that VM; session resurrection after app crash/restart is not required in the current scope.
  - **Priority:** P1

- **FR-048:** The system shall execute read-heavy Hyper-V queries through an explicit query execution seam that may reuse PowerShell sessions across requests instead of creating a brand-new session for every query call.
  - **Acceptance details:** This query seam is intentionally separate from the Deploy workflow-session model and applies to Machines/admin read paths such as inventory, edit snapshot loading, switch listing, IP lookup, and comparable read-only Hyper-V probes.
  - **Priority:** P1

- **FR-049:** The system shall emit diagnostics that measure Hyper-V PowerShell session creation cost, command/query execution cost, and key read-flow duration before deeper backend changes are evaluated.
  - **Acceptance details:** Diagnostics must make it possible to distinguish workflow-session creation, query-session creation, one-shot administrative session creation, and key read flows such as Machines inventory load and edit snapshot load.
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
  - `LabAssistant` (WPF) remains available as a legacy baseline/maintenance surface
  - `LabAssistant.WinUI` is introduced as a separate application project
  - **Acceptance details:** both UI projects build in solution and are independently launchable.
  - **Priority:** P1

- **FR-068:** WinUI shell navigation shall use icon-rail + hamburger drawer interaction:
  - normal desktop widths may show a persistent icon rail for top-level capabilities
  - hamburger opens a slide-out capability drawer with scrim
  - drawer dismisses on outside click or `Esc`
  - **Acceptance details:** full-menu navigation must not depend on hover-only behavior; compact widths may replace a persistent icon rail with a hamburger-invoked drawer model when that better preserves workspace economy.
  - **Priority:** P1

- **FR-069:** WinUI shall default to `Machines` on startup and shall not persist last selected capability across restarts.
  - **Acceptance details:** app startup route is deterministic (`Machines`) unless explicitly changed by future approved requirements.
  - **Priority:** P1

- **FR-070:** WinUI shell shall include right-side panel infrastructure that is collapsed by default and available for approved capability- or lane-owned secondary context.
  - **Acceptance details:** the shell owns the panel container, layout host, generic visibility mechanics, and collapsed-by-default behavior; active capability or lane contracts decide whether workflow-local triggers or counts are exposed; panel presence does not block normal workspace interaction when collapsed.
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

- **FR-075:** WinUI shell shall use a global `NavigationView` (`LeftCompact`) for top-level capability navigation, while capability-local navigation owns routine child surfaces within the active capability workspace.
  - **Acceptance details:** Expanded mode shows capability labels; compact mode remains icon-first; parent capability selection must not depend on hover-only child menus or flyouts to reach the approved default surface.
  - **Priority:** P1

- **FR-076:** WinUI shell navigation shall use canonical route keys in `capability.subview` format, with deterministic startup at `machines.overview`.
  - **Acceptance details:** Selecting a parent entity routes to its default child route; `Settings` is placed as footer navigation.
  - **Priority:** P1

- **FR-077:** WinUI `Templates` capability shall use canonical child routes with deterministic parent default routing:
  - `templates.library` (default child)
  - `templates.editor`
  - `templates.builder`
  - `templates.details` is deferred unless explicitly approved in a future milestone contract.
  - **Acceptance details:** Selecting parent `Templates` routes to `templates.library` and remains consistent with global navigation rules from FR-075/FR-076. `templates.editor` and `templates.builder` are workflow-state destinations entered from explicit actions rather than stable/default peer routes.
  - **Priority:** P1

- **FR-078:** WinUI `Templates` capability shall provide a unified workflow surface that keeps template library and template editing in one coherent capability context.
  - **Acceptance details:** Users can list/search/select templates, open selected V1/simple/legacy templates into the current editor, open V2 template authoring into the Builder workflow, and perform create/edit/save flows without leaving `Templates` capability context.
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
  - **Acceptance details:** Switch selection is optional at VM level; zero switch rows remain valid, but if one or more switch rows are present, each row must resolve to a valid host switch value, empty rows are invalid until removed or completed, duplicate switch values are disallowed, row order is preserved, and selector UX shall support add/remove row interactions.
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
  - **Acceptance details:** Deploy compatibility handling prefers `switchNames` with fallback to legacy `switchName`; disk identity uses AE normalization semantics and required unresolved/ambiguous disk identity is blocking; when one or more switch assignments are present, every assigned switch must resolve successfully before deploy may start; deploy execution creates one NIC per assigned switch in listed order, while the first assigned switch remains the compatibility/default switch for existing single-switch consumers and current guest-network placeholder expectations.
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
  - **Acceptance details:** Required unresolved inputs (for example missing required disk identity, invalid VM entry state, or invalid required switch selection) are blocking; Quick Deploy may keep zero switch rows when networking is optional, but if one or more switch rows are present each assigned switch must be valid and unique; non-critical mapping issues are warning-only with actionable guidance.
  - **Priority:** P1

- **FR-093:** WinUI Deploy `on-the-fly` shall provide correction affordances for blocking readiness issues and gate execution until blocking issues are resolved.
  - **Acceptance details:** readiness output provides explicit correction actions, should surface issues at field, group, or VM-row granularity where that improves fixability, and once unblocked deploy execution creates one NIC per assigned switch in listed order while preserving the first-switch compatibility/default rule; Quick Deploy draft editing remains live draft state rather than a per-VM apply workflow, and the handoff for saving the current draft into template authoring should remain explicitly labeled as a save-to-template action rather than an ambiguous editor-launch label.
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
  - **Acceptance details:** the surface shall support list, refresh, import/register, metadata edit, validation/readiness visibility, and remove actions without requiring a separate edit route; metadata editing remains in selected-item details context and does not invent new schema or disk-domain semantics; selection state remains clear and stable across refresh/update actions; existing embedded asset shortcuts from Deploy/Templates remain preserved.
  - **Priority:** P1

- **FR-102:** WinUI `Assets` Base Disks shall classify registration and catalog validation outcomes as blocking or warning with actionable user guidance.
  - **Acceptance details:** blocking states include invalid path/file type, inaccessible or locked disk, failed required metadata extraction, and other conditions that prevent a catalog entry from being safely registered or validated; warning states may indicate non-blocking readiness concerns while keeping the item visible and actionable; empty/loading/error states must be explicit and non-silent.
  - **Priority:** P1

- **FR-103:** WinUI `Assets` Base Disks removal shall enforce explicit safety guardrails and operation-scoped diagnostics.
  - **Acceptance details:** AJ scope covers registry removal, not underlying file deletion; remove requires explicit confirmation, must surface whether the disk appears in use or referenced, blocks or warns per approved safety taxonomy, and emits structured logs with `operationId`, `baseDiskId`, action context, result, and error details; failed removal must not leave partial registry state; if remove, update, or register flows fail after transient or in-memory state changes, the visible state must reconcile back to persisted truth.
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
  - **Acceptance details:** approved `Overview` surfaces are not decorative and must provide at least two of route-entry value, useful summary, or attention/health signaling; `Assets` uses `Overview`, `Base Disks`, and `Switches` as the current local model, and Assets Overview remains primarily a summary-and-navigation surface rather than the place that absorbs the full operational action set; `Deploy` uses `Overview`, `Quick Deploy`, and `From Template`, where Deploy Overview remains the route-entry chooser/index surface rather than a heavy deployment-history dashboard by default, `Quick Deploy` remains the direct configuration workflow, and `From Template` remains a review/remediation/deploy workflow rather than a duplicate Quick Deploy editor; `Diagnostics` uses `Overview` and `Logs` with Overview as a lightweight support dashboard; `Machines` remains single-surface for current scope; `Templates` keeps `Library` as the primary capability surface while `Editor` remains a workflow-state entered from explicit actions rather than a permanently exposed peer destination.
  - **Priority:** P1

- **FR-111:** WinUI shell right panel shall remain shell-owned infrastructure, but capability views shall own panel meaning, trigger placement, and issue/progress scoping according to the active workflow contract.
  - **Acceptance details:** shell-owned infrastructure includes the panel container, layout host, generic visibility mechanics, compact-width fallback, owner reset on capability change, and internal vertical scroll ownership; active capability or lane owns whether the panel is used, what it means, what content appears there, and the workflow-local triggers/titles/summaries/results/actions that open, close, or refresh it; workflow-local panel toggles and issue counts may live inside child views; `Deploy` right panel is progress/results-first, while pre-run validation issues move inline in the main workspace; current `Assets`, `Machines`, `Templates`, and `Diagnostics` scope do not require right-panel dependence by default; right-panel content remains secondary context and must not be the primary editor surface.
  - **Priority:** P1

- **FR-112:** WinUI migrated capability surfaces shall follow shared action-placement, iconography, and compact-layout rules derived from the cross-view audit.
  - **Acceptance details:** actions live nearest to the state they affect; inventory actions stay in inventory/list header context; current-object actions stay in details/editor context; workflow actions may remain text-capable where clarity requires it; support actions stay secondary; `New` defaults to an inventory-level action that clears the current details/editor into draft state; icon-first command chrome with tooltips is preferred, including trash-can delete affordances and icon-based save/apply where clarity remains sufficient; overview pages may use more page-like scrolling when appropriate, but dense operational surfaces still prioritize bounded scroll owners rather than unbounded page growth.
  - **Priority:** P1

- **FR-113:** WinUI `MainWindow` shall act as the shell composition root only and shall limit its direct ownership to shell chrome, route resolution, shell navigation behavior, shell header state, and shell right-panel host lifecycle.
  - **Acceptance details:** `MainWindow` may continue to own shell-level state such as active route, active capability, shell theme state, compact drawer behavior, right-panel container lifecycle, generic panel visibility mechanics, and shell-level size constraints, but it shall not remain the long-term owner of capability-local inventory state, selection state, edit drafts, workflow-specific readiness state, capability-specific action orchestration, or lane-specific right-panel meaning/content/update rules once AM extraction slices land.
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

- **FR-137:** WinUI `Assets Overview` cleanup shall remain under shared `AssetsCapabilityRuntime` rather than becoming a shell-owned surface, while converging Overview-specific state and UI coordination behind an Overview-local seam.
  - **Acceptance details:** the Overview cleanup target must explicitly keep shared Assets composition responsible only for capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Overview-local seam the target owner for Overview summary/navigation state and Overview-local interaction coordination.
  - **Priority:** P1

- **FR-138:** WinUI `Assets Overview` cleanup shall preserve current Overview behavior and boundaries while making Overview participation in the long-lived Assets workspace explicit.
  - **Acceptance details:** the Overview cleanup target must preserve `assets.overview` as the route-entry and index surface for `Assets`, must keep the Overview summary/navigation role intact, must avoid taking semantic ownership of Base Disks or Switches behavior, and must keep route activation refresh within the long-lived Assets workspace rather than per-navigation recreation.
  - **Priority:** P1

- **FR-139:** WinUI `Assets Overview` cleanup shall reject direct `MainWindow` view dependency, Overview-owned shared god-object growth, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Assets composition must not be widened into an Overview-centric shared owner; and the cleanup target must stay explicit that Base Disks extraction details, Switches extraction details, runtime implementation, overview redesign beyond ownership cleanup, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-140:** WinUI `Assets Base Disks` cleanup shall remain under shared `AssetsCapabilityRuntime` rather than becoming a shell-owned surface, while converging Base Disks-specific state, orchestration, and UI coordination behind a Base Disks-local seam.
  - **Acceptance details:** the Base Disks cleanup target must explicitly keep shared Assets composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Base Disks-local seam the target owner for Base Disks-specific state, orchestration, composition, and interaction coordination.
  - **Priority:** P1

- **FR-141:** WinUI `Assets Base Disks` cleanup shall preserve current `assets.base_disks` behavior and boundaries while making Base Disks participation in the long-lived Assets workspace explicit.
  - **Acceptance details:** the Base Disks cleanup target must preserve `assets.base_disks` as the operational Base Disks management surface, must keep Base Disks outside Overview and Switches semantic ownership, must keep route activation refresh within the long-lived Assets workspace rather than per-navigation recreation, and must preserve AJ and AL Base Disks behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-142:** WinUI `Assets Base Disks` cleanup shall reject direct `MainWindow` view dependency, shared Assets composition widening into the Base Disks workflow owner, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Assets composition must not become the Base Disks workflow owner; Base Disks-specific host bridges or control exposure remain temporary migration cleanup targets behind the Base Disks-local seam; and the cleanup target must stay explicit that Switches extraction details, Overview extraction details, runtime implementation, Base Disks behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-143:** WinUI `Assets Switches` cleanup shall remain under shared `AssetsCapabilityRuntime` rather than becoming a shell-owned surface, while converging Switches-specific state, orchestration, and UI coordination behind a Switches-local seam.
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

- **FR-149:** WinUI `Templates Library` cleanup shall remain under shared `TemplatesCapabilityRuntime` rather than becoming a shell-owned surface, while converging Library-specific state, orchestration, composition, and UI coordination behind a Library-local seam.
  - **Acceptance details:** the Library cleanup target must explicitly keep shared Templates composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Library-local seam the target owner for Library-specific state, orchestration, composition, and interaction coordination.
  - **Priority:** P1

- **FR-150:** WinUI `Templates Library` cleanup shall preserve current `templates.library` behavior and boundaries while making Library participation in the long-lived Templates workspace explicit.
  - **Acceptance details:** the Library cleanup target must preserve `templates.library` as the stable/default Templates surface, must keep Editor-specific ownership outside the Library seam, must keep route activation refresh within the long-lived Templates workspace rather than per-navigation recreation, and must preserve AD and AL7 Templates behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-151:** WinUI `Templates Library` cleanup shall reject direct `MainWindow` view dependency, shared Templates composition widening into the Library workflow owner, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Templates composition must not become the Library workflow owner; Library-specific host bridges or control exposure remain temporary migration cleanup targets behind the Library-local seam; and the cleanup target must stay explicit that Templates Editor extraction details, runtime implementation, Library behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-152:** WinUI `Templates Editor` cleanup shall remain under shared `TemplatesCapabilityRuntime` rather than becoming a shell-owned surface, while converging Editor-specific state, orchestration, composition, and UI coordination behind an Editor-local seam.
  - **Acceptance details:** the Editor cleanup target must explicitly keep shared Templates composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Editor-local seam the target owner for Editor-specific state, orchestration, composition, and interaction coordination.
  - **Priority:** P1

- **FR-153:** WinUI `Templates Editor` cleanup shall preserve current `templates.editor` behavior and boundaries while making Editor participation in the long-lived Templates workspace explicit.
  - **Acceptance details:** the Editor cleanup target must preserve `templates.editor` as a workflow-state destination entered from explicit actions rather than a stable/default peer route, must keep Editor inside the long-lived Templates workspace with route-activation refresh rather than per-navigation recreation, must keep Library-specific ownership outside the Editor seam, and must preserve AD and AL7 Templates behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-154:** WinUI `Templates Editor` cleanup shall reject direct `MainWindow` view dependency, shared Templates composition widening into the Editor workflow owner, Editor-owned Library growth, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Templates composition must not become the Editor workflow owner; Editor-specific host bridges or control exposure remain temporary migration cleanup targets behind the Editor-local seam; Editor must not absorb Library-specific ownership because it is a workflow-state destination; and the cleanup target must stay explicit that runtime implementation, Templates Library extraction details, Editor behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-155:** WinUI `Deploy` shared composition cleanup shall converge shared `Deploy Overview`, `Quick Deploy`, and `From Template` composition into a Deploy-local composition owner instead of leaving shared capability composition responsibilities in `MainWindow`.
  - **Acceptance details:** `MainWindow` remains the shell composition root and keeps only shell route switching, shell title/description, shell compact or drawer behavior, shell host visibility, right-panel infrastructure, and app-level workspace lifetime; the Deploy-local composition owner becomes the long-term home for shared Deploy-local composition and interaction boundaries across `deploy.overview`, `deploy.on_the_fly`, and `deploy.from_template`.
  - **Priority:** P1

- **FR-156:** WinUI `Deploy` shared composition cleanup shall treat capability-specific host interfaces implemented by `MainWindow` as temporary migration bridges only, and views shall not depend on or receive `MainWindow` directly.
  - **Acceptance details:** shared Deploy route activation handling, shared workspace lifetime participation, and shared local interaction boundaries for Deploy Overview, Quick Deploy, and From Template must converge behind the Deploy-local composition owner or narrow abstractions rather than direct `MainWindow` injection or permanent shell-host interface accumulation.
  - **Priority:** P1

- **FR-157:** WinUI `Deploy` shared composition cleanup shall preserve the long-lived Deploy workspace/session model and the existing Overview-first navigation contract so route activation reconciles shared Deploy state rather than recreating the Deploy workspace on every route change.
  - **Acceptance details:** cleanup-target definition must preserve `deploy.overview` as the route-entry surface, keep `deploy.on_the_fly` as the deep editor-oriented Quick Deploy workflow, keep `deploy.from_template` as a review/remediation/deploy workflow rather than a duplicate Quick Deploy editor, and remain explicit that Deploy Overview extraction details, From Template extraction details, Quick Deploy extraction details, runtime implementation, Deploy workflow redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-158:** WinUI `Deploy Overview` cleanup shall remain under shared `DeployWorkspaceComposition` rather than becoming a shell-owned surface, while converging Overview-specific state and UI coordination behind an Overview-local seam.
  - **Acceptance details:** the Deploy Overview cleanup target must explicitly keep shared Deploy composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make the Overview-local seam the target owner for Overview summary state, Overview-local navigation coordination, Overview-local interaction boundaries, and Overview-specific refresh or reconcile behavior triggered by `deploy.overview` activation.
  - **Priority:** P1

- **FR-159:** WinUI `Deploy Overview` cleanup shall preserve current `deploy.overview` behavior and boundaries while making Overview participation in the long-lived Deploy workspace explicit.
  - **Acceptance details:** the Deploy Overview cleanup target must preserve `deploy.overview` as the route-entry and index surface for `Deploy`, must keep Overview as primarily a summary and navigation surface, must keep route activation refresh within the long-lived Deploy workspace rather than per-navigation recreation, and must preserve AF, AG, AL, and AM78 Deploy behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-160:** WinUI `Deploy Overview` cleanup shall reject direct `MainWindow` view dependency, shared Deploy composition widening into the Overview workflow owner, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Deploy composition must not become the Overview workflow owner; Overview-specific host bridges or control exposure remain temporary migration cleanup targets behind the Overview-local seam; Overview must not absorb From Template or Quick Deploy ownership; and the cleanup target must stay explicit that runtime implementation, From Template extraction details, Quick Deploy extraction details, Deploy behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-161:** WinUI `Deploy From Template` cleanup shall remain under shared `DeployWorkspaceComposition` rather than becoming a shell-owned surface, while converging From Template-specific state, orchestration, composition, and UI coordination behind a From Template-local seam.
  - **Acceptance details:** the From Template cleanup target must explicitly keep shared Deploy composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make a From Template-local seam the target owner for template-driven state, review/remediation/deploy orchestration, From Template-local interaction boundaries, and From Template-specific composition or host cleanup.
  - **Priority:** P1

- **FR-162:** WinUI `Deploy From Template` cleanup shall preserve current `deploy.from_template` behavior and boundaries while making From Template participation in the long-lived Deploy workspace explicit.
  - **Acceptance details:** the From Template cleanup target must preserve `deploy.from_template` as a distinct template-driven review/remediation/deploy workflow surface rather than the route-entry Deploy surface, must keep route activation refresh or reconcile behavior within the existing long-lived Deploy workspace rather than per-navigation recreation, and must preserve AF, AG, AL, and AM78-AM86 Deploy behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-163:** WinUI `Deploy From Template` cleanup shall reject direct `MainWindow` view dependency, shared Deploy composition widening into the From Template workflow owner, From Template absorption of Quick Deploy semantics, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Deploy composition must not become the From Template workflow owner; From Template-specific host bridges or control exposure remain temporary migration cleanup targets behind the From Template-local seam; `deploy.from_template` must not absorb Quick Deploy semantics or Overview ownership; and the cleanup target must stay explicit that runtime implementation, Quick Deploy extraction details, Deploy Overview extraction details, Deploy behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-164:** WinUI `Deploy Quick Deploy` cleanup shall remain under shared `DeployWorkspaceComposition` rather than becoming a shell-owned surface, while converging Quick Deploy-specific state, orchestration, composition, and UI coordination behind a Quick Deploy-local seam.
  - **Acceptance details:** the Quick Deploy cleanup target must explicitly keep shared Deploy composition responsible only for shared capability-level composition concerns, must keep `MainWindow` limited to shell ownership, and must make a Quick Deploy-local seam the target owner for on-the-fly deploy state, Quick Deploy orchestration, Quick Deploy-local interaction boundaries, and Quick Deploy-specific composition or host cleanup.
  - **Priority:** P1

- **FR-165:** WinUI `Deploy Quick Deploy` cleanup shall preserve current `deploy.on_the_fly` behavior and boundaries while making Quick Deploy participation in the long-lived Deploy workspace explicit.
  - **Acceptance details:** the Quick Deploy cleanup target must preserve `deploy.on_the_fly` as a distinct on-the-fly deploy workflow surface rather than the route-entry Deploy surface, must keep route activation refresh or reconcile behavior within the existing long-lived Deploy workspace rather than per-navigation recreation, and must preserve AG, AF, AL, and AM78-AM94 Deploy behavior contracts while narrowing ownership.
  - **Priority:** P1

- **FR-166:** WinUI `Deploy Quick Deploy` cleanup shall reject direct `MainWindow` view dependency, shared Deploy composition widening into the Quick Deploy workflow owner, Quick Deploy absorption of From Template or Overview semantics, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Deploy composition must not become the Quick Deploy workflow owner; Quick Deploy-specific host bridges or control exposure remain temporary migration cleanup targets behind the Quick Deploy-local seam; `deploy.on_the_fly` must not absorb `deploy.from_template` semantics or Deploy Overview ownership; and the cleanup target must stay explicit that runtime implementation, From Template extraction details, Deploy Overview extraction details, Deploy behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-167:** WinUI `Diagnostics` shared composition cleanup shall converge shared `Diagnostics Overview` and `Diagnostics Logs` composition into a Diagnostics-local composition owner instead of leaving shared capability composition responsibilities in `MainWindow`.
  - **Acceptance details:** `MainWindow` remains the shell composition root and keeps only shell route switching, shell title/description, shell compact or drawer behavior, shell host visibility, right-panel infrastructure, and app-level workspace lifetime; the Diagnostics-local composition owner becomes the long-term home for shared Diagnostics-local composition and interaction boundaries across `Diagnostics Overview` and `Diagnostics Logs`.
  - **Priority:** P1

- **FR-168:** WinUI `Diagnostics` shared composition cleanup shall treat capability-specific host interfaces implemented by `MainWindow` as temporary migration bridges only, and views shall not depend on or receive `MainWindow` directly.
  - **Acceptance details:** shared Diagnostics route activation handling, shared workspace lifetime participation, and shared local interaction boundaries for Diagnostics Overview and Diagnostics Logs must converge behind the Diagnostics-local composition owner or narrow abstractions rather than direct `MainWindow` injection or permanent shell-host interface accumulation.
  - **Priority:** P1

- **FR-169:** WinUI `Diagnostics` shared composition cleanup shall preserve the long-lived Diagnostics workspace/session model and existing Overview-first route-entry semantics so route activation reconciles shared Diagnostics state rather than recreating the Diagnostics workspace on every route change.
  - **Acceptance details:** cleanup-target definition must preserve Diagnostics Overview as the route-entry surface, keep Diagnostics Logs as a child troubleshooting surface rather than a top-level shell destination, keep route activation refresh within the long-lived Diagnostics workspace rather than per-navigation recreation, and remain explicit that Diagnostics Overview extraction details, Diagnostics Logs extraction details, runtime implementation, Diagnostics workflow redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-170:** WinUI `Diagnostics Overview` cleanup shall remain under shared `DiagnosticsCapabilityRuntime` rather than becoming a shell-owned surface, while converging Overview-specific state behind an Overview-local seam.
  - **Acceptance details:** the Diagnostics Overview cleanup target must explicitly keep shared Diagnostics composition responsible only for shared capability-level concerns, must keep `MainWindow` limited to shell ownership, and must make an Overview-local seam the target owner for Overview summary state, entry-surface state, and Overview-specific refresh or reconcile behavior triggered by `diagnostics.overview` activation.
  - **Priority:** P1

- **FR-171:** WinUI `Diagnostics Overview` cleanup shall preserve current `diagnostics.overview` behavior and long-lived Diagnostics workspace participation while converging Overview-specific composition and UI coordination behind an Overview-local seam.
  - **Acceptance details:** the Diagnostics Overview cleanup target must preserve `diagnostics.overview` as the distinct Diagnostics route-entry and summary surface, must keep route activation refresh or reconcile behavior within the existing long-lived Diagnostics workspace rather than per-navigation recreation, and must make Overview-local composition, interaction boundaries, navigation coordination, and host-cleanup expectations explicit without widening shared Diagnostics composition into the Overview workflow owner.
  - **Priority:** P1

- **FR-172:** WinUI `Diagnostics Overview` cleanup shall reject direct `MainWindow` view dependency, shared Diagnostics composition widening into the Overview workflow owner, Overview absorption of Diagnostics Logs semantics, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Diagnostics composition must not become the Overview workflow owner; temporary Overview-specific host bridges or control exposure remain migration cleanup targets behind the Overview-local seam; `diagnostics.overview` must not absorb Diagnostics Logs ownership; and the cleanup target must stay explicit that Diagnostics Logs extraction details, runtime implementation, Diagnostics behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-173:** WinUI `Diagnostics Logs` cleanup shall remain under shared `DiagnosticsCapabilityRuntime` rather than becoming a shell-owned surface, while converging Logs-specific state behind a Logs-local seam.
  - **Acceptance details:** the Diagnostics Logs cleanup target must explicitly keep shared Diagnostics composition responsible only for shared capability-level concerns, must keep `MainWindow` limited to shell ownership, and must make a Logs-local seam the target owner for Logs troubleshooting state, log-exploration state, and Logs-specific refresh or reconcile behavior triggered by `diagnostics.logs` activation.
  - **Priority:** P1

- **FR-174:** WinUI `Diagnostics Logs` cleanup shall preserve current `diagnostics.logs` behavior and long-lived Diagnostics workspace participation while converging Logs-specific composition, orchestration, and UI coordination behind a Logs-local seam.
  - **Acceptance details:** the Diagnostics Logs cleanup target must preserve `diagnostics.logs` as the distinct Diagnostics troubleshooting and log-exploration surface, must keep route activation refresh or reconcile behavior within the existing long-lived Diagnostics workspace rather than per-navigation recreation, and must make Logs-local composition, interaction boundaries, workflow coordination, and host-cleanup expectations explicit without widening shared Diagnostics composition into the Logs workflow owner.
  - **Priority:** P1

- **FR-175:** WinUI `Diagnostics Logs` cleanup shall reject direct `MainWindow` view dependency, shared Diagnostics composition widening into the Logs workflow owner, Logs absorption of Diagnostics Overview semantics, and unapproved runtime or performance redesign during seam definition.
  - **Acceptance details:** views must not depend on or receive `MainWindow` directly; shared Diagnostics composition must not become the Logs workflow owner; temporary Logs-specific host bridges or control exposure remain migration cleanup targets behind the Logs-local seam; `diagnostics.logs` must not absorb Diagnostics Overview route-entry or index semantics; and the cleanup target must stay explicit that Diagnostics Overview extraction details, runtime implementation, Diagnostics behavior redesign, and performance redesign are out of scope.
  - **Priority:** P1

- **FR-176:** WinUI non-trivial lane cleanup shall use an explicit lane-local role split so lane ownership does not remain implied by host wiring or shared composition.
  - **Acceptance details:** when a lane owns meaningful local workflow, refresh/reconcile, helper coordination, auxiliary view composition, or shell interaction seams, the canonical target shall be an explicit `WorkspaceOwner`, `WorkspaceController`, `WorkspaceComposition`, and `WorkspaceShellBridge`, each with documented may-own and must-not-own boundaries that preserve the existing shell and capability composition contracts.
  - **Priority:** P1

- **FR-177:** WinUI lane architecture shall reject backpack hosts, attach cycles, lane-specific shell lambdas in `MainWindow`, and lane-specific logic inside shared capability composition as the long-term implementation pattern.
  - **Acceptance details:** host abstractions must not become mixed-responsibility dumping grounds; lane wiring must not rely on multi-step attach cycles to imply the final owner; lane-specific shell integration must converge behind named narrow shell bridges rather than ad hoc shell lambdas; and shared capability composition must remain limited to genuinely capability-shared concerns rather than lane-local workflow or panel semantics.
  - **Priority:** P1

- **FR-178:** WinUI lane architecture shall allow a slimmer form only for genuinely simple lanes and shall require that exception to stay explicit and bounded.
  - **Acceptance details:** a lane may avoid the full owner/controller/composition/shell-bridge split only when it lacks independent workflow orchestration, lane-local helper coordination, dedicated auxiliary surfaces, and genuine shell-owned interaction seams; even then, the shell-only `MainWindow` rule, shared-capability-only composition rule, and banned-pattern list shall still apply.
  - **Priority:** P1

- **FR-179:** WinUI capability bootstrap shall remain a shell-owned construction step that creates one typed capability runtime boundary per capability instead of leaving `MainWindow` to remain the long-term direct owner of lane seams and capability-local helpers.
  - **Acceptance details:** capability bootstrap may resolve DI services, shell-owned view roots, and shell bridges needed to instantiate a typed capability runtime, but bootstrap must remain distinct from the long-lived capability runtime and must not become the capability's ongoing workflow owner after construction.
  - **Priority:** P1

- **FR-180:** WinUI typed capability runtime shall own the capability-local runtime boundary above shared capability composition and lane-local seams without absorbing shell ownership or lane-local workflow ownership that belongs lower in the stack.
  - **Acceptance details:** a typed capability runtime may own shared capability composition lifetime, capability-shared helper lifetime, lane-local seam lifetime, route-activation handoff, and capability-level refresh or reconcile entry points; shared capability composition must remain capability-shared only; lane-local seams must remain responsible for lane-local workflow and view-state application.
  - **Priority:** P1

- **FR-181:** WinUI shell cleanup shall preserve `MainWindow` as the shell composition root after typed runtime introduction while converging `MainWindow` toward one typed runtime field per capability rather than direct lane-by-lane or helper-by-helper capability wiring.
  - **Acceptance details:** `MainWindow` must retain shell route switching, shell navigation, shell header/theme, shell container infrastructure, shell host visibility, app-level capability runtime lifetime, and capability bootstrap entry points; it must not remain the long-term direct owner of multiple lane-local fields, capability-local helper fields, or scattered capability-local route-activation wiring for the same capability; runtime implementation, eager-vs-lazy bootstrap policy, and behavior redesign remain out of scope.
  - **Priority:** P1

- **FR-182:** WinUI `DeployResultsPanelCoordinator` shall remain a narrow shared Deploy seam for results-panel intent aggregation rather than a general shared Deploy owner.
  - **Acceptance details:** the coordinator may aggregate active-lane title text, auto-open recommendations, Overview empty-state participation, and panel-state delegation across `deploy.on_the_fly` and `deploy.from_template`, while keeping its role limited to shared Deploy right-panel intent only.
  - **Priority:** P1

- **FR-183:** WinUI `DeployResultsPanelCoordinator` shall reject shell right-panel infrastructure ownership, lane-local workflow ownership, and general shared Deploy helper ownership.
  - **Acceptance details:** shell panel open state, compact fallback, width/layout, owner-capability precedence, container visibility, and lifecycle remain shell-owned; lane-local readiness, progress, result-row construction, issue-row construction, launcher semantics, and workflow sequencing remain lane-local; reference-data refresh, template-editor launch, and other general shared Deploy helper policy remain outside the coordinator.
  - **Priority:** P1

- **FR-184:** WinUI shared Deploy composition may delegate narrow results-panel integration questions to `DeployResultsPanelCoordinator` without widening it into a route owner or workflow owner.
  - **Acceptance details:** acceptable delegation includes panel title selection, auto-open recommendation, Overview empty-state visibility, panel-state application into the participating lanes, and capability-switch reset hooks specifically about Deploy panel presentation; route resolution, general Deploy refresh policy, and capability-wide workflow coordination remain out of scope.
  - **Priority:** P1

- **FR-185:** WinUI shared Deploy helpers shall remain narrow capability-shared, lane-triggered seams rather than becoming shared workflow owners by convenience.
  - **Acceptance details:** shared Deploy helpers may encode narrow reusable Deploy-side integration or data rules, but lane-local owners, hosts, or later capability-runtime seams shall remain responsible for invocation timing, workflow meaning, status messaging, and post-invocation orchestration.
  - **Priority:** P1

- **FR-186:** WinUI `DeployReferenceDataService` shall remain limited to shared Deploy reference-data loading, cached snapshot exposure, and explicit caller-requested refresh rather than absorbing lane refresh policy or workflow ownership.
  - **Acceptance details:** the helper may expose shared Deploy settings, switch inventory, catalog items, catalog options, and a narrow `EnsureAsync(forceRefresh)` boundary; route activation policy, readiness sequencing, lane-local defaults, remediation messaging, and shell/panel concerns remain outside it.
  - **Priority:** P1

- **FR-187:** WinUI `DeployResolveSuggestionsService` and `DeployTemplateEditorLauncher` shall remain narrow shared Deploy helper seams with explicit non-goals for workflow ownership.
  - **Acceptance details:** `DeployResolveSuggestionsService` may apply deterministic shared Deploy-side suggestions to a caller-supplied template using caller-supplied reference data, but it shall not own data loading, invocation timing, readiness re-evaluation, or lane messaging; `DeployTemplateEditorLauncher` may forward a caller-supplied document and status text into the Templates editor seam, but it shall not own document construction, invocation timing, template-library policy, or cross-capability workflow coordination.
  - **Priority:** P1

- **FR-188:** WinUI `DeployReferenceDataService` invalidation shall remain explicit and caller-owned until a later narrow contract defines stronger freshness guarantees.
  - **Acceptance details:** the current contract permits caller-owned `forceRefresh` triggers and existing cached/shared snapshot behavior, but it does not silently promise auto-invalidation across Assets, Templates, or other app-state changes; if stronger same-session freshness guarantees become required, they must be defined in a later narrow contract issue instead of inferred.
  - **Priority:** P1

- **FR-189:** WinUI surfaces shall preserve a baseline accessibility contract for primary workflows, including keyboard reachability, logical focus order, visible focus indication, non-color-only error communication, and resilience under supported text scaling and high-contrast modes.
  - **Acceptance details:** primary workflows must remain operable without hover-only or pointer-only dependency; compact/layout adaptations must not hide primary actions from keyboard users or break focus flow when text scaling or high-contrast presentation is applied.
  - **Priority:** P1

- **FR-190:** WinUI shell startup shall present a consistent LabAssistant application identity across the unpackaged executable, native desktop window/taskbar icon, window title, and custom shell header branding.
  - **Acceptance details:** WinUI shall own its branding asset location, native icon configuration, visible app-title configuration, and shell-header branding hookup without depending on the legacy WPF project path as the runtime source of truth; user-facing shell identity shall display `LabAssistant` rather than the WinUI project name; a repo-local asset derived from the existing LabAssistant icon is acceptable.
  - **Priority:** P1

Supporting authority:
- See `docs/00-overview/authoritative-doc-map.md` for the current authority model and the rule that milestone-coded docs are historical, not live authority.
- See `docs/01-requirements/machines-capability-contract.md` for v1 scope boundaries, safety constraints, and explicit TBDs.
- See `docs/03-architecture/winui-shell-bootstrap-runtime.md` for the current shell-only `MainWindow` boundary, capability bootstrap/runtime rules, long-lived workspace lifetime, and no-direct-`MainWindow` view rule.
- See `docs/03-architecture/winui-lane-architecture.md` for the current lane-local role split, interaction boundary, banned patterns, and simple-lane exception rule.
- See `docs/03-architecture/winui-shared-seam-ownership.md` for the current shared Deploy seam and shared helper ownership rules.

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
- See `docs/03-architecture/architecture.md` for current runtime structure notes.
- See `docs/03-architecture/hyperv-powershell-interaction.md` for the current PowerShell workflow/query/admin execution model and call-site classification.

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

## 6.2 Unified Lab Orchestration (V2 Requirements)

- **FR-191:** The system shall support a `V2` lab-template orchestration model that plans host provisioning, readiness, and guest configuration in one unified deployment graph instead of treating them as fully separate phases.
  - **Acceptance details:** The unified graph shall be a planning/runtime concern for `V2` templates only and shall not silently replace the current `V1` deployment engine.
  - **Priority:** P1

- **FR-192:** The system shall route templates to the deployment engine by schema-version compatibility and shall preserve the current deployment path for existing `V1` templates.
  - **Acceptance details:** `V1` templates must remain deployable without requiring V2-only fields; `V2` templates must not fall back to the legacy engine silently.
  - **Priority:** P0

- **FR-193:** The system shall model `V2` VM intent using both topology roles and additive capability roles.
  - **Acceptance details:** A VM may hold multiple roles simultaneously; additive capability roles must not imply exclusivity against topology roles.
  - **Priority:** P1

- **FR-194:** The system shall support first-class multi-NIC network intent in `V2` templates, including explicit per-NIC switch attachment and explicit per-NIC guest IP, gateway, and DNS authoring.
  - **Acceptance details:** Router-style VMs with multiple attached switches/NICs are in scope for the V2 schema and planner baseline.
  - **Priority:** P1

- **FR-195:** The system shall support deployment-profile-based scheduling for `V2` orchestration with at least `Conservative`, `Balanced`, and `Aggressive` profiles.
  - **Acceptance details:** Profiles tune overlap/resource behavior without changing dependency correctness, and the persisted V2 template value remains a validated string contract rather than a runtime-only free-form hint.
  - **Priority:** P1

- **FR-196:** The system shall classify `V2` orchestration work into coarse workload classes for scheduling decisions.
  - **Acceptance details:** The baseline classes are `HeavyHost`, `HeavyGuest`, `MediumGuest`, and `LightWaitValidation`; they provide policy-level scheduling input and do not replace dependency gates.
  - **Priority:** P1

- **FR-197:** The system shall support credential-slot references in `V2` templates and bootstrap-profile metadata on VHDX catalog entries without exporting reusable secret values inside templates.
  - **Acceptance details:** Template sharing must remain portable; unresolved credential slots may block V2 deployment but must not require secrets to be embedded in the exported JSON.
  - **Priority:** P0

- **FR-198:** The system shall provide a `Review and Resolve` planning surface for `V2` deployments before runtime execution begins.
  - **Acceptance details:** The review surface shall show the computed orchestration graph/waves, unresolved credentials/bootstrap assumptions, dependency blockers, and selected deployment profile.
  - **Priority:** P1

- **FR-199:** The V2 guest execution baseline shall use Hyper-V PowerShell Direct for supported guest operations in the current scope.
  - **Acceptance details:** Multi-hypervisor expansion is out of current scope and must not be assumed by the initial V2 contract.
  - **Priority:** P1

- **FR-200:** The V2 orchestration runtime shall support a first executable trust slice for bidirectional forest trusts between two managed V2 domains/forests.
  - **Acceptance details:** The first slice supports only bidirectional forest trusts between LabAssistant-managed V2 domains/forests. External trusts, realm trusts, one-way directions, selective authentication details, SID-filter details, and unmanaged external domains are out of scope. Trust execution waits until both participating domains are `DomainReady`, uses existing per-domain domain-admin credential slots, prepares cross-forest DNS forwarding/reachability before trust creation, validates the trust from both participating sides before marking it ready, emits structured logs with `operationId` for DNS prep, trust creation, validation, and cleanup, and attempts to delete LabAssistant-created trust objects on later failure or cancellation while leaving DNS forwarders in place for retry and diagnosis.
  - **Priority:** P1

- **FR-201:** WinUI `Templates` shall define a distinct V2 Builder workflow for creating and editing V2 templates under the Templates capability without replacing the current Templates Editor.
  - **Acceptance details:** The Builder is a Templates-local workflow-state destination, expected to use `templates.builder` or an equivalent Templates-local route key. The current `templates.editor` remains the V1/simple/legacy editing surface for existing schema editing and must not absorb V2 topology orchestration.
  - **Priority:** P1

- **FR-202:** The V2 Builder first authoring slice shall use topology-first ordering and expose only the approved first-slice V2 fields.
  - **Acceptance details:** The authoring order is schema/profile, lab networks, forests/domains, then VM assignments. First-slice fields are schema/profile, lab networks, forests/domains, VM topology role, membership mode, domain assignment, credential slot references, and per-VM NIC/IP/DNS/gateway authoring. Trust authoring is out of this first Builder slice even though trust runtime support exists.
  - **Priority:** P1

- **FR-203:** The V2 Builder shall keep deterministic suggestions separate from persisted user intent and require explicit user confirmation before save.
  - **Acceptance details:** Builder suggestions may prefill or propose derived values such as network/domain/assignment defaults, but save output is based on the user-confirmed draft. Builder output must remain compatible with Deploy From Template review and V2 planning contracts.
  - **Priority:** P1

- **FR-204:** The V2 Builder implementation target shall preserve Templates and shell ownership boundaries.
  - **Acceptance details:** Builder state, workflow orchestration, validation sequencing, composition, and view coordination belong behind Builder-local workspace/viewmodel/controller/composition/view seams under the long-lived Templates workspace. `MainWindow`, shared Templates composition, and the current Templates Editor must not become the Builder workflow owner.
  - **Priority:** P1

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
