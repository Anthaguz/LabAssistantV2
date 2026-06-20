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
- optional Hyper-V switch type (`External`, `Internal`, or `Private`)
- subnet metadata
- notes or intent metadata

Lab networks provide shared validation context. They do not remove the need for explicit per-NIC guest addressing by default.

`switchName` plus `switchType` is the portable switch ensure intent for deploy-time reconciliation. A lab network that declares only `switchName` remains valid reference intent for existing/imported templates, but it is not enough information to auto-create a missing switch on a new host. Host-specific External adapter mapping is resolved during Deploy From Template review for the current deployment run and must not be persisted into the shared template.

### VM NICs
Each V2 VM may declare a NIC collection.

Each NIC may include:

- nic id or key
- display name
- attached lab network id
- fallback Hyper-V switch name as an advanced/reference-only override
- guest IP address
- prefix length
- default gateway
- DNS servers
- optional flags such as "requires router path"

Per-NIC switch overrides can resolve an effective switch when explicit NIC-level attachment is needed, but they do not create missing switches unless backed by a lab network with complete switch name and type intent.

### Backend topology roles
Backend topology roles shape orchestration order and dependency semantics:

- `Router`
- `FirstDomainController`
- `ReplicaDomainController`

`RootDomainController` remains accepted as a backward-compatible alias, but new V2 templates should prefer `FirstDomainController`.

Ordinary machines do not need a topology role. In V2, domain participation is modeled separately through `membershipMode`.

The Templates V2 Builder must hide backend `topologyRole` details during authoring and map Builder VM role assignments to these backend fields on save.

### Membership mode
Membership mode controls whether a VM participates in domain-join execution:

- `DomainMember`
- `Standalone`

Membership mode is limited to those two values. Domain Controller is a VM role, not a membership mode.

### Builder VM roles
Builder VM roles are the user-facing role assignments in the first structured authoring workflow.

The first executable Builder-authored VM role is:

- Active Directory Domain Controller

Root CA, SQL, Web, and Operations are future Builder role extension points and are not authorable in the first Builder slice. Future roles require a separate approved contract before they become authorable. Future role-specific configuration belongs under `VMs > VM name > Roles > Role name`; this slice must not add empty Root CA, SQL, Web, or Operations configuration UI.

### Dependencies
V2 may declare explicit dependencies when role-derived ordering is not enough.

Examples:

- a domain-member VM waits for a domain-controller domain-readiness gate
- a cross-switch task waits for router readiness
- a future approved role task waits for a prior domain-join task

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

Templates must not embed reusable secret values. Reusable secrets stay in the local DPAPI-backed credential store and unresolved slot mappings are resolved during Deploy From Template review before V2 runtime execution.

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

Root domains, child domains, tree domains, and multiple independent root forests now execute through the V2 runtime path. Extended topology execution reuses the shared per-domain promotion, DNS stabilization, and join paths, with only first-domain creation varying by relation kind. The approved first trust runtime contract is limited to resolved trust contexts for managed bidirectional forest trusts between two LabAssistant-managed V2 domains/forests.

Base remote-access guest hardening is now a deploy-time V2 review/runtime option and remains deferred only at the template-authoring/schema level.

## Network Switch Reconciliation Contract

V2 planning must resolve network-backed switch requirements before runtime VM creation:

- a declared `switchName` plus `switchType` can produce deploy-time switch ensure work
- an existing switch is reusable only when both name and type match
- an existing same-name switch with a different type is a blocking review/planning issue
- missing `Internal` and `Private` switches can be created automatically before dependent VM creation or NIC attachment
- missing `External` switches require current-host adapter mapping during Deploy Review before runtime can create them
- host-specific External adapter selections are deploy-run resolution state, not shared template data
- legacy `switchName`-only network intent remains valid but cannot auto-create missing switches
- runtime cleanup deletes only switches created by the current deployment operation after VM cleanup has detached or removed dependent VMs
- reused pre-existing switches are never deleted by deployment cleanup

Switch ensure, reuse, blocking, creation, and cleanup must emit structured logs with `operationId`, switch name, switch type, result, and failure details where applicable.

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
- Deploy From Template review must resolve required local slot mappings before runtime execution starts

## Builder Output Compatibility

The Templates V2 Builder is an authoring surface for producing planner-compatible V2 template JSON, not a separate planning dialect. Saved Builder output must remain consumable by:

- V2 schema validation
- Deploy From Template review and resolve
- V2 orchestration planning

