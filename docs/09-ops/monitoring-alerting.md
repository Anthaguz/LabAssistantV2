# Monitoring & Alerting

**Purpose:** Define what to observe and how issues are detected.

## Logging
- Canonical local diagnostics log: `<LogFolder>\\structured-events.jsonl`
- Format: JSON Lines (one JSON object per line, UTF-8), canonical contract in `docs/01-requirements/logging-contract.md`
- Common fields: `ts`, `level`, `event`, `operationId`, optional `result`, optional `context`
- For deployment failures, structured events may include known artifact/path context (for example `parentVhdPath`, `targetVhdPath`, `vmPath`) to speed troubleshooting.
- The ambient tracer (`DebugLogger`) and the PowerShell timing tracer (`HyperVPowerShellTimingLogger`) now forward into the structured pipeline as coded `diag.debug` / `diag.ps_timing` events; the separate `log.txt` file is retired.
- Local rotation/retention (v1, size-based):
  - Structured logs:
    - Active file remains `structured-events.jsonl`
    - Rotate to `structured-events.1.jsonl`, `structured-events.2.jsonl`, ... (index-based)
    - Default active-file threshold: 5 MB
    - Default retained history files: 5
- Rotation occurs before appending a new record, so structured log lines are not split across files (rotated files remain valid JSONL)
- Diagnostics export currently remains compatible with the existing behavior and exports the active structured log file (`structured-events.jsonl`)
- PowerShell wrapper protocol/lifecycle traces (for troubleshooting) are off by default and can be enabled with environment variable `LABASSISTANT_POWERSHELL_WRAPPER_TRACE=1`; when enabled they are emitted as `diag.debug` structured events (Debug severity, so off in the log view unless Debug is shown)

## Metrics (optional)
- Deploy duration: TBD
- Failure rate: TBD

## Alerts (if applicable)
- What triggers alerts: TBD

## Open Questions / TBDs
- Whether to add local alerting/health summaries beyond diagnostics export
