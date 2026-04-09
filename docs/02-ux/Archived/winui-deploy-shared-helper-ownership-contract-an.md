# WinUI Deploy Shared Helper Ownership Contract (AN4)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define ownership limits for shared Deploy helpers so capability-shared helper seams do not become a sink for random lane logic as Deploy cleanup continues.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** Shared Deploy helper ownership limits for `DeployReferenceDataService`, `DeployResolveSuggestionsService`, and `DeployTemplateEditorLauncher`.

**Out of scope:** Runtime refactors of current callers, typed runtime implementation, full `DeployReferenceDataService` invalidation design, general helper policy outside Deploy, and product behavior redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-185`..`FR-188`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-046`)
- `docs/02-ux/Archived/winui-lane-architecture-standard-an.md`
- `docs/02-ux/Archived/winui-typed-capability-runtime-contract-an.md`
- `docs/02-ux/Archived/winui-deploy-results-panel-coordinator-contract-an.md`
- `docs/02-ux/Archived/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-from-template-extraction-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-quick-deploy-extraction-cleanup-target-am.md`

---

## 1) Why this contract exists

`DeployResultsPanelCoordinator` is no longer the only shared seam that could widen by convenience.

Post-`#660`, shared Deploy also has three helpers that now sit on the shared wiring path:
- `DeployReferenceDataService`
- `DeployResolveSuggestionsService`
- `DeployTemplateEditorLauncher`

Those helpers are acceptable in the current codebase shape, but they are still risky without explicit boundaries:
- `DeployReferenceDataService` could quietly become the owner of route refresh policy or lane-specific defaults
- `DeployResolveSuggestionsService` could quietly absorb readiness orchestration or user-facing correction policy
- `DeployTemplateEditorLauncher` could quietly absorb cross-capability workflow coordination instead of staying a narrow launcher seam

This contract exists to prevent that drift before more lanes or later typed runtime cleanup build on these helpers.

---

## 2) Shared-helper classification

All three helpers are **capability-shared, lane-triggered seams**.

That means:
- they are shared because more than one Deploy lane may depend on them or because they encode a shared Deploy-side integration rule
- they are lane-triggered because lane-local owners, hosts, or later capability-runtime seams still decide when to invoke them and what the invocation means in lane workflow terms

This is the key rule:
- a shared helper may provide a narrow reusable capability seam
- it must not become the owner of lane-local orchestration just because multiple lanes call it

---

## 3) `DeployReferenceDataService`

### 3.1 Classification

`DeployReferenceDataService` is:
- capability-shared
- lane-triggered

### 3.2 What it may own

`DeployReferenceDataService` may own:
- loading Deploy reference data that is genuinely shared across Deploy lanes
- caching shared Deploy reference-data snapshots when current callers do not require immediate live refresh
- exposing the current Deploy settings snapshot needed by Deploy lanes
- exposing the current shared switch inventory snapshot for Deploy lanes
- exposing the current shared VHDX catalog item snapshot and derived catalog-option snapshot for Deploy lanes
- the narrow `EnsureAsync(forceRefresh)` contract for callers that need to request a refresh

This helper is the shared data-loading seam for Deploy reference inputs.

### 3.3 What it must not own

`DeployReferenceDataService` must not own:
- route-activation policy
- lane-specific refresh timing or reconcile sequencing
- lane-specific default selection behavior
- readiness evaluation or readiness re-run policy
- resolve-suggestion policy
- user-facing status text or remediation messaging
- right-panel or shell coordination
- cross-capability invalidation assumptions that are not explicitly contracted

If the question is "when should this lane refresh?" or "what should the user be told after refresh?", that question belongs outside this helper.

---

## 4) `DeployResolveSuggestionsService`

### 4.1 Classification

`DeployResolveSuggestionsService` is:
- capability-shared
- lane-triggered

### 4.2 What it may own

`DeployResolveSuggestionsService` may own:
- deterministic shared Deploy-side normalization rules that apply to a provided `LabTemplate`
- matching supplied switch names against supplied available-switch data
- matching supplied disk identity hints against supplied catalog items using the current approved precedence and matching rules
- mutating the provided template when a deterministic shared Deploy-side suggestion is found
- returning an applied-count summary for the caller to interpret

This helper is the shared suggestion-application seam, not the workflow owner around that seam.

### 4.3 What it must not own

`DeployResolveSuggestionsService` must not own:
- loading or refreshing reference data itself
- deciding when resolve suggestions should run
- deciding whether resolve suggestions are surfaced automatically, manually, or both for a given lane
- readiness re-evaluation after suggestions are applied
- lane-specific status text, warning text, or review messaging
- template-editor launch behavior
- route switching or shell interaction

