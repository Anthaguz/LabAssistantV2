# WinUI Deploy On-the-Fly Contract (Milestone AG)

**Purpose:** Define the behavioral contract for WinUI `Deploy on-the-fly` so AG2/AG3/AG4 can implement without ambiguity.

**Status:** Approved docs-first contract for Milestone AG planning/implementation.

**Primary references:**
- `docs/01-requirements/srs.md` (FR-091, FR-092, FR-093, FR-094, FR-095, FR-096)
- `docs/01-requirements/acceptance-criteria.md` (AC-015, AC-016)
- `docs/02-ux/winui-deploy-from-template-contract-af.md` (interaction parity baseline)

---

## 1) AG Scope Boundary

AG covers WinUI Deploy `on-the-fly` convergence only.

In scope:
- route/subview contract for `deploy.on_the_fly`
- on-the-fly input model/readiness taxonomy
- correction affordances for blocking readiness issues
- compact-first results visibility parity with AF pattern

Out of scope:
- WPF Deploy changes
- AF from-template contract changes
- new deployment semantics
- schema/model expansion not required by approved requirements

---

## 2) Routing Contract

Canonical route:
- `deploy.on_the_fly` (AG active route)

Behavior:
- Deploy on-the-fly entry must resolve deterministically to `deploy.on_the_fly`.
- User-facing label should prefer `Quick Deploy` while route key remains `deploy.on_the_fly`.
- Global navigation contract from AC milestone remains authoritative.
- AF `deploy.from_template` route remains available and unchanged.

---

## 3) On-the-Fly Input Model Expectations

AG deploy input model supports one or more VM entries using existing deployment configuration fields.

Minimum expectations:
- VM entry identity/config fields required for deploy start are explicit in readiness output.
- Required disk identity input state is represented per VM entry.
- Switch selections are represented per VM entry and validated for required paths.

AG does not invent new domain fields; it validates and surfaces existing model expectations.

---

## 4) Readiness Taxonomy and Gating

Readiness outcomes must be classified as:
- `Pass`
- `Warn`
- `Fail`

Rules:
- Blocking (`Fail`) conditions prevent deploy start.
- Warning (`Warn`) conditions do not block deploy, but must be visible/actionable.
- Classification and reason text must be explicit per VM entry and in aggregate summary.

Examples of blocking conditions:
- unresolved/invalid required disk identity
- required VM-entry configuration incomplete/invalid
- required switch selection missing/invalid

Examples of warning conditions:
- partial/non-critical mapping or advisory compatibility notes where deploy can continue safely

---

## 5) Correction Affordances

When readiness reports blocking results:
- UI must provide explicit correction affordances for affected inputs.
- Correction actions must be non-silent and re-evaluable.
- Deploy start remains gated until blocking issues are resolved.

Correction interaction model should reuse AF readiness/correction UX patterns where applicable.

---

## 6) Execution Boundary

AG execution boundary:
- readiness must run before start
- start is allowed only when unblocked
- execution reuses existing deployment orchestration semantics

AG does not redefine deploy runtime semantics (cleanup/cancellation/terminal outcomes remain governed by existing deployment contracts).

---

## 7) Results UX Baseline (Parity with AF Pattern)

On-the-fly results UX follows compact-first progressive disclosure:
- sticky summary/progress remains visible during run lifecycle
- per-VM rows are concise by default
- per-VM details are expandable on demand
- global warnings/errors remain collapsed by default and discoverable
- in AH2+, deploy timeline/results/issue context is hosted by shell right panel ownership contract

Goal is parity of interaction model, not forced visual duplication.

Canonical timeline row contract:
- states: `Pending`, `Running`, `Succeeded`, `Failed`, `Skipped`
- one label per step (no duplicated start/finish labels)
- state drives spinner/icon rendering
- skipped/non-applicable rows are hidden
- nested parent rows are hidden when no child step executes

---

## 8) AG Work Packaging Guidance

### AG2 (routing + scaffold)
- route/subview scaffold for `deploy.on_the_fly`
- input/workspace placeholders
- no execution/readiness semantics wiring yet

### AG3 (readiness + execution wiring)
- readiness classification/gating behavior
- correction affordance wiring
- deploy start execution wiring to existing orchestration path

### AG4 (results parity + closure evidence)
- compact-first results parity implementation
- AG matrix/checklist and test-plan closure artifacts

---

## 9) Traceability Map

- FR-091 -> AC-015 scenario 1 (on-the-fly route/scope contract)
- FR-092 -> AC-015 scenario 2 (readiness taxonomy and blocking/warning behavior)
- FR-093 -> AC-015 scenario 3 and 4 (correction affordances + execution gate)
- FR-094 -> AC-015 scenario 5 (compact-first results parity)
- FR-095 and FR-096 -> AC-016 scenarios 1-4 (canonical timeline state and rendering parity)

---

## Open Questions / TBDs

- `TBD`: whether AG2 should include explicit placeholder affordance for deferred future Deploy history view.
- `TBD`: whether AG manual checklist should require a fixed set of on-the-fly blocking fixture configurations for repeatability.
- `TBD`: add clickable "jump to fix field" actions from readiness issue rows (for example disk/switch issues route focus directly to the affected VM field in Quick Deploy).
