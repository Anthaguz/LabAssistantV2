# UI Framework Spike Findings (Y2) — Shell Foundation Comparison

**Purpose:** Capture comparative spike findings for the Milestone Y framework decision path (`#252`) using the rubric from `docs/02-ux/ui-framework-decision-rubric.md`.

**Status:** Y2 evidence package (spike findings). This is **not** the final framework decision record (`#253`).

**Related:**
- `docs/02-ux/ui-framework-decision-rubric.md`
- `docs/02-ux/ui-migration-execution-plan.md`
- `spikes/ui-framework-shell-spike/README.md`
- `spikes/ui-framework-shell-spike/shared-shell-fixture.md`

---

## 1) Explicit Y2 choices (required open questions)

### 1.1 Prototype location choice

**Choice:** In-repo, isolated spike folder

- Location: `spikes/ui-framework-shell-spike/`
- Rationale:
  - keeps artifacts reviewable in the same PR/history as the decision evidence
  - avoids changing production app code or solution wiring
  - makes side-by-side prototype comparison easier in one branch

### 1.2 Dense-shell fixture comparability

**Choice:** Yes — both prototypes use the same shell fixture and interaction scenarios

- Shared fixture contract: `spikes/ui-framework-shell-spike/shared-shell-fixture.md`
- Rationale:
  - improves fairness of shell ergonomics comparison
  - reduces risk of “framework A got a simpler test”

### 1.3 Timing metrics vs qualitative evidence

**Choice:** Qualitative evidence only for Y2 (no lightweight timing metrics collected)

- Rationale:
  - Y2 scope is shell ergonomics/prototype feasibility, not performance benchmarking
  - CLI/headless environment limited reliable interactive runtime measurement
  - performance/responsiveness criterion is still addressed with qualitative observations and identified risks

**Carry-forward note for #253:** If rubric scoring remains close, a small follow-up timing probe may be justified before final decision.

---

## 2) Prototype artifacts and scope boundaries

## 2.1 Prototype artifacts (in-repo, isolated)

- `spikes/ui-framework-shell-spike/wpf-shell-spike/`
  - WPF shell foundation spike (ViewModel-driven shell interactions + dense placeholder region)
- `spikes/ui-framework-shell-spike/winui3-shell-spike/`
  - WinUI 3 shell foundation spike (ViewModel-driven shell interactions + dense placeholder region)
- `spikes/ui-framework-shell-spike/shared-shell-fixture.md`
  - common fixture both prototypes implement

## 2.2 What was intentionally prototyped (Y2 in scope)

- Capability Scope navigation concept (hamburger/menu mode)
- Context Scope left-panel behavior
- switching between capability and context scopes
- shell-level error feed placement concept
- dense content placeholder region to assess shell/layout ergonomics
- ViewModel-driven state interactions (not static mockup only)

## 2.3 What was intentionally not attempted (Y2 out of scope)

- full Deploy migration
- production-ready styling/theme system
- complete Machines capability
- feature parity migration
- release packaging validation
- behavior-preservation proof beyond shell/navigation ergonomics and composition

---

## 3) Build/run commands and environment notes (Y2 validation)

### 3.1 Commands used / intended

WPF spike:
- `dotnet build spikes/ui-framework-shell-spike/wpf-shell-spike/WpfShellSpike.csproj -c Debug`
- `dotnet run --project spikes/ui-framework-shell-spike/wpf-shell-spike/WpfShellSpike.csproj`

WinUI 3 spike:
- `dotnet build spikes/ui-framework-shell-spike/winui3-shell-spike/WinUi3ShellSpike.csproj -c Debug`
- `dotnet run --project spikes/ui-framework-shell-spike/winui3-shell-spike/WinUi3ShellSpike.csproj`

### 3.2 Environment limitations (important)

- This Codex CLI environment has a known local `.NET` shell issue where some `dotnet build/test` commands can fail with no diagnostics or stall during restore.
- GUI execution and screenshot/video capture are not reliable in this environment.
- Therefore, this Y2 evidence package is based on:
  - prototype artifact implementation
  - code-level ergonomics comparison
  - framework-specific implementation friction observed while authoring the spikes

**Expected local follow-up before/with #253:**
- run both prototypes on a local Windows dev machine
- capture screenshots/video
- add runtime interaction observations to refine scores if needed

---

## 4) Comparative findings mapped to rubric (Y2 evidence)

Scoring scale:
- `1` poor fit / high risk
- `3` acceptable with notable tradeoffs
- `5` strong fit / low risk

