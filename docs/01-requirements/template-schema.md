# Template Schema

**Purpose:** Define the canonical template data contract for `vm-template` and `lab-template`, including versioning, required fields, and forward-compatible sections.

## Scope

- This document defines schema behavior for storage/import/export/validation.
- Runtime deployment behavior is defined in acceptance criteria and policy docs.

## Schema Model

Two schema types are supported:

- `vm-template`: single VM definition reusable as a building block.
- `lab-template`: a full environment definition that contains one or more VM entries in `vmTemplates`.

`lab-template` keeps the existing `vmTemplates` field name as canonical for compatibility with current code and existing templates.

## Versioning Strategy

Use separate fields for compatibility vs user change history:

- `schemaVersion`: semantic version of template schema format (example `1.0.0`).
- `templateRevision`: user/content revision (example `1`, `2`, `3`).
- `createdWithAppVersion`: app version that produced the template (example `1.4.0`).

Rationale:

- `schemaVersion` controls parser compatibility.
- `templateRevision` tracks user updates without implying compatibility break.

## Compatibility Rules

- Unsupported major `schemaVersion`: import must block.
- Newer minor/patch version: import may proceed with warning if fields are understood.
- Migration path must be explicit: user can save in current schema when supported.

### Support Window Policy

To keep templates user-friendly across upgrades while avoiding unsafe parsing behavior:

- App supports loading templates for:
  - current major schema version `N`
  - previous major schema version `N-1`
- Templates older than `N-1` must be blocked with actionable guidance.
- Templates from newer major version (`N+1` or higher) must be blocked with:
  - clear message that the template was created with a newer schema
  - recommendation to update LabAssistant

Save/export rule:

- Any template saved/exported by the current app must be written in current schema major `N` with canonical fields.
- The app must not emit legacy-only shape on save.

## Canonical Fields

Common fields (both template types):

- `id` (required): system-generated immutable identifier.
- `name` (required): user-facing template name.
- `description` (optional): human-readable summary.
- `schemaVersion` (required): semantic schema version.
- `templateRevision` (required): user revision counter.
- `createdWithAppVersion` (required): originating app version.
- `templateType` (required): `vm-template` or `lab-template`.

Lab template fields:

- `vmTemplates` (required): array of VM entries, length >= 1.

VM entry fields (`vmTemplates[]`):

- `vmId` (required): system-generated immutable VM entry identifier for traceability.
- `name` (required): VM display name.
- `memoryMb` (required): positive integer.
- `cpuCount` (required): positive integer.
- `vhdxId` (optional): catalog reference id.
- `vhdPath` (optional fallback): base image path when id is unavailable.
- `vhdxSignature` (optional): portable match signature.
- `switchName` (required for v1 deployment): Hyper-V virtual switch name.

Validation rule:

- At least one of `vhdxId` or `vhdPath` must exist for each VM.

## Network Scope (v1)

For v1 topology, only switch attachment is canonical:

- `switchName` is required and references existing Hyper-V switch.

Future-friendly guest network config may exist as optional VM subobject:

- `guestNetworkConfig` (optional placeholder): `ipAddress`, `defaultGateway`, `dnsServers`.

This is guest configuration intent, not Hyper-V topology control.

## Guest/Role Placeholder Sections

The schema includes optional sections now for forward compatibility:

- `roleConfig` (optional): `enabled` + role-specific settings.
- `softwareConfig` (optional): `enabled` + package/install settings.
- `guestNetworkConfig` (optional): `enabled` + guest IP settings.

Rule:

- Presence in template does not guarantee runtime execution unless implemented and enabled by current app behavior.

## Example (lab-template)

```json
{
  "id": "1d62127b176547b8b37a1d84842e47bf",
  "name": "AD Lab",
  "description": "Two server lab",
  "schemaVersion": "1.0.0",
  "templateRevision": 1,
  "createdWithAppVersion": "1.0.0",
  "templateType": "lab-template",
  "vmTemplates": [
    {
      "vmId": "0b93a4ebf4fd4b54a7081b4b7c0f6e08",
      "name": "VM1",
      "memoryMb": 2048,
      "cpuCount": 2,
      "vhdPath": "C:\\training\\BaseVHDX\\En_Win_Server_2022.vhdx",
      "switchName": "Default Switch",
      "roleConfig": { "enabled": false },
      "softwareConfig": { "enabled": false },
      "guestNetworkConfig": { "enabled": false }
    },
    {
      "vmId": "362dd7f995e145b0995100b6c1d3f4cb",
      "name": "VM2",
      "memoryMb": 2048,
      "cpuCount": 2,
      "vhdPath": "C:\\training\\BaseVHDX\\En_Win_Server_2022.vhdx",
      "switchName": "Default Switch",
      "roleConfig": { "enabled": false },
      "softwareConfig": { "enabled": false },
      "guestNetworkConfig": { "enabled": false }
    }
  ]
}
```

## Open Questions / TBDs

- Final `templateRevision` format: integer counter vs semantic version.
- Exact migration flow UX for schema upgrades.
- Field-level compatibility matrix for minor schema version changes.
