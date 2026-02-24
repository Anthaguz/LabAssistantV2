# Logging Contract

**Purpose:** Define the required logging structure, event catalog, and diagnostics expectations for LabAssistant operations.

## Contract Summary

- Every major operation must emit start, step, and terminal events.
- Logs must be machine-parseable and traceable by operation identifier.
- Secrets must never be logged.
- Failures must include actionable context.

## Canonical Format

Canonical format: JSON Lines (`.jsonl`), one JSON object per line.

Current v1 structured event shape:

```json
{"ts":"2026-02-10T03:12:01.0000000Z","level":"info","event":"DeployLabStarted","operationId":"a1","result":"started","context":{"vmCount":2}}
{"ts":"2026-02-10T03:12:04.0000000Z","level":"info","event":"StepCompleted","operationId":"a1","result":"success","context":{"vmId":"vm1","vmName":"VM1","stepKey":"CreateVm"}}
{"ts":"2026-02-10T03:12:09.0000000Z","level":"error","event":"DeployLabFailed","operationId":"a1","result":"failed_with_residuals","context":{"cleanupVmCount":1,"residualVmCount":1}}
```

Transition note:

- Structured JSONL is the canonical diagnostics path moving forward.
- Legacy `DebugLogger` text logs remain available as supplemental/transitional diagnostics.
- Migration to broader coverage is incremental, but emitted event names/fields must converge to this contract.

## Identifier Model

Required identifier:

- `operationId`: unique id per deploy/import/export/action execution.

Transitional note:

- `correlationId` may appear in older documentation or legacy logs, but `operationId` is the canonical field for structured events.

Rule:

- A stable `operationId` is mandatory on all related structured events.

## Required Common Fields

All structured events must include:

- `ts` (UTC timestamp, ISO-8601)
- `level` (`debug` | `info` | `warn` | `error`)
- `event` (event name)
- `operationId`
- `result` where applicable

Optional event payload:

- `context` (object/map for scenario-specific fields)

Context fields by scenario (inside `context`):

- `templateId`, `templateName`
- `vmId`, `vmName`
- `stepKey`
- `durationMs`
- `errorCode`, `errorMessage`, `exceptionType`
- `resourcePath` (when relevant)

## Required Event Families

Deployment (v1 emitted):

- `DeployLabStarted`
- `DeployLabCompleted`
- `DeployLabFailed`
- `DeployLabCancelled`
- `VmDeployStarted`
- `VmDeployCompleted`
- `VmDeployFailed`

Step execution (v1 emitted):

- `StepStarted`
- `StepCompleted`
- `StepFailed`

Cleanup (v1 emitted):

- `CleanupStarted`
- `CleanupStepCompleted`
- `CleanupStepFailed`
- `CleanupCompleted`
- `CleanupResidualsDetected`

Template/catalog operations (v1 emitted):

- `TemplateImported`
- `TemplateSaved`
- `CatalogLoaded`
- `CatalogSaved`

Template/catalog operations (planned / incremental):

- `TemplateValidationFailed`
- `TemplateExported`

## Severity Rules

- `info`: lifecycle milestones and successful operations.
- `warn`: recoverable issues, compatibility warnings, degraded but successful behavior.
- `error`: operation failure, cleanup failure, residual detection.
- `debug`: optional low-level diagnostics.

## Redaction Rules

Never log:

- Passwords
- Secure strings
- Tokens/credentials

Allowed by default:

- Usernames
- File paths

If future policy changes, add field-level masking rules here.

## Diagnostics Bundle Policy

Diagnostics export bundle v1 is a ZIP package and includes:

- `bundle/manifest.json`
- `metadata/runtime-metadata.json`
- `metadata/operation-context.json`
- `logs/structured-events.jsonl`

Optional user-controlled inclusion:

- `artifacts/template-definition.json` (full template definition file)

Rules:

- `logs/structured-events.jsonl` must remain valid JSONL (one parseable JSON object per line).
- If an `operationId` filter is supplied to export, only matching structured events are included.
- User-facing labels should be plain language, not internal technical terms.

## Open Questions / TBDs

- Whether to enforce strict schema validation for each structured event at runtime.
- Retention and rotation policy for high-volume logs.
