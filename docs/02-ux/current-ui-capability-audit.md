# Current UI to Capability Audit (Draft)

**Purpose:** Audit the current UI surfaces against the future capability taxonomy and navigation IA. This identifies duplication, miscategorization, partial implementations, and migration risks before UI redesign/migration work begins.

**Status:** Draft for migration planning (Phase 2 of UI migration prep).

**Related:**
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/ui-inventory.md`
- `docs/02-ux/user-flows.md`

---

## 1. Scope and Method

This audit focuses on:
- current top-level navigation and page structure (`MainWindow`)
- major visible pages and key dialogs
- capability placement vs intended taxonomy
- obvious UX/IA gaps (duplication, dead-end flows, placeholders)

This is **not** yet the detailed button-to-code-path map (that is Phase 3: GUI Action Maps).

---

## 2. Current Top-Level Navigation (Observed)

From `LabAssistant/MainWindow.xaml`, current top-level buttons are:
- `Deploy VMs`
- `Templates`
- `Template Editor`
- `VHDX Catalog`
- `Switches`
- `Settings`
- `Logs`

Default page on startup:
- `Deploy VMs` (`MainFrame.Navigate(new Views.DeployPage())`)

### High-level audit result
- The current top-level navigation mixes:
  - capabilities (`Deploy`, `Settings`)
  - workflow fragments (`Template Editor`)
  - asset subtypes (`VHDX Catalog`, `Switches`)
  - transitional tooling (`Logs`)
- This confirms the need for the future entity-based IA (`Machines`, `Deploy`, `Templates`, `Assets`, `Diagnostics`, `Settings`).

---

## 3. Current UI -> Future Capability Mapping (Audit)

## 3.1 Mapping Table (Top-Level Surfaces)

### Current: `Deploy VMs`
- **Future category:** `Deploy`
- **Status:** Strong fit
- **Notes:**
  - This is the primary provisioning workflow surface.
  - It now includes readiness/preflight, deployment execution, outcomes, guest-step controls, and summaries (Milestones U/W).
  - It is behavior-rich and high migration risk (needs detailed action map first).

### Current: `Templates`
- **Future category:** `Templates`
- **Status:** Partial / fragmented fit
- **Notes:**
  - `TemplatesPage` loads and lists templates and opens `TemplateDetailsPage` on double-click.
  - It supports view/detail and VHDX mapping workflows via `TemplateDetailsPage`.
  - It does **not** provide an integrated edit path to `TemplateEditorPage`.
  - This creates a split workflow and forces users into separate navigation or filesystem-based editor usage.

### Current: `Template Editor`
- **Future category:** `Templates`
- **Status:** Misplaced as separate top-level entry
- **Notes:**
  - `TemplateEditorPage` is functionally powerful (open/save/save-as, validation, missing VHDX resolution, VM editing).
  - It is currently isolated from the template list/library workflow.
  - Users may need to manually open template files from disk to edit an existing template.
  - This is the clearest consolidation target in the current UI.

### Current: `VHDX Catalog`
- **Future category:** `Assets`
- **Status:** Good feature fit, wrong top-level granularity
- **Notes:**
  - Full CRUD behavior exists (`VhdxCatalogPage` + dialog/viewmodel).
  - Integrity validation and catalog save validation are integrated.
  - This belongs under `Assets` rather than as a standalone top-level category.

### Current: `Switches`
- **Future category:** `Assets`
- **Status:** Placeholder / incomplete
- **Notes:**
  - `SwitchesPage` is currently a placeholder surface (title-only page).
  - Top-level nav implies a complete management feature that does not exist yet.
  - This should move under `Assets` and remain clearly labeled as limited/placeholder until implemented.

### Current: `Settings`
- **Future category:** `Settings`
- **Status:** Good fit (with transitional content)
- **Notes:**
  - Contains app paths and deployment policy settings.
  - Still contains legacy/transitional optional-step policy settings that overlap conceptually with newer guest-step controls/readiness behavior.
  - Needs later UX consolidation, but top-level placement is correct.

### Current: `Logs`
- **Future category:** `Diagnostics`
- **Status:** Transitional / misaligned
- **Notes:**
  - `LogsPage` binds directly to `DeploymentViewModel`, effectively acting like a legacy/alternate deployment workflow/log view.
  - It is not the same as the canonical structured diagnostics flow introduced in Milestones S/V.
  - Top-level `Logs` does not reflect the broader Diagnostics capability (diagnostics export + structured logs + future in-app log viewer).

---

## 3.2 Missing Top-Level Capability (Planned)

### Missing: `Machines`
- **Future category:** `Machines`
- **Status:** Not represented in current top-level nav
- **Impact:**
  - Core Hyper-V VM administration has no dedicated user-facing home
  - VM lifecycle management is currently implicit/indirect (deploy-driven, services-level, or external tools)
- **Migration implication:**
  - This is not just a page move; it is a new primary capability area and likely a future major milestone

---

## 4. Major Workflow Fragmentation and IA Problems (Current State)

## 4.1 Templates Workflow is Split Across Separate Pages (High Priority IA Gap)

### What exists now
- `TemplatesPage`
  - list templates
  - reload
  - open `TemplateDetailsPage`
- `TemplateDetailsPage`
  - inspect VM entries
  - select VHDX mappings for selected VM
  - missing VHDX resolution dialog path
- `TemplateEditorPage`
  - open/save/save-as template files
  - edit template and VM details
  - validation and missing VHDX resolution paths

### What is missing in the UX
- No integrated "Edit template" action from the template list/details workflow
- No unified template library + details + editor flow
- Users can end up editing by browsing for files manually

### Migration implication
- `Templates` must be treated as a unified workflow in the new IA, not a page collection

---

## 4.2 Deploy Page is the Functional Center but Also an IA Density Hotspot

### What exists now (high-level)
- deployment configuration
- VM config panel(s)
- readiness/preflight panel (quick/full)
- deploy gating
- runtime progress
- per-VM outcomes (including guest-step outcomes)
- logs and summaries

### Why this matters
- This page is now the richest behavior surface in the app
- Migration risk is high because it contains many automatic/background behaviors
- Layout pressure already caused a usability issue (`#242`) which required an interim fix

