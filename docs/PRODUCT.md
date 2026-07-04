# LabAssistant Product Context (Verbose)

Last updated: 2026-02-08

## Purpose and Vision
LabAssistant is a Windows desktop app for building and managing lab environments based on reusable templates. The core idea is:
- A lab template describes a set of VMs and defaults (CPU/memory/switch).
- Each VM references a base VHDX image by catalog ID (or a fallback path).
- The VHDX catalog provides a curated, consistent source of OS images and metadata (OS name/version, generation, notes).
- Users can select VHDX images from the catalog, persist those selections, and later deploy VMs using those resolved paths.

This lets teams standardize lab environments while still allowing local overrides and fixes when a VHDX is missing or updated.

## High-Level User Workflows (End-to-End)

### 1) Maintain VHDX Catalog
1) Open the VHDX Catalog page from the sidebar.
2) Add a catalog entry with:
   - Path to the VHDX file
   - OS name and version
   - Generation (Hyper-V generation)
   - Notes (patch level, special build notes, etc.)
3) Edit or delete catalog entries as needed.
4) Catalog changes auto-save and persist to AppData.

### 2) Load Templates and Resolve VHDX
1) Open Templates and select a template.
2) The app loads the template list and validates VHDX references against the catalog.
3) For each VM:
   - If the selected VHDX exists, it is shown.
   - If the VHDX path is missing, the user is prompted to select a replacement.
4) The selection is stored in settings so it persists across restarts.

### 3) Compatibility Warnings (Non-Blocking)
1) Templates may expect a specific VHDX catalog entry (OS/version).
2) If a user selects a different entry with mismatched OS/version:
   - The app shows a warning.
   - The selection remains allowed (warn-only, not blocking).

### 4) Deployment (Existing Core Flow)
1) The app uses template definitions + resolved VHDX paths to build deployment plans.
2) VM creation and VHDX differencing are handled by the business/services layers.
3) This milestone intentionally did not change the deployment steps.

## Current Implementation Summary (Milestone D Complete)
Milestone D delivered a complete VHDX catalog + template mapping workflow:
- Catalog schema + storage.
- Catalog CRUD UI.
- Template-to-VHDX mapping persistence.
- Missing VHDX resolution prompts.
- Compatibility warnings (warn-only).
- Unit tests for catalog I/O, mappings, and compatibility.

## Key Data Concepts and Storage

### VHDX Catalog
- Stored at `%AppData%\\LabAssistant\\Catalog\\vhdx-catalog.json` by default.
- Each item contains:
  - `Id` (stable ID, auto-generated GUID on save)
  - `Path` (local file path)
  - `OsName`, `OsVersion`
  - `Generation`
  - `Notes`

### Template Mapping Persistence
- Stored in `AppSettings.TemplateSelections` as:
  - TemplateId
  - VmName
  - VhdxId
  - VhdPath
- When loading a template:
  - If a catalog entry is found for VhdxId, its `Path` is used.
  - Otherwise, VhdPath is used as fallback.

### Missing VHDX Resolution
- If a VM has a VHD path that does not exist on disk:
  - User is prompted to select a replacement from the catalog.
  - Selection updates template mappings in settings.
  - Cancel leaves a warning but does not block use of the template.

## UI Entry Points (Current)
- Sidebar:
  - Deploy VMs
  - Templates
  - VHDX Catalog
  - Switches
  - Settings
  - Logs

## Architecture Notes (Project Boundaries)
- UI: `LabAssistant.WinUI`
- Business: `LabAssistant.Business`
- Services: `LabAssistant.Services`
- Data: `LabAssistant.Data`
- Models: `LabAssistant.Models`
- Dependency rules enforced in `AGENTS.md`:
  - UI depends on Business/Services/Models.
  - Business depends on Data/Services/Models.
  - Services and Data depend only on Models.

## UI Models vs. Domain Models
- **Domain models live in `LabAssistant.Models`** and must be UI-agnostic (shared across WinUI/CLI).
- **UI-only models live in `LabAssistant.WinUI`** (e.g., under `ViewModels` or `UiModels`) and are safe to replace if the UI changes.
- If a type exists only to shape UI state (grouping, selection, formatting), keep it in the UI project.

## What Is Complete vs. What Is Next

### Completed (Milestone D)
- VHDX catalog schema + storage
- Catalog CRUD UI
- Template mapping persistence
- Missing VHDX resolution prompts
- Compatibility warnings (warn only)
- Unit tests for catalog/mapping/compatibility

### Not Done Yet (Potential Next Milestones)
These are candidates for the next milestone. They are not decided yet, but this is a suggested roadmap:

1) Template Authoring and Editing
   - UI to create/edit templates without manually editing JSON.
   - Inline validation and schema versioning.
   - Import/export template JSON.

2) Deployment UX Improvements
   - Clearer per-VM status and progress in deploy flow.
   - Better error reporting + retry guidance.
   - Optional "dry run" summary upgrades.

3) VHDX Management Enhancements
   - Detect stale paths and bulk relink.
   - Optional "scan folder" to discover VHDX files and prefill catalog.
   - Tagging and filtering for catalog entries.

4) Template-to-VHDX Compatibility Enforcement (Optional)
   - Today compatibility is warn-only.
   - Next step could be configurable blocking or policy-based checks.

5) Observability and Logs
   - Expose log filtering and more context in UI.
   - Auto-attach logs to reports.

## Proposed Next-Milestone Definition Template
To define the next milestone clearly, use this structure:

Milestone Name: <short name>
Goal: <one-sentence objective>
In Scope:
- Bullet list of concrete deliverables.
Out of Scope:
- Explicit exclusions.
Acceptance Criteria:
- Measurable checks for each deliverable.
Test Plan:
- Manual steps + automated tests required.

## Suggested Process for Defining the Next Milestone
1) Decide the next user-visible pain point or highest ROI improvement.
2) Draft a milestone goal and a small set of issues (3-6).
3) Write acceptance criteria + minimal tests for each issue.
4) Create the issues in GitHub before coding.
5) Use the PR workflow (documented in HANDOFF.md).

## Why This Document Exists
This document is the stable product context that HandOff can reference.
It is deliberately verbose so that a new session can quickly understand:
- What the app does
- What was recently built
- What the next logical milestone could be

## Link From Handoff
HANDOFF.md should include a short reference:
- "Product context and roadmap: docs/PRODUCT.md"