The first Builder slice uses a structured step-based workflow with a left active workflow tree and one active top-level step at a time: General, Networks, Forests & Domains, Credentials, VMs, and Review. It is not an all-sections scroll. Active top-level state uses existing shell resources, including `ShellAccentBrush`, selected background, and selected border treatment; completion/error badges are deferred to a later Review/validation UX slice. General authors template name, description, and deployment profile, and may show read-only schema/version metadata without renaming persisted schema fields. Deployment profile remains the Conservative/Balanced/Aggressive field, but Builder presents it as a visible horizontal selector below description rather than a dropdown and provides a Segoe MDL2 information tooltip that explains the practical pacing/resource-pressure tradeoff. The Builder also authors lab networks with switch name and optional switch type, reusable credential slot references, forests/domains, VM membership mode, domain assignment, Active Directory Domain Controller VM role assignment, and per-VM NIC/IP/DNS/gateway intent.

Previous and Next controls live in the Builder footer and compute routes in this order: General -> Networks -> Forests & Domains -> Credentials -> VMs overview -> each VM's Basics -> Resources -> Membership -> Roles -> Networking overview -> NIC details in draft order -> Credentials -> Review. Previous is disabled on General, Next is disabled on Review, and Previous/Next do not block on validation errors before Review. When the draft has zero VMs, Next from VMs overview goes to Review, and Review shows a blocker that at least one VM is required before Save. When a selected VM has zero NICs, Next from Networking overview goes to Credentials.

Networks and Credentials use list plus selected-detail layouts. Forests & Domains uses a constrained topology canvas plus selected-detail layout: separate forest containers, highlighted root domain nodes, child-domain branches under parents, tree-domain roots as separate trees inside the same forest, selectable forest/domain nodes, and stable edge-ready node identifiers for future/read-only trust edges. The canvas is not a freeform drag/drop graph editor in this slice. The `VMs` row expands to show VM child items by VM name and includes a small borderless right-aligned `+` button. Each VM child expands to nested category rows ordered as Basics, Resources, Membership, Roles, Networking, and Credentials. Clicking `VMs` opens a compact VM overview with total VM count, standalone/domain-member counts, AD DC role count, Add VM, and a simple VM summary list. Clicking a VM child opens that VM detail on `Basics`; clicking a VM category opens that category under `VMs > VM name > category`. Top-level workflow rows are active for non-VM steps, `VMs` is active for the VM overview, the VM name is active when any category for that VM is selected, and the selected VM category is active under that VM node. Direct workflow-tree clicks remain supported. New VM drafts use neutral incrementing names from the existing draft set, such as `vm-1` / `VM 1`, `vm-2` / `VM 2`, and so on; after adding a VM, the Builder selects the new VM child and opens `Basics`. VM detail categories appear as nested left-tree rows under `VMs > VM name`: Basics, Resources, Membership, Roles, Networking, and Credentials. Resources contains RAM, CPU, and base disk/VHDX fields. Networking owns a NIC overview/list and selected NIC detail; NIC details remain inside the selected VM's Networking route and do not become global workflow children. Add NIC selects the newly created NIC detail. The previous right-side VM selector list is not part of the Builder contract.

During Builder editing, the in-memory Builder draft is the active source of visible user intent. Field edits update that draft immediately, or through a short UI-safe debounce when appropriate, and the Builder must not require per-section or per-field Save buttons. Invalid intermediate values remain visible and remain in draft state instead of being discarded. Blocking errors prevent final Save, export, and planning until resolved, but do not block Previous/Next before Review. Navigation between top-level steps, VM children, VM detail categories, and NIC details must preserve edits, and narrow VM/category/NIC navigation should not require broad full-Builder rerendering.

Builder-local validation is a draft authoring seam and does not create a separate planning dialect or persisted validation payload. Draft edits trigger automatic validation for affected scopes where practical: domain name edits validate the edited domain and dependent references; IP edits validate IP format, subnet fit, and duplicate IPs; VM identity/name edits validate identity and name rules; membership edits validate domain assignment and role compatibility for that VM. Review aggregates current Builder-local blockers and warnings, but final Save/Save As still requires the draft to pass final document mapper/build validation and produce planner-compatible V2 JSON.

Deterministic suggestions may help populate those fields, but only explicit user-confirmed draft values become persisted template intent.

The Builder must not use multiline pipe-delimited fields as V2 resource authoring controls. Matrix views may support review or comparison, but they are not the primary authoring UI.

