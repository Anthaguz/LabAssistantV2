# WinUI Lane Architecture Rollout Plan

**Purpose:** Keep the post-`#660` WinUI lane-architecture follow-up ordered, narrow, and recoverable across sessions so contract-setting work lands before runtime slices and no approved follow-up is silently dropped.

**Status:** Active planning tracker. Update this document whenever a tracked slice is started, explicitly deferred, completed, or replaced.

**Milestone:** [Milestone AN - WinUI Lane Architecture Guardrails](https://github.com/Anthaguz/LabAssistantV2/milestone/41)

**Scope:** Sequencing, status, and debt-control rules for the next WinUI lane-architecture and Deploy follow-up slices.

**Out of scope:** This document is not a behavior contract, not an implementation spec, and not a substitute for `SRS`, `Acceptance Criteria`, or capability contract docs.

**Related:**
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`
- `docs/02-ux/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/winui-deploy-composition-cleanup-target-am.md`
- `docs/02-ux/winui-from-template-extraction-cleanup-target-am.md`
- `docs/02-ux/winui-quick-deploy-extraction-cleanup-target-am.md`
- `docs/02-ux/winui-ui-test-convergence-contract-am.md`
- `docs/03-architecture/code-organization.md`
- `docs/03-architecture/code-documentation.md`

---

## 1) Why this plan exists

Quick Deploy cleanup proved the extraction direction, but it also exposed a predictable failure mode:
- if the next slices are not ordered explicitly
- and if the new rules stay trapped in issue discussions or chat context
- then later Dev work will recreate pattern drift under new names

This plan exists to prevent that drift.

The plan is the source of truth for:
- which follow-up slices are still pending
- what order they should land in
- which items are intentionally deferred rather than forgotten

The plan is **not** the source of truth for final behavior.
Behavior must still be documented in canonical contract artifacts before implementation.

---

## 2) Operating model

### 2.1 One active slice at a time
- Keep exactly one implementation slice active at a time unless there is a clear non-overlapping docs-only sidecar.
- Do not open broad parallel cleanup work across multiple capabilities just because the pattern looks similar.

### 2.2 Contracts before runtime
- Cross-cutting ownership rules must land before additional runtime cleanup slices.
- If a runtime slice needs a new rule, write the rule first in canonical docs.

### 2.3 The roadmap is the queue; issues are the execution units
- This roadmap tracks the full ordered queue.
- GitHub issues should be created only for:
  - the current active slice
  - the next ready slice when useful
- Do not create a large stack of half-specified implementation issues far ahead of the active dependency chain.

### 2.4 Keep slices narrow
- Each slice must have one dominant reason to change.
- Fold related ideas together only when they are genuinely the same contract seam.
- If a slice starts needing a second contract seam or a second capability seam, split it.

### 2.5 Deferred is a real status
- If something should wait for a trigger, mark it `Deferred` here with the trigger written explicitly.
- Do not keep deferred work only in chat memory.

### 2.6 Update discipline
- When a slice starts: mark it active here and add the issue link.
- When a slice lands: mark it done here and add the PR link.
- When the order changes: update this roadmap first, then create or retarget issues.

---

## 3) Status legend

- `Active` - current slice in progress
- `Ready` - approved next slice with no unresolved dependency
- `Blocked` - should happen, but depends on an earlier slice
- `Deferred` - intentionally waiting for an explicit trigger
- `Done` - completed and reflected in linked artifacts

---

## 4) Current queue summary

| ID | Status | Dominant reason | Slice | Depends on |
| --- | --- | --- | --- | --- |
| `LA-01` | `Active` | `docs` | Canonical lane architecture standard | none |
| `LA-02` | `Blocked` | `docs` | Typed capability bootstrap/runtime contract | `LA-01` |
| `LA-03` | `Blocked` | `docs` | `DeployResultsPanelCoordinator` hard ownership contract | `LA-01` |
| `LA-04` | `Blocked` | `docs` | Shared Deploy helper ownership limits | `LA-01`, `LA-03` |
| `LA-05` | `Blocked` | `refactor` | Apply the standard to Deploy From Template | `LA-01`..`LA-04` |
| `LA-06` | `Blocked` | `test` | Smoke/system coverage strategy follow-up | `LA-05` |
| `LA-07` | `Blocked` | `test` | Source-shape test posture convergence | `LA-06` |
| `LA-TBD-01` | `Deferred` | `docs` | `DeployReferenceDataService` invalidation rule | trigger-based |

This roadmap itself is the current rollout-order artifact.
No separate rollout-order issue is needed unless the sequencing logic changes materially.

---

## 5) Slice definitions

## 5.1 `LA-01` - Canonical lane architecture standard

**Goal:** Define the canonical pattern for non-trivial WinUI lanes so future extractions stop inventing local variants.

**This slice should include:**
- the required roles for non-trivial lanes:
  - workspace owner
  - workspace controller
  - workspace composition
  - workspace shell bridge
- what each role may own
- what each role must not own
- the explicit banned-pattern list:
  - backpack hosts
  - attach cycles
  - lane-specific shell lambdas in `MainWindow`
  - lane-specific logic inside shared capability composition
- the explicit rule for when a lane is simple enough to use a slimmer form instead of the full split

**This slice must not do:**
- runtime refactors
- per-capability cleanup
- typed shell runtime design

**Expected artifacts:**
- one new canonical architecture/UX contract doc
- `SRS` updates if new FRs are needed
- `Acceptance Criteria` updates for the new contract

**Done when:**
- the lane pattern is explicit enough that a Dev can apply it without improvising
- the banned patterns are written into canonical docs
- the simple-lane escape hatch is explicit instead of implied

**Issue:** [#662](https://github.com/Anthaguz/LabAssistantV2/issues/662)
**PR:** `TBD`

---

## 5.2 `LA-02` - Typed capability bootstrap/runtime contract

**Goal:** Define the next shell-level pattern so `MainWindow` converges toward one typed runtime per capability instead of hand-wiring lane helpers directly.

**This slice should include:**
- what a typed capability runtime is responsible for
- what `MainWindow` still owns after the runtime boundary exists
- how capability bootstrap differs from capability runtime lifetime
- the allowed relationship between capability runtime, shared capability composition, and lane-local seams

**This slice must not do:**
- concrete runtime implementation
- per-lane cleanup
- broad shell redesign beyond the new boundary

**Expected artifacts:**
- one new canonical shell/capability runtime contract doc
- `SRS` and `Acceptance Criteria` updates if new FRs/ACs are needed

**Done when:**
- `MainWindow` future direction is explicit
- the typed runtime boundary is concrete enough to guide later implementation slices
- lane architecture from `LA-01` and shell runtime ownership do not conflict

**Issue:** [#663](https://github.com/Anthaguz/LabAssistantV2/issues/663)
**PR:** `TBD`

---

## 5.3 `LA-03` - `DeployResultsPanelCoordinator` hard ownership contract

**Goal:** Prevent `DeployResultsPanelCoordinator` from becoming a new shared dumping ground by writing an explicit may-own / must-not-own rule.

**This slice should include:**
- what panel coordination belongs there
- what lane-specific workflow ownership does not belong there
- what shared Deploy composition may still delegate to it
- what must remain in shell-owned panel infrastructure versus lane-local ownership

**This slice must not do:**
- general shared-helper policy for all Deploy services
- From Template or Quick Deploy runtime refactors
- panel behavior redesign

**Expected artifacts:**
- one narrow contract update or new seam-specific contract doc
- traceability updates if the new rule needs FR/AC coverage

**Done when:**
- a later Dev can tell whether new logic belongs in the coordinator without guessing
- the coordinator's non-goals are explicit

**Issue:** [#664](https://github.com/Anthaguz/LabAssistantV2/issues/664)
**PR:** `TBD`

---

## 5.4 `LA-04` - Shared Deploy helper ownership limits

**Goal:** Define ownership limits for shared Deploy helpers so "shared" does not become a justification for random logic accumulation.

**Applies to:**
- `DeployReferenceDataService`
- `DeployResolveSuggestionsService`
- `DeployTemplateEditorLauncher`

**This slice should include:**
- what kind of logic belongs in each shared helper
- what kind of lane-local orchestration must stay out
- whether any helper is purely capability-shared versus lane-triggered
- the required boundary between shared helper lookup/support behavior and lane-local workflow decisions

**This slice must not do:**
- full invalidation design unless the trigger in `LA-TBD-01` is met
- runtime refactors of every current caller
- a generic "shared helper" rule that ignores actual Deploy ownership seams

**Expected artifacts:**
- one narrow contract artifact or contract update covering the listed helpers
- explicit `TBD` handling if invalidation remains deferred

**Done when:**
- helper ownership limits are explicit and testable as a review boundary
- invalidation is either contracted now or left as an explicit deferred trigger

**Issue:** [#665](https://github.com/Anthaguz/LabAssistantV2/issues/665)
**PR:** `TBD`

---

## 5.5 `LA-05` - Apply the standard to Deploy From Template

**Goal:** Use `Deploy From Template` as the next runtime proof point after Quick Deploy, applying the cross-cutting rules without widening scope to the rest of the app.

**This slice should include:**
- implementation cleanup in the From Template lane to match the approved standard
- preservation of the existing AF/AL/AM87 behavior boundary
- test updates needed for the changed seam

**This slice must not do:**
- a broad Deploy-wide runtime rewrite
- a second capability rollout
- helper redesign beyond what `LA-03` and `LA-04` require

**Expected artifacts:**
- issue-scoped runtime refactor
- updated tests
- architecture-doc touch-up only if the implementation changes a documented seam shape

**Done when:**
- From Template no longer reflects the older ownership shape
- the lane follows the approved standard without inventing a new variant
- Quick Deploy, Overview, and shell boundaries remain preserved

**Issue:** [#666](https://github.com/Anthaguz/LabAssistantV2/issues/666)
**PR:** `TBD`

---

## 5.6 `LA-06` - Smoke/system coverage strategy follow-up

**Goal:** Define the first real system-level coverage target for the extracted WinUI lanes after the next proof point lands.

**This slice should include:**
- which flows deserve system-level or smoke-style coverage first
- the minimum viable surface to cover:
  - route activation
  - lane editing
  - readiness
  - deploy start
  - panel behavior
- what remains manual versus what becomes automated

**This slice must not do:**
- full harness implementation unless separately scoped
- retrofitting every past milestone into one test rewrite

**Expected artifacts:**
- test-strategy doc update
- possibly a narrow acceptance-criteria or checklist update if coverage commitments become contractual

**Done when:**
- the next coverage target is explicit
- system-level coverage work has a narrow, testable starting point

**Issue:** `TBD`
**PR:** `TBD`

---

## 5.7 `LA-07` - Source-shape test posture convergence

**Goal:** Decide which current source-shape assertions remain permanent seam guards and which should be retired once stronger behavioral coverage exists.

**This slice should include:**
- a review of current AG/AL/AM anti-cheat assertions
- explicit keep/replace/remove decisions
- alignment with the `LA-06` smoke/system coverage strategy

**This slice must not do:**
- a test rewrite before replacement coverage exists
- contract drift that weakens current safety before a stronger replacement lands

**Expected artifacts:**
- test-strategy or UX contract update
- follow-up test issue(s) only after keep/replace decisions are explicit

**Done when:**
- the long-term role of current source-shape tests is explicit
- future test cleanup stops being an open-ended aspiration

**Issue:** `TBD`
**PR:** `TBD`

---

## 5.8 `LA-TBD-01` - `DeployReferenceDataService` invalidation rule

**Status:** Deferred

**Why it is deferred:** the risk is real, but invalidation design should not be guessed early without a concrete second caller, second lane dependency, or stale-state failure mode.

**Activate this slice when any of the following happens:**
- another lane starts depending on cached Deploy reference data
- shared refresh timing becomes user-visible or bug-prone
- a stale-state bug demonstrates that current "refresh when convenient" behavior is no longer safe

**When activated, the slice should answer:**
- what invalidates cached/reference data
- who is allowed to trigger refresh
- whether route activation is sufficient or an explicit invalidation event is required

**Issue:** `TBD`
**PR:** `TBD`

---

## 6) Practical execution order

Proceed in this order:
1. Finish the cross-cutting contract slices: `LA-01` through `LA-04`
2. Use `LA-05` as the next runtime proof point
3. Reassess whether `LA-TBD-01` should stay deferred
4. Define stronger coverage direction in `LA-06`
5. Only then converge the long-term source-shape test posture in `LA-07`

This keeps the next work intentional without pretending the whole app needs to be standardized at once.

---

## 7) Session-start checklist

At the start of a new session:
1. Read this roadmap first.
2. Confirm the active slice and its dependencies.
3. Read only the canonical docs needed for that slice.
4. If the active slice is implementation, confirm the contract slice it depends on has already landed.
5. Update this roadmap before switching to a different slice.

---

## 8) Open Questions / TBDs

- `TBD:` What the next milestone code should be, if any, once these slices are turned into GitHub issues.
- `TBD:` Whether the future canonical lane standard belongs primarily under `docs/02-ux` or `docs/03-architecture`; the contract content matters more than the exact final file location.
- `TBD:` Which capability should follow `Deploy From Template` as the next runtime proof point after `LA-05`.
