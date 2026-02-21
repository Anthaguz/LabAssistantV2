# Logging Contract

**Purpose:** Define the required logging structure, event catalog, and diagnostics expectations for LabAssistant operations.

## Contract Summary

- Every major operation must emit start, step, and terminal events.
- Logs must be machine-parseable and traceable by operation identifier.
- Secrets must never be logged.
- Failures must include actionable context.

## Canonical Format

Target canonical format: JSON Lines (`.jsonl`), one JSON object per line.

Example:

```json
{"ts":"2026-02-10T03:12:01Z","level":"info","event":"DeployLabStarted","operationId":"a1","templateId":"t1"}
{"ts":"2026-02-10T03:12:04Z","level":"info","event":"VmCreated","operationId":"a1","vmName":"VM1","result":"success"}
{"ts":"2026-02-10T03:12:09Z","level":"error","event":"DeployLabFailed","operationId":"a1","result":"failed_with_residuals"}
```

Transition note:

- Current logger implementation may differ.
- Migration to full JSONL should be incremental, but emitted events and fields must converge to this contract.

## Identifier Model

Required identifier:

- `operationId`: unique id per deploy/import/export/action execution.

Optional additional identifier:

- `correlationId` may be retained if existing code already uses it.

Rule:

- At least one stable per-operation id is mandatory on all related events.

## Required Common Fields

All events must include:

- `ts` (UTC timestamp, ISO-8601)
- `level` (`debug` | `info` | `warn` | `error`)
- `event` (event name)
- `operationId`
- `result` where applicable

Context fields by scenario:

- `templateId`, `templateName`
- `vmId`, `vmName`
- `stepKey`
- `durationMs`
- `errorCode`, `errorMessage`, `exceptionType`
- `resourcePath` (when relevant)

## Required Event Families

Deployment:

- `DeployLabStarted`
- `DeployLabCompleted`
- `DeployLabFailed`
- `DeployLabCancelled`
- `VmDeployStarted`
- `VmDeployCompleted`
- `VmDeployFailed`

Step execution:

- `StepStarted`
- `StepCompleted`
- `StepFailed`

Cleanup:

- `CleanupStarted`
- `CleanupStepCompleted`
- `CleanupStepFailed`
- `CleanupCompleted`
- `CleanupResidualsDetected`

Template/catalog operations:

- `TemplateValidationFailed`
- `TemplateSaved`
- `TemplateImported`
- `TemplateExported`
- `CatalogLoaded`
- `CatalogSaved`

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

Default diagnostics export includes:

- Operation logs
- Runtime metadata
- Template ids/names and operation context metadata

Optional user-controlled inclusion:

- Full template definition files used in deployment

User-facing labels should be plain language, not internal technical terms.

## Open Questions / TBDs

- Whether to enforce strict schema validation for each log event at runtime.
- Retention and rotation policy for high-volume logs.
