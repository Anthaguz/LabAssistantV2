# WinUI Templates Builder Canvas

**Purpose:** Define the target design and delivery plan for the Templates Builder redesign, where the directory topology is authored on a two-level visual node-graph canvas instead of the current indented list inside a linear stepper.

**Status:** Target design and phased delivery plan.
This is the agreed design contract for the redesign, not yet the current implementation.
Each phase promotes part of this document into shipped behavior.
When a phase lands, the durable rules it implements become authoritative here, and the matching parts of the linear-stepper Builder are retired.

**Source basis:**
- Approved interaction prototype reviewed with the product owner (the v3 Builder canvas mock).
- `LabAssistant.Models/Templates/V2DirectoryTopologyTemplate.cs` (the V2 topology model this canvas is a view over).
- `LabAssistant.WinUI/ViewModels/Templates/Builder/TemplatesBuilderDirectoryTopologyProjection.cs` (the existing projector that already computes the forest and domain tree with edges).
- `docs/03-architecture/winui-lane-architecture.md` (lane role split the Builder must keep).
- `docs/04-data/v2-template-planning-contract.md` (the planning contract the authored template must satisfy).

## 1) Scope And Intent

The Builder is the surface where a user authors a reusable template of how a lab is built.
Templates are saved, edited, and shared, then later deployed by the Machines capability.

Today the Builder walks the user through a six-step linear stepper and renders the directory topology as an indented text list.
That flow is hard to read, hides the shape of the environment, and does not match how people think about forests, domains, trees, and trust.

The redesign replaces the topology step with a direct-manipulation canvas that has two levels of zoom:
- Level 1 is the directory topology: forests, domains, the trust relationships between them, and a container for standalone machines.
- Level 2 is a single domain opened up: the machines inside that domain, each with its operating system, sizing, network address, and roles.

The canvas authors the same underlying draft model the stepper authors today.
It does not introduce a second source of truth.
The draft model, the topology projector, the draft validator, and the draft-to-template mapper are reused.

## 2) Non-Negotiable Invariants

These are the rules the canvas must always enforce.
They were confirmed during the prototype review and are the reason the redesign exists.

### 2.1) Forests and root domains

Every forest has exactly one root domain.
The root domain relation is locked and cannot be edited to Child or Tree.
No forest may hold a second root.

The forest name is the root domain name.
It is derived and read-only.
When the root domain is renamed, the forest name follows it.

Default forest names cycle through a fixed friendly set: `contoso.lab`, then `fabrikam.lab`, then `contoso3.lab`, `contoso4.lab`, and so on when both friendly names are taken.
Additional domains default-name off the same scheme (`contoso2`, `contoso3`, and so on) so a new lab is immediately nameable without typing.

### 2.2) Adding domains

Hovering a domain reveals a faint `+` affordance.
Clicking it always creates a Child domain, already linked to the hovered domain as its parent.
A child domain name defaults to `<label>.<parentName>`.

The forest header carries a separate `+ tree` action.
It creates a Tree domain in that forest with a name defaulting to `<label>.lab`.

A non-root domain may be Tree or Child only.
Changing a domain relation never lets a second root appear.

### 2.3) Every domain is born with a domain controller

Creating any domain (a new child, a new tree, or the root of a new forest) also creates one domain controller machine inside it.
The default domain controller is named `<token>dc<NN>`, where `<token>` is the first DNS label of the domain name and `<NN>` is a two-digit sequence over the domain controllers already in that domain.
For example the second domain controller in `contoso5.lab` is `contoso5dc02`.
The born domain controller defaults to Windows Server 2022, dynamic memory, and host address `.10` on the domain subnet, with AD DS and DNS enabled.

### 2.4) Standalone machines

Not every machine is domain-joined.
A router, a root certificate authority, or any workgroup box lives outside the domains.

Level 1 shows a gray, dashed Standalone container next to the forests.
It holds every machine whose domain is unset.
Opening it lists those machines the same way a domain does at Level 2.

A router machine is a special standalone member.
It connects every switch in the environment and always owns the `.1` host address on each subnet.
Standalone machines pick which switch they attach to, since they are not scoped to a single domain.
This switch picker is the one justified dropdown in the whole surface.

