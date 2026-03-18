# Milestone AM Deploy Extraction Checklist

## Purpose

Manual runtime verification checklist for Milestone AM (`Deploy extraction closure`).

Use this checklist after AM automation passes to confirm the final shared Deploy workspace model, Overview lane, From Template lane, and Quick Deploy lane remain usable on a real Windows machine without regressing long-lived workspace behavior.

## Scope

- Shared Deploy workspace ownership and `deploy.overview` default-route behavior
- Overview summary and navigation sanity
- From Template selection, review, grouped-issue, deploy, and results sanity
- Quick Deploy collection, editor, readiness, deploy, and results sanity
- Shell/title ownership sanity for Deploy
- Long-lived Deploy workspace and route-switching sanity

## Out of Scope

- Runtime redesign
- Business/domain semantic changes
- WPF behavior
- New CI/workflow behavior
- Logging redesign

## Preconditions

- Build under test is from the target AM104 closure commit/branch
- WinUI app launches successfully
- At least one template exists if From Template flows are to be exercised fully
- Quick Deploy reference data is available enough to exercise editor/readiness flows if practical
- Hyper-V host access is available if real deployment execution is to be exercised end to end

## 1. Deploy Overview Default-Route Behavior

- Open parent `Deploy` from the global navigation
- Confirm it resolves to `deploy.overview`
- Confirm the shell title/description represent the capability context rather than a child view owning a duplicate page banner
- Confirm local navigation exposes:
  - `Overview`
  - `Quick Deploy`
  - `From Template`
- Confirm `Overview` is selected first when entering Deploy from outside the capability

## 2. Overview Summary / Navigation Surface Sanity

- Stay on `Deploy > Overview`
- Confirm the summary surface shows Quick Deploy draft/readiness context plus From Template inventory/status context
- If template inventory is still loading, confirm the Overview summary reflects loading rather than stale zero-state wording
- Click the Quick Deploy overview action
- Confirm navigation moves to `deploy.on_the_fly`
- Return to `Deploy > Overview`
- Click the From Template overview action
- Confirm navigation moves to `deploy.from_template`

## 3. From Template Selection / Review / Grouped-Issue / Deploy / Results Sanity

- Navigate to `Deploy > From Template`
- Confirm the route resolves to `deploy.from_template`
- Confirm template selector, review/remediation surface, and results/right-panel surface are present without a redundant nested page banner
- Select a template and confirm selection/review state updates coherently
- Trigger readiness evaluation and confirm grouped blocking/warning issue state is explicit and actionable
- If practical, use `Resolve Suggestions` and confirm issue/readiness state refreshes coherently
- If practical, start a deploy and confirm progress plus result rows update coherently through terminal state
- Switch away to `Overview` or `Quick Deploy`, then return to `From Template`
- Confirm the Deploy workspace stays alive and the route activation behaves like a refresh/reconcile pass rather than a full workspace recreation

## 4. Quick Deploy Collection / Editor / Readiness / Deploy / Results Sanity

- Navigate to `Deploy > Quick Deploy`
- Confirm the route resolves to `deploy.on_the_fly`
- Confirm VM entries list, editor surface, readiness summary, and results/right-panel surface are present without a redundant nested page banner
- Add or select a VM entry and confirm editor state updates coherently
- Edit VM fields and confirm draft/readiness affordances update while staying local to the Quick Deploy workflow
- Trigger readiness evaluation and confirm blocking vs warning state remains explicit
- If practical, start a deploy and confirm progress plus result rows update coherently through terminal state
- Switch away to `Overview` or `From Template`, then return to `Quick Deploy`
- Confirm the Deploy workspace stays alive and the route activation behaves like a refresh/reconcile pass rather than a full workspace recreation

## 5. Shell / Title Ownership Sanity for Deploy

- Navigate across:
  - `Deploy > Overview`
  - `Deploy > Quick Deploy`
  - `Deploy > From Template`
- Confirm the shell remains the owner of page-level title/description context
- Confirm child views do not reintroduce large duplicate page-title bands
- Confirm local operational section headings still exist where needed for workflow clarity

## 6. Long-Lived Deploy Workspace / Route Switching Sanity

- From `Deploy > Quick Deploy`, create a visible draft or selection state if practical
- Move to `Deploy > Overview`
- Move back to `Deploy > Quick Deploy`
- Confirm the capability behaves like one long-lived Deploy workspace rather than a newly constructed surface each time
- Repeat the same route-switching sanity pass for `Deploy > From Template`
- Confirm route activation visibly refreshes/reconciles the active lane without collapsing Deploy workflow/state back into `MainWindow`

## Result Record

- Commit tested:
- Environment:
  - Windows version:
  - App build type:
  - Hyper-V available:
  - Templates available:
  - Quick Deploy reference data available:
- Section 1 Deploy Overview Default-Route Behavior: Pass / Fail
- Section 2 Overview Summary / Navigation Surface Sanity: Pass / Fail
- Section 3 From Template Selection / Review / Grouped-Issue / Deploy / Results Sanity: Pass / Fail
- Section 4 Quick Deploy Collection / Editor / Readiness / Deploy / Results Sanity: Pass / Fail
- Section 5 Shell / Title Ownership Sanity for Deploy: Pass / Fail
- Section 6 Long-Lived Deploy Workspace / Route Switching Sanity: Pass / Fail
- Findings / follow-up observations:

## Open Questions / TBDs

- Whether a future closure pass should add a dedicated Deploy compact-width exploratory checklist beyond the current extraction-closure scope
- Whether later milestones should add one shared cross-capability workspace-lifetime checklist once more extracted capabilities converge on the same long-lived pattern
