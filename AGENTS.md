# AGENTS.md — LabAssistantV2 Working Agreement

This file is the **contract** that all agents must follow when working in this repository.
It combines: (1) repo architecture rules, (2) documentation/process rules, and (3) build/test expectations.

---

## 0) Scope and Product Constraints

- This repo contains a **Windows desktop app** (WPF/WinUI-style) plus supporting class libraries.
- **Supported hypervisor (current scope): Hyper-V only.**
- UI is the top-level composition root.
- **Cleanup policy is mandatory:** on runtime failure (or cancellation), the system must cleanup resources created by the operation (VMs, disks, etc.).

---

## 1) Source of Truth & Decision Rules (Non-negotiables)

1) **Do NOT invent requirements.**
   - If something is unclear, write `TBD` in the relevant `/docs` file and open an issue:
     - `TBD: <topic>`
2) **When docs and code disagree:**
   - Treat **code as reality** and **docs as intent**.
   - Do NOT “pick one silently.”
   - Open an issue: `Mismatch: docs vs code — <topic>` and propose a resolution.
   - When multiple docs disagree, use this precedence order unless a newer approved canonical doc explicitly states otherwise:
     1. `docs/01-requirements/acceptance-criteria.md`
     2. `docs/01-requirements/srs.md`
     3. canonical docs named in `docs/00-overview/authoritative-doc-map.md`
     4. specialized requirement supplements explicitly cited by `srs.md` or `acceptance-criteria.md`
     5. the active issue brief, but only for slice scope, ordering, and execution notes
   - `docs/01-requirements/user-stories.md`, `docs/01-requirements/non-functional-requirements.md`, `docs/07-testing/test-strategy.md`, and `docs/07-testing/test-plan.md` are live support docs within their own domains, but they do not override `acceptance-criteria.md` / `srs.md` on product behavior unless a higher-order doc explicitly delegates.
   - Milestone-coded docs, milestone checklists, and archived migration/reference docs are historical by default and must not be treated as current authority for new work unless the user explicitly asks for historical reconstruction or audit work.
   - If a historical milestone doc still contains a rule that appears missing from the current authority set, treat that as a documentation bug to fix in the current authority docs, not as permission to reactivate the milestone doc silently.
3) **New behavior must be documented before implementation**, except tiny refactors that don’t change behavior.
   - “Documented” means: update **SRS + Acceptance Criteria** at minimum.
4) **Acceptance Criteria is the implementation contract.**
   - If you can’t map work to Acceptance Criteria, stop and escalate.
5) **Never commit secrets.**
   - If secrets are found: stop, report, and propose remediation (rotate + purge history if needed).
6) **Future-facing agent workflow rules must live in `AGENTS.md`.**
   - If a new rule changes how agents should choose authority, respect roles, scope issues, prepare issue briefs, or decide what is historical vs authoritative, add that rule to `AGENTS.md` in the same docs slice.
   - Do NOT leave durable process/governance rules only in milestone docs, issue threads, or chat.

---

## 2) Roles & Responsibilities

### PM Agent responsibilities
Owns and maintains:
- `/docs/00-overview/*`
- `/docs/01-requirements/*`

Produces and maintains:
- User stories (what users need)
- Acceptance criteria (testable contract)
- Priorities (P0/P1/P2)
- Milestones + Definition of Done

Rules:
- Every milestone must be testable (explicit scenarios, failure cases).
- Any unresolved decisions go into **Open Questions / TBDs** and become issues.
- PM must keep issue scope within a single decision seam or implementation slice.
- PM must avoid milestone issues that require reading many capability contracts just to begin.
- PM should prefer a chain of narrow issues over one “finish the capability” issue.
- PM must keep durable architecture and ownership rules in canonical docs or in `srs.md` / `acceptance-criteria.md`, not in new milestone-coded docs.
- PM must treat milestone-coded docs as historical decision artifacts or closure evidence, not as long-term implementation authority.
- PM issue briefs must state the acting mode for the slice (`PM-only`, `Dev-only`, or another explicitly approved mode) and list the exact authoritative docs for that slice.