### 2.5) One switch per domain, host-octet IP editing

Each domain owns exactly one virtual switch, described by a name, a subnet, and a mask.
There is no per-machine switch dropdown inside a domain.

A machine inside a domain does not type a full IP address.
It edits only the host portion of the address, seeded from the domain subnet.
A `/24` domain exposes a single editable last octet, valid `2` through `255`.
A `/16` domain exposes the last two editable octets.
The `.1` address is reserved for the router and is rejected.
A duplicate host address inside the same domain is flagged inline with a clear message naming the machine that already uses it.

### 2.6) Trusts are drawn edge-to-edge

A forest trust is drawn between the two forests' root-domain boxes.
The trust line meets the outer edges of the boxes, computed by intersecting the connecting line with each box rectangle.
It never terminates at box centers, because a center-anchored line reads as pointing at nothing.

### 2.7) Roles and features

Roles and features are two separate, searchable, collapsible sections in the machine inspector.

A role's `configure` action expands an inline configuration panel.
It never unchecks the role.

Active Directory Domain Services and DNS are bound both ways.
Enabling AD DS on a machine installs and locks DNS automatically.
Disabling AD DS removes the managed DNS.
DNS cannot be toggled independently while it is managed by AD DS.

Roles fall into three configuration tiers:
- Fully configurable later: AD DS is promoted now, with directory configuration (DSRM policy, functional level, sites and subnets, Global Catalog, RODC) authored in a later phase.
- Install now, configure later: DHCP, AD Certificate Services, File Server, and Web Server (IIS) are installed but carry only a placeholder for their configuration.
- Install only, no configuration: features such as Failover Clustering, .NET Framework 3.5, RSAT AD DS Tools, and Windows Server Backup are enabled with no in-tool configuration.

This tiering is deliberate.
Some roles are too involved to fully configure inside the Builder right now, and the honest contract is to install them and mark their configuration as future work rather than to fake it.

## 3) Level 1: Directory Topology

The topology canvas renders the output of `TemplatesBuilderDirectoryTopologyProjector.Project()`.
The projector already computes forests, their root and descendant domains, parent-child and forest-root edges, depth, selection, missing-parent detection, and an unassigned-domains pseudo-forest.
The canvas reads that projection and positions nodes by an `X` and `Y` coordinate rather than by an indent depth.

Nodes:
- A forest is a dashed container grouping its domains.
- A domain is a card badged by relation: Root, Child, or Tree.
- The Standalone container is a gray dashed card grouping non-domain machines.

Edges:
- Parent-child domain edges connect a child to its parent.
- Forest-root edges tie a domain to its forest.
- Forest trusts connect two forests edge-to-edge as described in section 2.6.

Interactions:
- Nodes are draggable, and their positions persist.
- Selecting a domain shows its properties, including the switch subnet and mask, in the inspector.
- Double-clicking a domain, or using its "manage machines" action, zooms into Level 2 for that domain.
- Opening the Standalone container zooms into its machine list.
- There is no separate zoom button.
  Double-click and the explicit "manage machines" action are the only zoom entry points, to avoid an ambiguous control.
- Zoom transitions animate with a swipe so the level change is legible.

## 4) Level 2: Domain And Standalone Machines

Level 2 shows the machines inside one domain, or the machines in the Standalone container.

A prominent "Back to topology" control returns to Level 1.
It must stand out, since a faint back link is easy to miss for a first-time user.

Machines are shown as cards in a grid.
A single "Add computer" action creates a machine.
The operating system is chosen in the inspector afterward, which decides whether it is a server or a client and which base disk it uses.
There is no separate "add domain controller" versus "add member server" split.
A machine becomes a domain controller by enabling AD DS, which also renames it on the `<token>dc<NN>` scheme.

## 5) Machine Inspector

Selecting a machine opens a resizable inspector.
The inspector default width is comfortable and it can be dragged wider, because some role configuration needs room.

The inspector edits:
- Name.
- Operating system, which drives the base disk.
- Memory mode (dynamic or static) and memory size in megabytes.
- Virtual processor count.
- IP address, host-octet only, per section 2.5.
- Roles, in a searchable collapsible section.
- Features, in a separate searchable collapsible section.
- Advanced Hyper-V options, in a collapsible section.

