# V2 Unified Orchestration Roadmap

**Purpose:** Define the connected milestone and issue structure for the V2 unified orchestration initiative so work stays sequenced and dependency-aware instead of splitting into unrelated feature shards.

## Milestones

### Milestone 1: Architecture, Schema, And Scheduler Package
- behavior inventory from `LabAssistantConfig script.ps1`
- ADR for separate V2 pipeline and unified host+guest orchestration
- V2 schema/data contract docs
- scheduler profile/workload-class contract
- connected epic/issue tree

### Milestone 2: Code Contracts Only
- V2 models/interfaces
- schema-version routing seam
- bootstrap profile resolver seam
- credential-slot discovery seam
- PowerShell Direct guest executor seam

### Milestone 3: Thin Planning Slice
- parse V2 template
- resolve bootstrap and credential-slot references
- build unified orchestration graph
- emit review-and-resolve planning output

### Milestone 4: First Runtime Slice
- DC-first host provisioning
- readiness gates
- network bootstrap
- optional router path
- profile-based overlap limits

### Milestone 5: AD-Core Execution
- root DC/forest path
- replica DC path
- member join path

### Milestone 6: Extended Topologies
- child domain runtime execution
- tree-domain runtime execution
- additional independent root-forest runtime execution
- trust runtime as the remaining explicit topology runtime gap
- V2 Builder authoring so users can create/edit the backend model without hand-authored JSON
- richer additive capability-role execution
- separate follow-up for persisting base remote-access guest behavior in V2 template authoring after deploy-time validation lands

## GitHub Epic Structure

### Epic 1: V2 Unified Orchestration Architecture
GitHub: `#716`
1. inventory/contract issue
2. planner issue
3. runtime-slice issue

### Epic 2: V2 Template Schema And Compatibility
GitHub: `#717`
1. schema/model issue
2. schema-version routing issue
3. validation/preflight issue

### Epic 3: V2 Credentials And Bootstrap Profiles
GitHub: `#718`
1. disk/bootstrap profile issue
2. credential-slot issue
3. unresolved-slot UX/review issue

### Epic 4: V2 Scheduler Profiles And Workload Classes
GitHub: `#719`
1. workload-class contract issue
2. profile/cap issue
3. scheduler issue

### Epic 5: V2 Review And Resolve Planner
GitHub: `#720`
1. graph output issue
2. dependency blocker visibility issue
3. deploy-time review surface issue

### Epic 6: V2 AD-Core Runtime Path
GitHub: `#721`
1. DC-first provisioning/runtime issue
2. root forest issue
3. replica/member follow-up issue

### Epic 7: V2 Extended Domain Topologies
GitHub: `#722`
1. child-domain contract issue
2. child-domain runtime issue
3. extra-forest/tree follow-up issue
4. trust runtime follow-up issue

## Current Backend State

The backend V2 runtime now executes:

- root domain and root forest creation
- child domain creation
- tree domain creation
- multiple independent root forests
- replica domain-controller promotion
- per-domain DNS stabilization
- domain-member joins
- router guest configuration and routed readiness gates
- deploy-time V2 review-and-resolve planning
- deploy-time base remote-access guest configuration

Trust declarations remain persisted and validated contract data only. They are not represented as executable plan nodes and do not run in the current runtime path.

## Historical Initial Recommended Issue Chain

This chain records the original issue setup. It is historical by default and should not be treated as the current next-action list.

1. `#716` `Epic: V2 Unified Orchestration Architecture`
2. `#723` `Architecture: Inventory LabAssistantConfig script behavior and implicit scheduling waves`
3. `#717` `Epic: V2 Template Schema and Compatibility`
4. `#724` `Schema: Define V2 template planning contract and schema-version routing rules`
5. `#718` `Epic: V2 Credentials and Bootstrap Profiles`
6. `#725` `Contract: Extend VHDX catalog with bootstrap-profile metadata and credential-slot references`
7. `#719` `Epic: V2 Scheduler Profiles and Workload Classes`
8. `#726` `Contract: Define V2 deployment profiles, workload classes, and overlap rules`
9. `#720` `Epic: V2 Review and Resolve Planner`
10. `#727` `Planner: Emit deterministic V2 orchestration graph and review-and-resolve output`
11. `#721` `Epic: V2 AD-Core Runtime Path`
12. `#728` `Runtime: Add DC-first provisioning and readiness-gated V2 execution slice`
13. `#722` `Epic: V2 Extended Domain Topologies`
14. `#729` `Contract: Reserve child-domain and extra-forest extension seam for V2`

## Current Recommended Issue Chain

1. `#750` `Mismatch: docs vs code - V2 extended topology execution boundary`
2. `#751` `Contract: Define first V2 forest-trust runtime slice`
3. `#753` `Runtime: Execute bidirectional managed-forest V2 trust path`
4. `#754` `Contract: Define Templates V2 Builder workflow`
5. `#755` `UX: Add Templates V2 Builder topology-first authoring surface`

## Open Questions / TBDs

- Whether V2 Builder should eventually replace the current Templates Editor for all templates or remain only the V2 authoring surface.
