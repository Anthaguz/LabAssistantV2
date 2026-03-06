# WinUI Assets Base Disks Capability Contract (Milestone AJ)

**Purpose:** Define the behavioral contract for `Assets > Base Disks` in WinUI so AJ2+ can implement the capability without inventing route, operation, or safety behavior.

**Status:** Approved contract for Milestone AJ planning/implementation.

**Related:**
- `docs/01-requirements/srs.md` (FR-050, FR-051, FR-052, FR-053, FR-054, FR-100, FR-101, FR-102, FR-103)
- `docs/01-requirements/acceptance-criteria.md` (AC-004, AC-019)
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/ui-migration-execution-plan.md`
- `docs/03-architecture/gui-action-map.assets.md`

---

## 1) Navigation and Route Contract

Parent capability:
- `Assets`

Canonical AJ child route:
- `assets.base_disks`

Future child routes (deferred unless approved by later contract):
- `assets.switches`
- `assets.isos`

Local Assets navigation pattern:
- `Assets` may use local tabs or segmented navigation inside the capability workspace.
- Local tabs must map to canonical child routes rather than transient visual-only state.

Parent behavior constraint:
- AJ1 does not redefine shell-wide top-level `Assets` click behavior.
- AJ1 only defines `assets.base_disks` as the canonical Base Disks child route and default Base Disks subview contract when `Assets` resolves to a child route.

Global-nav compatibility requirements:
- Must remain compatible with the AC global NavigationView contract (`LeftCompact`, canonical `capability.subview` routing, deterministic startup at `machines.overview`, `Settings` footer placement).
- AJ1 does not change any other capability route behavior.

---

## 2) Base Disks Surface Contract

`assets.base_disks` is a capability-local management surface for imported base disks used by Deploy and Templates workflows.

Required surface regions:
- base disk list
- refresh/import/register actions
- selected-disk details context
- in-context metadata edit fields
- validation/readiness visibility
- remove action with safety messaging

Required state visibility:
- explicit loading state
- explicit empty state with import/register guidance
- explicit failure state with actionable recovery guidance
- explicit selected-item context when an item is present

Interaction baseline:
- metadata edit is in-context within selected-disk details, not a separate editor route
- selection state must remain clear and stable across refresh/update actions
- the capability must preserve embedded asset shortcuts in Deploy/Templates; AJ does not remove those shortcuts

---

## 3) Operation Contract

### 3.1 List and refresh

- The surface must load the current registered base disk catalog using existing persistence/orchestration paths.
- Refresh must re-query current catalog state and update visible list/details context coherently.
- Refresh failures must be visible and actionable; no silent stale-state behavior.

### 3.2 Import/register

- User may register/import an existing VHD/VHDX path into the catalog.
- Register uses current domain validation/persistence behavior; AJ does not redesign catalog semantics.
- Blocking validation must stop invalid or inaccessible disks from being registered.
- Successful register makes the disk available in Base Disks list and downstream selection surfaces.

### 3.3 Metadata edit

- Existing display metadata behavior from FR-051 remains in scope for WinUI.
- Metadata editing happens in selected-disk details context.
- AJ does not require a separate edit page or edit route.
- Save/update must preserve current transactional expectations of existing catalog behavior.

### 3.4 Validate/readiness visibility

- Base Disks surface must expose validation/readiness signals relevant to catalog usability.
- AJ1 contracts visibility and taxonomy; it does not redesign underlying validation engines.
- Validation outcomes must distinguish blocking from warning states.

### 3.5 Remove

- Remove in AJ scope means removing the disk from LabAssistant registry/catalog surfaces.
- Underlying file deletion is explicitly out of scope for AJ1 and must not be implied by the WinUI contract.
- Remove requires explicit confirmation.

---

## 4) Blocking vs Warning Taxonomy

Blocking conditions (must block completion of the current action):
- invalid file path or unsupported file type during registration
- inaccessible, locked, or unreadable disk when required validation cannot complete
- required metadata extraction or persistence failure
- catalog/remove actions that cannot complete safely
- safety check indicates removal is not allowed under current approved rules

Warning conditions (must remain visible but do not automatically block all browsing/listing behavior):
- non-critical validation/readiness concerns that still leave the disk usable or reviewable
- disk appears referenced and removal requires heightened user attention under the approved guardrail flow
- refresh/list contexts that complete with degraded or stale-status warnings but still return usable catalog state

Messaging rules:
- blocking and warning states must use explicit labeling
- actionable next step guidance is required for non-pass results
- empty/loading/error states are not folded into generic "unknown" status

---

## 5) Removal Safety Contract

AJ1 must make removal guardrails explicit for support and future implementation work.

Required rules:
- confirmation is mandatory before remove proceeds
- confirmation copy must state that AJ1 removal affects registry/catalog presence, not underlying file deletion
- the UI must surface whether the disk appears referenced or in use
- in-use/reference outcomes must be classified as block or warning according to approved implementation policy
- failure to remove must not leave partial registry state or misleading success state

Safety boundary:
- AJ1 defines the UX contract and guardrail expectations
- AJ1 does not invent new file-deletion semantics or broader disk-lifecycle semantics

Cleanup expectation:
- if a remove/update/register workflow fails after partial in-memory or transient state changes, the visible state must reconcile back to persisted truth
- registry persistence must not be left half-updated

---

## 6) Logging and Diagnostics Contract

All user-triggered Base Disks actions must emit structured logs with `operationId`.

Required action coverage:
- list/load
- refresh
- import/register
- metadata edit/update
- validation evaluation
- remove

Required context fields:
- `operationId`
- action name
- `baseDiskId` when available
- file path when relevant
- readiness/result classification when relevant
- result
- error details for failures

AJ1 logging rule:
- WinUI capability work must reuse existing business/data logging semantics where they already exist and must not create disconnected UI-only pseudo-logging contracts.

---

## 7) AJ Scope Boundary

In scope for AJ1/AJ2/AJ3 planning:
- `assets.base_disks` route contract
- list/refresh/import/register/metadata edit/validate/remove capability contract
- explicit empty/loading/error UX expectations
- blocking vs warning taxonomy
- remove safety guardrails
- logging/diagnostics expectations

Out of scope:
- shell-wide redefinition of what clicking top-level `Assets` does
- WPF changes
- Deploy semantic redesign
- schema redesign unrelated to Base Disks
- file deletion workflow for base disks
- full `assets.switches` or `assets.isos` implementation contracts unless separately approved

---

## 8) Traceability

- FR-100 -> AC-019 scenario 1 (Assets/Base Disks route and local navigation contract)
- FR-101 -> AC-019 scenarios 2 and 3 (surface structure, list/refresh/import/edit behavior)
- FR-102 -> AC-019 scenario 4 (blocking vs warning taxonomy and actionable validation messaging)
- FR-103 -> AC-019 scenarios 5 and 6 (remove safety guardrails, registry-only scope, logging/operationId expectations)
- FR-050 -> AC-019 scenario 3 and AC-004 scenario 1 (register/import behavior preserved in WinUI)
- FR-051 -> AC-019 scenario 3 and AC-004 scenario 1 (metadata edit remains in scope through in-context details editing)
- FR-052 -> AC-019 scenario 5 and AC-004 scenario 2 (remove behavior preserved with explicit WinUI safety contract)
- FR-053 -> AC-019 scenario 4 and AC-004 scenario 4 (blocking missing/invalid base-disk conditions remain explicit)
- FR-054 -> AC-019 expected side effects and AC-004 scenarios 3 and 4 (existing mapping behavior preserved without AJ redesign)

---

## 9) Deferred Items / Non-Goals

- Separate edit page or `assets.base_disks.details` route
- `assets.switches` and `assets.isos` implementation contracts
- asset-wide shell redesign
- redesign of missing-disk substitution logic
- underlying file deletion UX

---

## Open Questions / TBDs

- Exact in-use/reference detection source for removal guardrails (`TBD`: template references only, active runtime consumers, or both)
- Whether local Assets navigation is implemented as tabs, segmented buttons, or equivalent WinUI control while preserving route binding
- Future `assets.switches` and `assets.isos` route sequencing once those contracts are approved