### Dev Agent responsibilities
Implements **only** from approved requirements:
- Acceptance Criteria is the contract
- SRS defines FR scope and boundaries
- NFR defines quality bars (performance/reliability/security)

Also responsible for:
- Updating architecture docs when flow/structure changes
- Adding/adjusting tests per strategy
- Keeping changes scoped to a single issue/story
- Ensuring cleanup/cancellation/logging standards are preserved
- Stopping when an issue starts to require a second behavior change, a second capability slice, or an unapproved contract update.

---

## 3) Repository Projects

- **LabAssistant.WinUI** (primary UI app, `net8.0-windows`)
- **LabAssistant** (legacy WPF UI; migration/maintenance only)
- **LabAssistant.Models** (domain objects, enums, DTOs)
- **LabAssistant.Data** (repositories, persistence)
- **LabAssistant.Services** (external APIs, file services, configuration)
- **LabAssistant.Business** (business logic, validation, coordination)

UI policy:
- `LabAssistant.WinUI` is the primary and only active UI product surface.
- `LabAssistant` (WPF) is legacy and must not be treated as the current source of truth for UI appearance, interaction design, or workflow ownership.
- WPF may be touched only when explicitly requested for legacy maintenance, removal, or migration cleanup.
- Current UI behavior decisions must be driven by WinUI docs/contracts and WinUI code, not by WPF parity assumptions.

---

## 4) Architecture Boundaries & Dependency Rules (Strict)

Allowed dependencies:
- `LabAssistant.WinUI` -> `LabAssistant.Business`, `LabAssistant.Models`, `LabAssistant.Services`
- `LabAssistant` (legacy WPF) -> `LabAssistant.Business`, `LabAssistant.Models`, `LabAssistant.Services`
- `LabAssistant.Business` -> `LabAssistant.Data`, `LabAssistant.Models`, `LabAssistant.Services`
- `LabAssistant.Data` -> `LabAssistant.Models` (only if shared types are required)
- `LabAssistant.Services` -> `LabAssistant.Models` (only if shared types are required)
- `LabAssistant.Models` -> **no project dependencies**

Disallowed dependencies:
- No non-UI project may reference a UI project (`LabAssistant.WinUI` or legacy `LabAssistant`).
- `LabAssistant.Models` must not reference any other project.
- `LabAssistant.Data` and `LabAssistant.Services` must not reference `LabAssistant.Business` or either UI project.
- Cross-references between `LabAssistant.Data` and `LabAssistant.Services` are not allowed.

Layering rules (behavioral):
- UI triggers actions and displays state; it should not contain orchestration logic.
- Business orchestrates workflows (validation, ordering, coordination).
- Services perform external actions (Hyper-V, PowerShell, filesystem, environment checks).
- Data handles persistence (template registry, base disk registry, settings storage, etc.).

WinUI composition rules:
- WinUI uses a **pragmatic hybrid** approach: MVVM-style state separation where useful, minimal view code-behind, and workspace/controller-style orchestration where that keeps the shell understandable.
- `MainWindow` should own shell chrome, route changes, and shell-level panel/container lifecycle.
- Capability workflow state and orchestration should not accumulate in `MainWindow`.
- Views must not become raw control bags unless there is a narrow, justified reason.

---

## 5) Engineering Standards (Required)

### 5.1 Logging & Diagnostics
- All external operations must emit **structured logs**.
- Every user-initiated operation must have an **operationId** propagated through the workflow.
- Legacy/transitional text logs may still mention `correlationId`, but structured events use `operationId` as the canonical field.
- Logs must include enough context to diagnose failures (operation, vmName(s), templateId, baseDiskId, switchName, result, error details).

### 5.2 Error Handling
- Prefer actionable user errors over raw exception dumps.
- Validate early (before starting Hyper-V operations) whenever possible.
- **Cleanup policy:** if any step fails after resources were created, cleanup is required.

### 5.3 Cancellation (where supported)
- Long-running operations must support cancellation and must cleanup on cancel.
- If cancellation can only happen at safe boundaries, UI should show “Cancelling…” and stop at the next safe boundary.

### 5.4 Testability
- External integrations must be behind unit-testable abstractions (interfaces).
- Core decision logic (validation, mapping, naming policies) must be unit-testable without Hyper-V present.

