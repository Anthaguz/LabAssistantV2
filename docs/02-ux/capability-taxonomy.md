# Capability Taxonomy (Draft)

**Purpose:** Define the product capability map from a user/product perspective (not the current page layout). This is the foundation for the future UI migration and navigation redesign.

**Status:** Draft for migration planning (post-Milestones U, V, W).

**Related:**
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/ui-inventory.md`
- `docs/02-ux/user-flows.md`

---

## 1. Why This Exists

LabAssistant now spans multiple capability domains:
- deployment orchestration
- template authoring and validation
- reusable asset management (base disks, switches)
- diagnostics and support workflows
- guest-step configuration and execution controls
- planned Hyper-V VM administration (Machines page)

The current UI exposes many of these capabilities, but several are split, duplicated, or miscategorized (for example, template list vs template editor). This taxonomy defines the intended product model first so the UI migration can preserve behavior while improving usability and discoverability.

---

## 2. Taxonomy Principles

### 2.1 Navigation Style (Product-Level)
- Top-level navigation should be **entity-based** (user-friendly and stable).
- Actions/tasks should live **inside** each entity area (task-oriented subviews/actions).

This is a hybrid IA strategy:
- stable top-level categories users can learn
- efficient workflows within each category

### 2.2 Capability vs Current Screens
- This taxonomy describes **what the product can do**.
- It does not assume the current pages are the correct long-term structure.
- Current page layout is implementation history, not the target IA.

### 2.3 Migration Safety Requirement
Each capability should later map to:
- user entry points
- code/workflow paths
- automatic/background behaviors
- side effects (Hyper-V, filesystem, logs, diagnostics)

This is required for safe UI migration without behavior loss.

---

## 3. Top-Level Capability Categories (Future)

## 3.1 Machines (Primary Hyper-V VM Management)

**Intent:** Main operational area for managing Hyper-V VMs on the host.

**Agreed direction (v1 scope B)**
- Manage **all Hyper-V VMs on the host** (not only LabAssistant-created VMs)
- Provide basic VM operations and edits
- Provide delete flows with configurable disk cleanup behavior

**Planned v1 operations**
- List VMs on host
- Inspect VM details and state
- Start/Stop VMs (where supported by current services)
- Delete VM registration
- Delete VM with optional disk/file cleanup
- Edit basic VM settings:
  - CPU
  - memory
  - switch attachment
  - basic Hyper-V properties (custom UI where practical)

**Delete behavior (agreed)**
- Default UX offers delete options (VM only vs VM + disk cleanup)
- App setting may allow "always delete disks"

**Future UX requirement**
- VM origin/status labels should be supported:
  - LabAssistant-created
  - External/host VM
  - Unknown/untracked

**Current implementation status**
- No unified Machines page yet
- Some overlapping capabilities exist in services and deploy flows

---

## 3.2 Deploy (Provisioning and Execution)

**Intent:** Configure and run provisioning workflows (single VM or lab), with readiness checks, execution tracking, cancellation, cleanup, and outcomes.

**Capabilities**
- Configure on-the-fly VM deployment
- Configure on-the-fly multi-VM/lab deployment
- Deploy from template
- Save current deployment config as template
- Readiness/preflight checks (quick/full)
- Deployment progress and per-VM status
- Cancellation
- Cleanup and residual reporting
- Global/per-VM outcome summaries
- Guest-step selection/execution controls

**Current implementation status**
- Strong and mature (Milestones R/U/W)

**Known UX concern**
- Readiness panel usability issue was mitigated by `#242` (interim containment fix, not redesign)

---

## 3.3 Templates (Template Authoring and Management)

**Intent:** Unified workflow for the template lifecycle (author, validate, manage, import/export).

**Capabilities**
- List templates
- Create template
- Edit template
- Delete template
- Import/export template JSON
- Schema validation and compatibility handling
- Missing VHDX resolution
- Template details/review
- VM-level template configuration

**Current implementation status**
- Capability exists in parts
- Current UI is fragmented (template list and template editor are separate/incomplete workflows)

**Migration objective**
- Consolidate list + details + editor into one Templates area

---

## 3.4 Assets (Shared Deployment Resources / Hyper-V Assets)

**Intent:** Manage reusable/shared resources used by deployments and templates.

**Naming (agreed)**
- Top-level label: **Assets**

**Capabilities (current + planned)**
- Base Disk Catalog (VHDX) CRUD
  - add/import
  - edit metadata
  - remove from catalog
  - integrity validation
- Virtual Switch management (planned expansion)
  - list
  - create/edit/delete
- Asset validation/health status (future)
- Resource mappings/substitutions for compatibility workflows (future)

**Current implementation status**
- VHDX Catalog implemented
- Switches page exists (partial/limited)
- Not yet consolidated into one coherent Assets area

---

