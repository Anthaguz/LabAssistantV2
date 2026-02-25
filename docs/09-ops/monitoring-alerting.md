# Monitoring & Alerting

**Purpose:** Define what to observe and how issues are detected.

## Logging
- Canonical local diagnostics log: `<LogFolder>\\structured-events.jsonl`
- Format: JSON Lines (one JSON object per line, UTF-8), canonical contract in `docs/01-requirements/logging-contract.md`
- Common fields: `ts`, `level`, `event`, `operationId`, optional `result`, optional `context`
- For deployment failures, structured events may include known artifact/path context (for example `parentVhdPath`, `targetVhdPath`, `vmPath`) to speed troubleshooting.
- Legacy debug text logs (`DebugLogger`) may still exist as supplemental/transitional diagnostics
- Local rotation/retention (v1, size-based):
  - Structured logs:
    - Active file remains `structured-events.jsonl`
    - Rotate to `structured-events.1.jsonl`, `structured-events.2.jsonl`, ... (index-based)
    - Default active-file threshold: 5 MB
    - Default retained history files: 5
  - Debug logs:
    - Active file remains `log.txt`
    - Rotate to `log.1.txt`, `log.2.txt`, ... (index-based)
    - Default active-file threshold: 2 MB
    - Default retained history files: 5
- Rotation occurs before appending a new record, so structured log lines are not split across files (rotated files remain valid JSONL)
- Diagnostics export currently remains compatible with the existing behavior and exports the active structured log file (`structured-events.jsonl`)

## Metrics (optional)
- Deploy duration: TBD
- Failure rate: TBD

## Alerts (if applicable)
- What triggers alerts: TBD

## Open Questions / TBDs
- Whether to add local alerting/health summaries beyond diagnostics export
