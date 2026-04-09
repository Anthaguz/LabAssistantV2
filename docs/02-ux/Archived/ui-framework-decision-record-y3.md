# UI Framework Decision Record (Y3)

> Historical note: this planning or decision doc is retained for reference only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Issue:** #253 - Y3: Produce UI framework decision record and migration-shell implementation recommendation  
**Milestone:** Y - UI Framework Decision Spike  
**Decision date:** 2026-02-27

**Purpose:** Finalize the framework decision using Y1 rubric + Y2 spike evidence and define the first post-Y implementation slice.

**Related:**
- `docs/02-ux/Archived/ui-framework-decision-rubric.md`
- `docs/02-ux/Archived/ui-framework-spike-findings-y2.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`
- `docs/02-ux/Archived/migration-preservation-matrix.md`
- `spikes/ui-framework-shell-spike/README.md`
- `spikes/ui-framework-shell-spike/shared-shell-fixture.md`

---

## 1) Decision

**Chosen framework:** `WinUI 3`

### Decision rationale
- Y2 shell spikes proved both frameworks can represent capability scope/context scope, dense shell composition, and shell-level issue surfacing.
- WinUI 3 showed stronger alignment with the target UX direction (modern Windows shell look/interaction language), which is a stated product objective.
- After post-spike stabilization fixes, WinUI 3 shell parity was achieved for fixture density and interaction coverage, removing the initial "incomplete comparison" blocker.
- The remaining WinUI 3 concerns are operational and delivery risks, not architecture blockers. Those are accepted with explicit mitigations and sequencing.

---

## 2) Rejected Alternative and Tradeoffs

**Rejected option:** `WPF modernized`

### Why not selected
- WPF modernization remains lower-risk for near-term delivery and has stronger immediate team familiarity, but it underperforms on long-term UX modernization potential relative to project direction.
- Selecting WPF would likely optimize short-term migration comfort while increasing long-term design-system burden to reach the intended Windows 11-quality experience.

### Tradeoffs accepted by choosing WinUI 3
- Higher upfront framework/tooling/runtime setup cost.
- Higher early migration execution risk vs WPF.
- Need for stricter incremental rollout discipline to preserve behavior semantics.

---

## 3) Rubric Evidence Summary (Criterion-by-Criterion)

Source basis:
- Y2 findings doc (`ui-framework-spike-findings-y2.md`)
- Spike artifacts in `spikes/ui-framework-shell-spike/`
- Post-Y2 WinUI stabilization/parity updates merged in PR #259

### A. Windows 11 UX fidelity potential
- **Decision signal:** favors WinUI 3
- **Evidence:** native control set and shell idioms align with target direction; parity fixture demonstrates the intended shell form without heavy custom skinning.

### B. Navigation/shell composition ergonomics
- **Decision signal:** near tie, slight WinUI 3 edge after parity fixes
- **Evidence:** both support capability/context scope pattern; WinUI 3 parity fixture now supports required interactions and shell issue feed concept cleanly.

### C. Dense workflow surface support
- **Decision signal:** slight WPF edge, acceptable for WinUI 3
- **Evidence:** WPF remains naturally strong for dense legacy-style layouts; WinUI 3 fixture reached workable parity but needs deeper validation during shell-first implementation.

### D. MVVM/testability fit
- **Decision signal:** WPF edge, acceptable for WinUI 3
- **Evidence:** WPF has lower immediate friction; WinUI 3 is viable with explicit binding/state patterns and stricter conventions.

### E. Integration cost with existing layers
- **Decision signal:** WPF edge
- **Evidence:** WinUI requires higher UI-layer adaptation cost; lower layers remain reusable, so risk is bounded to presentation/composition and app-shell wiring.

### F. Migration/behavior-preservation risk
- **Decision signal:** WPF edge, risk acceptable
- **Evidence:** framework switch raises risk surface, but risk is manageable with shell-first slice boundaries and preservation gates.

### G. Tooling maturity/productivity/debugging ergonomics
- **Decision signal:** WPF edge in short term
- **Evidence:** WinUI showed setup/runtime friction during spike; this is accepted as a known cost with mitigation.

### H. Performance/responsiveness expectations
- **Decision signal:** inconclusive
- **Evidence:** Y2 remains qualitative. No measured performance benchmark was run. Decision proceeds with explicit follow-up validation in first implementation slice.

