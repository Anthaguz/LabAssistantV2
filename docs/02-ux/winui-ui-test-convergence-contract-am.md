# WinUI UI Test Convergence Contract (AM3)

**Purpose:** Define how WinUI UI tests should evolve during AM extraction so stable shell/capability contracts stay protected without freezing the codebase into one shell-centric implementation shape.

**Status:** Approved implementation contract for Milestone AM.

**Scope:** WinUI UI test strategy for shell/workspace extraction and capability-local ownership changes.

**Out of scope:** Runtime extraction implementation, domain-semantic redesign, or replacing all source-based tests immediately.

**Related:**
- `docs/01-requirements/srs.md` (`FR-119`..`FR-121`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-024`)
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/winui-view-interaction-contract-am.md`
- `docs/02-ux/winui-shell-view-consistency-contract-al.md`

---

## 1) Core rule

UI tests must preserve stable contracts while allowing legitimate internal extraction work.

That means:
- keep strong coverage for stable shell/capability rules
- reduce brittle coupling where tests only protect an interim implementation shape
- update tests in the same slice as the runtime extraction that changes the seam

---

## 2) What tests should keep protecting

Tests should continue to protect stable product and shell contracts such as:
- canonical route continuity
- shell header ownership rules
- Overview-vs-exception capability rules
- Templates Library-first / Editor workflow-state rule
- Deploy right-panel ownership rules already approved
- capability-local interaction rules once they become stable
- capability-specific semantics already contracted elsewhere

These are contract anchors, not accidental implementation details.

---

## 3) What tests should reduce over time

Tests should reduce dependence on:
- exact source-string checks that only prove one possible implementation
- exact control-proxy shape that is being intentionally extracted away
- temporary scaffolding assertions created to guard an interim migration step
- assertions that fail only because ownership moved from `MainWindow` to a capability seam while user-visible behavior stayed the same

This does not mean “stop using source-based tests entirely.”  
It means they should protect stable contracts, not every internal arrangement.

---

## 4) Preferred direction during AM

Where practical, tests should move toward:
- route and host continuity
- state-owner seam existence
- capability-level behavior checks
- narrow interaction-boundary assertions
- explicit contract anchors tied to approved docs

They may still use source/XAML inspection when:
- that is the most practical way to protect a stable shell/view contract
- no better behavioral seam exists yet

---

## 5) Same-slice rule

If a runtime extraction slice changes:
- ownership boundaries
- interaction seams
- view/control exposure shape
- capability-local state structure

then the directly impacted UI tests must be updated in the same issue/PR.

AM should not allow:
- runtime refactor now
- “we’ll fix the tests later”

That is how milestone safety drifts.

---

## 6) Stable contract vs temporary scaffold distinction

### 6.1 Stable contract tests
Keep and maintain:
- shell/capability behavior required by approved FR/AC contracts
- explicit exception models like Templates
- capability-local routing/ownership rules that the product depends on

### 6.2 Temporary scaffold tests
Reduce over time:
- tests that only guard transitional placeholder structure
- tests that only prove a shell-centric implementation shape that AM is intentionally removing

This distinction should be made explicitly during each capability extraction slice.

---

## 7) Practical AM guidance

### 7.1 Good extraction/testing pattern
1. preserve contract anchors
2. extract one capability seam
3. update directly impacted tests in the same issue
4. keep milestone evidence coherent

### 7.2 Bad pattern
1. refactor runtime ownership
2. leave broken/brittle tests for later
3. add more source-shape assertions to patch over unclear boundaries

---

## 8) Traceability

- `FR-119`
  - stable contract protection with reduced brittle source-shape coupling
- `FR-120`
  - test updates happen in the same extraction slice
- `FR-121`
  - stable contract tests distinguished from temporary migration scaffolding

Mapped acceptance criteria:
- `AC-024`

---

## 9) Open Questions / TBDs

- `TBD:` Which existing milestone source-shape tests should remain permanent contract anchors versus which should be retired as each capability extraction lands.
- `TBD:` Whether any light integration-style UI harness should be added later to reduce dependence on source/XAML inspection for extracted capability seams.
- `TBD:` The exact balance between seam-presence assertions and higher-level observable behavior assertions once the first extracted capability (`Machines`) is complete.