### 5.5 Scope Discipline
- Do not add features outside the issue scope.
- If a new requirement appears while coding: stop and open an issue.

### 5.6 Context Budget Discipline
- Agents must optimize for **small active context**, not maximum parallel understanding of the repo.
- Do not load broad swaths of docs “just in case.”
- Load only the docs, files, and tests needed for the current issue.
- If the work requires holding multiple capabilities, contracts, and test suites in memory at once, the issue is probably too large and should be split.

### 5.7 Code Documentation
- Code comments must improve maintainability, not narrate obvious mechanics.
- Prefer documenting:
  - ownership boundaries,
  - shell vs capability responsibilities,
  - long-lived workspace expectations,
  - route refresh vs recreate behavior,
  - cleanup/cancellation sequencing,
  - and ordering-sensitive workflow coordination.
- XML documentation comments are expected for:
  - public types and public members,
  - architecture seam contracts such as workspace composition classes, controllers, host interfaces, and shell bridge interfaces when they are an important maintenance boundary.
- Inline comments should be used sparingly and only where the code would otherwise hide an important invariant, boundary, or non-obvious decision.
- Avoid comments that:
  - restate names already visible in code,
  - narrate line-by-line mechanics,
  - or explain trivial getters/setters/event hookups.
- Comment length should match the complexity of the concept:
  - one sentence is often enough,
  - but 1-3 short sentences are preferred when a method or type needs boundary or invariant context.
- Do not attempt broad comment backfills in unrelated code. Apply the standard incrementally in touched files, prioritizing composition/workflow seams first.
- Canonical guidance and examples live in `docs/03-architecture/code-documentation.md`.

### 5.8 File Organization
- Large seam-heavy WinUI files must follow a stable member ordering when the relevant sections exist:
  1. fields
  2. constructor
  3. public API
  4. interface implementation
  5. event wiring / event handlers
  6. core workflow methods
  7. UI/application helpers
  8. small private parsing/format helpers
- Only include sections that actually apply to the file.
- Do not add empty placeholder sections.
- Do not use `#region` as a substitute for proper file splitting or method grouping.
- Use one top-level class per file.
- Use one top-level interface per file.
- Keep files as short as practically possible while preserving cohesive logic; for seam-heavy files, prefer splitting before they become navigation-hostile and generally aim to stay below roughly 300-500 lines when the responsibility can be separated cleanly.
- In `LabAssistant.WinUI/ViewModels/<Capability>/`, capability-shared files stay at the capability root.
- Lane-local files should live in PascalCase lane folders named for the intended route/workflow surface, for example `Overview`, `QuickDeploy`, `FromTemplate`, or `Logs`.
- Apply these organization rules incrementally in touched files rather than through broad repo-wide churn.
- Canonical guidance and examples live in `docs/03-architecture/code-organization.md`.

---

## 6) Documentation Rules

All docs live in `/docs` with the standard structure.

Minimum documentation rules:
- Each major feature must link:
  - **User story → Acceptance criteria → Tests**
- Every doc must include:
  - Purpose
  - Open Questions / TBDs

Update rules:
- If behavior changes, update Acceptance Criteria first (or alongside the code).
- If quality bars change (performance/reliability), update NFR.

TBD rules:
- `TBD` is acceptable only when the missing decision is not required for the active implementation slice.
- If an active PM or Dev issue depends on a `TBD`, that `TBD` must be resolved or turned into a blocking PM issue before implementation continues.
- If a `TBD` survives across multiple milestones, PM should explicitly classify it as:
  - resolve now,
  - convert to issue,
  - keep deferred with rationale,
  - or remove as obsolete.
- Active unresolved TBDs must also be listed in `docs/00-overview/tbd-register.md`.
- The source document remains authoritative for the actual rule or decision boundary; `docs/00-overview/tbd-register.md` is the tracking index, not a replacement source of truth.
- Archived docs, templates, and examples do not create active TBDs unless the unresolved item is explicitly promoted into `docs/00-overview/tbd-register.md`.

