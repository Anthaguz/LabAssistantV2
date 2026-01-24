# PLAN

## Core User Journeys (5)
1) Build a lab manually by configuring multiple VMs and their properties.
2) Save a manual lab configuration as a reusable template.
3) Browse templates, resolve missing VHDX selections, and deploy.
4) Maintain a catalog of VHDX images with OS metadata for reuse.
5) Manage existing labs (view status, start/stop, cleanup).

## North Star Demo (Minimal)
Goal: Template Catalog + VHDX Catalog -> Resolve Missing VHDX -> Dry-Run Deploy.

Demo steps:
- Load templates from disk and show them in a catalog.
- Load VHDX catalog entries with OS metadata.
- Open a template details view and show required VHDX mapping.
- If the VHDX path is missing, allow re-selecting a catalog entry.
- Click Deploy (dry-run) to execute the pipeline without Hyper-V calls.
- Display ordered steps and a success summary with log output.

## Data Model Sketch (v0)
- VhdxCatalogItem: Id, Path, OsName, OsVersion, Generation, Notes
- LabTemplate: Id, Name, Description, VmTemplates[], NetworkConfig
- VmTemplate: Name, MemoryMb, CpuCount, VhdxId, VhdPath, SwitchName
- DeploymentPlan: Steps[], Warnings[], Errors[]
- DeploymentStepResult: StepName, Status, Message, Timestamp
- TemplateLoadResult: Templates[], Errors[]
- VhdxCatalogLoadResult: Items[], Errors[]

## Non-Goals (for now)
- Authentication/authorization and multi-user support.
- Hyper-V side effects in the North Star demo (dry-run only).
- Advanced UI polish/animation; focus on workflow clarity.
- Remote orchestration or cloud deployment.

## Backlog (Small Issues, 1 PR each)

### Milestone A: VHDX Catalog + Template Schema
1) Define VHDX catalog schema v0 (doc + example JSON).
2) Add model for VhdxCatalogItem with OS metadata.
3) Define template schema v0 with VHDX references.
4) Add models for LabTemplate/VmTemplate with VhdxId + fallback path.
5) Add validation rules for templates and catalog entries.
6) Add unit tests for validation (missing VHDX, invalid catalog).
7) Define catalog/template storage convention and folder layout.
8) Add sample catalog + templates and schema notes.
9) Add catalog loader service (read, parse, validate).
10) Add template loader service (read, parse, validate).

### Milestone B: Template UI + VHDX Resolution
11) Add template catalog view (list/grid).
12) Add template tile item view model with readiness badge.
13) Add template details view with VHDX mapping.
14) Add navigation from catalog to details.
15) Add "select VHDX" action for missing paths.
16) Add persistence for last-selected VHDX per template.
17) Add basic error banner area in UI shell.
18) Add inline help text for template fields.

### Milestone C: Deploy Dry-Run (Business + UX)
19) Add DeploymentPlan builder (template -> ordered steps).
20) Add unit tests for DeploymentPlan output.
21) Add dry-run deployment pipeline (logs only).
22) Add step logging abstraction for dry-run pipeline.
23) Add dry-run results view with step list.
24) Add "export plan" action (JSON/text).
25) Add "copy logs" action for dry-run output.
26) Add failure handling with clear error summaries.

### Milestone D: Manual Lab Workflow (Read-Only)
27) Add manual lab builder view (VM list + properties).
28) Add validation for manual VM entries (name, memory, CPUs).
29) Add "save as template" action (writes JSON).
30) Add "load from template" action (read-only preview).