If the question is "should this lane run suggestions now?" or "what happens next after suggestions apply?", that question belongs outside this helper.

---

## 5) `DeployTemplateEditorLauncher`

### 5.1 Classification

`DeployTemplateEditorLauncher` is:
- capability-shared
- lane-triggered

### 5.2 What it may own

`DeployTemplateEditorLauncher` may own:
- the narrow cross-capability handoff from Deploy into the existing Templates editor workspace
- forwarding a caller-supplied `TemplateEditorDocument` and caller-supplied status text into the Templates editor seam
- nothing more than the launch/invocation boundary needed to avoid re-wiring the Templates workspace directly in every Deploy lane

This helper is a launcher seam, not a cross-capability workflow coordinator.

### 5.3 What it must not own

`DeployTemplateEditorLauncher` must not own:
- deciding when a Deploy lane should open Templates editor
- building the template snapshot or selecting the source document contents on behalf of a lane
- lane-specific validation, correction, or save policy
- lane-specific status text semantics beyond forwarding the caller-supplied status
- template-library selection, template loading, or template reconciliation policy
- route ownership, shell ownership, or general Templates capability coordination

If the question is "what document should be opened?" or "why is this lane opening the editor right now?", that question belongs to the caller.

---

## 6) What remains outside shared Deploy helpers

The following concerns remain outside these helpers:
- lane-local refresh and reconcile sequencing
- lane-local readiness flow
- lane-local correction/remediation messaging
- lane-local template snapshot construction rules
- lane-local route-activation behavior
- shared Deploy panel coordination
- shell-owned panel/container mechanics
- typed capability runtime ownership when AN2 is implemented later

In current code, those responsibilities remain with:
- lane owners
- lane-local hosts where they still exist
- shared Deploy composition only for genuinely shared capability-level concerns
- `MainWindow` only for current shell-owned concerns until later runtime cleanup lands

Shared helpers are support seams under those owners, not replacements for them.

---

## 7) Explicit invalidation decision for `DeployReferenceDataService`

This issue does **not** define a broader invalidation system for `DeployReferenceDataService`.

The current contract is intentionally narrow:
- callers may request `EnsureAsync(forceRefresh: true)` when they own a concrete refresh trigger
- otherwise the helper may continue using its current cached/shared snapshot behavior
- there is no newly approved guarantee that Deploy reference data auto-invalidates whenever Assets, Templates, or other app state changes elsewhere

This is an explicit deferred rule, not an omission.

The trigger for a follow-up invalidation contract is:
- another approved lane or capability begins depending on stronger same-session freshness guarantees than caller-owned refresh can provide
- or concrete stale-state bugs show that the current force-refresh trigger model is no longer sufficient

Until then:
- invalidation remains caller-owned
- "refresh when convenient" is not a hidden rule
- stronger freshness guarantees require a new narrow contract issue

---

## 8) Sequencing implication

Later runtime cleanup may:
- move these helpers behind a typed `DeployCapabilityRuntime`
- reduce current host/delegate wiring around them
- or split one helper further if later code proves it still owns more than one reason to change

But later implementation must preserve the ownership rule defined here:
- shared helpers may stay reusable
- lane-local orchestration stays outside them
- shell ownership stays outside them

---

## 9) Traceability

- `FR-185`
  - shared Deploy helpers remain narrow capability-shared, lane-triggered seams rather than shared workflow owners
- `FR-186`
  - `DeployReferenceDataService` owns shared Deploy reference-data loading/caching only and rejects lane refresh/orchestration ownership
- `FR-187`
  - `DeployResolveSuggestionsService` and `DeployTemplateEditorLauncher` remain narrow shared helper seams with explicit non-goals for workflow ownership
- `FR-188`
  - `DeployReferenceDataService` invalidation remains explicitly deferred behind caller-owned force-refresh triggers until a later narrow contract says otherwise

Mapped acceptance criteria:
- `AC-046`

---

## 10) Open Questions / TBDs

- `TBD:` Whether a later typed `DeployCapabilityRuntime` will own these helpers directly or hide some of them behind narrower lane/runtime seams.
- `TBD:` Whether `DeployTemplateEditorLauncher` eventually becomes the only approved Deploy-to-Templates editor handoff seam, or whether From Template keeps a lane-local editor-launch path for reasons that stay explicit.
- `TBD:` Whether future shared Deploy helper growth stays narrow enough to remain helper-level or proves a missing Deploy-runtime boundary instead.

