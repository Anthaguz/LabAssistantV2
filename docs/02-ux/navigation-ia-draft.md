# Navigation IA Draft (Entity-Based + Scope Switching)

**Purpose:** Propose a future navigation/information architecture for LabAssistant that supports UI migration while preserving behavior and improving discoverability.

**Status:** Draft for planning and migration preparation (not an implementation spec yet).

**Related:** `docs/02-ux/capability-taxonomy.md`

---

## 1. Design Goals

### Primary goals
- Make capabilities discoverable for non-developer users
- Reduce miscategorization/duplication of current flows
- Preserve complex existing behaviors during UI migration
- Support future growth (Machines page, Assets expansion, Logs UI)

### Constraints
- Current UI will eventually be replaced (likely WPF refresh or WinUI 3 migration)
- Behavior preservation matters more than current page structure
- The app already has substantial background/automatic behaviors (readiness, cleanup, diagnostics)

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

This matches the capability taxonomy and supports future expansion without turning navigation into a command list.

---

## 2.2 Two Navigation Scopes (Important)

The app should support two distinct navigation scopes:

### A. Capability Scope (Hamburger mode)
Shows **what the application can do**:
- Machines
- Deploy
- Templates
- Assets
- Diagnostics
- Settings

This is the mode you described for the hamburger menu.

### B. Context Scope (normal left panel mode)
Shows **what the user is currently working on** inside a capability:
- VM list in current deploy session
- template list / selected template sections
- asset list / selected asset categories
- etc.

### Why this distinction matters
Without separating these scopes, left navigation becomes confusing because it mixes:
- feature discovery
- current workflow context
- object lists
- status data

The “hamburger temporarily replaces left panel” idea is a good implementation of this scope switch.

---

## 3. Proposed Interaction Pattern (Hamburger + Left Panel)

## 3.1 Hamburger behavior (future)
- Clicking the hamburger button switches the left panel from **Context Scope** to **Capability Scope**
- The capability list is shown (top-level entities)
- Exiting capability mode happens by:
  - selecting a capability, or
  - clicking outside / closing the panel (exact interaction TBD by framework)

## 3.2 After capability selection
- Main content navigates to the selected top-level area
- Left panel returns to **Context Scope**, now showing context-relevant items for that area

Examples:
- Select `Templates` -> left panel returns showing template list / filters / sections
- Select `Assets` -> left panel returns showing base disks / switches categories and selected asset list
- Select `Deploy` -> left panel returns showing lab/VM deployment context items

---

## 4. Proposed Top-Level IA (Draft)

## 4.1 Machines

**Primary user goal:** Operate and manage existing Hyper-V VMs on the host.

### Suggested subviews/actions
- VM Inventory (default)
- VM Details / Inspector
- Basic Edit (memory, CPU, switch)
- Actions
  - Start / Stop
  - Delete (with cleanup options)
  - Open advanced settings (if MMC/shell integration is feasible)

### Context panel ideas (normal left panel mode)
- VM list (search/filter)
- status indicators
- origin labels (LabAssistant / external / unknown)
- quick actions for selected VM

---

## 4.2 Deploy

**Primary user goal:** Create and run deployments (VMs/labs) with readiness and outcomes.

### Suggested subviews/actions
- Deploy Workspace (default)
  - on-the-fly configuration
  - deploy from template
  - readiness report
  - progress/outcomes
- Recent Deployments / History (future, if implemented)

### Context panel ideas
- current VM entries in deployment
- selected VM context
- readiness summary counts (compact)
- quick actions (add VM, load template, save as template)

### Note
- Deploy is **not** the same as Machines management.
- Deploy should focus on provisioning workflows, not full Hyper-V CRUD/admin.

---

## 4.3 Templates

**Primary user goal:** Manage reusable deployment definitions end-to-end.

### Target consolidation (important)
Current split between:
- template list
- template editor
- template details
should become one coherent Templates workflow.

### Suggested subviews/actions
- Template Library (list/search/filter)
- Template Details
- Template Editor (integrated, not isolated)
- Import / Export
- Validation + missing-reference resolution

### Context panel ideas
- template list
- selected template metadata
- VM list within selected template
- validation status summary

---

## 4.4 Assets

**Primary user goal:** Manage shared Hyper-V and deployment resources.

### Suggested subviews/actions
- Base Disks (VHDX Catalog)
  - list / add / edit / remove
  - integrity validation
- Virtual Switches (future expansion)
  - list / create / edit / delete
- Asset Health / Validation (future)

### Context panel ideas
- asset type selector (Base Disks / Switches)
- list of assets
- selected asset details / actions

### Naming
- Top-level category remains **Assets** (agreed)

---

## 4.5 Diagnostics

**Primary user goal:** Troubleshoot problems and export support artifacts.

### Suggested subviews/actions
- Diagnostics Export
- Logs (future `#215` Phase 1 read-only viewer)
- Recent Issues / error feed history (optional future)

### Context panel ideas
- operation filters
- recent operations / failures
- export presets (future)

---

## 4.6 Settings

**Primary user goal:** Configure app-wide behavior and environment paths/policies.

### Suggested subviews/actions
- General settings
- Storage paths
- Deployment policies
- Logging/diagnostics settings
- Future defaults (e.g., VM delete disk cleanup preference)

### Context panel ideas
- settings categories
- unsaved changes indicator

---

## 5. Current UI -> Future IA Mapping (High-Level Draft)

This is a preliminary mapping, not the full audit yet.

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
- `Deploy VMs` -> **Deploy**
- `Templates` + `Template Editor` -> **Templates** (unified workflow)
- `VHDX Catalog` + `Switches` -> **Assets**
- `Logs` (+ diagnostics export flows) -> **Diagnostics**
- `Settings` -> **Settings**
- **Machines** -> new top-level area (new capability, not currently represented)

## 5.3 Known consolidation targets
- Templates list/editor/details (merge)
- Assets (VHDX catalog + switch management)
- Logs/Diagnostics (separate from raw implementation pages)

---

## 6. Behavior Preservation Considerations for Migration

UI migration risk is not only layout. It is **hidden behavior loss**.

Examples of behaviors that must be preserved when moving to new UI:
- quick preflight auto-refresh on relevant field changes
- full preflight deploy gating
- deployment cancellation and terminal-state recovery
- cleanup/residual summaries
- guest-step skip reporting (`StepSkipped`, `skipReason`)
- diagnostics export and structured logging expectations

This is why the next deliverable after IA should be the **GUI Action Map**.

---

## 7. Proposed Documentation Sequence (Migration Prep)

### Phase 1 (current)
- Capability taxonomy (this doc + `capability-taxonomy.md`)
- Navigation IA draft (this doc)

### Phase 2
- Current UI / capability audit (what is duplicated, misplaced, missing, non-functional)

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

- Should `Machines` become the default landing page immediately in the new UI, or remain `Deploy` initially?
- How much of Hyper-V VM editing should be native LabAssistant UI vs shelling out/opening Hyper-V dialogs (if possible)?
- Should Diagnostics include a lightweight “Recent Issues” history view using the existing error feed service, or remain focused on export/logs initially?
- Should Assets later split into subcategories in top navigation if scope grows (for example, `Base Disks` and `Switches` as separate top-level items)?
- Final interaction pattern details for capability-scope dismissal (click-outside, pin, keyboard shortcuts) depend on framework choice (WPF refresh vs WinUI 3).
