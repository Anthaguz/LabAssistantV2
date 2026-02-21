# Threat Model (Lightweight)

**Purpose:** Identify key risks and define mitigations early.

## Assets
- Templates (files): TBD
- Lab configs: TBD
- Logs/diagnostics: TBD
- Credentials (if any): TBD

## Threats (examples)
- Malicious template file (tampering)
- Path traversal / unsafe file IO
- Running elevated operations without validation
- Sensitive data leaked in logs

## Mitigations
- Schema validation + allowlist fields
- Safe path handling
- Least privilege where possible
- Log scrubbing rules

## Open Questions / TBDs
- TBD
