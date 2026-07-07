# AGENTS.md - LabAssistantV2

This is the working agreement for agents in this repo.
It is intentionally short.
It captures the invariants that must always hold and the quality bar I expect.
Heavy process (handoffs, PR/label ceremony, branch models) lives in skills that load only when that work is happening, not here.

## 1. Product Constraints

- Windows desktop app (WinUI) plus supporting class libraries.
- Supported hypervisor: Hyper-V only.
- Cleanup is mandatory: on runtime failure or cancellation, the system must clean up resources it created (VMs, disks, switches, etc.). No orphans.

## 2. Working Philosophy (Quality First)

When making technical decisions, do not give much weight to development cost or time.
Prefer quality, simplicity, robustness, scalability, and long term maintainability instead.

- A complete, correct solution beats a minimal one.
  Large refactors and full rewrites are acceptable when they raise quality.
  Do not artificially shrink scope just to keep a change small.
- When fixing a bug, first reproduce it end to end, as close to how a real user hits it as possible.
  This makes sure you find the real problem so the fix actually solves it.
- When testing a product end to end, be picky about the UI and obsess over pixel perfection.
  If something clearly looks off, even if it is unrelated to your task, get it fixed along the way.
- Apply the same standard to engineering excellence.
  If you see a lint error, test failure, or test flakiness, fix it, even if you did not cause it.
- Do not invent requirements.
  If something is genuinely unclear, ask or mark it clearly rather than guessing.
- When docs and code disagree, treat code as reality and docs as intent, and reconcile them rather than silently picking one.
- Never commit secrets.
  If you find one, stop, report it, and propose remediation.

## 3. Repository Projects

- **LabAssistant.WinUI** - the UI (`net8.0-windows`).
- **LabAssistant.Models** - domain objects, enums, DTOs.
- **LabAssistant.Data** - repositories, persistence.
- **LabAssistant.Services** - external APIs, Hyper-V, PowerShell, filesystem, config.
- **LabAssistant.Business** - business logic, validation, orchestration.

## 4. Architecture Boundaries (Strict)

Allowed dependencies:

- `LabAssistant.WinUI` -> `Business`, `Models`, `Services`
- `LabAssistant.Business` -> `Data`, `Models`, `Services`
- `LabAssistant.Data` -> `Models` (only if shared types are required)
- `LabAssistant.Services` -> `Models` (only if shared types are required)
- `LabAssistant.Models` -> no project dependencies

Disallowed:

- No non-UI project may reference a UI project.
- `Models` must not reference any other project.
- `Data` and `Services` must not reference `Business` or any UI project.
- `Data` and `Services` must not reference each other.

Layering behavior:

- UI triggers actions and displays state.
  It should not hold orchestration logic.
- Business orchestrates workflows (validation, ordering, coordination).
- Services perform external actions.
- Data handles persistence.

WinUI composition:

- Pragmatic hybrid MVVM.
  `MainWindow` owns shell chrome, routing, and shell-level panel lifecycle.
  Capability workflow state and orchestration should not accumulate in `MainWindow`.
- Views should not become raw control bags without a narrow, justified reason.

## 5. Engineering Standards

- **Logging.** Every external operation emits structured logs.
  Every user-initiated operation propagates an `operationId` through the workflow.
  Logs must carry enough context to diagnose failures (operation, vm name(s), templateId, baseDiskId, switchName, result, error details).
- **Errors.** Prefer actionable user errors over raw exception dumps.
  Validate early, before starting Hyper-V operations, whenever possible.
- **Cancellation.** Long-running operations support cancellation and clean up on cancel.
  If cancellation only happens at safe boundaries, show "Cancelling..." and stop at the next safe boundary.
- **Testability.** External integrations sit behind unit-testable interfaces.
  Core decision logic (validation, mapping, naming policies) must be unit-testable without Hyper-V present.
- **Test style.** Prefer behavior and seam tests around state owners, controllers, and viewmodels.
  Avoid source-shape or XAML-string tests unless they protect genuinely contract-critical shell structure or routing.
  If runtime behavior or a seam changes, update the directly impacted tests in the same change.

## 6. Code Style

- Never use the em dash "-".
  Use a plain hyphen "-" instead.
- When writing or substantially editing long Markdown files, put each full sentence on its own line.
  Preserve normal Markdown structure, but do not wrap multiple sentences onto one physical line.
- Comment to explain non-obvious invariants, ownership boundaries, and ordering-sensitive or cleanup/cancellation sequencing.
  Do not narrate obvious mechanics or restate names.
- XML doc comments are expected on public types/members and on architecture seam contracts (workspace composition, controllers, host/shell interfaces).
- One top-level class per file, one top-level interface per file.
  Keep seam-heavy files reasonably short; split before they become navigation-hostile.
- When writing commit messages, never auto-add your agent name as co-author.
- Never manually edit `CHANGELOG.md` or any file marked auto-generated.

## 7. Build Commands

From repo root:

- `dotnet restore LabAssistant.sln`
- `dotnet build LabAssistant.sln -c Debug`
- `dotnet build LabAssistant.sln -c Release`

UI targets `net8.0-windows`, so builds run on Windows with .NET SDK 8.

## 8. Workspace Hygiene

Keep the repository working tree clean and singular.

- Never nest a second clone, git worktree, scratch project, or generated artifact inside the repository working tree.
  The only nested checkouts allowed are the app-managed `copilot-worktrees/` sessions.
- Temporary worktrees used to ship a discrete branch live outside the repository tree and are removed once their PR is pushed.
- Throwaway runners, logs, downloaded images, and other scratch belong outside the tree or in an ignored scratch directory.
  Never commit them and never leave them in the repository root.

## 9. Deeper Rules and Workflows (load on demand)

This file holds only always-on invariants.
Procedural workflows live in skills that load when that work is happening, and detailed contracts live in canonical docs.
Pull these in when the task calls for them.

Skills:

- **`ship-change`** - the change lifecycle: branch naming, commits, when to open a GitHub issue vs. not, simple vs. umbrella branch, PR creation, PR body format, labels.
- **`write-handoff`** - cross-session handoff format when coordinating multiple agents.

Default unit of work is branch -> commits -> PR.
Do not open a GitHub issue unless I ask, or the work is being deferred, tracked, or coordinated for later.
Never open an issue just to attach a PR to it.

Canonical docs (deep reference):

- Product behavior contract: `docs/01-requirements/srs.md`, `docs/01-requirements/acceptance-criteria.md`.
- Cleanup/cancellation: `docs/01-requirements/cleanup-cancellation-policy.md`.
- Logging: `docs/01-requirements/logging-contract.md`.
- Architecture and WinUI seams: `docs/03-architecture/architecture.md` and the `winui-*` docs it links.
- Which doc is authoritative for a given topic: `docs/00-overview/authoritative-doc-map.md`.

If docs and code disagree, treat code as reality and reconcile the doc.