### Migration implication
- Deploy page gets first GUI Action Map in Phase 3

---

## 4.3 Assets are Split by Resource Type and Maturity Level

### What exists now
- `VhdxCatalogPage`: implemented CRUD + validation
- `SwitchesPage`: placeholder surface only
- Additional asset-related actions embedded in other flows:
  - `VmConfigPanel` can launch VHDX catalog add/select flows
  - missing VHDX resolution dialogs can import/select catalog entries

### IA issue
- Asset management is partially centralized and partially embedded in task flows
- Top-level `Switches` overstates current capability maturity

### Migration implication
- Consolidate under `Assets`
- Keep embedded task-local asset actions (for workflow efficiency), but make the central asset home authoritative

---

## 4.4 Diagnostics is Conceptually Bigger Than the Current `Logs` Page

### Current reality
- Canonical diagnostics path is structured logging + diagnostics export (Milestones S/V)
- `LogsPage` is a page bound to `DeploymentViewModel`, not a full diagnostics center
- Operational hardening (rotation, metadata normalization, wrapper traces) exists but is not clearly represented in UI IA

### IA issue
- Current `Logs` entry reflects an older implementation surface, not the product capability

### Migration implication
- Replace top-level `Logs` with `Diagnostics`
- Later add `#215` log viewer under Diagnostics, not as a standalone top-level page

---

## 4.5 Global Error Feed / Snack Surface Is Cross-Cutting but Unclassified

### Current reality
- `MainWindow` hosts a global error feed summary + active snack cards
- This surface exists across all pages and supports "View details" / dismiss actions
- It is functionally important but not represented in navigation IA

### IA implication
- Treat as cross-cutting UI shell behavior (not top-level category)
- It should later map into Diagnostics and/or contextual drill-down flows, but remain available globally as a shell component if useful

---

## 5. Capability Coverage Snapshot (Current vs Planned)

This section classifies each future capability area by current coverage.

### Machines
- **Current coverage:** Not implemented as a unified user workflow
- **Hidden/partial support exists in:** services, deploy runtime behavior
- **Priority implication:** major future milestone candidate

### Deploy
- **Current coverage:** Strong
- **Maturity:** high (Milestones R/U/W)
- **Main work left:** payload editors / polish / future deploy-related UX refinement

### Templates
- **Current coverage:** Medium (capabilities exist but fragmented)
- **Main gap:** unified template library/editor workflow

### Assets
- **Current coverage:** Medium
- **Implemented:** VHDX catalog
- **Placeholder/limited:** Switches
- **Main gap:** consolidated IA + switch CRUD maturity

### Diagnostics
- **Current coverage:** Strong backend/services, weak UI IA
- **Implemented:** diagnostics export, structured logs, hardening
- **Deferred UI:** `#215` in-app structured log viewer

### Settings
- **Current coverage:** Good
- **Main gap:** future reorganization as capabilities grow (especially operational defaults and transitional options)

---

## 6. Migration Risk Areas (UI/Behavior)

These are the highest-risk areas to migrate without behavior mapping:

### 6.1 Deploy page behavior density
- readiness auto-refresh
- full-preflight gating
- cancellation/cleanup/outcomes
- guest-step controls + readiness completeness
- per-VM summary and step outcomes

### 6.2 Template lifecycle fragmentation
- list/details/editor split
- missing VHDX resolution flows in more than one place
- file-based editing paths

### 6.3 Embedded asset actions in task flows
- `VmConfigPanel` includes catalog interactions that cross into Assets capability
- migration must preserve efficient in-flow asset operations while improving IA

### 6.4 Shell-level global error feed behavior
- cross-page snack/feed interactions need explicit mapping during migration

---

## 7. Immediate Planning Implications (After This Audit)

Based on this audit, the next migration-prep artifact should be:

### Phase 3A (first): Deploy GUI Action Map
Reason:
- highest behavior density
- highest regression risk
- recent Milestones R/U/W added substantial automatic behavior

### Phase 3B (next): Templates GUI Action Map
Reason:
- major workflow fragmentation
- central consolidation target in the future UI

---

## 8. Open Questions / TBDs (Audit Follow-Up)

- Should the current `LogsPage` be treated as a temporary deployment-debug page in documentation, or reframed immediately as a transitional Diagnostics surface?
- How much of the global error feed should survive in the future shell vs move into Diagnostics/contextual panels?
- Should `SwitchesPage` remain visible as a top-level placeholder in the current UI until Assets consolidation work starts, or be demoted/hidden earlier?
- When Machines is introduced, should selected Deploy-created VMs have direct handoff actions into the Machines area (future UX decision)?
