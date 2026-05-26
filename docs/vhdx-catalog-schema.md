# VHDX Catalog Schema (v0)

File name: vhdx-catalog.json

Purpose: Catalog available VHDX base images with OS metadata so templates can reference them by ID.

## Top-level shape
- version (string, required): Schema version, e.g., "v0".
- items (array, required): List of VHDX catalog entries.

## VhdxCatalogItem fields
- id (string, required): Stable identifier used by templates (unique).
- path (string, required): Absolute or relative path to the base VHDX.
- osName (string, required): OS family or name, e.g., "Windows Server".
- osVersion (string, required): OS version/build label, e.g., "2022".
- generation (integer, required): Hyper-V VM generation, typically 1 or 2.
- sizeBytes (integer, optional): Size hint used by portability/signature logic.
- signature (string, optional/generated): Stable identity hint derived from catalog metadata.
- notes (string, optional): Freeform notes about the image.

## Bootstrap profile extension (V2 planning baseline)

V2 planning extends catalog entries in place with optional bootstrap metadata.

Purpose:

- capture guest-access assumptions that belong to the base image itself
- avoid embedding lab/domain intent into the disk catalog
- let shared templates stay portable by referencing slot names instead of secrets

### bootstrapProfile fields

- expectedLocalUser (string, optional): Expected local/bootstrap username for the image.
- localCredentialSlotRef (string, optional): Credential-slot reference expected to unlock local PowerShell Direct access.
- guestOsFamily (string, optional): Additional OS family detail if needed for guest-executor assumptions.
- guestTransport (string, optional): Current baseline is `powershell-direct`.
- notes (string, optional): Image/bootstrap-specific operational notes.

Rule:

- bootstrap profile stores references and assumptions only
- reusable secret values are not stored in exported templates
- lab/domain behavior such as domain role, routing, PKI, or workload intent must not live in the disk bootstrap profile

## Example
See: docs/examples/vhdx-catalog.sample.json

## Open Questions / TBDs
- Whether future catalog evolution needs multiple named bootstrap profiles per disk.
- Whether guest transport needs any supported value beyond `powershell-direct` in current scope.
