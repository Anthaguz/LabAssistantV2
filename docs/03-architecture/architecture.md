# Architecture

**Purpose:** Explain how the system is structured and how code should be organized.

## 1. High-Level Overview
- **Architecture style:** Layered desktop application with MVVM-style UI state management.
- **Key goals:** Maintainable local-first automation, safe Hyper-V operations, testable orchestration logic, and clear diagnostics.

## 2. Solution Structure
- **UI (`LabAssistant`)**
  - WPF application and top-level composition root.
  - Owns navigation, views, viewmodels, and user interaction state.
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

## 4. Key Workflows (end-to-end)
### Workflow: Deploy Lab (on-the-fly or template-based)
1. UI/ViewModel validates deploy prerequisites (selected VHDX, switch, settings).
2. UI creates `MultiVmDeploymentContext` and starts business coordinator.
3. Business coordinator runs per-VM deployment pipelines using Hyper-V/PowerShell services.
4. Runtime emits progress/log events and updates operation state (`Running`, `Cancelling`, etc.).
5. On blocking failure or user cancellation, cleanup orchestration runs for affected VMs.
6. Business builds structured outcome summary (per-VM + global + residuals).
7. UI displays final summary and re-enables configuration after terminal state.

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
- **Logging**
  - Structured JSONL logging (`structured-events.jsonl`) is the canonical diagnostics path.
  - Legacy `DebugLogger` text logs remain supplemental/transitional.
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

## Open Questions / TBDs
- Sequence diagrams for deploy/cancel/cleanup and diagnostics export (`docs/03-architecture/sequence-diagrams.md`).
- Whether future switch management and guest configuration features should introduce new business workflow coordinators or extend current deployment pipeline abstractions.
