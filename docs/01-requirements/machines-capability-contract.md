# Machines Capability Contract (Draft)

**Purpose:** Define the v1 contract for the future `Machines` capability area so implementation can proceed without guessing.

**Status:** Draft (Milestone Z, docs-first contract).

**Related:**
- `docs/01-requirements/srs.md` (FR-060..FR-066)
- `docs/01-requirements/acceptance-criteria.md` (AC-006)
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/migration-preservation-matrix.md`

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
  - switch attachment
- Connection actions:
  - Open Hyper-V Console
  - Open RDP (disabled when readiness unknown/unmet)
- Delete VM with explicit scope:
  - VM registration only
  - VM + associated disks/files
- App setting support:
  - default delete behavior can be configured to always default to delete-with-disks

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
- If always-delete-disks setting is enabled, scope must still be visible to the user.
- Failures must be actionable and non-silent.
- RDP and Console actions are separate controls; one must not hide the other.
- `Open RDP` shall be disabled (grayed out) when readiness is unknown/unmet.

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

- **TBD:** Exact RDP readiness detection criteria for enabling/disabling `Open RDP`.
- **TBD:** VM origin/status labeling details (LabAssistant-created vs external vs unknown) for v1 UI presentation.
- **TBD:** Whether advanced settings handoff to Hyper-V MMC is included in v1 or deferred.

