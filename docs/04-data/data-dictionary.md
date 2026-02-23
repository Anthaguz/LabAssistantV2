# Data Dictionary

**Purpose:** Field-by-field definitions for your stored data.

## How to fill this
Add rows for every field in templates/labs/config.

| Entity | Field | Type | Required | Description | Example |
|--------|-------|------|----------|-------------|---------|
| LabTemplate | id | string | yes | System-generated immutable template identifier | `"1d62127b176547b8b37a1d84842e47bf"` |
| LabTemplate | name | string | yes | User-facing template display name | `"AD Lab"` |
| LabTemplate | description | string | no | Human-readable summary | `"Two server lab"` |
| LabTemplate | schemaVersion | string | yes | Canonical schema version used for compatibility checks | `"1.0.0"` |
| LabTemplate | templateRevision | int | yes | User/content revision counter | `3` |
| LabTemplate | createdWithAppVersion | string | yes | App version that produced the saved template | `"0.0.0"` |
| LabTemplate | templateType | string | yes | Template kind; current lab flow uses `lab-template` | `"lab-template"` |
| LabTemplate | vmTemplates | array<VmTemplate> | yes | VM definitions contained in the lab template | `[ ... ]` |
| LabTemplate | networkConfig | object | no | Optional lab-level network defaults | `{ "switchName": "LabSwitch" }` |
| VmTemplate | vmId | string | yes | System-generated immutable VM entry identifier | `"7eb637f580f14f0a878df9974ea8d1fa"` |
| VmTemplate | name | string | yes | VM display name | `"web-01"` |
| VmTemplate | memoryMb | int | yes | VM memory in MB | `4096` |
| VmTemplate | cpuCount | int | yes | VM CPU count | `2` |
| VmTemplate | vhdxId | string | conditional | Preferred catalog reference id (requires catalog entry) | `"win-server-2022-gen2"` |
| VmTemplate | vhdPath | string | conditional | Fallback base VHD path when catalog id is unavailable | `D:\Catalog\Base\Win2022.vhdx` |
| VmTemplate | vhdxSignature | string | no | Portable signature used to match catalog entries across machines | `"win-server-2022|2022|gen2"` |
| VmTemplate | switchName | string | yes (v1 deploy) | Hyper-V virtual switch name for VM attachment | `"LabSwitch"` |
| VmDeploymentContext | vhdPath | string | yes | Differencing disk path created for the VM | `C:\LabAssistant\Disks\VM1\VM1.vhdx` |
| VmDeploymentContext | baseVhdPath | string | yes (deploy) | Parent/base VHD path used to create the differencing disk | `D:\Catalog\Base\Win2022.vhdx` |
| VmDeploymentContext | vhdxId | string | no | Catalog id selected for the VM during deploy/template mapping | `"win-server-2022-gen2"` |
| VmDeploymentContext | vhdxSignature | string | no | Signature used for catalog matching and portability workflows | `"win-server-2022|2022|gen2"` |

## Open Questions / TBDs
- Final canonical values for `createdWithAppVersion` formatting if release version source changes.