Consolidation rules:
- Milestone-local docs may record local decisions, but durable cross-capability rules must eventually be promoted into canonical docs.
- If the current rule for a behavior requires reading multiple milestone docs, consolidation is overdue.
- Preferred consolidation target:
  - durable behavioral rules -> Acceptance Criteria / SRS / architecture docs
  - milestone contracts -> local decision history, migration guidance, or references to canonical docs
- Milestone contracts should not remain the long-term only place where cross-capability behavior is defined.
- A legacy doc may be moved into an `Archived` folder only after:
  - any still-live rules have been promoted into the current authority docs,
  - live references have been updated,
  - and the file no longer acts as a required source to understand current behavior.
- Archiving is the last step of consolidation, not the consolidation itself.

Authority rules:
- The current authority set for new work is:
  - `AGENTS.md`
  - `docs/00-overview/scope.md`, `docs/00-overview/product-vision.md`, and `docs/00-overview/glossary.md` when product framing or terminology matters
  - `docs/01-requirements/user-stories.md` when story framing or story-to-FR/AC/test traceability matters
  - `docs/01-requirements/srs.md`
  - `docs/01-requirements/acceptance-criteria.md`
  - `docs/01-requirements/non-functional-requirements.md` and specialized requirement supplements when quality bars or specialized contracts matter
  - `docs/07-testing/test-strategy.md` and `docs/07-testing/test-plan.md` when test strategy or recurring verification scope matters
  - canonical docs named in `docs/00-overview/authoritative-doc-map.md`
  - the active issue brief for scope and execution notes only
- Milestone-coded docs under `docs/02-ux/`, milestone checklists under `docs/07-testing/`, and archived migration/reference aids under `docs/03-architecture/Archived/` are historical by default.
- Do not create a new milestone-coded doc when the rule is meant to remain authoritative beyond that slice.
- When a milestone doc is absorbed, update the canonical authority docs in the same slice and explicitly mark the milestone doc as historical.

---

## 7) Workflow for Agents (Do This Every Time)

### Before coding
1) Read `AGENTS.md`
2) Identify the driving requirement:
   - User story ID + Acceptance Criteria section
3) Confirm:
   - FR mapping exists in SRS
   - NFR constraints are respected (perf/reliability/security)
4) Write or update missing AC if needed (PM Agent) **before implementation**
5) Confirm the issue fits the context budget rules in section 7.1 before proceeding

### During coding
- Keep PRs small and focused
- Prefer additive changes over massive refactors unless explicitly required
- Do not reformat unrelated files

### After coding
- Ensure AC scenarios are satisfied (happy path + failures + cleanup behavior)
- Add/update tests
- Update docs if behavior/architecture changed
- Ensure logs are emitted at key steps with operationId (canonical structured field)
- Prepare the final handoff using the handoff rules in section 7.6

### 7.1 Context Budget & Issue Sizing Rules

#### PM issue sizing rules
- A PM issue should usually touch **1-3 docs**.
- A PM issue should define **one** of the following:
  - a contract,
  - a seam,
  - a test convergence rule,
  - or a closure/checklist artifact.
- Do not combine multiple capability seams into one PM issue.
- Do not mix “define contract” and “implement code” in the same issue.
- If an issue cannot be summarized as one sentence with one primary deliverable, split it.
- For seam-definition issues, definition of done should include:
  - ownership boundaries,
  - what stays in the shell/current owner,
  - what moves out,
  - interaction surface,
  - affected tests/docs,
  - and explicit non-goals.

#### Dev issue sizing rules
- A Dev issue should usually touch **one capability slice or one infrastructure seam** plus related tests.
- A Dev issue should not require understanding the full WinUI shell, multiple capabilities, and multiple milestone docs just to start.
- A Dev issue should target **one dominant reason to change**:
  - extract state,
  - extract orchestration,
  - reduce view exposure,
  - or converge tests.
- If implementation starts crossing from one dominant reason into another, stop and split follow-up work.
- For extraction issues, definition of done should usually include:
  - the targeted state/orchestration moved out of the current owner,
  - no unapproved behavior expansion,
  - related tests updated,
  - and shell behavior preserved unless the issue explicitly changes contract behavior.

