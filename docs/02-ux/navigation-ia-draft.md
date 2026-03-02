# Navigation IA Draft (Entity-Based + Scope Switching)

**Purpose:** Propose a future navigation/information architecture for LabAssistant that supports UI migration while preserving behavior and improving discoverability.

**Status:** Draft for migration planning (not an implementation spec).

**Related:** `docs/02-ux/capability-taxonomy.md`
 
**See also:**
- `docs/02-ux/current-ui-capability-audit.md`
- `docs/02-ux/migration-preservation-matrix.md`
- `docs/02-ux/ui-migration-execution-plan.md`
- `docs/02-ux/ui-framework-decision-record-y3.md`
- `docs/02-ux/winui-shell-contract-aa.md`
- `docs/02-ux/winui-global-navigationview-contract-ac.md`
- `docs/02-ux/winui-templates-capability-contract-ad.md`
- `docs/02-ux/winui-layout-constraints-contract.md`

---

## 1. Design Goals

### Primary goals
- Make capabilities discoverable for non-developer users
- Reduce current duplication/miscategorization of workflows
- Preserve existing behavior during UI migration
- Support growth (Machines page, Assets expansion, Logs UI)

### Constraints
- Current UI is a functional bridge, not the target end state
- Behavior preservation is more important than preserving page layout
- The app already contains significant automatic/background behaviors (readiness, cleanup, summaries, diagnostics)

---

## 2. Navigation Model (Recommended)

## 2.1 Top-Level Model
- **Entity-based top-level navigation**
- **Task-oriented subviews/actions** within each entity area

### Proposed top-level entries
- `Machines`
- `Deploy`
- `Templates`
- `Assets`
- `Diagnostics`
- `Settings`

This aligns with the capability taxonomy and gives stable top-level structure without turning navigation into a raw command list.

## 2.3 Milestone AC Navigation Convergence (Approved)

For WinUI migration implementation, the shell navigation baseline is now:
- single global `NavigationView` in `LeftCompact` mode
- hierarchical entity -> child-action model
- canonical route keys in `capability.subview` format
- deterministic startup route `machines.overview`
- `Settings` as footer navigation entry

Behavioral contract source:
- `docs/02-ux/winui-global-navigationview-contract-ac.md`

## 2.4 Milestone AD Templates Capability Contract (Approved)

For Templates convergence planning and implementation sequencing:
- parent capability route is `Templates`
- canonical child routes are:
  - `templates.library` (default child)
  - `templates.editor`
- `templates.details` is deferred unless explicitly approved by a later milestone contract
- selecting parent `Templates` routes to `templates.library` and remains aligned with AC global nav behavior

Behavioral contract source:
- `docs/02-ux/winui-templates-capability-contract-ad.md`

---

## 2.2 Two Navigation Scopes (Key Concept)

The future UI should support two distinct navigation scopes:

### A. Capability Scope (Hamburger mode)
Shows what the application can do:
- Machines
- Deploy
- Templates
- Assets
- Diagnostics
- Settings

This is the mode the user described for the hamburger menu.

### B. Context Scope (Capability workspace mode)
Shows what the user is currently working on inside the selected capability workspace:
- current deploy VM list + selection context
- template list / selected template sections
- asset list / asset categories
- diagnostics filters/history
- settings categories

### Why the scope split matters
Without this separation, a single left panel becomes overloaded with:
- feature discovery
- object lists
- workflow status
- contextual actions

The shell should keep capability selection in the left rail/drawer and render context scope in the capability workspace rather than overloading navigation chrome.

---

## 3. Proposed Interaction Pattern (Hamburger + Left Panel)

## 3.1 Hamburger behavior (future)
- Clicking the hamburger opens a **slide-out capability drawer** from the left
- Drawer shows top-level capability labels/actions while icon rail remains the default navigation surface
- Hover should be tooltip-only, not the primary full-menu interaction path
- Exiting capability mode happens by:
  - selecting a capability, or
  - clicking outside the drawer, or
  - pressing `Esc`
- When drawer is open, shell shows a scrim over the remaining content

## 3.2 After capability selection
- Main content navigates to the selected top-level area
- Capability workspace shows **Context Scope** for that area

Examples:
- Select `Templates` -> workspace shows template list/filters/sections
- Select `Assets` -> workspace shows asset categories and current asset list
- Select `Deploy` -> workspace shows deployment VM entries and deploy context items

---

## 4. Proposed Top-Level IA (Draft)

## 4.1 Machines

**Primary user goal:** Manage Hyper-V VMs on the host.

### Suggested subviews/actions
- VM Inventory (default)
- VM Details / Inspector
- Basic Edit (CPU, memory, switch)
- Actions
  - Start / Stop
  - Delete (with cleanup options)
  - Open Hyper-V console for selected VM
  - Open RDP session (when reachable / configured)
  - Open advanced settings (if MMC/shell integration is feasible)

### Workspace context ideas
- VM list (search/filter)
- status indicators
- origin labels (LabAssistant / external / unknown)
- quick actions for selected VM

### Layout decision (AA contract)
- default `Machines` layout uses side-by-side list + details
- details pane uses section-based editing (for example: Overview, Hardware, Storage, Network, Guest OS)
- CPU/Memory should be grouped under a Hardware-oriented section rather than split into sparse standalone panes

---

## 4.2 Deploy

**Primary user goal:** Configure and run provisioning workflows with readiness, progress, and outcomes.

### Suggested subviews/actions
- Deploy Workspace (default)
  - on-the-fly configuration
  - deploy from template
  - readiness report
  - progress/outcomes
