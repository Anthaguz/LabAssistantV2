# Data Dictionary

**Purpose:** Field-by-field definitions for your stored data.

## How to fill this
Add rows for every field in templates/labs/config.

| Entity | Field | Type | Required | Description | Example |
|--------|-------|------|----------|-------------|---------|
| Template | version | string | yes | Schema version | "1.0" |
| Template | name | string | yes | Display name | "Win11 Lab" |
| VM | memoryMb | int | TBD | VM memory | 4096 |
| VmDeploymentContext | vhdPath | string | yes | Differencing disk path created for the VM | `C:\LabAssistant\Disks\VM1\VM1.vhdx` |
| VmDeploymentContext | baseVhdPath | string | yes (deploy) | Parent/base VHD path used to create the differencing disk | `D:\Catalog\Base\Win2022.vhdx` |
| VmTemplate | vhdPath | string | yes (template) | Base VHD path selected from catalog for future deployment | `D:\Catalog\Base\Win2022.vhdx` |

## Open Questions / TBDs
- TBD
