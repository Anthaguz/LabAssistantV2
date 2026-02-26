# UI Framework Decision Rubric and Spike Contracts (Milestone Y)

**Purpose:** Define the evaluation rubric, spike scope, and decision-record outputs for selecting the UI framework migration path (`WPF modernized` vs `WinUI 3`) without guesswork.

**Status:** Decision-prep contract for Milestone Y (`#251` -> `#253`). This document does **not** choose a framework and does **not** define prototype implementation details beyond required evidence outputs.

**Related:**
- `docs/02-ux/ui-migration-execution-plan.md`
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/migration-preservation-matrix.md`
- `docs/03-architecture/code-path-index.md`
- `docs/03-architecture/gui-action-map.deploy.md`
- `docs/03-architecture/gui-action-map.settings-diagnostics-shell.md`

---

## 1) Decision Inputs and Constraints (already agreed)

The framework decision must respect these product/architecture constraints:

- Future top-level IA is entity-based:
  - `Machines`, `Deploy`, `Templates`, `Assets`, `Diagnostics`, `Settings`
- Shell navigation must support:
  - Capability Scope (hamburger/menu)
  - Context Scope (left panel)
- First migration implementation slice is expected to be **shell foundation first**
- Modern Windows UX quality is desired (Windows 11 feel)
- Behavior preservation is non-negotiable:
  - readiness/gating
  - cleanup/cancellation
  - summaries (including guest-step outcomes)
  - diagnostics/export/logging/error-feed behavior

This rubric compares framework fit under these constraints. It is not a greenfield UI preference exercise.

---

## 2) Framework Decision Rubric (Y1/Y2 evaluation contract)

Compare **both** options using the same rubric:
- `WPF modernized`
- `WinUI 3`

### 2.1 Scoring format (required)

For each criterion:
- **Score:** `1-5`
  - `1` = poor fit / high risk
  - `3` = acceptable with notable tradeoffs
  - `5` = strong fit / low risk
- **Evidence required:** concrete observations from Y2 prototypes and/or implementation spike notes
- **Risk note:** top risk or limitation for that criterion

Do not assign scores without evidence notes.

### 2.2 Rubric criteria (required)

#### A. Windows 11 UX fidelity potential
Evaluate:
- native-feeling controls/layout capability
- modern Windows shell patterns support
- ability to produce a high-quality Windows 11 UX without excessive custom work

Evidence examples:
- shell prototype screenshots/video
- control behavior observations (navigation, panes, density, spacing)
- notes on custom control/template effort needed

#### B. Navigation/shell composition ergonomics (Capability Scope + Context Scope)
Evaluate:
- ability to implement shell with:
  - capability navigation (hamburger/menu)
  - context panel behavior
  - shell-level error feed placement concept
- clarity/complexity of navigation state management

Evidence examples:
- shell prototype interaction demo
- state/command wiring complexity notes
- code structure comparison for shell navigation state

#### C. Dense workflow surface support (Deploy complexity fit)
Evaluate framework ergonomics for dense, stateful pages similar to `Deploy`:
- readiness panel + VM list + logs + summaries
- expandable/collapsible regions
- scroll behavior and layout constraints
- command-heavy interaction density

Evidence examples:
- shell prototype includes a dense mock surface or representative shell content region
- layout density observations
- responsiveness notes under many controls/items

#### D. MVVM/testability fit for command/state-heavy workflows
Evaluate:
- command binding ergonomics
- observable state updates
- test seam friendliness
- friction introduced by framework-specific patterns or threading/dispatcher behavior

Evidence examples:
- prototype ViewModel wiring sample
- notes on testability patterns and UI-state synchronization
- debugging notes for command/state transitions

#### E. Integration cost with existing app layers (Business/Services/Data/Models)
Evaluate:
- UI layer integration complexity without changing lower layers
- compatibility with current service/business abstractions
- migration friction for existing ViewModel-heavy workflows

Evidence examples:
- prototype integration notes using existing ViewModel/service slices
- required adapters/shims identified
- estimated migration burden for shell-first slice

#### F. Migration risk / behavior-preservation risk
Evaluate risk of preserving existing semantics during migration:
- readiness/gating behavior
- cleanup/cancellation status flow
- summary visibility/detail paths
- diagnostics/error-feed integration

Evidence examples:
- explicit mapping from preservation matrix/action maps to framework risks
- prototype notes on likely breakpoints/unknowns

#### G. Tooling maturity / developer productivity / debugging ergonomics
Evaluate:
- day-to-day iteration speed
- XAML tooling/designer reliability (if used)
- debugging quality for UI/state issues
- diagnostics quality during implementation

Evidence examples:
- prototype implementation experience log
- build/debug friction notes
- known tooling blockers encountered

#### H. Performance/responsiveness expectations (shell + dense pages)
Evaluate:
- perceived shell responsiveness
- layout/render behavior for dense surfaces
- startup/interaction concerns relevant to migration shell and heavy pages

Evidence examples:
- qualitative prototype measurements/observations
- identified hot spots or concerns requiring mitigation

#### I. Packaging/runtime deployment implications (high-level only)
Evaluate at a high level:
- packaging/runtime constraints introduced by framework choice
- operational/deployment implications relevant to LabAssistant maintenance

Evidence examples:
- brief deployment/runtime notes only (not a full release readiness study)

### 2.3 Required rubric output format (Y2 -> Y3 handoff)

For each option, produce:
- criterion-by-criterion table (Score, Evidence, Risk note)
- overall strengths summary (top 3)
- overall weaknesses/risks summary (top 3)
- unresolved questions list (if any)

If a criterion was not evaluated in Y2, mark it explicitly as `Evidence incomplete` (do not guess).

---

## 3) Y2 Prototype Spike Scope Contract (what #252 must prove)

Y2 is a **framework decision spike**, not migration implementation.

### 3.1 Required prototype scope (must prove)

Both candidate frameworks should demonstrate a **shell foundation prototype** that includes:

- Capability Scope navigation concept
  - hamburger/menu behavior
  - active capability selection
- Context Scope navigation/panel concept
  - left panel mode switching / contextual content
- Shell-level error feed placement concept
  - placement and layout interaction only (not full error-feed feature completion)

The prototype must include enough realistic state interaction to evaluate:
- navigation state management ergonomics
- command binding/state updates
- layout density/responsiveness with a representative shell + content area

### 3.2 Minimum realism required (to avoid misleading spikes)

Each prototype should include at least:
- a shell frame with capability navigation + context pane
- one representative “dense content” placeholder region that simulates real workflow density
  - e.g., readiness summary + list + cards/log panel placeholders
- ViewModel-driven state changes (not static mock XAML only)

### 3.3 Explicitly out of scope for Y2

Y2 prototypes should **not** attempt to prove or implement:
- full Deploy migration
- production-quality styling/theme system
- complete Machines page implementation
- final visual design polish
- full feature parity or behavior-preservation implementation
- release packaging readiness

If a prototype expands beyond shell scope, record it as extra exploration, not required evidence.

### 3.4 Y2 evidence capture requirements

Each prototype run must produce:
- code branch/repo location reference
- screenshots and/or short video capture of shell interactions
- short implementation notes:
  - what was easy
  - what was awkward
  - what could not be represented cleanly
- rubric evidence mapped back to Section 2 criteria

---

## 4) Y3 Decision Record Contract (what #253 must produce)

The final framework decision record must include all of the following:

### 4.1 Decision and rationale
- chosen option (`WPF modernized` or `WinUI 3`)
- clear rationale tied to rubric evidence (not preference wording)

### 4.2 Rejected alternative and tradeoffs
- why the alternative was not selected
- tradeoffs accepted by choosing the selected option

### 4.3 Evidence summary mapped to rubric
- summary table or section mapping Y2 evidence to each rubric criterion
- explicit note for any incomplete evidence and why decision was still possible (or why decision should be deferred)

### 4.4 Migration implications
- high-level migration approach implications for LabAssistant
- impact on shell-first migration slice
- any required enabling work before migration implementation begins

### 4.5 Top technical risks + mitigations
- at least top 3 risks
- mitigation approach for each
- indication of which risks must be retired in the first migration slice

### 4.6 Recommended first implementation slice (shell foundation only)
- explicit scope for first slice
- what is included
- what is deferred
- how behavior-preservation constraints will be protected during that slice

The decision record is incomplete if any of these sections are missing.

---

## 5) Milestone Y Acceptance / Done Criteria (Y1 -> Y3 path)

### 5.1 Y1 done (this issue / #251)

Y1 is complete only when:
- framework comparison rubric is explicit and evaluable
- Y2 prototype scope is defined (including out-of-scope boundaries)
- Y3 decision record output contract is defined
- execution plan links to this rubric/contract

### 5.2 Y2 done (prototype spike / #252)

Y2 is complete only when:
- both candidate frameworks are evaluated against the same rubric
- shell scope-switching ergonomics are demonstrated (capability + context scope)
- shell-level error-feed placement concept is represented
- evidence is captured and mapped to rubric criteria

Evidence is incomplete (Y2 not done) if:
- shell scope-switching behavior is not prototyped
- only static UI mockups are produced (no state interactions)
- rubric scores exist without evidence notes

### 5.3 Y3 done (decision record / #253)

Y3 is complete only when:
- chosen framework and rationale are documented
- rejected option/tradeoffs are documented
- rubric evidence summary is included
- behavior-preservation risks are explicitly addressed
- first implementation slice recommendation is shell foundation only and clearly scoped

Decision should be escalated/reworked (do not finalize) if:
- evidence does not cover shell composition ergonomics
- behavior-preservation risks are unaddressed
- the decision relies primarily on preference or “future potential” with weak prototype evidence

---

## 6) Open Questions / TBDs (keep explicit)

- `TBD:` Whether Y2 prototypes will live in this repo (spike branches) or a separate spike repo.
- `TBD:` Whether both frameworks must use the same shell mock content density fixture for closer comparison, or only equivalent behavior demonstrations.
- `TBD:` Whether any lightweight qualitative timing metrics (startup interaction snapshots) are required for Y2, or observations remain qualitative-only.

