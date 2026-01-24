# Lab Template Schema (v0)

File name: lab-template.json

Purpose: Define a lab template that references VHDX catalog items and provides VM/network defaults.

## Top-level shape
- version (string, required): Schema version, e.g., "v0".
- id (string, required): Stable identifier for the template (unique).
- name (string, required): Display name for the template.
- description (string, optional): Short summary of the lab.
- vmTemplates (array, required): List of VM definitions.
- networkConfig (object, optional): Network defaults (switch name, optional IP hints).

## VmTemplate fields
- name (string, required): VM name.
- memoryMb (integer, required): Memory in MB.
- cpuCount (integer, required): CPU count.
- vhdxId (string, optional): Reference to VHDX catalog item id.
- vhdPath (string, optional): Fallback VHDX path if catalog id is missing or not resolved.
- switchName (string, optional): Override for virtual switch name.

Notes:
- Either vhdxId or vhdPath must be present; vhdxId is preferred when available.
- If vhdxId is provided but not found in the catalog, the template requires user selection.

## Example
See: docs/examples/lab-template.sample.json