The Builder exposes a persistent footer: Back is a left secondary action; authoring steps show Previous as a right secondary action and Next as the right primary action; Review shows Previous as a right secondary action, Save As as a right secondary action, and Save as the right primary action. Apply Suggestions and manual Validate are not footer actions in this contract. Save and Save As are Review-only actions. Save remains blocked unless the current draft builds into valid template JSON and has no current validation blockers. Review plus Save is the confirmation; there is no separate Review confirmation checkbox. Final Save persists validated template JSON.

Builder output hides backend `topologyRole` details while preserving planner compatibility. On save, ordered Active Directory Domain Controller role assignments map to the current backend fields, including each domain's derived `firstDomainControllerVmId`.

Credential semantic redesign, Review validation redesign, runtime implementation, trust authoring, WPF work, field badges, inline markers, section badges, and completion/error badges are outside this contract slice. The Forests & Domains topology canvas may remain edge-ready for future/read-only trust rendering, but trust authoring is outside this Builder slice. The existing trust runtime contract remains a planner/runtime capability for templates that already declare supported trust intent; existing trust declarations must be preserved when a V2 template is opened and saved by Builder, but they are deferred/read-only in Builder. Adding first-class Builder trust authoring requires a later approved issue.

## Directory Topology Contract

The canonical V2 backend topology contract is a top-level `directoryTopology` object.

It may declare:

- `forests`
- `domains`
- `trusts`

Directory topology is optional for VM-only standalone templates. Zero domains plus standalone VMs is valid. Zero domains plus a `DomainMember` VM is invalid in this slice, and zero domains plus an Active Directory Domain Controller role VM is invalid in this slice. When domains are present, each domain must have at least one Active Directory Domain Controller role VM or save/planning validation blocks with actionable feedback.

The current domain runtime executes root, child, tree, and additional independent root-forest domain creation paths. The first trust runtime slice is limited to bidirectional forest trusts between two LabAssistant-managed V2 domains/forests.

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

Each saved domain must have at least one VM assigned the Active Directory Domain Controller role. The first domain controller is derived from ordered DC role assignments and persisted as `firstDomainControllerVmId`. PDC emulator/FSMO selection or transfer is out of scope.

### Trusts

Trust declarations are durable template intent. Runtime execution must consume a resolved trust context built from the template declaration, resolved domain/forest records, readiness gates, credential-slot mappings, DNS prep decisions, and cleanup ownership. Runtime must not execute directly from raw template trust rows.

Each trust declaration may carry:

- `trustId`
- `sourceDomainId`
- `targetDomainId`
- `trustType`
- `direction`

The first executable trust shape is:

- `trustType`: forest trust
- `direction`: bidirectional
- source and target: LabAssistant-managed V2 domains/forests in the same resolved deployment plan

External trusts, realm trusts, one-way directions, selective authentication details, SID-filter details, and unmanaged external domains are out of scope for the first trust runtime slice and must block before runtime with actionable guidance.

The resolved trust context must include:

- trust identity
- source and target domain identity
- source and target anchor domain controllers
- existing per-domain domain-admin credential slots for both participating domains
- `DomainReady` prerequisites for both participating domains
- cross-forest DNS forwarding/reachability preparation before trust creation
- source-DC anchored trust creation work
- validation from both participating sides
- cleanup ownership for LabAssistant-created trust objects

Trust creation uses the existing per-domain domain-admin credential slots. The first slice must not add dedicated trust credential fields.

If trust objects are created and the operation later fails or is cancelled, cleanup must attempt to delete the LabAssistant-created trust objects. DNS forwarders or equivalent DNS prep remain in place for retry and diagnosis.

Structured logs with `operationId` are required for DNS prep, trust creation, both-side validation, and cleanup.

### Current Execution Boundary

- root forests execute in the current V2 slice
- child domains execute through the shared per-domain runtime path
- tree domains execute through the shared per-domain runtime path
- additional independent root forests execute as peer first-domain creation paths
- managed bidirectional forest trusts are the first approved executable trust shape and execute through resolved trust contexts after both participating domains are `DomainReady`
- unsupported trust shapes remain validation/planning blockers and are not partially executed

## Open Questions / TBDs

- Whether future V2 authoring should support deterministic auto-allocation alongside explicit per-NIC guest addressing.
- Whether future trust slices need schema fields beyond the current `trustId`, source/target domain, type, and direction shape.
