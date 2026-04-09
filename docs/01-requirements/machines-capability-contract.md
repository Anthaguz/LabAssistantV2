# Machines Capability Contract (Draft)

**Purpose:** Define the v1 contract for the future `Machines` capability area so implementation can proceed without guessing.

**Status:** Draft (Milestone Z, docs-first contract).

**Related:**
- `docs/01-requirements/srs.md` (FR-060..FR-066)
- `docs/01-requirements/acceptance-criteria.md` (AC-006)
- `docs/02-ux/Archived/capability-taxonomy.md`
- `docs/02-ux/Archived/navigation-ia-draft.md`
- `docs/02-ux/Archived/migration-preservation-matrix.md`

---

## 1. Scope and Intent

`Machines` is a first-class capability for host VM administration.

It is not a replacement for `Deploy`.

- `Deploy` = provisioning workflows (readiness, orchestration, cleanup, outcomes)
- `Machines` = VM administration/inventory/actions on existing host VMs

v1 scope follows the agreed **scope B** direction:
- host VM inventory (all host Hyper-V VMs)
- basic actions (start/stop/delete)
- basic edits (CPU/memory/switch)
- separate connection actions (Hyper-V Console, RDP)

---

## 2. Included v1 Operations

- List all host Hyper-V VMs
- Select VM and view key state/details
- Start VM
- Stop VM
- Edit VM:
  - CPU
  - memory
  - switch attachment (per adapter when multiple NICs are present)
- Connection actions:
  - Open Hyper-V Console
  - Open RDP (disabled when readiness unknown/unmet)
- Delete VM with explicit scope:
  - VM registration only
  - VM + associated disks/files
- App setting support:
  - default delete behavior can be configured with policy modes:
    - Ask every time (default)
    - Always delete disks
    - Always delete disks for LabAssistant-provisioned VMs
    - Always delete disks for differencing disks only

---

## 3. Explicit v1 Non-Goals

- Full Hyper-V MMC parity
- Advanced VM configuration coverage beyond basic edits above
- Full network guest OS configuration implementation from Machines
- Broad automation/role orchestration from Machines

---

## 4. Safety and UX Constraints

- Destructive actions require explicit confirmation.
- Delete flow must keep scope visible and understandable at confirmation time.
- Policy defaults must be visible at confirmation time before destructive action.
- Disk safety classification guardrails must prevent automatic delete-with-storage defaults when attached disks are:
  - known base/full disks
  - potential base/uncertain disks
- Delete-with-storage should remove safe/owned VM folder artifacts when possible; failures must be explicit and actionable.
- Failures must be actionable and non-silent.
- RDP and Console actions are separate controls; one must not hide the other.
- Console and RDP actions should remain grouped together as the dedicated remote-access action cluster rather than being scattered across unrelated action areas.
- `Open RDP` shall be disabled (grayed out) when readiness is unknown/unmet.
- Basic edit workflow is draft-based:
  - edits are local until user clicks `Apply`
  - unsaved state indicator appears when draft differs from loaded values
  - no dedicated `Reset` button in v1
  - navigating away (capability/subview/VM selection) discards unapplied draft state

### 4.1 RDP readiness v1 policy (resolved)
`Open RDP` enablement in v1 is based on fast host-observable checks only:
- VM state is running
- At least one VM IPv4 address is discoverable from Hyper-V host-side data
- Host TCP probe to `<vm-ip>:3389` succeeds within short timeout (4000ms target)

Readiness evaluation requirements:
- asynchronous and non-blocking
- background refresh while `Machines` view is active
- manual recheck available
- disabled state exposes concise reason text/guidance
- transient probe timeout/cancellation/unreachable outcomes are non-fatal and resolve to readiness state updates (`NotReady`/`Unknown`) without crashing app flow

---

## 5. Logging / Diagnostics Constraints

All user-initiated Machines operations shall emit structured logs with `operationId`, including:
- action type
- vm identity
- result
- error details on failure
- delete scope for delete actions

Destructive action logs should make action intent explicit.

---

## 6. Open Questions / TBDs

- **TBD:** VM origin/status labeling details (LabAssistant-created vs external vs unknown) for v1 UI presentation.
- **TBD:** Whether advanced settings handoff to Hyper-V MMC is included in v1 or deferred.

## 7. Explicit non-goals for #261
- No guest OS mutation/automation for RDP readiness:
  - no auto-enable Remote Desktop
  - no auto-disable firewall
  - no auto-toggle NLA
  - no guest IP configuration changes
- No Deploy/guest-step feature expansion in Machines.

