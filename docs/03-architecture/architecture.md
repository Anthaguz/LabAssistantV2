# Architecture

**Purpose:** Explain how the system is structured and how code should be organized.

## How to fill this
- Describe your solution/projects and allowed dependencies.
- Document key workflows and where logic lives.
- Keep this aligned with the actual repo structure.

## 1. High-Level Overview
- **Architecture style:** TBD (layered, MVVM, etc.)
- **Key goals:** TBD (maintainability, automation, offline, etc.)

## 2. Solution Structure
Describe each project/module:
- **UI:** TBD (Views/ViewModels responsibilities)
- **Business:** TBD (orchestrates workflows, validation)
- **Services:** TBD (PowerShell/Hyper‑V/file ops)
- **Data:** TBD (repositories, persistence)
- **Models:** TBD (domain objects/DTOs)

## 3. Dependency Rules
- UI → Business → Services/Data → OS/External
- Forbidden dependencies: TBD

## 4. Key Workflows (end-to-end)
### Workflow: Deploy Lab
- Steps: TBD
- Error handling: TBD
- Logging: TBD

### Workflow: Template Import
TBD

## 5. Cross-Cutting Concerns
- Logging: TBD
- Configuration: TBD
- Background work / progress / cancellation: TBD

## Open Questions / TBDs
- TBD
