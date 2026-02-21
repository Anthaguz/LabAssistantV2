# Product Vision

LabAssistantV2 is a Windows desktop tool designed to drastically reduce the time, effort, and friction required for support engineers to create reproducible virtual lab environments using Microsoft Hyper-V.

In many support organizations, engineers delay or avoid building proper test labs due to the time investment and complexity involved, which leads to slower case resolution, unnecessary escalations, and reduced customer satisfaction.

LabAssistantV2 addresses this by enabling engineers to deploy fully configured virtual machines or complete lab environments in minutes, using predefined templates and base virtual disks with unattended configuration.
The goal is to make lab creation fast, repeatable, and shareable, while still allowing advanced customization when required.

## One‑liner
A Hyper-V lab automation tool that lets engineers deploy complete virtual environments with minimal interaction and maximum repeatability.

## Target Users
The target users are:
Support engineers who need fast, local environments to reproduce customer issues.

Senior engineers who require customizable multi-machine scenarios without manual setup.

Lead engineers / subject matter experts who design and distribute reusable lab templates to teams.

## Problems We Solve
Slow lab creation delays troubleshooting and case resolution.

Lack of reproducible environments makes debugging inconsistent.

High setup effort discourages engineers from creating labs at all.

Limited sharing of scenarios forces repeated manual configuration.

Restricted centralized lab environments reduce accessibility and flexibility.

LabAssistantV2 enables fast, local, reproducible, and shareable lab environments to remove these barriers.

## Top Outcomes (What users can do)
Users can:

Deploy a single virtual machine from a base disk in seconds.

Deploy multi-machine lab environments with predefined configuration.

Create, save, and reuse lab templates.

Share templates across engineers and teams.

Configure:

Windows Server roles

Installed software

Network topology and virtual switches

Manage Hyper-V virtual networking directly from the application.

Recreate complex troubleshooting environments on demand.

## Success Metrics (KPIs)
Initial measurable indicators of success:

Time to deploy a single VM reduced to under 2 minutes.

Time to deploy a full lab reduced by >80% compared to manual setup.

Increase in lab usage among engineers (qualitative or telemetry-based).

Reduction in escalation time due to faster reproduction of issues.

Number of reusable templates created and shared internally.

(Exact telemetry strategy TBD.)

## Non‑Goals
LabAssistantV2 will not:

Support non-Hyper-V hypervisors (VMware, VirtualBox, cloud providers).

Provide centralized cloud orchestration or remote VM hosting.

Implement enterprise identity, RBAC, or multi-tenant infrastructure.

Replace full configuration-management or imaging systems.

These may be explored in future versions, but are outside the current vision.

## Open Questions / TBDs
- 
Should template sharing remain file-based or evolve into a central repository?

Will future versions support post-deployment automation scripts?

Is telemetry collection acceptable in restricted enterprise environments?

Long-term direction: single-user power tool vs team platform?
