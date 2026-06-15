# V2 Template Planning Contract

**Purpose:** Define the data-contract direction for V2 unified orchestration planning without forcing full runtime support into the current codebase immediately.

## Scope

- This document supplements `docs/01-requirements/template-schema.md`.
- It focuses on V2 planning objects and data relationships.
- It does not override the current V1 schema contract.

## V2 Planning Objects

### Lab networks
Each V2 template may declare one or more lab networks that provide:

- stable network id
- display name
- Hyper-V switch association
- subnet metadata
- notes or intent metadata

Lab networks provide shared validation context. They do not remove the need for explicit per-NIC guest addressing by default.

### VM NICs
Each V2 VM may declare a NIC collection.

Each NIC may include:

- nic id or key
- display name
- attached lab network id
- fallback Hyper-V switch name
- guest IP address
- prefix length
- default gateway
- DNS servers
- optional flags such as "requires router path"

### Topology roles
Topology roles shape orchestration order and dependency semantics:

- `Router`
- `FirstDomainController`
- `ReplicaDomainController`

`RootDomainController` remains accepted as a backward-compatible alias, but new V2 templates should prefer `FirstDomainController`.

Ordinary machines do not need a topology role. In V2, domain participation is modeled separately through `membershipMode`.

### Membership mode
Membership mode controls whether a VM participates in domain-join execution:

- `DomainMember`
- `Standalone`

### Capability roles
Capability roles request additive guest work:

- `Pki`
- `Sql`
- `Web`
- `Operations`

Capability roles are additive and may coexist with topology roles.

### Dependencies
V2 may declare explicit dependencies when role-derived ordering is not enough.

Examples:

- a domain-member VM waits for a root domain controller domain-readiness gate
- a cross-switch task waits for router readiness
- a capability-role task waits for a prior domain-join task

### Deployment profile
Each V2 template may select a deployment profile or leave it to deploy-time default:

- `Conservative`
- `Balanced`
- `Aggressive`

Profiles affect overlap/resource policy, not correctness.

The persisted `deploymentProfile` value remains a string in the V2 template JSON for portability, but it is a validated whitelist field that maps to typed planner policy. Blank or absent values mean "use the deploy-time default profile."

## Credential Slot References

Templates may reference credential slots for:

- local bootstrap access
- domain-join access
- DSRM access for domain-controller promotion
- role-specific guest operations

Each reference should carry:

- stable slot key
- human-readable label
- scope hint
- optional expected owner such as disk profile or template override

Templates must not embed reusable secret values.

## Current Planning And Runtime Boundary

The current V2 slice executes explicit graph nodes for:

- host-side guest-access prerequisites
- guest transport readiness
- guest NIC/IP/DNS preparation
- base remote-access guest configuration when enabled at deploy time
- router NIC configuration
- router RRAS/RemoteAccess feature installation
- router routing enablement
- router NAT configuration
- router cross-switch readiness validation
- router outbound egress validation
- AD DS feature installation
- first domain-controller promotion
- replica domain-controller promotion
- per-domain DNS stabilization
- domain joins
- joined-state and domain-login validation

Root domains, child domains, tree domains, and multiple independent root forests now execute through the V2 runtime path. Extended topology execution reuses the shared per-domain promotion, DNS stabilization, and join paths, with only first-domain creation varying by relation kind. Trusts remain known contract data only until the explicit trust-runtime slice lands.

Base remote-access guest hardening is now a deploy-time V2 review/runtime option and remains deferred only at the template-authoring/schema level.

## Bootstrap Profile References

Each VM may reference bootstrap expectations derived from its VHDX catalog entry.

Bootstrap profile intent includes:

- expected local/bootstrap user
- local credential slot reference
- guest transport assumption
- guest OS assumptions relevant to PowerShell Direct access

Bootstrap profiles describe image facts, not lab/domain intent.

## Portability Rules

- exported templates carry slot references and labels only
- imported templates may remain editable even when local slot mappings are unresolved
- deployment must block until required slot mappings are resolved locally

## Directory Topology Contract

The canonical V2 backend topology contract is a top-level `directoryTopology` object.

It may declare:

- `forests`
- `domains`
- `trusts`

The current runtime executes root, child, tree, and additional independent root-forest domain creation paths. Trusts remain reserved for a later runtime slice.

### Forests

Each forest declaration should carry:

- `forestId`
- `rootDomainId`

### Domains

Each domain declaration should carry:

- `domainId`
- `dnsName`
- `netBiosName`
- `forestId`
- `relationKind`
- `parentDomainId` when `relationKind` is `Child` or `Tree`
- `firstDomainControllerVmId`

Supported `relationKind` values are:

- `Root`
- `Child`
- `Tree`

All supported `relationKind` values are executable in the current V2 backend runtime. Root domains create or extend independent forests according to their forest declaration; child and tree domains require a valid parent-domain reference and wait for the parent domain readiness gate.

### Trusts

Trust declarations are reserved contract data in the current slice.

Each trust declaration may carry:

- `trustId`
- `sourceDomainId`
- `targetDomainId`
- `trustType`
- `direction`

Trusts validate shape and references only. They do not execute yet.

### Current Execution Boundary

- root forests execute in the current V2 slice
- child domains execute through the shared per-domain runtime path
- tree domains execute through the shared per-domain runtime path
- additional independent root forests execute as peer first-domain creation paths
- trusts remain known-but-non-executable

## Open Questions / TBDs

- Whether future V2 authoring should support deterministic auto-allocation alongside explicit per-NIC guest addressing.
- Whether future trust slices need schema fields beyond the current `trustId`, source/target domain, type, and direction shape.
