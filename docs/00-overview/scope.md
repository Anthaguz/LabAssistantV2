# Scope

LabAssistantV2 is a Windows desktop application designed to automate the creation, configuration, and deployment of virtual machine labs using Microsoft Hyper-V.

The system focuses on repeatable lab deployments from predefined templates that reference base virtual disks and environment configuration.

Support for other hypervisors is not included in the current scope, but the architecture should remain extensible to allow future integration.

If no valid base disk is available, lab deployment is not possible, as the deployment model depends on differencing disks derived from base images.

## In Scope (by module)

1. Template Management
  - Create, edit, delete, and version lab templates.
  - Templates define:
    - VM count and configuration (CPU, RAM, disk, network).
	- Base disk reference.
	- Network/switch configuration.
	- Software to be installed.
	- Roles to be installed.
	- Roles to be configured.
  - Import/export templates via structured files (e.g., JSON).
  - Validate template integrity before deployment.

2. Lab Deployment

  - Deploy a new lab environment from a selected template.
  - Create differencing disks from base images.
  - Provision and configure Hyper-V virtual machines.
  - Attach VMs to the appropriate virtual switch/network.
  - Provide progress reporting, logging, and failure feedback.
  - Support cancellation of long-running deployments.
  - Allow the injection of powershell commands to the VM to configure Networking and installation and configuration of roles.

3. Base Disk Management

  - Register and manage base VHD/VHDX images.
  - Validate disk existence and compatibility.
  - Prevent deployment when base disk requirements are unmet.

4. Observability & Diagnostics

  - Produce structured logs for:
    - Template operations
	- VM creation
	- Disk operations
	- Network configuration
  - Provide user-visible status and error messages.
  - Enable export of diagnostic information for troubleshooting.

5. Desktop Application Framework

  - Windows-only .NET desktop UI (MVVM architecture).
  - Separation of:
    - UI
	- Business orchestration
	- Services interacting with Hyper-V/PowerShell/filesystem
  - Configuration stored locally on the user machine.

## Out of Scope
  - Support for non-Hyper-V hypervisors (VMware, VirtualBox, etc.).
  - Cloud VM provisioning or remote infrastructure orchestration.
  - Automatic OS installation.
  - Advanced network topology simulation beyond Hyper-V virtual switches.
  - Multi-user collaboration or centralized server backend.
  - Enterprise authentication, RBAC, or identity providers.
  - High-availability or distributed deployment management.
These may be considered future extensions, but are not part of LabAssistantV2 v1.

## Assumptions
  - The application runs on a Windows machine with Hyper-V enabled.
  - The user has sufficient permissions to create and manage Hyper-V VMs.
  - Valid base virtual disks are available locally.
  - The environment is primarily single-user, local workstation usage.
  - PowerShell and required Hyper-V management components are functional.
  - There are enough resources to create the requested VMs.

## Constraints
Technical Constraints
  - Must operate offline without cloud dependency.
  - Must rely on Hyper-V APIs/PowerShell, not third-party hypervisor SDKs.
  - Must function within Windows desktop security and permission model.
  - Disk operations are limited by local storage performance and capacity.

Architectural Constraints
  - Maintain clear separation of layers (UI, Business, Services, Data).
  - External operations must be logged and error-handled.
  - Long-running operations must support progress reporting and cancellation.

Usability Constraints
  - Initial version targets technical users familiar with virtualization.
  - UI complexity should remain manageable for solo workstation use.

## Milestones (high level)
TBD

## Open Questions / TBDs
Template schema versioning strategy.

Whether labs should support post-deployment scripting.

Handling of partial deployment failures (rollback vs resume).

Long-term plan for supporting other hypervisors.

Future possibility of centralized template repository.
