# Capability Taxonomy (Draft)

**Purpose:** Define the application's feature/capability map from a user and product perspective (not from current implementation pages). This is the foundation for future navigation redesign and UI migration.

**Status:** Draft for UI migration planning (post-Milestones U, V, W).

---

## 1. Why This Exists

LabAssistant has grown beyond a single deploy utility. Features now span:
- deployment orchestration
- template authoring and validation
- base disk asset management
- operational diagnostics
- Hyper-V administration (planned expansion)

The current UI exposes many of these capabilities, but they are split, duplicated, or miscategorized (for example, template list and template editor are separate and incomplete workflows).

This taxonomy defines the **product capability model** first, so future UI migration (including potential WinUI 3 migration) preserves behavior while improving navigation and usability.

---

## 2. Taxonomy Principles

### 2.1 Top-Level Navigation Style
- **Top-level navigation should be entity-based** (user-friendly).
- **Actions/tasks live inside each entity area** (task-oriented subviews/actions).

This is a hybrid IA strategy:
- stable top-level categories users can learn
- task efficiency inside each category

### 2.2 Capability vs UI
- This taxonomy describes **what the product can do**.
- It does **not** assume the current UI page structure is correct.
- Current page layout/navigation is treated as an implementation detail that may be replaced.

### 2.3 Migration Safety
- Each capability should later map to:
  - user-facing entry points
  - code paths / workflows
  - side effects (Hyper-V, filesystem, logs)
- This supports safe UI migration without functional regressions.

---

## 3. Top-Level Capability Categories (Future)

## 3.1 Machines (Primary Hyper-V VM Management)

**Intent:** Main operational page for managing Hyper-V virtual machines on the host.

**Scope direction (v1, agreed):**
- List all Hyper-V VMs on host (not only LabAssistant-created VMs)
- Basic VM operations and editing
- Delete with configurable disk cleanup behavior

**Planned v1 operations (Machines page scope B)**
- List VMs on host
- Inspect VM details / state
- Start / stop VMs (as supported by existing services)
- Delete VM registration
- Delete VM with optional disk/file cleanup
- Edit basic VM settings:
  - CPU
  - memory
  - switch attachment
  - basic Hyper-V VM properties (custom UI where practical)

**Delete behavior (agreed)**
- Default UX should offer delete options (VM-only vs VM+disk cleanup)
- App setting may allow users to configure **always delete disks**

**Important future distinction**
- VM origin/status labels should be supported (for example):
  - LabAssistant-created
  - External/host VM
  - Unknown/untracked

**Current implementation status**
- Not implemented as a unified page yet
- Some overlapping capabilities exist inside Deploy flow and Hyper-V services

---

## 3.2 Deploy (Provisioning / Execution)

**Intent:** Configure and run deployments (on-the-fly or from templates), with readiness checks, progress, cleanup, and outcomes.

**Capabilities**
- Configure on-the-fly VM deployment
- Configure on-the-fly multi-VM/lab deployment
- Deploy from template
- Save current deploy configuration as template
- Readiness/preflight checks (quick/full)
- Deploy progress + per-VM status
- Cancellation
- Cleanup and residual reporting
- Final outcome summaries (global + per-VM)
- Guest-step selection/execution controls (Milestone W)

**Current implementation status**
- Implemented and mature (Milestones R/U/W)
- Main deploy workflow is a core strength of the current app

**Known UI concern**
- Readiness panel usability issue addressed by `#242` (interim fix, not redesign)

---

## 3.3 Templates (Template Authoring + Management)

**Intent:** Unified workflow for template lifecycle management.

**Capabilities**
- List templates
- Create template
- Edit template
- Delete template
- Import/export template JSON
- Schema validation and compatibility handling
- Missing VHDX resolution
- Template details/review
- VM-level configuration within templates

**Current implementation status**
- Capability exists in parts, but current UI is fragmented:
  - template list page and template editor are separate/incomplete workflows

**Migration objective**
- Consolidate list + editor + details into one coherent Templates area

---

## 3.4 Assets (Shared Deployment Resources / Hyper-V Assets)

**Intent:** Manage reusable/shared resources used by deployments and templates.

**Capabilities (current + planned)**
- Base Disk Catalog (VHDX) CRUD
  - add/import
  - edit metadata
  - remove from catalog
  - integrity validation
- Virtual Switch management (planned expansion)
  - list
  - create/edit/delete (future)
- Asset validation/health status (future)
- Resource mappings / substitution support (template compatibility workflows)

**Naming (agreed)**
- Top-level label: **Assets**

**Current implementation status**
- VHDX Catalog is implemented
- Switches page exists (partial/limited)
- Full Hyper-V asset management not yet consolidated

---

## 3.5 Diagnostics

**Intent:** Troubleshooting and operational observability for support users.

**Capabilities**
- Diagnostics export bundle
- Structured JSONL logs (canonical diagnostics path)
- Debug logs (supplemental)
- (Future) In-app structured log viewer (`#215`)