#### Session hygiene rules
- Prefer a fresh session per issue.
- Hand off using a short issue brief, not a full replay of prior discussion.
- Reference only the canonical docs and tests needed for the active issue.
- Summaries should reduce context, not restate the whole milestone.
- Do not pull milestone-coded docs into the active authority set for a normal implementation slice.
- If historical milestone context is genuinely needed, cite it as history and restate the surviving rule in the current authority docs or issue brief before relying on it.

### 7.2 Project-Specific AGENTS.md Files
- A project-specific `AGENTS.md` is worth adding only when a project has rules that genuinely differ from the repo default.
- Good reasons:
  - a UI project has specific composition, binding, or test-surface rules,
  - a Services project has external-process or diagnostics rules,
  - a Data project has persistence/migration constraints not shared elsewhere.
- Bad reasons:
  - repeating repo-wide rules,
  - restating folder names,
  - adding vague style preferences.
- Project-specific `AGENTS.md` files must be short and only define local overrides or additions.
- Repo `AGENTS.md` remains the default contract; project-specific files refine it for that subtree.

### 7.3 Test Target Preference
- Prefer tests around extracted seams, state owners, controllers, or viewmodels over tests that assert literal source layout.
- When extracting WinUI seams, prefer route continuity, state-owner seam presence, capability-level behavior, and narrow interaction-boundary coverage where practical.
- Use source-shape or XAML/source-string tests only when they protect contract-critical shell structure, routing, or named interaction surfaces that are intentionally part of the contract.
- Manual checklist verification remains valid for end-to-end workflow confirmation, but it does not replace targeted automated tests for extracted logic seams.
- If runtime extraction changes ownership boundaries, interaction seams, or route-bearing structure, the directly impacted tests must be updated in the same issue/PR.
- Temporary scaffold tests may be reduced only when stable contract coverage remains explicit after the change.

### 7.4 Active TBD Handling
- Agents are not expected to keep all repo TBDs in active memory.
- When the active issue, file, or discussion reaches a `TBD`, the agent must do one of the following before continuing implementation:
  - resolve it in docs,
  - convert it into a blocking PM issue,
  - or explicitly defer it with rationale when it does not affect the active slice.
- Do not silently code through a `TBD` by making local assumptions.

### 7.5 Git Branch and Base Sync Rules
- Every issue must use its own dedicated branch.
- Preferred branch naming pattern is:
  - `milestone-<code>/issue-<number>-<short-slug>`
  - If no milestone code exists, use:
    - `issue-<number>-<short-slug>`
- Issue branches must be created from the current `origin/master` state, not from a potentially stale local `master`.
- Before starting issue work, agents must fetch `origin/master` and confirm the branch base reflects the current remote baseline.
- If the issue depends on earlier merged work, agents must verify that required docs/code are present on `origin/master` before editing.
- Local scratch or support files are not part of issue scope unless the user explicitly asks to include them.
- `HANDOFF.md` is local-only workflow state and must be ignored for issue branching, commits, and PR scope unless the user explicitly says otherwise.
- Unrelated tracked or untracked worktree state must not be pulled into an issue branch or commit just because it exists locally.
- If issue work starts on the wrong branch, agents must move the issue-specific changes onto a fresh dedicated branch from updated `origin/master` before committing or opening a PR.
- PRs should contain only issue-scoped changes plus any unavoidable prerequisite baseline sync required to make the branch coherent against current `origin/master`.
- If an issue depends on an earlier issue that is already merged, agents must check whether that earlier issue is still open and close it before finishing the current slice, or explicitly note that it was already closed.
- If validation uses `--no-build` and the result appears stale, inconsistent with the current source, or likely to be using an older assembly/test host, agents must rebuild and rerun until the validation result is trustworthy before handoff.

### 7.6 Handoff Rules

#### Link-first references
- Handoffs must use fully linked, one-click references for the active work item, not bare issue or PR numbers.
- This handoff rule applies to agent handoff text returned in chat or stored in local workflow notes.
- At minimum, every final handoff for an issue-driven slice must include clickable links for:
  - the issue
  - the PR, if one exists
  - the milestone, if one exists and is relevant to the slice
