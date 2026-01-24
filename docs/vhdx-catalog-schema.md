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
- notes (string, optional): Freeform notes about the image.

## Example
See: docs/examples/vhdx-catalog.sample.json
