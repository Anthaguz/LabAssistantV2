# UI Migration Preservation Matrix (Draft)

**Purpose:** Define what behavior and workflows must be preserved during future UI migration (for example a WinUI 3 / Windows 11-style redesign), what can be reorganized, and what is intentionally deferred or placeholder-only.

**Status:** Draft (Phase 4 of UI migration prep).

**Scope:** Current implemented capabilities plus planned near-term capability additions needed for launch-adjacent UX planning (including the future `Machines` area).

**Related:**
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/current-ui-capability-audit.md`
- `docs/03-architecture/gui-action-map.deploy.md`
- `docs/03-architecture/gui-action-map.templates.md`
- `docs/03-architecture/gui-action-map.assets.md`
- `docs/03-architecture/gui-action-map.settings-diagnostics-shell.md`

---

## 1. How to Use This Matrix

This matrix is a migration planning tool, not an implementation spec.

Use it to decide, for each feature/workflow:
- what must remain behaviorally identical
- what can move or be merged in the new UI
- what can be deferred without losing core product value
- what is placeholder-only and must be represented honestly
- what is a planned new capability (for example `Machines`) and therefore must be designed, not "migrated"

### Classification meanings
- **Preserve Exactly**: behavior/state transitions/side effects must remain the same unless a separate requirement change is approved.
- **Can Move / Merge**: behavior must be preserved, but screen placement/navigation grouping can change.
- **Can Improve Presentation Only**: same behavior, different layout/visual treatment allowed.
- **Defer**: not required for first migration cut, but track explicitly.
- **Placeholder Only**: visible in UI as planned capability, non-executable now.
- **New Capability**: not in current UI as a coherent workflow; requires new contract/milestone before implementation.

---

## 2. Top-Level Capability Matrix (Future IA)

## 2.1 `Machines` (future main page)

**Classification:** `New Capability` (major)

**Why**
- No current unified user workflow exists for host VM inventory/admin.
- Behavior cannot be migrated directly from a current page; it must be designed and implemented.

**Planned v1 scope (agreed)**
- List all Hyper-V VMs on host
- Basic edits (memory/CPU/switch) (scope B)
- Start/Stop (as part of core management actions)
- Delete VM with cleanup options
- Open Hyper-V console
- Open RDP (when reachable)

**Preserve-from-current dependencies (indirect)**
- Hyper-V service safety patterns
- structured logging/operationId
- cleanup expectations where destructive actions are offered
- diagnostics/error reporting patterns

**Must decide before implementation (future contract)**
- delete confirmation defaults + "always delete disks" setting semantics
- external VM labeling/origin tracking
- supported edit surface vs "open advanced settings" handoff to Hyper-V tools

## 2.2 `Deploy`

**Classification:** `Preserve Exactly` (behavior), `Can Improve Presentation Only` (layout/UX)

**Why**
- Deploy is the highest-risk behavior surface and now encodes Milestones R/U/W.

**Must preserve**
- Quick preflight on relevant changes (debounced)
- Full preflight on Deploy click before Hyper-V actions
- Blocking vs warning readiness semantics
- Deploy gating behavior
- Cancellation + cleanup + terminal states
- Outcome summary generation (global + per-VM)
- Guest-step selection/skip outcomes and summary/log visibility
- `Save as Template` bridge behavior

**Can change**
- Layout, density, panels, navigation chrome
- Visual presentation of readiness and summary sections
- Placement of logs/details panels
- How VM entries are displayed (cards/list/master-detail)

**Already addressed usability fix to preserve**
- Readiness details containment from `#242` (collapsible + internal scroll) solved a real usability issue; future redesign may replace the visual pattern, but must preserve usability outcome (VM list remains reachable).

## 2.3 `Templates`

**Classification:** `Can Move / Merge` (high priority), `Preserve Exactly` for schema/validation behavior

**Why**
- Current functionality exists but is fragmented across list/details/editor pages.
- Migration is an opportunity to unify workflow without changing core template behavior.

**Must preserve**
- Canonical schema handling and support-window compatibility behavior
- Load/save/import/export behavior and normalization
- validation panel semantics / field issue detection
- missing VHDX resolution workflows
- vmId preservation and metadata lifecycle
- structured logging/events for template operations

**Can move/merge**
- Template list/details/editor into one coherent Templates area
- File-open flows into library-driven flows
- Validation and missing-reference tools into side panels or tabs

**Current pain point explicitly targeted**
- Editing existing templates should not require filesystem browsing as the primary path.

## 2.4 `Assets`

**Classification:** `Can Move / Merge`, mixed maturity

