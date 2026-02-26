# UI Migration Execution Plan (Behavior-Preserving Rollout) — Draft

**Purpose:** Translate the migration-planning artifacts (taxonomy, IA, audits, action maps, preservation matrix, code-path index) into an execution strategy for a future UI migration that preserves behavior while enabling a modern Windows 11-quality experience.

**Status:** Draft (Phase 6 of UI migration prep).

**Scope:** Planning and rollout strategy for UI migration and navigation re-architecture. This is **not** an implementation spec and does not yet select a framework (WPF modernized vs WinUI 3) as a final decision.

**Related:**
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/current-ui-capability-audit.md`
- `docs/02-ux/migration-preservation-matrix.md`
- `docs/02-ux/ui-framework-decision-rubric.md`
- `docs/03-architecture/gui-action-map.deploy.md`
- `docs/03-architecture/gui-action-map.templates.md`
- `docs/03-architecture/gui-action-map.assets.md`
- `docs/03-architecture/gui-action-map.settings-diagnostics-shell.md`
- `docs/03-architecture/code-path-index.md`

---

## 1. Why This Plan Exists

The project now has enough behavior and workflow depth that a UI migration can easily break functionality even if the new UI looks better.

This plan exists to prevent:
- behavior regressions hidden behind a visual redesign
- navigation reorganization that splits workflows again
- accidental loss of automatic/background behaviors (readiness, cleanup, summaries, diagnostics)
- "pretty but weaker" replacement UX

The migration target is not just visual modernization. It is:
- **better information architecture**
- **better usability for non-developer users**
- **preserved operational correctness**
- **better extensibility for future capabilities** (especially `Machines`)

---

## 2. Migration Principles (Non-Negotiable)

## 2.1 Behavior First, Visuals Second
- Behavior preservation is the primary constraint.
- Visual redesign is allowed to change layout and styling, but not hidden semantics unless a separate requirement change is approved.

## 2.2 Migrate by Capability, Not by "Page Count"
- The current UI pages reflect implementation history, not product architecture.
- Migration should follow the future capability model:
  - `Machines`
  - `Deploy`
  - `Templates`
  - `Assets`
  - `Diagnostics`
  - `Settings`

## 2.3 Preserve Automatic / Background Behaviors Explicitly
- Quick preflight debounce and refresh
- Full preflight gating
- cancellation / cleanup behavior
- outcome summary generation
- structured logging and diagnostics context
- shell error feed semantics

If a behavior is automatic today, it must be listed and preserved intentionally. It must not be "rediscovered" during implementation.

## 2.4 Separate "UI Migration" from "Feature Completion"
Examples:
- Guest-step payload editors are **feature completion**, not just migration
- `Machines` page is a **new capability**, not a migration of an existing page
- `#215` Logs UI is a diagnostics feature, not required to move shell navigation

The migration may include some feature completion work when needed for usability, but they must be tracked explicitly.

## 2.5 Prefer Parallel Safety Nets
- Keep existing behavior working while new UI surfaces are introduced incrementally
- Use feature flags / parallel routes / staged rollout (implementation choice later) when risk is high
- Validate with milestone-style checklists and scenario matrices

---

## 3. What "Success" Looks Like (Migration Definition of Done)

A migration phase is only successful when all of the following are true:
- Users can complete the target workflows in the new UI without losing capability
- Automated tests for preserved behavior remain green (or are replaced by equivalent coverage)
- Manual verification checklist for the migrated capability passes
- Structured logs and diagnostics still show expected operation behavior
- Existing critical semantics remain intact:
  - readiness/gating
  - cleanup/cancellation
  - summary visibility
  - guest-step skip/reporting
  - asset validation behavior

Visual improvements alone are not sufficient.

---

## 4. Decision Track: Framework / UI Stack (WPF vs WinUI 3)

This project should support a modern Windows 11 user experience, and **WinUI 3 is a reasonable target**. But framework choice should be made using an explicit decision gate, not momentum.

