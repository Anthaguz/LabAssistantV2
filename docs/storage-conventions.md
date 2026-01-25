# Storage Conventions

This document defines the default folder layout and file naming for templates and the VHDX catalog.

## Catalog
- Folder: docs/examples (for sample files)
- Runtime location (recommended): %ProgramData%\LabAssistant\catalog\vhdx-catalog.json

## Templates
- Folder: docs/examples (for sample files)
- Runtime location (recommended): %ProgramData%\LabAssistant\templates\*.json

## Notes
- Catalog file is a single JSON document containing an array of VHDX entries.
- Each template is a standalone JSON file with a unique template id.
- Runtime locations can be overridden via app settings in the future.