- Acceptable examples:
  - `Issue [#424](https://github.com/<org>/<repo>/issues/424)`
  - `PR [#560](https://github.com/<org>/<repo>/pull/560)`
  - `[Milestone AM - WinUI Composition and Workspace Extraction](https://github.com/<org>/<repo>/milestone/39)`
- Do not assume `#424` by itself is sufficient in a handoff.
- This rule matters especially for later Dev/PM handoffs where the next agent needs one-click navigation.
- This does **not** mean Markdown links should replace native GitHub issue references everywhere:
  - in handoffs, direct links are preferred for clarity and one-click navigation
  - in GitHub issue/PR bodies or comments, native references such as `#424` should still be used where GitHub cross-linking and hover behavior are valuable
  - milestone references may still use direct links because GitHub does not provide an equivalent lightweight native mention syntax

#### Dev handoff minimum content
- A Dev completion handoff must include, in compact form:
  - issue link
  - PR link
  - branch name
  - commit hash if committed
  - milestone link when milestone-scoped
  - labels applied to the issue and PR
  - validation run and result
  - stale prior-issue closure status when applicable
- The handoff must clearly state whether the slice was:
  - runtime
  - docs-only
  - tests-only
  - or another narrow category relevant to the issue
- Default Dev handoff structure should use the Dev template in `#### Template consistency`.

#### PM handoff format
- When handing off to PM or preparing a reusable handoff for a future Dev/PM, use a stable structure rather than ad hoc prose.
- PM handoffs should preserve the existing structure/style the PM is already using in this repository rather than inventing a new layout.
- PM handoffs are issue briefs for the next slice, not implementation-result summaries.
- PM handoffs must not use the Dev handoff header or the Dev result-summary template.
- Preferred PM handoff sections are:
  - `## PM Issue Brief — Issue #<n> (<milestone code>)`
  - `### Mode`
  - `### Authoritative Docs`
  - `### Issue`
  - `### Goal`
  - `### Why this issue exists`
  - `### In Scope`
  - `### Expected outcome after <milestone code>`
  - `### Constraints`
  - `### What should still be protected` when the issue is a convergence/alignment slice or the PM brief uses that structure
  - `### Important boundary`
  - `### Out of Scope`
  - `### Likely files`
  - `### Tests` or `### Test direction`
  - `### Validation required`
  - `### Repo hygiene requirement`
  - `### Required workflow`
  - `### PR Requirements`
  - `### Definition of Done`
- PM handoffs should keep the current PM level of detail unless the user explicitly asks for a shorter issue brief.
- The `### Mode` section must state the acting role and allowed action class for the slice, for example `PM-only; no code edits or PRs` or `Dev-only; implementation permitted`.
- The `### Authoritative Docs` section must list only the small current authority set needed for the slice.
- The `### Issue` section should preserve the PM’s existing issue-brief style and should normally include:
  - issue number and title
  - milestone name/code
  - dependency baseline
  - status target
- When the PM uses a richer issue brief structure already established in the repo, agents should preserve that structure instead of compressing or renaming sections.
- If the user asks for “the HO” in a planning or PM context, default to this PM issue-brief format.

#### Metadata expectations in handoff
- If the work item belongs to a milestone, handoff text must say whether:
  - the issue is on the correct milestone
  - the PR is on the correct milestone
- If labels were applied, handoff text must say which labels were used and why when not obvious.
- If labels or milestone were missing and could not be applied, the handoff must say that explicitly.

#### Template consistency
- PM and Dev handoffs are intentionally different and must not be collapsed into one shared template.

##### PM template
- Use the PM issue-brief structure from `#### PM handoff format`.
- This is the template for planning the next slice and handing work to a Dev.

##### Dev template
- Dev handoffs are implementation-result summaries and should use this structure by default:
  - `## Dev Session Handoff — Issue #<n> (<milestone code>)`
  - `### Status`
  - `### What landed`
  - `### Outcome`
  - `### Validation`
  - `### Boundary preserved`
  - `### Suggested next slice`
  - `### Notes`
- Dev handoffs should usually be more detailed than a minimal close-out message and should be reusable by PMs and future Devs.
- The `### Status` section should include:
  - linked issue
  - linked PR
  - linked milestone when relevant
  - branch name
  - commit hash if committed
  - labels applied to issue and PR
  - stale predecessor issue closure status when applicable