## 4.1 Recommendation (current planning stance)
- Do **not** finalize framework choice before migration behavior mapping and IA planning are complete (now largely complete).
- Run a small decision phase before implementation:
  - confirm constraints
  - prototype the shell/navigation model
  - evaluate migration cost vs UX gain

## 4.2 Framework Decision Criteria / Spike Scope / Decision Output Contract

Use the dedicated decision rubric/contract:
- `docs/02-ux/ui-framework-decision-rubric.md`

It defines:
- explicit rubric criteria and scoring expectations for `WPF modernized` vs `WinUI 3`
- required Y2 prototype spike scope (shell foundation only)
- required Y3 decision record outputs
- sufficiency/rework conditions for the Milestone Y decision path

## 4.3 Milestone Y decision track (Y1 -> Y3)

- **Y1 (`#251`)**: define rubric + spike scope + decision-record contract (docs only)
- **Y2 (`#252`)**: run shell foundation prototypes and collect evidence mapped to the rubric
- **Y3 (`#253`)**: finalize framework decision record with rationale, tradeoffs, risks, and first-slice recommendation

Framework choice should not be finalized until Y2 evidence covers shell composition ergonomics and Y3 addresses behavior-preservation risks explicitly.

---

## 5. Execution Strategy (Behavior-Preserving Rollout)

## 5.1 Recommended Rollout Shape

Use a staged migration plan, not a big-bang rewrite.

### Stage A — Architecture & Shell Contract (no major behavior changes)
- finalize top-level IA (`Machines`, `Deploy`, `Templates`, `Assets`, `Diagnostics`, `Settings`)
- define shell behavior:
  - capability scope (hamburger)
  - context scope (left panel)
  - shell error feed placement/behavior
- define page/surface ownership and navigation transitions

### Stage B — Highest-Risk Behavior Surface First: Deploy
- migrate/rebuild `Deploy` workspace first (or prototype it first)
- preserve:
  - readiness behaviors
  - deploy/cancel/cleanup semantics
  - outcome summaries and guest-step outcomes
- verify with existing Milestones R/U/W tests + targeted migration manual checklist

### Stage C — Templates Consolidation
- unify list/details/editor into one Templates area
- preserve schema/validation/missing-VHDX behavior
- remove file-system-hunting as the primary edit path

### Stage D — Assets Consolidation
- move VHDX catalog and future switch management under `Assets`
- preserve embedded asset shortcuts from Deploy/Templates
- preserve catalog validation and subset-validation semantics

### Stage E — Settings + Diagnostics Rehome
- introduce `Diagnostics` as a real top-level area
- decide transitional fate of `LogsPage`
- expose diagnostics export in user-facing UI
- preserve shell error feed behavior (or intentionally redesign with equivalent functionality)

### Stage F — Machines Capability (new)
- implement as its own feature/milestone(s), not as a migration side effect
- use new shell/IA and diagnostics patterns

This order minimizes behavior risk and addresses the most complex surface early.

---

## 6. Capability-by-Capability Migration Plan (Detailed)

## 6.1 Deploy (first migration implementation target)

**Why first**
- Most behavior-rich surface
- Highest migration risk
- Core user value path

**Must preserve from current implementation**
- All items listed in `docs/02-ux/migration-preservation-matrix.md` for `Deploy`
- `#242` usability outcome (readiness details no longer hide the VM list)

**Allowed improvements**
- modern Windows 11 visual layout
- better density management
- clearer grouping of:
  - VM list
  - readiness
  - outcomes
  - runtime logs
  - guest-step controls

**Migration risk hotspots**
- quick preflight race/debounce behavior
- command enable/disable state transitions
- cancellation/terminal recovery interactivity
- summary + guest-step outcome visibility
- error feed / deep-link behavior

**Verification baseline**
- Milestones R/U/W automated tests
- manual U/W checklists
- targeted Deploy migration checklist (new)

## 6.2 Templates (second migration implementation target)

**Goal**
- unify currently split template workflows into a coherent Templates area

**Current pain points to fix**
- list/details/editor split
- editor not naturally reachable from library workflow
- file-open often used as a workaround

