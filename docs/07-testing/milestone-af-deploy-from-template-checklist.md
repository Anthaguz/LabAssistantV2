# Milestone AF Deploy From-Template Checklist

**Purpose:** Manual closure checklist for Milestone AF Deploy from-template convergence (`#322`, `#323`, `#324`, `#325`, `#326`).

**Scope:** WinUI Deploy from-template route, readiness/correction behavior, and compact-first results presentation.

---

## 1) Route and Surface Sanity

- [ ] Launch WinUI and navigate to Deploy capability.
- [ ] Confirm AF path lands on `deploy.from_template`.
- [ ] Confirm deploy surface loads with template selector, compact status strip, correction actions, and results regions.

## 2) Template Selection and Readiness

- [ ] Select a template and confirm readiness evaluation updates automatically (or via Evaluate action).
- [ ] Confirm compact readiness summary clearly reflects pass/warn/fail state.
- [ ] Confirm status strip remains visible while evaluating readiness.

## 3) Blocking and Warning Validation

- [ ] Validate unresolved/ambiguous required disk identity blocks deploy start.
- [ ] Validate partial/missing switch mapping surfaces warning guidance.
- [ ] Confirm blocked state message is explicit and actionable (no silent failure).

## 4) Correction Flows

- [ ] Execute `Resolve Suggestions` and confirm readiness state updates afterward.
- [ ] Execute `Open in Templates Editor` and confirm template context handoff is preserved.
- [ ] Return to Deploy and confirm updated state is reflected.

## 5) Execution Flow Sanity

- [ ] Confirm deploy start is enabled only when unblocked.
- [ ] Start deploy in a non-blocking scenario and verify status/progress updates through lifecycle.
- [ ] Confirm final summary includes concise counts and per-VM result states.

## 6) AF4 Results UX Verification

- [ ] Sticky compact strip remains visible during run lifecycle.
- [ ] Per-VM rows remain concise by default.
- [ ] Per-VM detail panels are collapsed by default and expandable on demand.
- [ ] Global warnings/errors drawer is collapsed by default.
- [ ] Global issues badge/count updates when issues exist.

## 7) Layout and Overflow Sanity

- [ ] Compact width: controls remain reachable (no clipped critical actions).
- [ ] Normal width: row readability and progress strip clarity are maintained.
- [ ] Wide width: no dead-zone or unbounded growth regression.
- [ ] Scroll ownership remains usable (no runaway vertical expansion).

## 8) Results Recording Template

- **Build/commit tested:** `<commit>`
- **Environment:** `<OS version / WinAppSDK / .NET SDK>`
- **Result by section:**
  - Route and Surface Sanity: `Pass | Fail`
  - Template Selection and Readiness: `Pass | Fail`
  - Blocking and Warning Validation: `Pass | Fail`
  - Correction Flows: `Pass | Fail`
  - Execution Flow Sanity: `Pass | Fail`
  - AF4 Results UX Verification: `Pass | Fail`
  - Layout and Overflow Sanity: `Pass | Fail`
- **Findings / limitations:**
  - `<item>`

---

## Open Questions / TBDs

- `TBD`: whether AF closure should mandate one standardized blocking-disk fixture template and one warning-only switch-mapping fixture for repeatable manual runs.
