# WinUI Shared Seam Ownership

**Purpose:** Define the current authoritative ownership rules for shared WinUI seams so reusable helpers and coordinators do not become convenience dumping grounds.

**Status:** Current authoritative WinUI architecture rule.

**Current scope:** Deploy shared seams only.

**Absorbs:**
- `docs/02-ux/Archived/winui-deploy-results-panel-coordinator-contract-an.md`
- `docs/02-ux/Archived/winui-deploy-shared-helper-ownership-contract-an.md`

**Source basis:**
- `docs/01-requirements/srs.md` (`FR-182`..`FR-188`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-045`, `AC-046`)
- `docs/03-architecture/winui-lane-architecture.md`
- `docs/03-architecture/winui-shell-bootstrap-runtime.md`

## 1) Core Shared-Seam Rule

A shared seam may be reusable without becoming a shared workflow owner.

Current shared Deploy seams are treated as:
- narrow panel-intent integration seams
- or capability-shared, lane-triggered helper seams

Lane-local owners or later typed runtimes still own:
- invocation timing
- workflow meaning
- status and remediation messaging
- post-invocation orchestration
- route-activation refresh or reconcile behavior

## 2) `DeployResultsPanelCoordinator`

### May own
- shared Deploy right-panel intent aggregation
- active-lane title selection
- active-lane auto-open recommendation
- Overview empty-state participation for the shared Deploy panel region
- delegation of shell-owned panel visibility and compact-unavailable state into participating lane-local seams
- capability-switch reset hooks that are specifically about Deploy panel presentation

### Must not own
- shell panel open state, width, compact fallback, container visibility, or lifecycle
- lane-local readiness, progress, result-row construction, or issue-row construction
- lane-local launcher semantics beyond reading already-exposed lane intent
- route resolution or route switching
- general shared Deploy helper coordination

It is a narrow shared Deploy panel-intent seam, not a general shared Deploy owner.

## 3) `DeployReferenceDataService`

### Classification
- capability-shared
- lane-triggered

### May own
- loading Deploy reference data that is genuinely shared across Deploy lanes
- cached snapshot exposure for settings, switch inventory, catalog items, and catalog options
- a narrow `EnsureAsync(forceRefresh)` boundary

### Must not own
- route-activation policy
- lane refresh timing or reconcile sequencing
- lane-local default selection behavior
- readiness sequencing or re-run policy
- remediation messaging
- shell or panel coordination

## 4) `DeployResolveSuggestionsService`

### Classification
- capability-shared
- lane-triggered

### May own
- deterministic shared Deploy-side suggestion rules
- applying those suggestions to a caller-supplied template using caller-supplied reference data
- returning an applied-count summary

### Must not own
- reference-data loading or refresh
- invocation timing
- readiness re-evaluation
- lane-local messaging
- template-editor launch behavior
- route or shell interaction

## 5) `DeployTemplateEditorLauncher`

### Classification
- capability-shared
- lane-triggered

### May own
- the narrow handoff from Deploy into the Templates editor seam
- forwarding a caller-supplied `TemplateEditorDocument` and caller-supplied status text into that seam

### Must not own
- deciding when a lane opens the editor
- document construction
- lane validation or correction policy
- template-library loading or reconciliation policy
- route ownership, shell ownership, or general Templates coordination

## 6) Explicit Invalidation Rule

`DeployReferenceDataService` invalidation remains caller-owned.

Current rule:
- callers may request `EnsureAsync(forceRefresh: true)` when they own a concrete refresh trigger
- otherwise cached/shared snapshot behavior remains acceptable
- there is no implicit same-session auto-invalidation guarantee across Assets, Templates, or other app state

If stronger freshness guarantees become necessary, update this doc and the requirements docs in a new narrow slice.

## 7) Future Shared Seams

If a later reusable seam is expected to survive beyond one milestone:
- extend this file
- do not create a new milestone-coded shared-seam contract as the only authority

## Open Questions / TBDs

- `TBD:` Whether a later typed `DeployCapabilityRuntime` should own all current Deploy shared seams directly.
- `TBD:` Which non-Deploy shared seams, if any, are mature enough to be promoted into this canonical doc next.