**Must preserve**
- schema compatibility/migration behavior (Milestone Q)
- vmId preservation/lifecycle
- validation + missing VHDX resolution flows
- template logging/error-feed behavior distinctions

**Verification baseline**
- template load/save/import/export tests
- template schema compatibility tests
- manual template workflow checks (new migration checklist section)

## 6.3 Assets (third migration implementation target)

**Goal**
- centralize asset management while preserving in-flow shortcuts

**Must preserve**
- VHDX catalog CRUD + integrity validation
- subset validation behavior (post-`#219`)
- missing VHDX repair + inline import
- `VmConfigPanel` asset shortcuts in Deploy/Templates

**Must represent honestly**
- Switch management maturity (placeholder vs implemented)

**Verification baseline**
- catalog CRUD/integrity tests
- missing VHDX resolution tests/checks
- manual asset workflow checks

## 6.4 Settings + Diagnostics (fourth migration implementation target)

**Goal**
- align user-facing IA with actual diagnostics capability
- preserve shell/global support behaviors

**Must preserve**
- settings persistence semantics and side effects
- diagnostics export service behavior
- structured log assumptions
- shell error feed semantics (or approved equivalent)

**Decision required during this stage**
- whether `LogsPage` remains temporarily as a "Deploy Debug" subview or is retired after Diagnostics UI is introduced

## 6.5 Machines (new capability after shell/IA foundation)

**Goal**
- introduce Hyper-V VM administration as a first-class capability

**v1 scope (agreed)**
- all-host VM inventory
- basic edits
- start/stop
- delete with cleanup options
- console + RDP launch actions

**Why later**
- not a migration of an existing page
- depends on stable shell IA and preserved diagnostics/error patterns

---

## 7. Risk Register (UI Migration-Specific)

## 7.1 Behavioral Regression Risks (High)

### R1. Readiness semantics accidentally changed by UI rework
- **Risk:** Quick/full preflight and blocking/warn logic gets altered while redesigning Deploy surfaces.
- **Impact:** Deploy may start when it should block, or block when it should not.
- **Mitigation:**
  - treat readiness semantics as fixed contract (Milestone U + W)
  - test against existing UI/Business readiness suites
  - include explicit migration checklist for deploy gating behavior

### R2. Cleanup/cancellation visibility and control regressions
- **Risk:** New UI changes command states or terminal recovery behavior.
- **Impact:** User cannot retry, cancel state becomes inconsistent, cleanup status visibility degrades.
- **Mitigation:**
  - preserve Milestone R state model and tests
  - keep terminal-state UI recovery checks in migration validation

### R3. Hidden background behaviors lost during refactor
- **Risk:** debounce/versioning/auto-refresh behaviors are omitted because they are not obvious in the UI.
- **Impact:** stale readiness data, duplicate operations, noisy or missing updates.
- **Mitigation:**
  - use GUI action maps + code-path index during implementation
  - explicit "automatic behaviors" checklist for each migrated surface

## 7.2 IA / UX Risks (High)

### R4. Consolidation causes loss of in-flow shortcuts
- **Risk:** centralizing `Assets` or `Templates` removes embedded actions (VHDX import/select/repair) that users need in context.
- **Impact:** workflow becomes slower and more confusing.
- **Mitigation:**
  - preserve embedded shortcuts as contextual actions
  - centralize authority, not all interaction

### R5. Shell redesign weakens global issue surfacing
- **Risk:** shell error feed is removed or over-hidden before Diagnostics area is ready.
- **Impact:** users lose immediate feedback and deep-link ability.
- **Mitigation:**
  - preserve shell error feed behavior until an explicit replacement is approved
  - migration matrix marks this as preserve-exact behavior

## 7.3 Execution / Delivery Risks (Medium)

### R6. Big-bang rewrite stalls feature progress
- **Risk:** large rewrite branch diverges and blocks incremental improvements.
- **Mitigation:**
  - stage by capability
  - keep deliverables mergeable
  - use migration-specific checklists and milestone slices