- The `### What landed` section should list touched files or major artifacts.
- The `### Outcome` section should explain the architectural or behavioral result and explicitly call out important non-goals that remained untouched when relevant.
- The `### Notes` section should mention repo hygiene, leftover local artifacts, and any follow-up guidance that matters for the next slice.
- If a shorter Dev handoff is needed, shorten within this structure rather than replacing it with loose prose.

---

## 8) Work Item Classification Rules

Agents must classify issues, PRs, and milestones consistently enough that repo history remains operationally useful for planning, release notes, and traceability. Classification should be lightweight and stable, not perfect or overly granular.

### 8.1 Core Label Set

Use a small fixed taxonomy unless the user explicitly approves changes:
- `bug` — fixes incorrect behavior, regressions, missing cleanup, validation gaps, or reliability defects
- `feature` — introduces new user-visible behavior or a newly supported workflow
- `refactor` — restructures code without intended behavior change
- `docs` — changes documentation or contracts without changing runtime behavior
- `test` — adds or reshapes tests without changing runtime behavior
- `chore` — repository maintenance, tooling, dependency, CI, or other support work that is not primarily feature/bug/refactor/docs/test

Rules:
- Apply the **dominant reason to change** label at minimum.
- Additional labels are allowed when they describe real secondary aspects of the same work item.
- Do not add labels casually; every applied label should be defensible from the issue or PR scope.
- If a change mixes multiple dominant reasons, the issue is probably too large and should be split.

### 8.2 How To Classify Issues

- Every implementation issue should have at least one type label from section 8.1.
- Issues may carry multiple type labels when the scope genuinely spans more than one category and the extra labels improve triage or reporting.
- Every issue should be assigned to a milestone when the work belongs to a planned delivery slice and the mapping is clear.
- Issues that define or adjust product behavior should also map to a user story and acceptance criteria.
- Use the title prefix and the label together; the prefix is not a replacement for the label.
- If the issue only resolves uncertainty, mismatch, or a `TBD`, classify it by its real deliverable:
  - contract clarification with doc-only change -> `docs`
  - behavior fix after clarification -> `bug`
  - new approved capability slice -> `feature`
- If the correct label is unclear, do not guess silently:
  - add `TBD` in docs when appropriate
  - open or update an issue describing the ambiguity
- If milestone membership is unclear, resolve the ambiguity or leave a brief note; do not silently leave milestone assignment undone for active milestone work.

### 8.3 How To Classify PRs

- Every PR must link to its driving issue when the PR is created.
- Use explicit closing linkage in the PR body when appropriate, for example `Closes #123`, so merge state and issue state stay synchronized.
- If a PR is intentionally not closing the linked issue, the PR body should say so explicitly and still reference the issue.
- Every PR should carry the same primary type label as its driving issue unless scope drift forced a split.
- PRs may carry multiple labels when that better reflects the actual scope and matches the linked issue classification.
- Every PR should be assigned to the same milestone as its driving issue when the mapping is clear.
- PRs should reference the issue, acceptance criteria, and milestone in the PR body when those exist.
- A PR should normally map to one issue and one dominant reason to change.
- If a PR contains both behavior change and incidental cleanup, classify by the behavior change, not the incidental cleanup.
- Pure renames, extraction, or structural cleanup with no intended behavior change -> `refactor`.
- Test-only or docs-only PRs should use `test` or `docs` rather than inheriting a nearby implementation label.
- If the linked issue has labels or a milestone and the PR does not, agents must add them before handoff unless there is a documented reason not to.

### 8.4 Milestone Rules

- Milestones represent planned delivery slices, not generic buckets for every kind of work.
- Create or use milestones only when there is a concrete delivery objective, testable definition of done, and an identifiable user or product outcome.
- A work item should belong to at most one milestone.
- Do not force a milestone onto backlog grooming, repo maintenance, or historical cleanup work unless that work is itself the milestone deliverable.
- If a PR merges work for a milestone, its issue and PR should both point to that milestone when the mapping is clear.
- If milestone membership is ambiguous, leave it unassigned and note the ambiguity instead of inventing a mapping.