- Post-deploy quick actions (future refinement)
  - open VM console
  - open RDP (when available)
- Recent Deployments / History (future, if implemented)

### Workspace context ideas
- current VM entries in deployment
- selected VM context
- compact readiness summary
- quick actions (add VM, load template, save as template)

### Important boundary
- `Deploy` is not a substitute for `Machines`
- `Deploy` should focus on provisioning workflows, not general Hyper-V VM administration
- Single-VM and multi-VM deployment should remain the same Deploy workspace (same GUI), not separate top-level modes

### AF contract note
- AF migration slice is `from-template` first with canonical route `deploy.from_template`.
- `deploy.on_the_fly` migration is explicitly deferred in AF scope.
- Deploy readiness/review surface must expose correction actions when AE compatibility issues block deploy.

---

## 4.3 Templates

**Primary user goal:** Manage reusable deployment definitions end-to-end.

### Consolidation target (important)
Current split between:
- template list
- template editor
- template details
should become one coherent Templates workflow.

### Suggested subviews/actions
- Template Library (list/search/filter)
- Template Editor (integrated, not isolated)
- Import / Export
- Validation + missing-reference resolution

### Canonical route contract
- `templates.library` (default)
- `templates.editor`
- `templates.details` deferred (not required for AD2/AD3)

### Model note (current reality)
- Templates are lab-level (`LabTemplate`) and contain per-VM definitions (`VmTemplate`).
- The Templates UX should expose both levels in one area (library + lab details + per-VM editing).

### Workspace context ideas
- template list
- selected template metadata
- VM list inside selected template
- validation status summary

---

## 4.4 Assets

**Primary user goal:** Manage shared deployment and Hyper-V resources.

### Suggested subviews/actions
- Base Disks (VHDX Catalog)
  - list / add / edit / remove
  - integrity validation
- Virtual Switches (future expansion)
  - list / create / edit / delete
- Asset Health / Validation (future)

### Workspace context ideas
- asset type selector (Base Disks / Switches)
- asset list
- selected asset details / actions

### Naming
- Top-level category remains **Assets** (agreed)

---

## 4.5 Diagnostics

**Primary user goal:** Troubleshoot issues and export support artifacts.

### Suggested subviews/actions
- Diagnostics Export
- Logs (future `#215` Phase 1 read-only viewer)
- Recent Issues / error history (optional future)

### Workspace context ideas
- operation filters
- recent operations/failures
- export options/presets (future)

---

## 4.6 Settings

**Primary user goal:** Configure app-wide behavior and operational defaults.

### Suggested subviews/actions
- General
- Storage paths
- Deployment policies
- Logging/diagnostics settings
- Future defaults (for example, VM delete disk cleanup preference)

### Workspace context ideas
- settings categories
- unsaved changes indicator

---

## 5. Current UI -> Future IA Mapping (High-Level Draft)

This is a preliminary mapping (not the full UI audit).

## 5.1 Current MainWindow sidebar (today)
Current top-level buttons:
- Deploy VMs
- Templates
- Template Editor
- VHDX Catalog
- Switches
- Settings
- Logs

## 5.2 Proposed mapping
- `Deploy VMs` -> `Deploy`
- `Templates` + `Template Editor` -> `Templates` (unified workflow)
- `VHDX Catalog` + `Switches` -> `Assets`
- `Logs` + diagnostics-export-related access points -> `Diagnostics`
- `Settings` -> `Settings`
- `Machines` -> new top-level area (new user-facing capability)

## 5.3 Known consolidation targets
- Templates list/editor/details (merge)
- Assets (VHDX catalog + switch management)
- Diagnostics (export + logs + future log viewer)

---

## 6. Behavior Preservation Considerations for Migration

Migration risk is not only layout. It is hidden behavior loss.

Examples of behaviors that must be preserved:
- quick preflight auto-refresh on relevant field changes
- full preflight deploy gating
- deployment cancellation and terminal-state recovery
- cleanup/residual reporting
- guest-step skip reporting (`StepSkipped`, `skipReason`)
- diagnostics export and structured logging expectations

This is why the next deliverable after IA should be the GUI Action Map.

---

## 7. Documentation Sequence (Migration Prep)

### Phase 1 (current)
- Capability taxonomy (`capability-taxonomy.md`)
- Navigation IA draft (this doc)

### Phase 2
- Current UI / capability audit (duplication, miscategorization, non-functional surfaces)

### Phase 3
- GUI Action Maps by area:
  - Deploy
  - Templates
  - Assets
  - Settings
  - Diagnostics

### Phase 4
- Migration preservation matrix:
  - must preserve
  - may merge/move
  - may defer
  - placeholder/future

---

## 8. Open Questions / TBDs

- Resolved: default landing capability is `Machines` for WinUI shell implementation.
- Resolved: layout constraints and scroll ownership are defined in `docs/02-ux/winui-layout-constraints-contract.md` and are mandatory for AB2+ implementation slices.
- Resolved: Templates capability routing/workflow contract is defined in `docs/02-ux/winui-templates-capability-contract-ad.md`.
- How much Hyper-V VM editing should be native LabAssistant UI vs opening Hyper-V dialogs (if possible)?
- Should Diagnostics include a lightweight "Recent Issues" history view using the existing error feed service, or stay focused on export/logs initially?
- Should Assets eventually split into separate top-level items if scope grows significantly?
- `TBD:` Capability-specific inner layouts for Assets/Diagnostics in WinUI (within layout contract constraints)
