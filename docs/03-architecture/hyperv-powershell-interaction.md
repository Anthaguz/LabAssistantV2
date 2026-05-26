# Hyper-V PowerShell Interaction

**Purpose:** Define the current PowerShell interaction model for Hyper-V work so Deploy, Machines, and future backend work do not silently drift toward conflicting execution patterns.

## 1. Locked Direction

- PowerShell remains the primary Hyper-V backend for current scope.
- Deploy is the reference architecture for workflow-oriented Hyper-V execution.
- Workflow state is in-memory only for the current scope.
- Crash/restart session resurrection is not required.
- Read-heavy Hyper-V queries are a separate execution concern from Deploy workflow orchestration.

## 2. Execution Patterns

### Workflow session pattern
- Used for Deploy runtime workflow commands.
- One persistent PowerShell session is created per VM workflow.
- The same workflow-owned session is reused across the ordered create/configure/start path and the cleanup path for that VM.
- Failure isolation is per VM workflow; one VM workflow must not depend on a shared deploy shell used by other VMs.
- Medium batch scale should be managed by coordinator policy and measurement, not by collapsing all deploy work into a shared shell.

### PowerShell Direct guest-execution pattern (V2 direction)
- Used for V2 guest-side orchestration inside Hyper-V guest VMs.
- PowerShell Direct is the baseline guest execution transport for current scope.
- Guest execution is a separate concern from host-side Hyper-V management:
  - host-side Hyper-V commands provision and wire the VM
  - guest-side PowerShell Direct commands configure the OS and domain/service behavior
- V2 planning may interleave host provisioning and guest execution in one unified graph, but the execution seams must remain explicit so host-management failure analysis and guest-configuration failure analysis do not collapse into one generic shell path.
- Future multi-hypervisor expansion must not be assumed by the initial V2 guest-execution seam.

### Query execution pattern
- Used for read-heavy Hyper-V paths such as Machines inventory, edit snapshot loading, switch listing, attached-VM listing, IP lookup, VHD metadata lookup, and deploy-side VHD probe reads.
- Query execution uses a reusable query-session seam instead of creating a brand-new PowerShell session for every read call.
- The query-session seam is intentionally separate from the Deploy workflow-session seam.
- Query-session reuse is an implementation detail of Services/provider infrastructure, not a Business/UI concern.

### One-shot administrative action pattern
- Used for isolated administrative commands that should remain action-scoped rather than workflow-scoped.
- Examples: start/stop/restart VM, apply VM edits, create/rename/delete switch, remove VM registration.
- Each action may use its own short-lived session so failures stay scoped to the action that triggered them.
- This pattern is distinct from both the long-lived Deploy workflow session and the reusable read-query session.

## 3. Current Call-Site Classification

| Call site | Pattern | Current direction |
| --- | --- | --- |
| `MultiVmDeploymentCoordinator` + `HyperVService` + deploy cleanup orchestration | Workflow session | Canonical. One persistent session per VM workflow. |
| `PowerShellHyperVVhdxProbe` | Query execution | Converged. Uses reusable query execution seam. |
| `HyperVMachineAdminService.ListHostVmsAsync` | Query execution | Converged. |
| `HyperVMachineAdminService.GetVmEditSnapshotAsync` | Query execution | Converged. |
| `HyperVMachineAdminService.GetVirtualSwitchNamesAsync` | Query execution | Converged. |
| `HyperVMachineAdminService.ListVirtualSwitchesAsync` | Query execution | Converged. |
| `HyperVMachineAdminService.GetAttachedVmNamesForSwitchAsync` | Query execution | Converged. |
| `HyperVMachineAdminService.GetVmIpAddressesAsync` | Query execution | Converged. |
| `HyperVMachineAdminService` VHD/storage read helpers used by delete preview | Query execution | Converged. |
| `HyperVMachineAdminService.Start/Stop/Restart/Apply/Delete/CreateSwitch/RenameSwitch/DeleteSwitch` | One-shot administrative action | Converged. |
| `VirtualSwitchProvider` using `HyperVService.GetVirtualSwitchNamesAsync` | Read query | Legacy call site; acceptable for now, but not the target pattern for broad read-heavy paths. |
| `PowerShellExecutor` | Process-per-call utility | Not the long-term foundation for Deploy or broad Hyper-V query architecture. |

## 4. Measurement Expectations

- Timing diagnostics must be available for:
  - workflow session creation cost
  - query session creation cost
  - one-shot administrative session creation cost
  - command/query execution cost
  - key read flows such as Machines inventory load and edit snapshot load
- These measurements exist to decide whether read-heavy PowerShell paths are still sufficient after session/query-shape cleanup.
- Backend replacement discussion for read paths should start only after these measurements are available.

## 5. Boundary Rules

- Business code depends on Hyper-V capability interfaces, not raw PowerShell process/session mechanics.
- PowerShell session ownership and lifecycle stay in Services/provider infrastructure.
- Future WMI/CIM exploration, if justified, must sit behind the same Hyper-V capability seams rather than forcing UI/business redesign.
- Do not redesign toward a single generic PowerShell executor for every Hyper-V use case.

## Open Questions / TBDs

- Whether remaining non-Machines read call sites such as `VirtualSwitchProvider` should converge onto the explicit query seam now or be removed/replaced in a later narrow slice.
