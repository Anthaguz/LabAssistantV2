# Data Model

**Purpose:** Define core entities and relationships (even if stored as JSON/files).

## Entities
### LabTemplate
- Canonical persisted template definition for reusable lab deployments.
- Contains metadata (id, name, schemaVersion, templateRevision, createdWithAppVersion, templateType) and one or more VM template entries.
- Fields: see `docs/04-data/data-dictionary.md` and `docs/01-requirements/template-schema.md`.

### VmTemplate (VM Definition)
- One VM entry inside a lab template (`vmTemplates[]`).
- Stores hardware settings and deployment references (VHDX catalog id/path, switch name, optional signature).
- Includes immutable `vmId` for traceability across edits and deployments.
- Future guest-step execution selections/configuration may be persisted for implemented guest-step capabilities; placeholder-only UI affordances do not require persisted payloads by themselves.

### VHDX Catalog / VhdxCatalogItem
- Local registry of base disks available for deployments.
- Stores stable id, path, OS metadata, generation, and notes.
- Used for template references and missing-VHDX resolution workflows.

### Deployment Operation Context
- Runtime-only state for an active multi-VM deployment (`MultiVmDeploymentContext` + `VmDeploymentContext`).
- Tracks operation state, cancellation requests, per-VM progress/failures, cleanup results, summary inputs, and failure context (including known artifact/path context when available).
- Not the same as persisted templates.

### Deployment Readiness Report / Check Results
- Runtime-only preflight/readiness output used by Deploy UI and deploy-start gating.
- `DeploymentReadinessReport` contains mode (`Quick`/`Full`), ordered results, and aggregate helpers (`HasBlockingFailures`, `HasWarnings`, `CanDeploy`, `IsAuthoritative`).
- `DeploymentReadinessCheckResult` captures status/category/code/message/guidance and optional VM/resource attribution.
- Produced by the preflight engine and consumed by UI; not persisted as template data.

### VHDX Integrity Validation Result
- Shared runtime validation result used by catalog-time validation and deploy preflight for base disk checks.
- Distinguishes states such as valid, missing, unreadable/inaccessible, and invalid/corrupt.
- Supports both quick/cheap checks and full Hyper-V-readable validation paths.

### Cleanup / Outcome Results
- Structured runtime results for cleanup and deployment summaries:
  - `VmCleanupResult`, cleanup step results, residuals
  - deployment operation state and outcome summary models
- Used by UI summaries and diagnostics/logging workflows.

### Diagnostics Export Bundle (artifact model)
- ZIP package produced by diagnostics export service.
- Includes manifest, runtime metadata, operation context metadata, structured logs, and optional template definition artifact.

## Relationships
- `LabTemplate` -> contains -> `VmTemplate` entries (`vmTemplates[]`)
- Deployment operation -> uses -> `LabTemplate` or on-the-fly VM configuration
- `VmTemplate` -> references -> VHDX catalog items (`vhdxId`) or fallback base VHD path (`vhdPath`)
- Deployment operation -> produces -> `DeploymentReadinessReport` (quick/full preflight) before runtime execution
- Deployment operation -> produces -> cleanup results and deployment outcome summary
- Diagnostics export -> packages -> structured logs + runtime/operation metadata (+ optional template artifact)

## Versioning
- **Current canonical template schema version:** `1.0.0` (see `LabTemplate.CurrentSchemaVersion`)
- **Template compatibility policy:** support current major `N` and previous major `N-1`; block older/newer majors outside support window (see `docs/01-requirements/template-schema.md`)
- **Upgrade strategy:** deterministic upcaster chain documented in `docs/04-data/migrations.md`
- **Save/export behavior:** always persist current canonical schema version

## Open Questions / TBDs
- Whether to persist deployment history records as first-class local entities (currently optional/deferred).
- Whether diagnostics bundle metadata should become a documented persisted schema if imported/replayed in future features.