**Why**
- VHDX catalog is mature enough to preserve behavior.
- Switch management is still placeholder/incomplete.
- Asset actions are embedded in Deploy/Templates and must remain accessible in-flow.

**Must preserve**
- VHDX catalog CRUD and save validation semantics
- VHDX integrity validation on catalog save/edit
- subset validation behavior (`itemsToValidate`) from `#219` follow-up stabilization
- embedded VHDX selection/import shortcuts in VM config flows
- missing VHDX resolution dialog + inline catalog import behavior

**Can move/merge**
- `VHDX Catalog` + `Switches` under top-level `Assets`
- dedicated pages + embedded dialogs reorganized under a more coherent asset navigation

**Placeholder / limited**
- `SwitchesPage` currently placeholder-grade and must be labeled honestly until CRUD exists

## 2.5 `Diagnostics`

**Classification:** `Can Move / Merge`, `Defer` (some UI), backend behavior is `Preserve Exactly`

**Why**
- Diagnostics backend capability is strong (structured logs, export, hardening)
- current UI representation (`LogsPage`) is transitional/misaligned

**Must preserve**
- structured JSONL as canonical diagnostics path
- diagnostics export service behavior and artifact expectations
- log rotation/retention semantics
- normalized runtime error metadata and path-context logging
- wrapper trace toggle behavior (`LABASSISTANT_POWERSHELL_WRAPPER_TRACE`)

**Can move/merge**
- Replace top-level `Logs` page with `Diagnostics`
- Add diagnostics export UI entry point(s)
- Add `#215` log viewer as a Diagnostics subview
- Potentially integrate shell "Recent Issues" concepts later

**Defer (acceptable)**
- `#215` in-app structured log viewer (read-only phase)
- richer diagnostics dashboards/history UI

## 2.6 `Settings`

**Classification:** `Can Improve Presentation Only`, some `Can Move / Split`

**Why**
- Current Settings behavior is functional and important, but layout/category organization can evolve.

**Must preserve**
- persisted path settings behavior (template/log/vm/disk/catalog paths)
- save vs reload semantics
- deployment policy flags (`PerVmFailFast`, `StopAllOnAnyVmFailure`)
- non-blocking optional steps policy persistence
- debug logger log-folder side effect when logs path changes (or a consciously equivalent replacement)

**Can move/split**
- one page -> subsections (General / Paths / Deploy Policy / Diagnostics)
- code-behind page -> ViewModel-driven UI

---

## 3. Cross-Cutting Behavior Preservation Matrix

These are not "pages," but they are migration-critical.

## 3.1 Global Error Feed / Shell Snacks

**Classification:** `Preserve Exactly` (behavioral semantics), `Can Improve Presentation Only`

**Must preserve**
- global active error/snack list (transient)
- dismiss behavior
- hover-pause expiry semantics (or an explicitly redesigned equivalent timing policy)
- recent error history list + total error count
- `View details` callback/deep-link behavior from publisher

**Can change**
- visual style
- shell placement
- compact vs expanded summary treatment
- whether recent history is partly surfaced in Diagnostics as long as shell-level behavior remains intentionally designed

## 3.2 Structured Logging + Diagnostics Context

**Classification:** `Preserve Exactly`

**Must preserve**
- `operationId` propagation
- structured event shapes and canonical event names (unless separately versioned/approved)
- path-context and normalized error metadata behavior
- diagnostics export compatibility assumptions

**Migration note**
- UI migration must not cause hidden regressions by bypassing logging/diagnostics entry points.

## 3.3 Cleanup / Cancellation / Outcome Summaries

**Classification:** `Preserve Exactly`

**Must preserve**
- cancellation semantics and operation states
- cleanup orchestration and residual tracking
- per-VM and global outcome summary behavior
- guest-step outcomes in per-VM summary (Milestone W)

**Can change**
- summary layout, visual grouping, and navigation path to details

## 3.4 Readiness / Preflight

**Classification:** `Preserve Exactly` (semantics), `Can Improve Presentation Only`

**Must preserve**
- quick vs full preflight behavior
- blocking vs warning semantics
- readiness result categories/codes/messages contract
- deploy gating based on full preflight

**Can change**
- how readiness results are visually presented
- panel layout/density
- placement inside the future Deploy workspace

---

## 4. Current Surface-by-Surface Migration Decisions

## 4.1 `DeployPage`
- **Classification:** `Preserve Exactly` behavior + `Can Improve Presentation Only`
- **Action:** Migrate as a behavior-rich workspace; redesign layout but preserve readiness/cancel/cleanup/summary/log behavior.
- **Reference:** `docs/03-architecture/gui-action-map.deploy.md`

