# Milestone AG - Deploy On-the-Fly Verification Checklist

**Purpose:** Manual closure checklist for Milestone AG (`#332`-`#336`) covering WinUI Deploy on-the-fly routing, readiness/gating, correction actions, execution behavior, and compact results UX.

**Related contract references**
- `docs/01-requirements/srs.md` (`FR-091`..`FR-094`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-015`)
- `docs/02-ux/winui-deploy-on-the-fly-contract-ag.md`
- Automated matrix: `LabAssistant.UI.Tests/Tests/MilestoneAGScenarioMatrixTests.cs`

---

## 1) Route and Surface Sanity

- [ ] Launch WinUI app and navigate to Deploy capability.
- [ ] Confirm on-the-fly route is reachable (`deploy.on_the_fly`).
- [ ] Confirm surface shows three functional regions:
  - VM entries list
  - VM properties editor
  - Deployment results pane
- [ ] Confirm from-template deploy route remains reachable and unaffected.

**Expected**
- Deploy on-the-fly surface loads with no crashes or empty-host regressions.
- Global nav behavior remains stable while switching between Deploy subviews.

---

## 2) Input and Configuration Flow Sanity

- [ ] Add a VM entry and confirm default naming uses `Quick VM <n>` format.
- [ ] Select VM and edit name, memory, and CPU fields.
- [ ] Verify base disk dropdown loads catalog-backed options.
- [ ] Verify switch dropdown loads host switch options.
- [ ] Apply VM changes and confirm updates persist in list/editor state.

**Expected**
- VM properties panel is scrollable when content exceeds visible area.
- Apply action remains reachable (pinned at panel bottom).
- VM entries list avoids GUID clutter (name-first presentation).

---

## 3) Readiness - Blocking vs Warning

- [ ] Run **Evaluate** on a valid configuration.
- [ ] Run **Evaluate** on a configuration with at least one blocking condition (for example unresolved required disk identity).
- [ ] Run **Evaluate** on a configuration with warning-only condition(s) (for example partial non-blocking mapping guidance).
- [ ] Verify Start Deploy button enablement tracks readiness gating.

**Expected**
- Blocking findings prevent start.
- Warning-only findings allow start.
- Readiness/result rows prioritize problematic VMs (pass-only VMs not shown as issues).
- Summary wording uses actionable counts (`Blocking: X | Warnings: Y`).

---

## 4) Correction Actions

- [ ] Use **Resolve Suggestions** and confirm readiness state updates after applying suggestions.
- [ ] Use **Open in Templates Editor** and confirm handoff to Templates editor with context loaded.
- [ ] Return to Deploy on-the-fly and re-evaluate readiness.

**Expected**
- Correction actions are wired and visible.
- Context handoff remains coherent.
- No silent failures in correction paths.

---

## 5) Execution Flow Sanity

- [ ] Start deploy from an unblocked readiness state.
- [ ] Observe lifecycle progression in compact summary strip.
- [ ] Confirm terminal state and status summary are shown when operation completes.
- [ ] Execute at least one failure-path run (if practical) to confirm explicit terminal failure messaging.

**Expected**
- Deploy start path uses existing orchestration semantics.
- Blocking readiness state does not fall through to execution.
- Success/failure terminal outcomes are explicit and non-silent.

---

## 6) Compact Results UX Checks

- [ ] Confirm compact top strip remains visible during run.
- [ ] Confirm per-VM result rows are concise and readable.
- [ ] Confirm VM detail sections are collapsed by default and expandable on demand.
- [ ] Confirm issue summary badge is visible and updates with blocking/warning counts.
- [ ] Confirm details text wraps (no unreadable clipped lines).

**Expected**
- Results remain compact-first, with details available on demand.
- Per-VM context is primary for troubleshooting.

---

## 7) Layout / Overflow / Scroll Ownership

- [ ] Verify behavior at compact, normal, and wide window sizes.
- [ ] Confirm no unbounded vertical growth in Deploy on-the-fly surface.
- [ ] Confirm VM editor scroll ownership is local to the properties panel.
- [ ] Confirm results pane remains usable at narrow widths (no hidden primary actions).

**Expected**
- Surface remains operable across size bands.
- Scroll behavior is predictable and bounded.

---

## 8) Run Record Template

Use this table for each verification run.

| Field | Value |
|---|---|
| Date/Time |  |
| Tester |  |
| Build commit |  |
| Environment |  |
| Hyper-V host details |  |
| Result summary | Pass / Partial / Fail |
| Blocking findings |  |
| Non-blocking findings |  |
| Follow-up issue(s) opened |  |

---

## Manual Notes / Findings

- Notes:
  - 
- Limitations:
  - 