### R7. Framework choice is made too early or too late
- **Risk:** committing to WinUI 3 without proving shell/workflow feasibility, or delaying decision until implementation blocks.
- **Mitigation:**
  - explicit framework decision gate (Section 4)
  - small shell/prototype spike if needed

---

## 8. Test and Verification Strategy During Migration

## 8.1 Reuse Existing Milestone Evidence

Use existing milestone tests/checklists as migration regression baselines:
- Milestone U (readiness + diagnostics)
- Milestone V (operational hardening)
- Milestone W (guest-step workflows)

These already capture behavior contracts that must survive the migration.

## 8.2 Add Migration-Specific Verification by Capability

For each migrated capability, add:
- **Automated regression mapping**
  - which existing tests must stay green
  - which new UI/ViewModel tests are needed
- **Manual migration checklist**
  - capability-specific UI behaviors
  - key cross-capability interactions
  - expected limitations (if any)

## 8.3 "Do Not Break" Checklist (Global)

Before merging any major migrated surface, explicitly verify:
- Readiness quick/full semantics unchanged
- Full preflight still blocks before Hyper-V actions
- Cancellation/cleanup behavior and terminal state recovery unchanged
- Per-VM/global summaries still surface required outcomes
- Guest-step outcomes still appear in logs and summaries
- Structured logging still emits expected events with `operationId`
- Diagnostics export still works (if touched)
- Global error feed snack/dismiss/deep-link behavior preserved (or consciously replaced)

## 8.4 Manual Real-Host Validation Remains Required

Some behaviors should continue to be validated manually on a real Hyper-V host:
- actual Hyper-V cmdlet behavior
- PowerShell process lifecycle quirks
- no-hang regression checks
- real deploy/cleanup side effects

Migration work must not assume UI-level test coverage replaces these checks.

---

## 9. Implementation Work Packaging (How to Break It Down)

Use small, reviewable milestones/issues. Avoid "UI rewrite" as a single task.

### Suggested packaging pattern
- **Contract / UX behavior spec** (docs-first, where behavior changes)
- **Shell/navigation foundation**
- **One capability migration slice**
- **Capability validation/checklist**
- Repeat

### Example future migration milestone sequence (illustrative)
- M1: Shell + navigation scope foundation (capability vs context scope)
- M2: Deploy workspace migration (behavior-preserving)
- M3: Templates consolidation migration
- M4: Assets consolidation migration
- M5: Diagnostics + Settings rehome
- M6: Machines capability v1 (new)

This is illustrative, not a committed roadmap yet.

---

## 10. What This Plan Intentionally Does Not Decide Yet

- Final UI framework choice (WPF modernized vs WinUI 3)
- Exact visual design system, tokens, typography, theming
- Exact component library
- Detailed `Machines` page contract/AC
- Guest-step payload editor UX design
- Final fate/timing of `#215` Logs UI

Those should be decided in dedicated issues/milestones with the behavior-preservation artifacts as input.

---

## 11. Immediate Next Steps (Recommended)

1. **Review and approve this execution strategy**
   - confirm migration order and risk posture

2. **Run Milestone Y framework decision track**
   - `#251` rubric/criteria contract
   - `#252` shell foundation prototypes
   - `#253` decision record

3. **Draft a migration shell contract**
   - capability scope vs context scope
   - shell error feed placement/behavior
   - navigation state ownership

4. **Prepare the first migration implementation milestone**
   - likely shell foundation + Deploy migration planning slice

5. **Create a future milestone for `Machines`**
   - contract-first, separate from UI migration mechanics

---

## Open Questions / TBDs

- `TBD:` Should Y2 prototypes be built in-repo (spike branches) or in a separate spike repo/workspace?
- `TBD:` Should the first implementation slice be shell foundation only, or shell foundation + Deploy in the same milestone?
- `TBD:` Should the transitional `LogsPage` be kept temporarily as a hidden developer/debug route during Diagnostics migration?
- `TBD:` When `Machines` is introduced, should console/RDP actions also appear as contextual quick actions in Deploy outcomes from day one or later?