### 8.5 Backfill Policy For Existing History

- Backfill classification only when it provides current operational value:
  - open PRs/issues
  - recently merged work
  - active milestone work
  - items needed for release notes, audits, or traceability
- Prefer “good enough and consistent” over perfect historical reconstruction.
- For older mixed-scope PRs, choose the dominant user-facing or repo-impacting reason to change.
- If an old PR cannot be classified confidently from its title, body, and diff, leave milestone blank and apply only the most defensible type label.
- Do not spend time decomposing ancient mixed PRs into ideal categories unless the user explicitly asks for a historical audit.

---

## 9) PR Checklist (Dev Agent must follow)

- [ ] Matches Acceptance Criteria exactly
- [ ] No new scope added
- [ ] Cleanup policy preserved (no orphaned VMs/disks after failure/cancel)
- [ ] Logs added/updated with operationId (canonical structured field) and required context
- [ ] Tests added/updated (unit tests for logic, mocks for Hyper-V if needed)
- [ ] Docs updated if behavior or architecture changed
- [ ] PR links to its driving issue
- [ ] PR and issue labels are applied appropriately
- [ ] PR and issue milestones are aligned when the work belongs to a milestone

### PR Body Format
- PR bodies must use the repository-standard section layout with these exact Markdown level-2 headers:
  - `## What`
  - `## Why`
  - `## Scope`
  - `## Out of scope`
  - `## Validation`
  - `## Traceability`
- Each section must use bullet points for its content.
- `## Scope` may use nested bullets when listing touched files, tests, or traceability artifacts.
- Do not replace these headers with bold labels, top-level list items, or other ad hoc formatting.
- Keep PR body wording concise and issue-scoped.
- PR bodies must include explicit issue linkage in `## Traceability`.
- In GitHub PR bodies, issue linkage must use **native GitHub issue references**, not just plain text or only a Markdown URL.
- Preferred pattern in `## Traceability`:
  - `- Issue: #123`
  - `- Closes #123`
  - `- Milestone: [Milestone Name](https://github.com/<org>/<repo>/milestone/<n>)` when milestone-scoped
- When the PR should close the issue on merge, include a GitHub closing keyword such as `Closes #123`.
- Do not assume writing `Issue 123`, `Issue #123` as plain prose, or only `[Issue #123](https://...)` is enough for PR traceability.
- If one-click milestone navigation is useful, a direct milestone Markdown link is still appropriate because GitHub does not provide an equivalent native milestone mention syntax.
- If the issue title already includes the slice code (for example `AM67`), do not add a redundant `Milestone slice: AM67` line unless the user explicitly wants it.
- This PR traceability format applies to all GitHub PR bodies in this repo, regardless of whether the slice originated from a PM handoff or a Dev-owned follow-up.

### PR Body Drafting
- Agents must draft every PR body in a local temporary Markdown file before creating or editing the PR through GitHub CLI.
- PR body draft files must live under `.local/` rather than as ad hoc files in the repo root.
- PR body draft files are local workflow artifacts only and must not be committed unless the user explicitly asks.
- Agents should use the draft file as the source when running `gh pr create` or `gh pr edit` so Markdown formatting is preserved without shell-escaping issues.
- After creating or updating the PR, agents should remove the temporary PR body draft if it is no longer needed.
- Temporary PR body drafts from unrelated issues are not part of issue scope and must not be included in commits or PRs.

---

## 10) Build Commands (Canonical)

From repo root:
- `dotnet restore LabAssistant.sln`
- `dotnet build LabAssistant.sln -c Debug`
- `dotnet build LabAssistant.sln -c Release`

Notes:
- UI targets `net8.0-windows` → builds must run on **Windows** with **.NET SDK 8** installed.

---

## 11) Issue Naming Conventions (Recommended)

- `US-###: <title>` — implementation for a user story
- `AC-###: <title>` — acceptance criteria updates
- `TBD: <topic>` — unresolved decision
- `Mismatch: docs vs code — <topic>` — inconsistency requiring resolution
- `Refactor: <area>` — behavior-preserving refactor (must confirm “no behavior change”)

