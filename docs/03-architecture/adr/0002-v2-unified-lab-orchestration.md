# ADR-0002: V2 Unified Lab Orchestration

**Status:** Accepted  
**Date:** 2026-05-25

## Context
LabAssistant currently deploys VMs through a host-oriented Hyper-V pipeline and only has placeholder-level guest-configuration support. The Active Directory lab orchestration script in `scripts/LabAssistantConfig script.ps1` shows a more advanced reality:

- host provisioning and guest configuration are interdependent
- critical infrastructure such as root domain controllers must start first
- some work can overlap, but only when dependency and resource pressure permit it
- router setup is optional, but cross-switch communication depends on it
- PowerShell Direct is the intended guest transport for current scope

The current V1 pipeline is valuable and must remain stable for existing templates. Replacing it in place would create unnecessary regression risk.

## Decision
- Introduce a `V2` lab-template orchestration path alongside the current `V1` engine.
- Route templates by schema version rather than by a user toggle or silent capability inference.
- Model V2 execution as one unified orchestration graph covering:
  - host provisioning tasks
  - readiness/wait tasks
  - guest configuration tasks
- Use topology roles, additive capability roles, explicit dependencies, workload classes, and deployment profiles as scheduling inputs.
- Do not use a single numeric priority as the primary scheduling contract.
- Use PowerShell Direct as the V2 guest-execution baseline.
- Keep exported templates portable by storing credential-slot references only; reusable secret values remain local-machine data.
- Extend the existing VHDX catalog with bootstrap-profile metadata instead of creating a parallel disk metadata system.

## Alternatives Considered
- Extend the V1 pipeline in place:
  - rejected because it would mix stable host-only deployment semantics with unfinished V2 orchestration semantics and increase rollback risk
- Keep host deployment and guest configuration as fully separate planner stages:
  - rejected because it cannot express the intended overlap/resource-economy model cleanly
- Use a per-VM numeric priority queue:
  - rejected because role/dependency semantics are richer than a single sortable number
- Embed secrets in V2 templates:
  - rejected because template sharing must remain portable without silently exporting reusable credentials

## Consequences
- Positive:
  - existing users keep the V1 path unchanged
  - V2 can evolve toward AD-core orchestration without distorting V1 contracts
  - scheduler/resource-policy decisions become explicit
  - disk/bootstrap assumptions become reusable without coupling disks to lab/domain intent
- Tradeoffs:
  - documentation, schema, and planner contracts become more complex
  - V1 and V2 must coexist for a period
  - import/deploy-time credential resolution UX needs deliberate design
