# Milestone AM Diagnostics Extraction Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


## Purpose

Manual runtime verification checklist for Milestone AM (`Diagnostics extraction closure`).

Use this checklist after AM automation passes to confirm the final shared Diagnostics workspace model, Overview lane, and Logs lane remain usable on a real Windows machine without regressing long-lived workspace behavior.

## Scope

- Shared Diagnostics workspace ownership and `diagnostics.overview` default-route behavior
- Overview summary and action sanity
- Logs filter, selection, detail, reload, clear, and open-location sanity
- Shell/title ownership sanity for Diagnostics
- Long-lived Diagnostics workspace and route-switching sanity

## Out of Scope

- Runtime redesign
- Business/domain semantic changes
- WPF behavior
- New CI/workflow behavior
- Logging redesign

## Preconditions

- Build under test is from the target AM121 closure commit/branch
- WinUI app launches successfully
- Structured diagnostics logging is enabled for the app build under test
- At least one structured log file exists if Logs interactions are to be exercised fully

## 1. Diagnostics Overview Default-Route Behavior

- Open parent `Diagnostics` from the global navigation
- Confirm it resolves to `diagnostics.overview`
- Confirm the shell title/description represent the capability context rather than a child view owning a duplicate page banner
- Confirm local navigation exposes:
  - `Overview`
  - `Logs`
- Confirm `Overview` is selected first when entering Diagnostics from outside the capability

## 2. Overview Summary / Actions Sanity

- Stay on `Diagnostics > Overview`
- Confirm the summary surface shows structured log/support context rather than a blank or duplicated shell-level header
- If Logs are still loading, confirm the summary text reflects loading rather than stale zero-state wording
- Click the Overview action that opens Logs
- Confirm navigation moves to `diagnostics.logs`
- Return to `Diagnostics > Overview`
- Trigger the support/log-location action
- Confirm the action is reachable and any resulting status text remains coherent

## 3. Logs Filter / Query Sanity

- Navigate to `Diagnostics > Logs`
- Confirm the route resolves to `diagnostics.logs`
- Confirm filter inputs and primary actions are present without a redundant nested page banner
- Edit one or more filter fields such as operation ID, level, event, or text search
- Confirm filter-state changes are reflected coherently in the view
- Apply filters
- Confirm the results/status text updates coherently for the current query
- Clear filters
- Confirm the filter inputs reset and the view returns to the expected cleared state

## 4. Logs Selection / Detail Sanity

- Stay on `Diagnostics > Logs`
- Select a structured log row if entries are available
- Confirm selection details update coherently, including envelope/context text
- Change selection to a different row and confirm the detail surface updates without requiring a full page reload
- If no entries are available, confirm the empty or no-selection messaging remains explicit and stable

## 5. Logs Reload / Clear / Open-Location Sanity

- From `Diagnostics > Logs`, trigger reload
- Confirm the loading state is explicit and returns to a coherent terminal status
- Re-apply a filter and then clear it again
- Confirm clear remains functional after reload
- Trigger the open raw JSONL / open log location action
- Confirm the action is reachable and any resulting status text remains coherent

## 6. Shell / Title Ownership Sanity for Diagnostics

- Navigate across:
  - `Diagnostics > Overview`
  - `Diagnostics > Logs`
- Confirm the shell remains the owner of page-level title/description context
- Confirm child views do not reintroduce large duplicate page-title bands
- Confirm local operational headings still exist where needed for summary, filters, results, and detail clarity

## 7. Long-Lived Diagnostics Workspace / Route Switching Sanity

- From `Diagnostics > Logs`, enter a visible filter or select a log row if practical
- Move to `Diagnostics > Overview`
- Move back to `Diagnostics > Logs`
- Confirm the capability behaves like one long-lived Diagnostics workspace rather than a newly constructed surface each time
- Repeat the route-switching pass in the opposite direction starting from `Diagnostics > Overview`
- Confirm route activation visibly refreshes/reconciles the active lane without collapsing Diagnostics-local state/workflow back into `MainWindow`

## Result Record

- Commit tested:
- Environment:
  - Windows version:
  - App build type:
  - Structured log data available:
- Section 1 Diagnostics Overview Default-Route Behavior: Pass / Fail
- Section 2 Overview Summary / Actions Sanity: Pass / Fail
- Section 3 Logs Filter / Query Sanity: Pass / Fail
- Section 4 Logs Selection / Detail Sanity: Pass / Fail
- Section 5 Logs Reload / Clear / Open-Location Sanity: Pass / Fail
- Section 6 Shell / Title Ownership Sanity for Diagnostics: Pass / Fail
- Section 7 Long-Lived Diagnostics Workspace / Route Switching Sanity: Pass / Fail
- Findings / follow-up observations:

## Open Questions / TBDs

- Whether a future closure pass should add a dedicated Diagnostics compact-width exploratory checklist beyond the current extraction-closure scope
- Whether later milestones should add one shared cross-capability workspace-lifetime checklist once more extracted capabilities converge on the same long-lived pattern

