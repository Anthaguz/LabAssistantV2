# Glossary

**Purpose:** Keep consistent terminology across PM/dev/QA/docs.

## How to fill this
- Add terms as they appear in requirements and the UI.
- Use short definitions and, where useful, examples.
- Prefer *one meaning per term*.

---

## Terms

| Term | Definition | Example / Notes |
|------|------------|-----------------|
| **LabAssistantV2** | Windows desktop application that automates creation and deployment of virtual lab environments using Microsoft Hyper-V. | Primary product being developed. |
| **Lab** | A reproducible virtual environment composed of one or more virtual machines configured to simulate a real scenario. | Multi-VM troubleshooting setup. |
| **Virtual Machine (VM)** | A Hyper-V virtualized computer instance created from a base disk and configured by the application. | Windows Server test VM. |
| **Single-VM Deployment** | Creation of one standalone virtual machine from a base disk without additional lab topology. | Quick reproduction environment. |
| **Multi-VM Lab** | Deployment of multiple coordinated VMs forming a complete scenario. | Domain controller + client + server. |
| **Template** | Structured configuration describing how a lab or VM should be created, including VM settings, disks, and networking. | Likely stored as JSON and shareable between engineers. |
| **Template Sharing** | Distribution of templates between engineers to recreate identical environments. | Export/import of template files. |
| **Base Disk / Base Image** | Preconfigured virtual disk (VHD/VHDX) containing an installed and prepared operating system used as the source for new VMs. | Windows Server image with updates installed. |
| **Differencing Disk** | Virtual disk derived from a base disk that stores changes independently, allowing rapid VM creation without duplicating the full image. | Child VHDX created during deployment. |
| **Unattended Configuration (unattended.xml)** | Automated Windows setup configuration embedded in the base image to ensure each deployed VM becomes a uniquely configured machine. | Generates new SID, hostname, etc. |
| **Hyper-V** | Microsoft’s native Windows hypervisor used to create and manage virtual machines and networking. | Only supported hypervisor in v1 scope. |
| **Virtual Switch** | Hyper-V networking component that connects virtual machines to each other or external networks. | Internal or external virtual switch. |
| **Network Configuration** | Definition of how VMs connect to virtual switches and communicate within a lab. | Private lab network topology. |
| **Lab Deployment** | Automated process of creating all VMs, disks, and networking defined by a template. | One-click environment creation. |
| **Local Lab Execution** | Running virtual labs directly on the user’s workstation rather than centralized infrastructure. | Offline troubleshooting scenario. |
| **Support Engineer** | Primary user who needs fast reproducible environments to diagnose customer issues. | Tier 2/3 engineer. |
| **Senior Engineer** | Advanced user requiring customizable or complex lab scenarios. | Deep troubleshooting or escalation handling. |
| **Lead Engineer / SME** | User responsible for designing reusable templates for others. | Creates standardized troubleshooting labs. |
| **Reproducible Environment** | Lab that can be recreated consistently from the same template and base disks. | Same issue reproduced across machines. |
| **Case Resolution Time** | Time required to diagnose and solve a support case, which the product aims to reduce. | Key business outcome metric. |
| **Local Configuration Storage** | Storage of templates, settings, and metadata on the user’s machine rather than a server. | File-based configuration. |
| **Observability / Diagnostics** | Logs and status information that describe deployment progress, failures, and system behavior. | Structured deployment logs. |
| **Offline Operation** | Ability to function without internet or cloud connectivity. | Required technical constraint. |
| **Hypervisor Support Scope** | Limitation of the product to Hyper-V only in the current version. | VMware not supported. |

---

## Open Questions / TBDs

- Exact **template file format and schema versioning**.
- Whether **template sharing** will remain file-based or move to a **central repository**.
- Level of **post-deployment automation** supported inside VMs.
- Future abstraction needed to support **multiple hypervisors**.
- Degree of **telemetry or usage metrics** collected locally or centrally.