**Current implementation status**
- Diagnostics export implemented
- Structured logging and operational hardening implemented (Milestones S/V)
- Logs page exists but is not the future target UX for structured diagnostics

---

## 3.6 Settings

**Intent:** Application-wide behavior and environment configuration.

**Capabilities**
- Storage/log paths
- Deployment failure policy settings (app-wide)
- Optional/non-blocking step policy settings (legacy/current)
- Future operational defaults (for example delete-with-disk behavior on Machines page)
- Trace/debug settings (where appropriate)

**Current implementation status**
- Implemented page with app settings
- Some settings still represent transitional behavior that may evolve as guest-step model matures

---

## 4. Cross-Cutting Capability Areas (Not Top-Level by Default)

These are important capabilities but should generally appear **within** top-level categories instead of as standalone top-level nav entries.

### 4.1 Validation / Readiness
- Deploy readiness preflight (quick/full)
- Template validation
- Catalog VHDX integrity validation
- Guest-step configuration completeness validation

Recommended placement:
- **Deploy** (deployment readiness)
- **Templates** (template validation)
- **Assets** (catalog validation)

### 4.2 Execution Outcomes / Error Reporting
- Per-VM outcomes
- Cleanup/residual reporting
- Structured runtime diagnostics

Recommended placement:
- Primary visibility in **Deploy**
- Deep troubleshooting under **Diagnostics**

### 4.3 Import / Export
- Template import/export
- Diagnostics export
- (Future) asset import/export patterns

Recommended placement:
- In the relevant entity section, not a top-level “Import/Export” page

---

## 5. Capability-to-User Operation Inventory (High-Level)

This section groups the user operations you called out (and adjacent ones) into the taxonomy.

### Machines
- List Hyper-V VMs on host
- Edit VM basic configuration
- Delete VM (VM only / VM + disks)
- Attach additional disks (future within Machines)
- Attach additional switches/NICs (future within Machines)
- Open advanced Hyper-V config UI (MMC integration) if feasible

### Deploy
- Deploy VM on-the-fly
- Deploy multi-VM lab on-the-fly
- Deploy VM from template
- Deploy lab from template
- Save current deployment config as template
- Configure guest-step execution selections
- Review readiness failures/warnings before deploy
- Cancel deployment

### Templates
- CRUD templates
- Import/export templates
- List templates
- Edit template VM configurations
- Resolve missing VHDX references

### Assets
- CRUD base disks (VHDX catalog)
- Validate base disks
- CRUD virtual switches (future)
- Manage shared deployment assets/mappings (future)

### Diagnostics
- Export diagnostics bundle
- Inspect structured logs (future Logs UI)
- Access raw logs (power-user path)

### Settings
- App-wide deployment policies
- Storage paths
- Logging paths
- Future default delete behavior for Machines page

---

## 6. Current UI Mismatch Themes (Preliminary)

This is not the full UI audit yet (that is a later phase), but these are already known taxonomy/navigation mismatches:

### 6.1 Templates are split across multiple disconnected surfaces
- Template list and template editing are not a coherent workflow
- Users may need filesystem access to complete tasks that should be in-app

### 6.2 Deploy page carries too much mixed responsibility
- Deploy configuration, readiness, execution, outcomes, and guest-step visibility all coexist (functional but dense)
- The page is behavior-rich and migration-risky (needs detailed action map)

### 6.3 Assets are not fully consolidated
- VHDX Catalog and Switches exist separately, but the product direction suggests a unified Assets area

### 6.4 Diagnostics UX is transitional
- Logging/diagnostics infrastructure is strong, but UI access and navigation are not yet product-grade

### 6.5 Future Machines capability has no primary home yet
- Hyper-V VM administration operations exist in services/pipeline behavior but not as a user-facing entity-centric workflow

---

## 7. Future Navigation Implications (Summary)

This taxonomy implies:
- A future **Machines** page should become the primary landing page (or at least a primary top-level destination), not just Deploy.
- **Deploy** should focus on provisioning workflows, not general VM administration.
- **Templates** should unify list/editor/details/import/export into one workflow area.
- **Assets** should unify VHDX catalog and future switch management.
- **Diagnostics** should become a user-meaningful troubleshooting area (with Logs UI later).

Detailed navigation behavior is defined in `docs/02-ux/navigation-ia-draft.md`.

---

## 8. Open Questions / TBDs (for later phases)

- Exact Machines page v1 operation list (which basic edit actions are in custom UI vs delegated to Hyper-V UI/MMC)
- Whether Machines becomes the default landing page immediately or after the new UI migration
- How to represent VM origin/trust labels in UX (LabAssistant-managed vs external)
- Whether Diagnostics should include a lightweight “Recent Issues” dashboard or remain mostly export/log access in v1
- Whether Assets should later split into subcategories (Base Disks, Switches, ISOs, etc.) as scope grows
