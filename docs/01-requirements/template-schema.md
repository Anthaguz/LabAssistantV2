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
- `switchName` (legacy fallback): single Hyper-V virtual switch name.
- `switchNames` (optional): ordered list of Hyper-V virtual switch names for multi-NIC template editing and deployment mapping.

Validation rule:

- At least one of `vhdxId` or `vhdPath` must exist for each VM.
- If `switchNames` is present:
  - values must be non-empty
  - duplicate switch names are not allowed
- If `switchNames` is absent, `switchName` may be used for legacy compatibility.

Switch persistence compatibility rule:

- During transition, writers may dual-write:
  - `switchNames` as canonical (when available)
  - `switchName` as legacy fallback using the first `switchNames` entry
- Readers must prefer `switchNames` when present, and fallback to `switchName` otherwise.

Editor parity clarification:

- WinUI template VM-entry editing parity (AD5/AD6) operates on these existing `vmTemplates[]` fields and existing optional subobjects only.
- Templates selector hardening (AE) introduces `switchNames` as a controlled schema extension with compatibility fallback to legacy `switchName`.

VHDX normalization clarification:

- Resolution precedence for effective base-disk identity is:
  1. `vhdxId`
  2. `vhdxSignature`
  3. `vhdPath`
- `vhdxSignature` is a deterministic catalog-level signature composed from normalized OS metadata + generation (+ optional size), not a cryptographic file hash.

## Network Scope (v1)

For v1 topology, only switch attachment is canonical:

- `switchName` is required and references existing Hyper-V switch.

Future-friendly guest network config may exist as optional VM subobject:

- `guestNetworkConfig` (optional placeholder): `ipAddress`, `defaultGateway`, `dnsServers`.

This is guest configuration intent, not Hyper-V topology control.

## V2 Planning Direction

`V2` is a schema evolution for unified orchestration planning, not a silent replacement for `V1`.

### V2 compatibility rule

- `V1` templates remain valid for the current engine.
- `V2` templates are routed to the new orchestration planner by schema version.
- `V2` fields must not be required for `V1` templates.

### V2 first-class objects

For `V2`, the schema direction expands the current model to include:

- lab networks
- VM NIC collections
- explicit per-NIC guest addressing
- topology roles
- additive capability roles
- dependency declarations
- deployment profile selection
- credential slot references
- VHDX/bootstrap profile references

### V2 topology vs capability roles

Topology roles affect scheduling and dependency semantics:

- `Router`
- `RootDomainController`
- `ReplicaDomainController`
- `MemberServer`
- `StandaloneServer`

Capability roles request additive work without replacing topology roles:

- `Pki`
- `Sql`
- `Web`
- `Operations`

Rule:

- a VM may hold one topology role and multiple additive capability roles
- a VM may also carry future topology/capability combinations when explicitly supported

### V2 network authoring direction

V2 uses a hybrid network model:

- central network objects define shared validation and naming context
- each VM NIC keeps explicit switch attachment and guest IP intent by default

This avoids relying on hidden auto-allocation while still giving templates shared network structure.

### V2 NIC direction

Instead of only `switchName` / `switchNames`, a V2 VM entry may declare a NIC collection with:

- stable NIC id or name
- target lab network / switch attachment
- guest IP address
- prefix length
- default gateway
- DNS server list
- optional router/dependency annotations where needed

During transition:

- `switchNames` remains the V1 canonical multi-NIC list
- a later V2-compatible writer/reader may add richer NIC objects without breaking the V1 contract

### V2 credentials and bootstrap references

V2 templates must reference credentials by slot/label rather than embedding reusable secret values.

Template-owned references may include:

- local bootstrap credential slot reference
- domain join credential slot reference
- role-specific credential slot reference

Reusable secret values remain a local-machine concern and must not travel in exported templates.

## Guest/Role Placeholder Sections

The schema includes optional sections now for forward compatibility:

- `timeZoneConfig` (optional): `enabled` + optional `timeZoneId`.
- `roleConfig` (optional): `enabled` + role-specific settings.
- `softwareConfig` (optional): `enabled` + package/install settings.
- `guestNetworkConfig` (optional): `enabled` + guest IP settings.

Rule:

- Presence in template does not guarantee runtime execution unless implemented and enabled by current app behavior.
- Current app behavior may omit unimplemented placeholder sections when saving templates; UI visibility of a placeholder control does not require persisting a placeholder payload.

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

- Exact migration flow UX for schema upgrades.
- Field-level compatibility matrix for minor schema version changes.
- Whether deterministic auto-allocation should ever complement explicit per-NIC addressing in V2.
- Exact child-domain/tree/extra-forest V2 field set beyond the reserved extension seam.