## 4.2 `TemplatesPage` + `TemplateDetailsPage` + `TemplateEditorPage`
- **Classification:** `Can Move / Merge` (high priority consolidation)
- **Action:** Merge into one Templates area with integrated library/details/editor workflow.
- **Reference:** `docs/03-architecture/gui-action-map.templates.md`

## 4.3 `VhdxCatalogPage` + asset dialogs + embedded VHDX actions
- **Classification:** `Can Move / Merge`
- **Action:** Consolidate under `Assets`, preserve in-flow asset shortcuts in Deploy/Templates.
- **Reference:** `docs/03-architecture/gui-action-map.assets.md`

## 4.4 `SwitchesPage`
- **Classification:** `Placeholder Only`
- **Action:** Keep visible under `Assets` as limited/placeholder until CRUD is implemented.
- **Reference:** `docs/03-architecture/gui-action-map.assets.md`

## 4.5 `LogsPage`
- **Classification:** `Defer / Transitional`
- **Action:** Treat as transitional deployment-debug surface; likely retire or rehome after Diagnostics UI (`#215`) and Deploy diagnostics drill-down are stronger.
- **Reference:** `docs/03-architecture/gui-action-map.settings-diagnostics-shell.md`

## 4.6 `SettingsPage`
- **Classification:** `Can Improve Presentation Only` (behavior preserve), `Can Move / Split`
- **Action:** Keep settings semantics, reorganize UX later under future Settings IA.
- **Reference:** `docs/03-architecture/gui-action-map.settings-diagnostics-shell.md`

## 4.7 `MainWindow` Shell Navigation + Error Feed
- **Classification:** mixed
  - Navigation buttons: `Can Move / Merge`
  - Error feed/snacks: `Preserve Exactly` behavior + `Can Improve Presentation`
- **Action:** Replace nav IA with capability/context-scope model while preserving shell error feed semantics.
- **Reference:** `docs/03-architecture/gui-action-map.settings-diagnostics-shell.md`

---

## 5. Launch-Relevant Gaps and Deferrals (Explicit)

These are important to keep visible so migration planning does not accidentally assume they are complete.

## 5.1 Guest-step payload editors (follow-up to Milestone W)

**Classification:** `Defer` (feature completion), not a migration-only task

**Current state**
- Guest-step toggles exist
- Readiness blocks enabled-but-incomplete configs (correct)
- Payload editors are not yet implemented

**Implication**
- UI migration should not "paper over" this by pretending guest-step config editing exists.
- This needs a future feature milestone.

## 5.2 Guest network runtime implementation

**Classification:** `Placeholder Only`

**Current state**
- Visible in UI (network grouping preserved)
- Non-executable placeholder
- runtime emits explicit skip (`not_implemented`) when surfaced in guest-step outcomes

## 5.3 Logs UI (`#215`)

**Classification:** `Defer`

**Current state**
- Diagnostics backend is strong
- no in-app structured log viewer yet

**Planned direction**
- Diagnostics subview (read-only phase first)
- stable envelope columns + dynamic `context` details

## 5.4 `Machines` page (Hyper-V VM management)

**Classification:** `New Capability`

**Current state**
- not represented in current UI

**Implication**
- requires contract + milestone work
- should not be treated as a migration of an existing page

---

## 6. Migration Sequencing Guidance (Behavior Safety First)

Recommended order for future migration implementation planning:

1. **Design/contract the new shell + navigation IA**
   - capability scope vs context scope
   - top-level `Machines / Deploy / Templates / Assets / Diagnostics / Settings`

2. **Preserve and migrate Deploy behavior**
   - highest behavioral risk surface

3. **Consolidate Templates workflow**
   - biggest IA fragmentation pain point

4. **Consolidate Assets**
   - preserve embedded VHDX shortcuts and repair flows

5. **Rehome Settings + Diagnostics**
   - introduce Diagnostics as a real capability entry point
   - decide transitional fate of `LogsPage`

6. **Implement new `Machines` capability**
   - as a dedicated milestone, not a side effect of UI migration

---

## 7. Open Questions / Decisions to Revisit Later

- Should the future shell keep the global error feed as a persistent shell element, or reduce it after Diagnostics gains a richer issue/history view?
- When Diagnostics UI is introduced, should diagnostics export be available in both:
  - global Diagnostics area, and
  - contextual post-deploy/outcome actions?
- When `Machines` is implemented, how should VM origin/status be labeled (LabAssistant-created vs external vs unknown)?
- Should settings be split into multiple subpages at the first migration cut, or kept consolidated initially to reduce scope?
- What is the migration strategy for the transitional `LogsPage`:
  - temporary hidden developer surface,
  - user-visible "Deploy Debug" subview,
  - or full retirement once Diagnostics UI exists?
