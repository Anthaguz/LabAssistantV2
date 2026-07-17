# Architecture

**Purpose:** Explain how the system is structured and how code should be organized.

Current document authority for WinUI shell, lane, and shared seam rules is defined in `docs/00-overview/authoritative-doc-map.md`.

## 1. High-Level Overview
- **Architecture style:** Layered desktop application with MVVM-style UI state management.
- **Key goals:** Maintainable local-first automation, safe Hyper-V operations, testable orchestration logic, and clear diagnostics.

## 2. Solution Structure
- **UI (`LabAssistant.WinUI`)**
  - Primary desktop UI and current shell composition root.
  - Owns navigation, views, viewmodels, and user interaction state for the supported product surface.
  - Binds to business/data/service abstractions and displays progress, summaries, and errors.
- **Business (`LabAssistant.Business`)**
  - Orchestrates workflows (deployment pipeline, cleanup/cancellation coordination, outcome summaries).
  - Owns validation and workflow decision logic that should be unit-testable.
  - Coordinates Data + Services without UI dependencies.
- **Services (`LabAssistant.Services`)**
  - External system integrations (Hyper-V/PowerShell execution, filesystem helpers, logging, diagnostics export).
  - Structured logging foundation and diagnostics bundle export live here.
- **Data (`LabAssistant.Data`)**
  - Persistence and local storage concerns (template store, catalog store, app settings store).
  - JSON serialization/deserialization, compatibility loading, and local file access for persisted artifacts.
- **Models (`LabAssistant.Models`)**
  - Shared domain models, DTOs, enums, and result types.
  - Includes template schema models, deployment contexts, cleanup/result models, and operation states.

## 3. Dependency Rules
Canonical dependency rules are defined in `AGENTS.md` and enforced in project references:
- UI -> Business, Models, Services
- Business -> Data, Models, Services
- Data -> Models
- Services -> Models
- Models -> no project dependencies

Behavioral boundaries:
- UI triggers actions and renders state; it should not own orchestration logic.
- Business coordinates ordering, validation, cancellation, cleanup, and summaries.
- Services execute external actions and infrastructure concerns.
- Data persists and loads local artifacts/configuration.

Current authoritative WinUI architecture docs:
- `docs/03-architecture/winui-shell-navigation-layout.md`
- `docs/03-architecture/winui-shell-bootstrap-runtime.md`
- `docs/03-architecture/winui-lane-architecture.md`
- `docs/03-architecture/winui-shared-seam-ownership.md`

Code documentation guidance for composition seams, workflow methods, and shell bridges is defined in `docs/03-architecture/code-documentation.md`.
Code organization guidance for seam-heavy files is defined in `docs/03-architecture/code-organization.md`.
Hyper-V execution-pattern authority is defined in `docs/03-architecture/hyperv-powershell-interaction.md`.

## 4. Key Workflows (end-to-end)
### Workflow: Deploy Lab (Quick Deploy or template-based)
1. UI/ViewModel maintains a deployment readiness report and triggers **quick preflight** on relevant configuration changes (debounced).
2. On Deploy click, UI/ViewModel runs **full preflight** and blocks deployment start if any blocking readiness failures exist.
3. UI creates `MultiVmDeploymentContext` and starts business coordinator only after full preflight passes (warnings-only is allowed).
4. Business coordinator runs per-VM deployment pipelines using a workflow-owned persistent PowerShell session per VM plus Hyper-V services bound to that session.
5. Runtime emits progress/log events and updates operation state (`Running`, `Cancelling`, etc.).
6. On blocking failure or user cancellation, cleanup orchestration runs for affected VMs.
7. Business builds structured outcome summary (per-VM + global + residuals) and emits structured failure context (paths/artifacts) when available.
8. UI displays readiness/failure summaries, final outcome summary, and re-enables configuration after terminal state.

### Workflow: Template Import / Load / Save
1. UI/ViewModel requests template load/save via Data layer stores and Business validation services.
2. Data layer loads JSON and normalizes legacy/canonical schema shape.
3. Compatibility gate enforces schema support window (`N` and `N-1`).
4. Business/UI validation surfaces field-level issues and missing catalog references.
5. Save writes canonical schema (`schemaVersion`, `templateRevision`, `templateType`, `vmId`, etc.).