### I. Packaging/runtime implications
- **Decision signal:** WPF edge in certainty, not a blocker to WinUI
- **Evidence:** WinUI runtime/packaging complexity is real. This is deferred as an execution risk to manage with early operational validation.

---

## 4) Accepted Risks vs Deferred Risks

## 4.1 Accepted now
- WinUI setup/runtime complexity for developer machines.
- Initial productivity dip while patterns/tooling stabilize.
- Additional shell composition/binding rigor needed versus WPF.

## 4.2 Deferred (must be retired early post-Y)
- Dense-page responsiveness confidence beyond qualitative evidence.
- Packaging/runtime deployment confidence for production rollout.
- Finalized team playbook for WinUI debugging/tooling consistency.

---

## 5) Risk Mitigations and Ownership

1. **Risk:** behavior regressions during shell migration  
   **Mitigation:** shell-first scope isolation + preservation gates from matrix + readiness/cancel/summary semantics protected by explicit non-goals  
   **Owner:** migration implementation lead (post-Y milestone owner)

2. **Risk:** WinUI runtime/tooling instability across dev environments  
   **Mitigation:** standardized bootstrap/build/run playbook, pinned SDK/workload guidance, preflight env checklist  
   **Owner:** engineering lead + dev experience owner

3. **Risk:** dense-surface UX regressions in Deploy-adjacent flows  
   **Mitigation:** shell slice keeps Deploy semantics untouched; add focused manual/automated UX checks for dense shell regions before expanding scope  
   **Owner:** UI implementation + QA verification owner

4. **Risk:** decision confidence affected by incomplete quantitative evidence  
   **Mitigation:** capture structured runtime observations during first slice and gate expansion on evidence review  
   **Owner:** milestone owner with PM review

---

## 6) Confidence and Remaining Evidence Gaps

**Confidence level:** `Medium`

Why medium:
- Strong enough evidence for shell-foundation direction and framework fit.
- Remaining uncertainty is concentrated in operational/delivery concerns (tooling, packaging, dense-surface performance), not in conceptual shell viability.

Remaining evidence gaps:
- No formal measured performance benchmark from Y2.
- No production packaging validation in Y2.
- Team-wide WinUI setup consistency not yet proven.

Decision approach:
- Proceed with WinUI 3 and retire gaps in the first implementation slice before widening migration scope.

---

## 7) First Implementation Slice Recommendation (Post-Y)

**Slice name:** `Shell Foundation (WinUI 3) - Behavior-Preserving`

## 7.1 In scope
- New shell frame with capability scope and context scope navigation behavior.
- Hamburger mode switching pattern and active capability state ownership.
- Shell-level issue feed placement concept (behavior-preserving, not feature redesign).
- Dense placeholder workspace region sufficient to validate shell composition under load.
- Diagnostics hooks needed to keep shell-level issue visibility intact.

## 7.2 Out of scope
- Deploy page migration
- Templates/Assets migration
- Machines capability implementation
- New readiness checks or readiness semantic changes
- Guest-step feature completion
- Full visual design system rollout

## 7.3 Preservation constraints (non-negotiable)
- Readiness semantics remain unchanged (quick/full behavior, blocking/warn contract, deploy gating untouched).
- Cleanup/cancellation/summary semantics untouched.
- Error feed behavior remains operationally equivalent (dismiss/surface/deep-link expectations preserved).
- No business-layer behavior changes are introduced by shell slice work.

## 7.4 Validation expectations for this slice
- Runnable shell with capability/context scope switching and issue feed interactions.
- Evidence that current behavior contracts are not altered (tests + manual verification checklist).
- Explicit checklist sign-off before authorizing next migration slice.

---

## 8) Outcome for Milestone Y

Milestone Y can be closed after this decision record:
- framework choice is explicit and evidence-traceable
- rejected alternative and tradeoffs are documented
- risks/mitigations are explicit
- post-Y first implementation slice is defined and bounded

## Open Questions / TBDs
- `TBD:` Exact post-Y milestone numbering and sequencing relative to Milestone Z contract work.
- `TBD:` Whether lightweight perf instrumentation is added in shell-first slice start or as an immediate follow-up gate.