Scores are **preliminary Y2 spike scores** and should be finalized (or adjusted) in `#253` after local runtime evidence capture.

---

## 4.1 WPF Modernized (prototype findings)

| Criterion | Score | Evidence (Y2) | Risk note |
|---|---:|---|---|
| A. Windows 11 UX fidelity potential | 3 | Shell spike can represent the navigation model and dense layout using standard WPF primitives; achieving Windows 11 feel requires more custom styling/templates. | Higher styling/control-template effort to reach modern feel without visual drift. |
| B. Shell composition ergonomics | 4 | WPF spike shell built quickly with explicit `Grid` regions, left-pane mode switching, and shell-issue rail using straightforward XAML bindings. | Custom shell semantics remain manual (fewer built-in modern shell patterns). |
| C. Dense workflow surface support | 5 | WPF handles dense, nested layout + constrained readiness details + multiple scroll regions with low friction (mirrors existing app strengths). | Easy to recreate current density patterns can bias toward preserving old layout habits. |
| D. MVVM/testability fit | 5 | Existing LabAssistant app already proves WPF/MVVM command/state-heavy workflows; spike ViewModel pattern is trivial to implement and reason about. | Risk is incremental complexity from legacy WPF patterns if modernization discipline is weak. |
| E. Integration cost with existing layers | 5 | Lowest migration friction: current app is WPF, existing ViewModels/commands/bindings map naturally; shell-first slice can reuse many patterns. | Risk of underestimating modernization debt if “WPF modernized” remains too close to current shell patterns. |
| F. Migration / behavior-preservation risk | 4 | Lower framework-switch risk; preserves dispatcher/XAML/runtime behavior characteristics closest to current app. | Can still regress behavior if shell/IA redesign is mixed with migration without strict preservation checks. |
| G. Tooling/productivity/debugging ergonomics | 4 | Strong team familiarity + existing project patterns; spike authoring is fast and low-friction in repo. | WPF designer/tooling can still be inconsistent; modernization UX polish work may slow iteration. |
| H. Performance/responsiveness expectations | 4 | Qualitative expectation favorable for shell + dense pages given current app behavior and dense-layout fit; spike fixture implementation straightforward. | Evidence incomplete: no runtime interactive measurements collected in this environment. |
| I. Packaging/runtime implications (high-level) | 4 | Minimal packaging shift from current app path; operational/deployment implications likely lower risk than framework switch. | Evidence incomplete: no packaging spike or distribution validation attempted (out of scope). |

### WPF modernized — Top strengths observed (Y2)
- Lowest integration/migration friction with existing command/state-heavy workflows and app layers
- Strong fit for dense workflow surfaces and constrained multi-region layouts
- Fast iteration for shell composition prototype in current repo/tooling context

### WPF modernized — Top weaknesses/risks observed (Y2)
- Modern Windows 11 visual fidelity likely requires more deliberate custom styling effort
- Risk of “modernized WPF” drifting into a cosmetic refresh without shell/IA improvement discipline
- Some evidence still qualitative-only (runtime UX/perf and packaging implications)

### WPF modernized — Unresolved questions
- How much styling/template effort is acceptable for a Windows 11-quality shell before migration speed suffers?
- Does the team want a design-system/token layer early, or staged after shell-first slice?

---

## 4.2 WinUI 3 (prototype findings)

| Criterion | Score | Evidence (Y2) | Risk note |
|---|---:|---|---|
| A. Windows 11 UX fidelity potential | 5 | WinUI 3 spike structure aligns naturally with modern Windows control set and visual idioms; lower conceptual friction for Windows 11 feel. | Achieving product-quality polish still requires design work; “native look” alone does not solve workflow usability. |
| B. Shell composition ergonomics | 4 | Shell concept is representable; WinUI controls (e.g., `InfoBadge`) support modern shell affordances well. | Command bindings and template-relative bindings in prototype are more finicky; shell composition requires learning curve. |
| C. Dense workflow surface support | 3 | Dense fixture is representable, but complexity risk is higher: control behavior/layout interactions need more runtime validation for dense enterprise-style surfaces. | Evidence incomplete without reliable local runtime run in this environment; dense Deploy-like surfaces remain a key risk. |
| D. MVVM/testability fit | 3 | MVVM is feasible, but prototype wiring suggests more framework-specific friction (binding syntax/relative binding patterns, control behaviors). | Migration to WinUI raises adaptation cost for current WPF-heavy ViewModel/binding patterns. |
| E. Integration cost with existing layers | 3 | Lower layers remain reusable, but UI layer migration cost is materially higher than WPF modernization due to framework switch and shell infrastructure replacement. | Risk of hidden adapter/shim effort and migration slowdown before user-visible gains stabilize. |
| F. Migration / behavior-preservation risk | 3 | Framework switch increases risk surface while preserving readiness/summary/cancel/diagnostics behaviors. | Behavior-preservation proof will require stricter incremental boundaries and regression checks in shell-first rollout. |
| G. Tooling/productivity/debugging ergonomics | 3 | Potentially strong long-term, but immediate spike authoring indicates higher setup/tooling friction in this environment (package/runtime/toolchain sensitivity). | Team productivity risk if toolchain/workload setup is inconsistent across dev machines. |
| H. Performance/responsiveness expectations | 3 | Modern framework potential is good, but Y2 evidence is qualitative and incomplete for dense-shell responsiveness in this environment. | Evidence incomplete: no runtime interaction metrics or reliable local GUI run captured here. |
| I. Packaging/runtime implications (high-level) | 2 | Framework switch introduces more packaging/runtime considerations than WPF modernization; Y2 did not validate these. | Evidence incomplete and higher uncertainty; packaging/runtime implications could materially affect rollout planning. |

