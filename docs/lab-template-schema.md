# Lab Template Schema (Legacy v0 Reference)

File name: `lab-template.json`

Purpose: Historical reference for the early v0 template shape.

## Status

- Deprecated as the active schema reference.
- Kept only to document legacy templates that may still be migrated by the app.
- Canonical source of truth is now:
  - `docs/01-requirements/template-schema.md`

## Legacy v0 notes (high level)

- Used top-level `version` (for example `v0`) instead of canonical `schemaVersion`.
- Did not require canonical metadata fields such as:
  - `templateRevision`
  - `createdWithAppVersion`
  - `templateType`
- VM entries did not require `vmId`.

## Migration Guidance

- Legacy templates should be loaded through the app import/load pipeline and saved again.
- Current app saves templates in canonical schema format only.

## Example

- Current canonical example: `docs/examples/lab-template.sample.json`