### Workflow: Diagnostics Export (v1)
1. Caller constructs diagnostics export request/options (operation context + destination zip path).
2. Services diagnostics exporter builds zip package with manifest, runtime metadata, operation metadata, and structured logs.
3. Structured logs remain JSONL and can be filtered by `operationId`.

## 5. Cross-Cutting Concerns
- **Shell Right-Panel Ownership**
  - The shell owns right-panel infrastructure only:
    - container existence
    - layout host and bounded scroll host
    - generic visibility mechanics
    - compact-width fallback behavior
    - owner reset when active capability changes
  - The active capability or lane owns right-panel behavior:
    - whether the workflow uses the panel
    - what content appears there
    - what the panel means in that workflow
    - when workflow behavior opens, closes, or refreshes it
    - workflow-local titles, summaries, results, and contextual actions
  - If a concern mixes both categories, treat it as shared integration that needs a narrower seam decision; do not silently centralize capability semantics in `MainWindow` and do not duplicate shell container mechanics inside each capability.
- **Logging**
  - Structured JSONL logging (`structured-events.jsonl`) is the canonical diagnostics path.
  - The ambient `DebugLogger` and the raw PowerShell timing tracer forward into structured JSONL as coded `diag.debug` / `diag.ps_timing` events; the separate `log.txt` file is retired.
  - Runtime failure events now include known artifact/path context when available (for example `parentVhdPath`, `targetVhdPath`, `vmPath`) to speed diagnosis.
- **Configuration**
  - App settings and storage paths are persisted locally and used by UI/Business/Data/Services.
  - Log folder, templates folder, VM storage paths, and catalog paths are configurable.
- **Cancellation / Progress / Recovery**
  - Deployment operations expose explicit operation states.
  - Cancellation occurs at safe boundaries and triggers cleanup when resources were created.
  - Outcome summaries and residual reporting are structured and reusable for diagnostics.
- **Testability**
  - Hyper-V, PowerShell, filesystem, and diagnostics/logging sinks are abstracted behind interfaces where practical.
  - Core decision/orchestration logic is covered by Business/Data/Services/UI tests.
  - Preflight/readiness behavior is covered at Business/UI levels (engine, checks, deploy gating).
  - Some PowerShell wrapper/process-lifecycle regressions remain intentionally validated via manual Hyper-V checklist observations (post-`#219`) rather than fragile process-timing tests.

## 6. Implementation Notes (Current Reality)
- **Persistent PowerShell session wrapper (`PersistentPowerShellSession`)**
  - Commands are executed in an automation-safe PowerShell host (`-NoProfile -NonInteractive -NoLogo`).
  - Command execution is serialized (`SemaphoreSlim`) per session instance.
  - Command completion is currently driven by a PowerShell stdout marker; native stderr is drained in the background and appended as supplemental diagnostics.
  - Session disposal uses bounded waits and may kill the process tree to avoid shutdown hangs (`powershell.exe` / `conhost.exe`) if the child process does not exit promptly.
  - Wrapper protocol/lifecycle trace logging is disabled by default and can be enabled for troubleshooting with environment variable `LABASSISTANT_POWERSHELL_WRAPPER_TRACE` (writes to debug log path).
  - These behaviors were stabilized during Milestone U follow-up hotfix `#219` and should be preserved unless intentionally redesigned/tested.
- **Hyper-V execution model**
  - Deploy uses one persistent workflow-owned PowerShell session per VM.
  - Read-heavy Hyper-V queries use a separate reusable query-session seam.
  - One-shot administrative actions remain isolated from both Deploy workflow sessions and read-query sessions.
  - Detailed call-site classification and non-goals live in `docs/03-architecture/hyperv-powershell-interaction.md`.

## Open Questions / TBDs
- Whether future switch management and guest configuration features should introduce new business workflow coordinators or extend current deployment pipeline abstractions.

## Historical Reference Aids
- Historical migration/reference aids retained in `docs/03-architecture/Archived/`:
  - `docs/03-architecture/Archived/sequence-diagrams.md`
  - `docs/03-architecture/Archived/gui-action-map.deploy.md`
  - `docs/03-architecture/Archived/gui-action-map.templates.md`
  - `docs/03-architecture/Archived/gui-action-map.assets.md`
  - `docs/03-architecture/Archived/gui-action-map.settings-diagnostics-shell.md`
  - `docs/03-architecture/Archived/code-path-index.md`
