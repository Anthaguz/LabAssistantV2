# WinUI Assets Switches Capability Contract (AK)

**Purpose:** Define the WinUI capability contract for `Assets > Switches` so AK2/AK3 can implement route, surface, CRUD behavior, validation, and deletion guardrails without guessing.

**Scope:** WinUI `Assets > Switches` capability contract only. This document preserves existing switch-management semantics and defines the WinUI route/surface behavior around them.

**Out of scope:**
- Shell-wide `Assets` click behavior redesign
- WPF changes
- Deploy/template semantic redesign
- Advanced topology-management redesign
- New switch-domain features beyond current CRUD/validation semantics

---

## 1. Canonical Route and Assets Positioning

- Parent capability remains `Assets`
- Canonical AK child route is:
  - `assets.switches`
- `assets.switches` is the next explicit child route under `Assets` after `assets.base_disks`
- Local Assets navigation may use tabs/segmented controls, but they must bind to canonical child routes
- AK1 does not redefine shell-wide top-level `Assets` click behavior

---

## 2. Source of Truth and Existing Requirement Alignment

AK1 preserves and references the existing switch-management requirements:
- `FR-030` existing switch selection
- `FR-031` switch availability validation
- `FR-033` create switch
- `FR-034` modify switch
- `FR-035` delete switch

AK1 does not invent a new switch-management domain. It defines the WinUI capability contract around the existing CRUD/validation behavior.

---

## 3. Surface Shape Contract

The WinUI `assets.switches` surface shall provide:
- list region
- selected-switch details region
- in-context create/edit region
- actions row
- status/feedback region
- explicit loading state
- explicit empty state
- explicit error state

The preferred workflow shape is:
- select an existing switch to inspect/edit in-context
- create a new switch using the same in-context details/edit surface
- avoid a separate route/page for create/edit unless later explicitly approved

---

## 4. Supported Operations

### 4.1 Load / Refresh
- Load the current Hyper-V switch inventory into the Switches surface
- Refresh re-evaluates host state and reconciles list/details state
- Empty state must be explicit when no switches exist
- Load/refresh failures must be explicit and actionable

### 4.2 Create
- Create uses existing switch CRUD semantics
- Validation runs before unsafe or unsupported create operations complete
- Success updates list and details selection context coherently
- Failure remains visible and actionable

### 4.3 Update
- Update uses existing switch CRUD semantics
- Edit occurs in-context on the selected-switch surface
- Validation blocks invalid or unsupported edits
- Success reconciles list/details state coherently

### 4.4 Delete
- Delete uses existing switch delete semantics with AK guardrails
- Confirmation is always required
- Delete is blocked if any Hyper-V VM is attached to the switch, regardless of VM power state
- Successful delete removes the switch from list/details context
- Failure feedback must be concrete and actionable

---

## 5. Validation and Readiness Taxonomy

Validation/readiness messaging must distinguish:

### Blocking
- duplicate-name conflicts that prevent safe create/update
- invalid or unsupported switch configuration
- unavailable host state required for the requested action
- delete attempts when any Hyper-V VM is attached to the switch

### Warning
- non-blocking host or inventory conditions that should remain visible but do not prevent the requested action
- degraded refresh/load conditions where usable data still exists

### Messaging rules
- blocking vs warning must be labeled explicitly
- messages must tell the user what to fix vs what they may review later
- load/empty/error states must remain visible instead of being hidden by generic status text

---

## 6. Delete Guardrails

AK1 explicitly defines the delete safety rule:
- a virtual switch may be deleted only when no Hyper-V VM is attached to it
- VM power state does not matter; attached running and attached powered-off VMs both block deletion
- delete still requires explicit confirmation even when allowed

This rule is intentionally conservative to avoid breaking existing VM connectivity by deleting an attached switch.

---

## 7. Logging and Diagnostics Expectations

All user-triggered Switches actions shall emit structured logs with `operationId`.

Expected action families:
- load
- refresh
- create
- update
- validation
- delete

Expected logged context includes:
- `operationId`
- action name
- `switchName`
- `switchType`
- result
- error details when present

---

## 8. Expected UI and Side Effects

### Expected UI
- `assets.switches` route-backed Switches surface under `Assets`
- list of current switches
- selected-switch details with in-context create/edit controls
- action row for load/refresh/create/update/delete
- explicit loading/empty/error states
- validation/readiness visibility

### Expected side effects
- Hyper-V switch inventory loads through existing switch-management behavior
- create/update/delete preserve existing switch CRUD semantics
- delete remains blocked when any Hyper-V VM is attached
- deploy/template switch selection semantics remain unchanged

---

## 9. Explicit Non-Goals / Deferred Items

AK1 does not define:
- deploy-time auto-create switch behavior
- template-owned switch inventory semantics
- same-name/different-properties reconciliation beyond existing CRUD semantics
- inventory-cache redesign for Hyper-V resources
- PowerShell execution-model redesign
- advanced network topology management
- `assets.isos` contract

---

## 10. Traceability

### FR -> AC
- `FR-104` -> `AC-020` route and Assets child-route contract
- `FR-105` -> `AC-020` surface shape and CRUD workflow contract
- `FR-106` -> `AC-020` validation/readiness taxonomy
- `FR-107` -> `AC-020` delete guardrails and logging expectations

### Existing requirements preserved
- `FR-030`
- `FR-031`
- `FR-033`
- `FR-034`
- `FR-035`

### UX contract source
- This document is the AK contract source of truth for `Assets > Switches`

---

## 11. Definition of Done for AK1

- WinUI `assets.switches` route and surface behavior are explicit
- Existing switch CRUD semantics are preserved, not narrowed or expanded accidentally
- Validation, delete guardrails, and logging expectations are explicit enough for AK2/AK3
- Docs are internally consistent and traceable across FR -> AC -> UX contract

---

## Open Questions / TBDs

- `TBD:` whether future milestones should introduce a cached Hyper-V inventory/projection for performance, while preserving Hyper-V as source of truth
- `TBD:` whether future template/deploy work should support template-declared switch inventory and deploy-time switch creation/reconciliation
- `TBD:` future `assets.isos` route contract and sequencing
