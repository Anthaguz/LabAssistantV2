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
- `RootDomainController`
- `ReplicaDomainController`
- `MemberServer`
- `StandaloneServer`

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

- a member server waits for a root domain controller domain-readiness gate
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
- role-specific guest operations

Each reference should carry:

- stable slot key
- human-readable label
- scope hint
- optional expected owner such as disk profile or template override

Templates must not embed reusable secret values.

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

The shape is designed so root forests are executable now, while child domains, tree domains, and trusts remain reserved for later runtime slices.

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

Only `Root` is executable in the current slice.

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
- child domains remain known-but-non-executable
- tree domains remain known-but-non-executable
- trusts remain known-but-non-executable

## Open Questions / TBDs

- Exact field names for child-domain and extra-forest declarations.
- Whether future V2 authoring should support deterministic auto-allocation alongside explicit per-NIC guest addressing.