The Advanced Hyper-V section exposes generation, Secure Boot, checkpoint policy, and virtual TPM.
These carry sensible developer defaults today and are hardcoded at deploy time.
The section notes that these defaults will later become user-editable globals in the Configuration surface, so the Builder and the future global defaults stay consistent.

## 6) Reused Model And The Deltas It Needs

The canvas is a view over the existing draft model, not a rewrite.
The following pieces are reused as-is:
- The Builder draft model and its snapshot.
- `TemplatesBuilderDirectoryTopologyProjector` for the forest and domain tree with edges.
- The draft validator and the draft-to-template mapper.
- The role projection catalog.

The prototype assumes model capabilities the current V2 model does not yet fully express.
These deltas are introduced in the phases that first need them, not up front:
- Node layout: an `X` and `Y` position per topology node and per standalone container, either persisted into the template or held in a side-car layout store.
  The persistence choice is an open question in section 8.
- Per-domain switch identity: switch name, subnet, and mask on the domain.
- Per-machine host address expressed as a host octet against the domain subnet.
- A standalone and router container concept, including the router's fixed `.1` per subnet and its all-switches attachment.
- The born-with-a-domain-controller behavior on domain creation.
- The roles-versus-features split with the install-now and configure-later tiers, and the AD DS to DNS binding.

Each delta is a small, additive change to the draft model, guarded by the draft validator and covered by unit tests, so no phase silently weakens the planning contract in `docs/04-data/v2-template-planning-contract.md`.

## 7) Architecture Placement

The Builder is a WinUI lane and keeps the lane role split defined in `docs/03-architecture/winui-lane-architecture.md`.
The canvas is view composition and interaction.
Workflow orchestration stays in the lane controller.
The draft model remains the single owned state.

WinUI 3 has no mature node-graph control and no SVG.
The canvas is therefore custom: node elements are data-templated onto a `Canvas` and positioned by their `X` and `Y`, and edges are hand-drawn `Shapes` or `Path` geometry recomputed as nodes move.
This is the single largest technical unknown in the redesign, which is why Phase 0 exists to prove it before any authoring behavior is built on top of it.

## 8) Phased Delivery Plan

Each phase is one shippable pull request that targets `master` and leaves the app working.
Every phase reuses the draft model, projector, validator, and mapper.

### Phase 0: Read-only node-graph canvas

Replace the indented topology list with a draggable node-graph canvas.
Render forests, domains, and edges from the existing projector.
Nodes drag and persist their positions.
Edges are drawn as geometry and recomputed on drag.
Selection only, no authoring changes yet.
This phase proves the custom-canvas approach is viable in WinUI 3.

### Phase 1: Topology authoring

Add hover-`+` to create a child, the forest `+ tree` action, one-root-per-forest enforcement, forest-name-follows-root, born-with-a-domain-controller, edge-to-edge trusts, and delete.
Introduce the small draft-model deltas these require.

### Phase 2: Standalone container and Level 2

Add the Standalone and router container, the zoom-into-domain Level 2 with machine cards, and the single "Add computer" action.
Introduce per-domain switch subnet and mask, the standalone concept, and the router.

### Phase 3: Machine inspector

Add the full inspector: operating system and disk, dynamic or static memory, the host-octet IP editor with duplicate detection, the roles-and-features split, and the Advanced Hyper-V section.

### Phase 4: Retire the stepper

Remove the linear stepper and the legacy editor path.
Wire the Library "new template" entry to the canvas.
Fix the Deploy surface "fix in editor" pointer to land on the canvas.

## 9) Open Questions

These are tracked and resolved inside the phase that first depends on them.
- Whether node `X` and `Y` positions are persisted into the template model itself or into a separate layout store keyed by template.
  Persisting into the template makes shared templates open with the same layout, at the cost of putting presentation data in the domain model.
- The exact draft-model shape for the per-domain switch, the standalone and router container, and the per-machine host octet, so each stays inside the existing validation and mapping seams.
- Whether the Advanced Hyper-V defaults move to the Configuration surface in this program or later, since Phase 3 only needs them as per-VM overrides with defaults.
