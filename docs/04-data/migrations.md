# Migrations

**Purpose:** Define how data/schema changes are handled over time.

## Strategy
- Approach: versioned JSON + deterministic upcaster chain.
- Backward compatibility: support current major schema `N` and previous major `N-1`.

### Upcaster Chain Rules

- Each migration is explicit and one-directional:
  - `vX -> vY` transforms one known source schema to one known target schema.
- Runtime load/import behavior:
  1. Parse source `schemaVersion`.
  2. If source major is `N`:
     - load directly (with minor/patch compatibility checks).
  3. If source major is `N-1`:
     - run sequential upcasters to current schema `N`.
  4. If source major is `< N-1`:
     - block with actionable guidance.
  5. If source major is `> N`:
     - block and recommend updating LabAssistant.
- Save/export behavior:
  - always persist current schema `N` only.

### Version Compatibility Outcomes

- Same major, older/equal minor/patch: allow.
- Same major, newer minor/patch: warn-and-continue if required fields are understood.
- Newer major than app supports: block.
- Older than support window: block.

## Migration List
- Legacy `version: v0` shape -> canonical `schemaVersion: 1.x`
  - Change:
    - map legacy `version` to canonical schema fields
    - generate required canonical metadata if missing (`templateType`, `templateRevision`, `createdWithAppVersion`, `vmId`)
  - Steps:
    1. load legacy fields
    2. normalize to canonical in-memory model
    3. validate against canonical required fields
    4. save/export in canonical schema
  - Validation:
    - required canonical fields present after migration
    - no data loss for existing VM definitions and VHD references

## Open Questions / TBDs
- Exact major version number for first canonical public release (`1.x` vs `2.x`) should follow release decision.
