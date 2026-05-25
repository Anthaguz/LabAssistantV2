# V2 Unified Orchestration

**Purpose:** Define the canonical V2 orchestration architecture for unified host provisioning, readiness gating, and guest configuration planning.

## Scope

- This document defines the V2 architecture direction.
- It does not replace the current V1 deployment engine.
- Runtime execution details remain incremental; this document sets the target contract.

## Core Model

V2 uses one orchestration graph that can contain:

- host provisioning nodes
- readiness/wait nodes
- guest configuration nodes

This is intentionally different from a simple "deploy VMs, then configure them later" model.

## Node Kinds

### Host provisioning
- create VM folder
- create differencing disk
- create VM registration
- attach NICs
- configure hardware
- start VM

### Readiness and validation
- PowerShell Direct availability
- reboot completion wait
- DNS readiness
- domain readiness
- router readiness
- post-change validation

### Guest configuration
- guest network bootstrap
- router/RRAS/NAT setup
- Windows feature installation
- domain promotion
- replica promotion
- domain join
- additive capability-role configuration

## Dependency Model

The planner should support:

- hard dependencies
- stage barriers where needed
- controlled overlap when safe
- deterministic output for the same input template

The primary planning inputs are:

- topology roles
- additive capability roles
- explicit dependencies
- workload class
- deployment profile

The primary planning input is not a single numeric priority.

## Role Semantics

### Topology roles
- `Router`
- `RootDomainController`
- `ReplicaDomainController`
- `MemberServer`
- `StandaloneServer`

Topology roles shape ordering and dependency semantics.

### Capability roles
- `Pki`
- `Sql`
- `Web`
- `Operations`

Capability roles request additive work and must not erase topology roles.

### Composition rule
- one VM may have multiple NICs
- one VM may hold multiple roles
- capability roles are additive by default

## Resource-Aware Scheduling

The initial scheduler supports three profiles:

- `Conservative`
- `Balanced`
- `Aggressive`

These profiles tune overlap limits without changing dependency correctness.

### Workload classes
- `HeavyHost`
  - differencing-disk creation
  - VM registration/start bursts
- `HeavyGuest`
  - AD DS promotion
  - large Windows feature installs
  - router role setup
- `MediumGuest`
  - guest network bootstrap
  - domain join
  - DNS changes
- `LightWaitValidation`
  - polling
  - readiness waits
  - post-checks

### Baseline policy
- do not default to "deploy everything at once"
- prefer conservative overlap until host measurements prove stronger defaults are safe

### AD-core baseline wave policy
- provision and boot the DC set first
- start critical DC guest work
- begin provisioning later machines only when profile/workload caps allow it
- keep domain-dependent guest work blocked until domain readiness gates pass

## Router Semantics

Router behavior is optional.

Use router planning only when:

- cross-switch communication is required, or
- outbound internet access through a lab router is required

If a task depends on cross-switch communication:

- router readiness must appear in the dependency chain
- dependent work must wait until the router is configured and ready

## V1 / V2 Coexistence

- V1 templates remain on the current engine
- V2 templates route to the new planner by schema version
- V2 must not silently fall back to V1

## Guest Execution Baseline

- Hyper-V PowerShell Direct is the baseline guest execution transport
- host-side Hyper-V execution and guest-side PowerShell Direct execution are separate seams, even when scheduled inside one graph

## Open Questions / TBDs

- Exact numeric scheduler caps for each deployment profile on representative hardware.
- Exact review-surface visualization for graph vs wave display.
- Exact runtime policy for workloads that partially overlap heavy guest and heavy host pressure.
