# WinUI Templates Capability Contract (Milestone AD)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the behavioral and routing contract for `Templates` in WinUI so AD2 (surface scaffolding) and AD3 (operational wiring) can execute without ambiguity.

**Status:** Historical milestone doc. Not authoritative for new work.

**Related:**
- `docs/01-requirements/srs.md` (FR-077, FR-078, FR-079, FR-080, FR-081, FR-082, FR-083, FR-084, FR-085, FR-086)
- `docs/01-requirements/acceptance-criteria.md` (AC-011, AC-012, AC-013)
- `docs/02-ux/Archived/winui-global-navigationview-contract-ac.md` (AC shell baseline)
- `docs/02-ux/Archived/navigation-ia-draft.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Navigation and Route Contract

Parent capability:
- `Templates`

Canonical child routes (`capability.subview`):
- `templates.library` (default child)
- `templates.editor`

Deferred route:
- `templates.details` is deferred unless explicitly approved in a later milestone contract.

Parent selection behavior:
- Selecting parent `Templates` routes to `templates.library`.
- Child selection routes directly to that child route.

Global-nav compatibility requirements:
- Must remain compatible with AC global NavigationView contract (`LeftCompact`, parent/child model, deterministic startup at `machines.overview`, `Settings` footer placement).
- Templates convergence does not alter global startup route behavior.

---

## 2) Unified Templates Workflow Contract

Templates is a single coherent capability workflow, not separate disconnected pages.

Required workflow expectations:
- library list/search/select lives inside Templates capability context
- opening selected template to editor happens inside Templates capability context
- create/edit/save path remains inside Templates capability context
- import/export entry points are discoverable inside Templates capability context

Primary workflow requirement:
- filesystem-first "hunt file to edit" must not be the primary workflow for template editing.

Behavior preservation requirement:
- existing template schema compatibility and validation semantics remain unchanged by this contract.

---

## 3) UX and Interaction Baseline

- Follow WinUI 3 Gallery-consistent interaction patterns where applicable.
- Keep behavior predictable and discoverable for non-developer users.
- Top app bar and global navigation behavior from AC remain unchanged in this contract.
- Breadcrumb strategy remains deferred unless explicitly introduced by a future requirement change.

---

## 4) AD2 / AD3 Scope Boundaries

## AD2 (Templates scaffolding only)
In scope:
- Templates route surfaces and navigation transitions for:
  - `templates.library`
  - `templates.editor`
- capability-local placeholders/layout scaffolding needed to validate route continuity.

Out of scope:
- operational template behavior wiring
- template import/export/save backend orchestration changes
- Deploy/Assets behavior changes

## AD3 (Templates operational wiring)
In scope:
- wire existing template behaviors into Templates capability surfaces:
  - library selection continuity to editor
  - create/edit/save flows
  - import/export entry points
- preserve existing domain semantics and validation behavior.

Out of scope:
- new template-domain feature invention
- Deploy/Assets capability redesign or behavior changes
- cross-capability IA redesign unrelated to Templates convergence.

---

## 5) Traceability

- FR-077 -> AC-011 scenarios 1 and 4 (route contract and global-nav compatibility)
- FR-078 -> AC-011 scenarios 2 and 3 (unified library/editor continuity and discoverability)
- FR-079 -> AC-011 scenario 3 (import/export discoverability and non-filesystem-first primary workflow)
- FR-080 -> AC-012 scenario 1 (editor VM-entry list visibility and selection context)
- FR-081 -> AC-012 scenarios 2, 3, and 4 (add/remove/edit VM-entry operations using existing schema fields)
- FR-082 -> AC-012 scenario 5 (save/reload round-trip behavior for VM-entry edits)
- FR-083 -> AC-012 scenario 6 (library/editor continuity after VM-entry operations)
- FR-084 -> AC-013 scenarios 1 and 2 (host-backed switch selector, multi-row rules, persistence compatibility)
- FR-085 -> AC-013 scenarios 3 and 4 (catalog-first VHDX selection and path-based backward compatibility)
- FR-086 -> AC-013 scenarios 5 and 6 (deterministic normalization precedence and save blocking on ambiguity)

---

## 6) Template VM Editing Parity Contract (AD5 -> AD6)

This section defines the VM-entry editing contract for AD6 implementation.

### 6.1 Required editor behaviors

- `templates.editor` must display template VM entries (`vmTemplates`) as an editor-local list.
- User must be able to:
  - select a VM entry
  - add a VM entry
  - remove a VM entry (with confirmation)
  - edit VM-entry fields that already exist in schema/contracts
- VM-entry operations are draft-first until save.

### 6.2 Field scope (no schema invention)

AD6 may edit only fields already defined by schema/model contracts for `vmTemplates[]` entries (for example: name, memory/cpu, base disk references, switch, and existing optional sections already represented in the model).

AD6 must not:
- add new schema keys
- reinterpret existing schema semantics
- introduce new domain workflows outside existing template behavior

### 6.3 Save/reload and continuity expectations

- Save must persist valid VM-entry edits through existing template persistence path.
- Reload/reopen must reflect saved VM-entry changes accurately.
- Validation failures must block invalid persistence and provide actionable feedback.
- Library/editor continuity must remain stable after VM-entry edits (including route/context coherence and visible metadata consistency where shown).

### 6.4 AD6 in/out boundary

In scope for AD6:
- VM-entry list and editor interactions in `templates.editor`
- add/remove/edit flows for existing schema fields
- confirmation flow for remove
- save/reload parity and continuity behavior

Out of scope for AD6:
- schema additions or migrations
- new template-domain feature invention
- Deploy/Assets/global-nav redesign
- cross-capability behavior changes unrelated to Templates editing parity

---

## 7) Templates Selector and Normalization Contract (AE1 -> AE2/AE3/AE4)

This section defines the docs-first contract for Templates selector/data-binding hardening work.

### 7.1 Switch selector contract (AE2)

- Replace free-text switch field with selector rows bound to host switch discovery.
- VM switch assignment is optional; no row is required for a VM entry.
- If one or more rows exist, each row must resolve to a valid switch selection.
- Users can add row(s) via `+` and remove row(s) individually via row-level remove action.
- Duplicate switch selections across rows are invalid and must show actionable validation.
- Persistence contract:
  - `switchNames` is canonical when present.
  - `switchName` is dual-written as legacy fallback from the first `switchNames` value.
  - Readers prefer `switchNames` then fallback to `switchName`.

### 7.2 VHDX catalog-first selector contract (AE3)

- Replace path-first primary interaction with catalog-first selection.
- Existing path-only templates remain loadable/editable.
- If catalog reference is missing/unavailable, UI must show explicit fallback status and guidance.
- Persistence remains within existing VHDX reference semantics (`vhdxId`, `vhdxSignature`, `vhdPath`) without adding new disk identity fields.

### 7.3 VHDX normalization and display contract (AE4)

- Deterministic precedence for effective identity:
  1. `vhdxId`
  2. `vhdxSignature`
  3. `vhdPath`
- Editor must show effective source-of-truth and ambiguity state.
- Conflicts/ambiguities require user resolution by selecting a catalog entry before save proceeds.
- Validation and warnings must be actionable and non-silent.

### 7.4 AE scope boundary

In scope:
- Templates selector UX/data-binding hardening using existing/frozen disk identity semantics and controlled switch schema compatibility extension.

Out of scope:
- Deploy/Assets/global-nav changes
- base-disk domain redesign
- new template runtime semantics beyond selector/normalization display and save-blocking behavior

### 7.5 Follow-up implementation issues

- `#313`: switch selector implementation
- `#314`: VHDX catalog-first selector implementation
- `#315`: VHDX normalization and display policy implementation

---

## Open Questions / TBDs

- Whether `templates.details` becomes a first-class child route or remains represented within `templates.editor`.
- Exact layout partitioning within Templates workspace under WinUI layout constraints (covered in AD2 implementation contract).


