# Monitoring & Alerting

**Purpose:** Define what to observe and how issues are detected.

## Logging
- Canonical local diagnostics log: `<LogFolder>\\structured-events.jsonl`
- Format: JSON Lines (one JSON object per line, UTF-8), canonical contract in `docs/01-requirements/logging-contract.md`
- Common fields: `ts`, `level`, `event`, `operationId`, optional `result`, optional `context`
- Legacy debug text logs (`DebugLogger`) may still exist as supplemental/transitional diagnostics

## Metrics (optional)
- Deploy duration: TBD
- Failure rate: TBD

## Alerts (if applicable)
- What triggers alerts: TBD

## Open Questions / TBDs
- Retention/rotation policy for local structured logs
- Whether to add local alerting/health summaries beyond diagnostics export
