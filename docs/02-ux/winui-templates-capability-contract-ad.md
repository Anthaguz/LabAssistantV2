# WinUI Templates Capability Contract (Milestone AD)

**Purpose:** Define the behavioral and routing contract for `Templates` in WinUI so AD2 (surface scaffolding) and AD3 (operational wiring) can execute without ambiguity.

**Status:** Approved contract for Milestone AD planning/implementation.

**Related:**
- `docs/01-requirements/srs.md` (FR-077, FR-078, FR-079)
- `docs/01-requirements/acceptance-criteria.md` (AC-011)
- `docs/02-ux/winui-global-navigationview-contract-ac.md` (AC shell baseline)
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/ui-migration-execution-plan.md`

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

---

## Open Questions / TBDs

- Whether `templates.details` becomes a first-class child route or remains represented within `templates.editor`.
- Exact layout partitioning within Templates workspace under WinUI layout constraints (covered in AD2 implementation contract).