## 3.5 Diagnostics

**Intent:** Troubleshooting and support workflows.

**Capabilities**
- Diagnostics export bundle
- Structured JSONL logs (canonical diagnostics path)
- Debug logs (supplemental path)
- Future in-app structured log viewer (`#215`)

**Current implementation status**
- Diagnostics export and structured logging implemented (Milestones S/V)
- UI access is transitional and not yet product-grade

---

## 3.6 Settings

**Intent:** App-wide behavior and environment configuration.

**Capabilities**
- Storage/log paths
- Deployment policies
- Logging/diagnostics defaults
- Future operational defaults (for example, Machines page delete-with-disk preference)
- Trace/debug toggles (where exposed)

**Current implementation status**
- Implemented page exists
- Some settings represent transitional behavior and may evolve

---

## 4. Cross-Cutting Capability Areas (Not Top-Level by Default)

These are important capabilities, but they should usually appear inside entity areas rather than as top-level navigation entries.

### 4.1 Validation / Readiness
- Deploy readiness preflight (quick/full)
- Template validation
- Catalog VHDX integrity validation
- Guest-step configuration completeness validation

**Recommended placement**
- `Deploy` for deploy readiness
- `Templates` for template validation
- `Assets` for catalog/asset validation

### 4.2 Execution Outcomes / Error Reporting
- Per-VM outcomes
- Cleanup/residual reporting
- Structured runtime diagnostics

**Recommended placement**
- Primary visibility in `Deploy`
- Deep troubleshooting in `Diagnostics`

### 4.3 Import / Export
- Template import/export
- Diagnostics export
- Future asset import/export

**Recommended placement**
- In the relevant entity area, not a top-level "Import/Export" page

---

## 5. Capability-to-User Operation Inventory (High-Level)

This groups concrete user operations into the future taxonomy.

### Machines
- List Hyper-V VMs on host
- Inspect VM state/details
- Edit basic VM configuration
- Start/Stop VMs
- Delete VM (VM only / VM + disks)
- Attach additional disks (future)
- Attach additional NICs/switches (future)
- Open advanced Hyper-V configuration UI (if feasible)

### Deploy
- Deploy VM on-the-fly
- Deploy multi-VM lab on-the-fly
- Deploy VM from template
- Deploy lab from template
- Save deployment config as template
- Configure guest-step execution selections
- Review readiness failures/warnings
- Cancel deployment

### Templates
- CRUD templates
- List/search templates
- Edit template VM configurations
- Import/export templates
- Resolve missing VHDX references

### Assets
- CRUD base disks (VHDX catalog)
- Validate base disks
- CRUD virtual switches (future)
- Manage shared deployment assets/mappings (future)

### Diagnostics
- Export diagnostics bundle
- Access logs (raw files today, in-app viewer later)
- Review structured operation traces (future UI)

### Settings
- Configure app-wide deployment policies
- Configure storage and logging paths
- Configure operational defaults (future Machines delete preference)

---

## 6. Current UI Mismatch Themes (Preliminary)

This is a preview of the later UI audit, not the full audit.

### 6.1 Templates are split across disconnected surfaces
- Template list and template editing are not a coherent workflow
- Users may need filesystem access for tasks that should be in-app

### 6.2 Deploy page carries dense mixed responsibilities
- Configuration, readiness, execution, outcomes, and guest-step visibility all coexist
- Functional but dense and migration-risky

### 6.3 Assets are not consolidated yet
- VHDX Catalog and Switches are separate surfaces, but the product direction suggests a unified Assets area

### 6.4 Diagnostics UX is transitional
- Logging/diagnostics internals are strong, but navigation and UI presentation are not yet aligned to a polished product UX

### 6.5 Machines capability has no primary home yet
- Hyper-V administration behavior exists in services and deploy paths, but not as a user-facing entity workflow

---

## 7. Navigation Implications (Summary)

This taxonomy implies:
- `Machines` should become a primary top-level destination in the future UI
- `Deploy` should focus on provisioning workflows, not general VM administration
- `Templates` should unify list/editor/details/import/export into one area
- `Assets` should unify base disks and future switch management
- `Diagnostics` should become a user-meaningful troubleshooting area (with `#215` later)

Detailed navigation behavior is defined in `docs/02-ux/navigation-ia-draft.md`.

---

## 8. Open Questions / TBDs

- Exact Machines page v1 action list (what is native LabAssistant UI vs delegated to Hyper-V UI/MMC)
- Whether `Machines` becomes the default landing page immediately or after UI migration stabilizes
- How to represent VM origin/trust labels in UX (LabAssistant-managed vs external)
- Whether Diagnostics should include a lightweight "Recent Issues" dashboard/history in v1
- Whether Assets later splits into subcategories in top navigation as scope grows (Base Disks, Switches, ISOs, etc.)
