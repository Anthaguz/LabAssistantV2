# Security Checklist (Release Gate)

**Purpose:** A quick checklist to run before shipping.

## Repo / Secrets
- [ ] No API keys/tokens committed
- [ ] No secrets in config files
- [ ] Secret scanning enabled (recommended)

## Input Validation
- [ ] Template files validated (schema)
- [ ] User-provided paths sanitized
- [ ] File operations use safe path handling

## Privileged Operations
- [ ] Admin-required actions are explicit
- [ ] Errors guide the user to fix permissions

## Logging
- [ ] Logs do not contain sensitive values
- [ ] Logs are structured (fields), not only strings

## Dependencies
- [ ] Dependencies updated regularly
- [ ] Known vulnerable packages addressed

## Open Questions / TBDs
- TBD
