# WinUI Deploy From-Template Contract (Milestone AF)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the behavioral contract for WinUI `Deploy from-template` so AF2/AF3/AF4 can implement without ambiguity.

**Status:** Historical milestone doc. Not authoritative for new work.

**Related:**
- `docs/01-requirements/srs.md` (FR-087, FR-088, FR-089, FR-090, FR-095, FR-096)
- `docs/01-requirements/acceptance-criteria.md` (AC-014, AC-016)
- `docs/02-ux/Archived/winui-global-navigationview-contract-ac.md`
- `docs/02-ux/Archived/winui-templates-capability-contract-ad.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`
- `docs/02-ux/Archived/navigation-ia-draft.md`

---

## 1) AF Scope Boundary

AF covers WinUI Deploy `from-template` only.

In scope:
- Deploy from-template route, readiness gating, and result presentation baseline
- Compatibility handling for template disk/switch references using AE behavior

Out of scope:
- Deploy on-the-fly migration
- WPF Deploy changes
- deployment semantics redesign
- template schema redesign

---

## 2) Routing and Navigation Contract

Parent capability:
- `Deploy`

AF child route:
- `deploy.from_template` (AF active route)

Route behavior:
- Parent `Deploy` selection must provide a deterministic path to `deploy.from_template`.
- Routing remains compatible with global NavigationView parent/child contract.
- No divergence from canonical `capability.subview` route key format.

Explicit defer:
- `deploy.on_the_fly` remains deferred for AF implementation.

---

## 3) Input Compatibility Contract (AE Reuse)

### 3.1 Disk identity handling

Deploy from-template must reuse AE disk identity semantics:
- effective precedence: `vhdxId` -> `vhdxSignature` -> `vhdPath`
- required unresolved or ambiguous disk identity is blocking
- deploy must not silently coerce conflicting identity

### 3.2 Switch mapping handling

Deploy from-template must use compatibility mapping:
- prefer canonical `switchNames`
- fallback to legacy `switchName`
- partial/missing mapping may be warning or blocking based on readiness policy for required fields

### 3.3 Compatibility transparency

Deploy readiness output must clearly indicate:
- effective source used for resolution
- unresolved/ambiguous items
- affected VM entries

---

## 4) Readiness and Gating Contract

Blocking conditions (Fail):
- required disk identity unresolved
- required disk identity ambiguous/conflicting
- any condition that prevents deterministic deployment input selection

Warning conditions (Warn, non-blocking):
- partial switch mapping where safe fallback exists
- compatibility degradations that do not invalidate required deployment inputs

User-correction affordances (required):
- auto-resolve suggestions when deterministic fix is available
- explicit `Open in Templates Editor` action from readiness output
- status text remains actionable and non-silent

---

## 5) Results Visibility Contract (Compact-First)

AF baseline for from-template run results:
- sticky status/progress remains visible while run is active
- per-VM rows are concise by default
- per-VM details are expandable on demand
- global warnings/errors are collapsed by default and expandable when needed
- in AH2+, deploy timeline/results/issue context is hosted by shell right panel ownership contract (not page-local right column)

Behavior-preservation requirement:
- deployment semantics (execution, cancellation, cleanup, logging) remain unchanged
- AF changes presentation and correction affordances, not core deployment logic

Canonical timeline row contract:
- states: `Pending`, `Running`, `Succeeded`, `Failed`, `Skipped`
- one label per step (no duplicated start/finish labels)
- state drives spinner/icon rendering
- skipped/non-applicable rows are hidden
- nested parent rows are hidden when no child step executes
- timeline state source is backend step-state update payload contract (AH3), not status-text inference

---

## 6) AF Work Packaging Guidance

### AF2 (routing + scaffold)
In scope:
- route wiring for `deploy.from_template`
- template selection/readiness scaffold surfaces

Out of scope:
- full readiness correction behavior
- full results compaction behavior

### AF3 (readiness compatibility + correction actions)
In scope:
- AE compatibility evaluation in Deploy readiness
- blocking/warning categorization
- auto-resolve suggestion surface
- `Open in Templates Editor` correction action

Out of scope:
- on-the-fly deploy migration

### AF4 (results visibility convergence)
In scope:
- compact-first run results UX
- expandable per-VM details
- collapsed-by-default global warning/error section

Out of scope:
- deployment semantics changes

---

## 7) Traceability

- FR-087 -> AC-014 scenario 1 (from-template-first AF scope + route behavior)
- FR-088 -> AC-014 scenarios 2 and 3 (disk/switch compatibility behavior)
- FR-089 -> AC-014 scenario 4 (correction affordances)
- FR-090 -> AC-014 scenario 5 (compact-first results visibility)
- FR-095 and FR-096 -> AC-016 scenarios 1-4 (canonical timeline state and rendering parity)

---

## Open Questions / TBDs

- `TBD`: whether AF2 should include placeholder affordances for deferred `deploy.on_the_fly` route discoverability.
- `TBD`: exact wording/severity taxonomy for warning vs blocking readiness rows in Deploy UI.

