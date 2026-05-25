# Lab Config Script Behavior Inventory

**Purpose:** Decompose `scripts/LabAssistantConfig script.ps1` into reusable behavior clusters, readiness gates, and scheduling waves that can inform the V2 orchestration engine.

## Source

- `scripts/LabAssistantConfig script.ps1`

## Behavior Clusters

### 1. Credentials and guest access assumptions
- local administrator credential creation
- domain administrator credential creation
- convenience admin credential creation
- plain-text to `PSCredential` conversion
- PowerShell Direct readiness probes

### 2. Retry, wait, and restart handling
- generic retry wrapper
- PowerShell Direct wait loop
- expected restart disconnect detection
- explicit restart warnings
- timed waits after rename/restart/domain operations

### 3. Hyper-V topology and NIC mapping
- convert Hyper-V MAC addresses to guest-visible format
- map VM NICs to switches by MAC
- validate lab topology against expected switch layout
- router and guest NIC detection by switch identity

### 4. Guest network bootstrap
- initialize guest NIC IP/gateway/DNS by switch mapping
- rename guest computer when needed
- restart after network/rename changes
- assert resulting guest network state

### 5. Router network and connectivity services
- initialize router NIC addressing across multiple switches
- enable base remote access behavior
- install router-related Windows features
- enable routing
- configure NAT

### 6. Domain-readiness helpers
- install Windows features
- test whether a VM is a domain controller for a domain
- wait for domain service readiness
- wait for domain DNS readiness

### 7. Domain topology creation
- create new forest
- promote replica domain controller
- create child domain
- create convenience domain admin user
- join member servers to domain
- update guest DNS after promotion/join

### 8. Capability-role setup
- install PKI-related Windows features on selected machines
- intentionally stop short of full AD CS configuration in current script

## Implicit Scheduling Waves In The Script

### Wave 0: Sequential preflight
- topology validation
- reachability validation
- credential map setup

### Wave 1: Parallel network bootstrap
- initialize guest networking for all VMs
- initialize router networking

### Wave 2: Sequential router services and base remote access
- router role install
- routing enablement
- NAT setup
- base remote access across the lab

### Wave 3: Parallel AD DS feature installation
- install `AD-Domain-Services` on DC candidates

### Wave 4: Parallel independent root forests
- create Contoso forest
- create Fabrikam forest

### Wave 5: Parallel post-forest convenience admin creation
- create convenience admin in Contoso
- create convenience admin in Fabrikam

### Wave 6: Replica promotion and DNS stabilization
- promote Contoso replica DCs
- then update DNS on Contoso DCs
- then stabilize Fabrikam DNS

### Wave 7: Child domain path
- repoint child DC DNS to Contoso
- create child domain
- repoint child DC DNS again
- create child-domain convenience admin

### Wave 8: Parallel member joins
- join Contoso member servers once domain/DNS are ready

### Wave 9: Parallel PKI feature installation
- install PKI-related Windows features on selected nodes after joins

## Design Signals For V2

- The script already behaves like a stage graph with controlled parallel windows.
- Domain correctness depends on explicit readiness gates, not just task order.
- Router behavior is optional but becomes a real dependency when cross-switch traffic or outbound access is required.
- Multi-NIC guest mapping is a first-class concern, especially for router scenarios.
- Credentials are not truly global; local bootstrap and domain phases use different assumptions.

## Open Questions / TBDs

- Exact decomposition level for converting these clusters into reusable V2 plan nodes.
- Which script helper behaviors become generic services versus orchestration-specific node logic.
