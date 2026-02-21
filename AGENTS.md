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
3) **New behavior must be documented before implementation**, except tiny refactors that don’t change behavior.
   - “Documented” means: update **SRS + Acceptance Criteria** at minimum.
4) **Acceptance Criteria is the implementation contract.**
   - If you can’t map work to Acceptance Criteria, stop and escalate.
5) **Never commit secrets.**
   - If secrets are found: stop, report, and propose remediation (rotate + purge history if needed).

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

---

## 3) Repository Projects

- **LabAssistant** (UI app, `net8.0-windows`)
- **LabAssistant.Models** (domain objects, enums, DTOs)
- **LabAssistant.Data** (repositories, persistence)
- **LabAssistant.Services** (external APIs, file services, configuration)
- **LabAssistant.Business** (business logic, validation, coordination)

---

## 4) Architecture Boundaries & Dependency Rules (Strict)

Allowed dependencies:
- `LabAssistant (UI)` -> `LabAssistant.Business`, `LabAssistant.Models`, `LabAssistant.Services`
- `LabAssistant.Business` -> `LabAssistant.Data`, `LabAssistant.Models`, `LabAssistant.Services`
- `LabAssistant.Data` -> `LabAssistant.Models` (only if shared types are required)
- `LabAssistant.Services` -> `LabAssistant.Models` (only if shared types are required)
- `LabAssistant.Models` -> **no project dependencies**

Disallowed dependencies:
- No project may reference the UI project (`LabAssistant`).
- `LabAssistant.Models` must not reference any other project.
- `LabAssistant.Data` and `LabAssistant.Services` must not reference `LabAssistant.Business` or `LabAssistant (UI)`.
- Cross-references between `LabAssistant.Data` and `LabAssistant.Services` are not allowed.

Layering rules (behavioral):
- UI triggers actions and displays state; it should not contain orchestration logic.
- Business orchestrates workflows (validation, ordering, coordination).
- Services perform external actions (Hyper-V, PowerShell, filesystem, environment checks).
- Data handles persistence (template registry, base disk registry, settings storage, etc.).

---

## 5) Engineering Standards (Required)

### 5.1 Logging & Diagnostics
- All external operations must emit **structured logs**.
- Every user-initiated operation must have a **correlationId** propagated through the workflow.
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

### During coding
- Keep PRs small and focused
- Prefer additive changes over massive refactors unless explicitly required
- Do not reformat unrelated files

### After coding
- Ensure AC scenarios are satisfied (happy path + failures + cleanup behavior)
- Add/update tests
- Update docs if behavior/architecture changed
- Ensure logs are emitted at key steps with correlationId

---

## 8) PR Checklist (Dev Agent must follow)

- [ ] Matches Acceptance Criteria exactly
- [ ] No new scope added
- [ ] Cleanup policy preserved (no orphaned VMs/disks after failure/cancel)
- [ ] Logs added/updated with correlationId and required context
- [ ] Tests added/updated (unit tests for logic, mocks for Hyper-V if needed)
- [ ] Docs updated if behavior or architecture changed

---

## 9) Build Commands (Canonical)

From repo root:
- `dotnet restore LabAssistant.sln`
- `dotnet build LabAssistant.sln -c Debug`
- `dotnet build LabAssistant.sln -c Release`

Notes:
- UI targets `net8.0-windows` → builds must run on **Windows** with **.NET SDK 8** installed.

---

## 10) Issue Naming Conventions (Recommended)

- `US-###: <title>` — implementation for a user story
- `AC-###: <title>` — acceptance criteria updates
- `TBD: <topic>` — unresolved decision
- `Mismatch: docs vs code — <topic>` — inconsistency requiring resolution
- `Refactor: <area>` — behavior-preserving refactor (must confirm “no behavior change”)