### WinUI 3 — Top strengths observed (Y2)
- Strong Windows 11 UX fidelity potential and modern shell control affordances
- Good fit for future-facing shell modernization direction conceptually
- Supports a cleaner break from legacy WPF shell patterns if chosen deliberately

### WinUI 3 — Top weaknesses/risks observed (Y2)
- Higher migration and behavior-preservation risk versus WPF modernization
- More immediate tooling/setup friction and prototype authoring friction in current environment
- Dense workflow surface suitability requires stronger runtime evidence before final decision

### WinUI 3 — Unresolved questions
- Can dense Deploy-like surfaces remain as usable/responsive without heavy custom layout tuning?
- What is the actual tooling/runtime friction on the team’s standard Windows dev machines (beyond this CLI environment)?
- Are packaging/runtime implications acceptable for the planned rollout cadence?

---

## 5) Cross-option observations (Y2 comparative notes)

## 5.1 What the shell fixture proved

The shared fixture was sufficient to compare:
- shell scope-switching concept implementation ergonomics
- left-panel composition patterns
- shell-level issue feed placement concept
- dense content + shell-rail layout tension

This means Y2 covered the required shell-scope ergonomics evidence category from `#251`.

## 5.2 What Y2 did **not** prove (and should not claim)

Y2 did not prove:
- full behavior-preservation feasibility for Deploy migration
- final visual design quality
- production performance characteristics
- packaging/runtime deployment readiness

These remain either:
- Y3 decision risk considerations, or
- later migration implementation/milestone concerns

## 5.3 Preliminary comparison signal (not final decision)

Based on Y2 spike implementation evidence:
- **WPF modernized** currently shows lower migration risk and lower implementation friction
- **WinUI 3** shows higher Windows 11 UX potential but higher uncertainty/risk in dense workflow and migration execution

This is a **signal**, not the final decision. `#253` must produce the formal decision record and may revise scores with local runtime evidence.

---

## 6) Evidence artifacts captured in Y2

### 6.1 Prototype code artifacts
- `spikes/ui-framework-shell-spike/wpf-shell-spike/*`
- `spikes/ui-framework-shell-spike/winui3-shell-spike/*`
- `spikes/ui-framework-shell-spike/shared-shell-fixture.md`

### 6.2 Comparative evidence notes
- This document (`docs/02-ux/ui-framework-spike-findings-y2.md`)

### 6.3 Screenshots / video capture
- **Not captured in this CLI environment** (headless/tooling limitation)
- Carry-forward requirement for `#253` prep:
  - capture local Windows screenshots and/or short videos for both prototypes and attach/reference them in the decision record

---

## 7) Explicit carry-forward items for #253 (decision record)

- Confirm/adjust rubric scores using local prototype runtime observations (especially C/H/I criteria)
- Include screenshot/video evidence references
- Decide whether the remaining evidence gaps are acceptable or require a small follow-up spike
- Produce final chosen/rejected option rationale and shell-first implementation recommendation

## Open Questions / TBDs (carry forward)

- `TBD:` Do both frameworks need a lightweight local runtime demo recording on the same machine before Y3 scores are finalized?
- `TBD:` Is a small packaging/runtime sanity check needed for WinUI 3 before final decision, or can that remain a tracked migration risk?
- `TBD:` Should Y3 use weighted rubric scoring, or equal weighting with narrative priority on behavior-preservation and shell ergonomics?

