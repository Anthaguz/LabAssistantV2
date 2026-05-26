# TBD Register

**Purpose:** Keep one inventory of active unresolved decisions without forcing the source document itself to become a tracking spreadsheet.

## Rules
- The source document named for each entry remains authoritative for the actual rule or decision boundary.
- This register tracks only active TBDs.
- Archived docs, templates, and examples are excluded unless their unresolved item is explicitly promoted here.
- Remove an entry when it is resolved or obsolete.

## Product And Scope

| ID | Topic | Source | Status | Trigger / Next step |
|---|---|---|---|---|
| TBD-001 | Template sharing model: remain file-based or evolve toward a central repository | `docs/00-overview/product-vision.md`, `docs/00-overview/scope.md`, `docs/00-overview/glossary.md` | Deferred | Revisit only if collaboration or centralized storage becomes an approved product slice. |
| TBD-002 | Post-deployment automation script support scope | `docs/00-overview/product-vision.md`, `docs/00-overview/scope.md`, `docs/01-requirements/srs.md` | Deferred | Revisit when guest automation expands beyond current deploy-step scope. |
| TBD-003 | Telemetry or usage metrics strategy, including enterprise acceptability | `docs/00-overview/product-vision.md`, `docs/00-overview/glossary.md`, `docs/01-requirements/srs.md` | Deferred | Revisit before any telemetry or adoption-reporting work is approved. |
| TBD-004 | Long-term product direction beyond the current single-user Hyper-V-only scope | `docs/00-overview/product-vision.md`, `docs/00-overview/scope.md`, `docs/01-requirements/srs.md` | Deferred | Revisit only if roadmap planning reopens team-platform or multi-hypervisor expansion. |

## Requirements And Quality

| ID | Topic | Source | Status | Trigger / Next step |
|---|---|---|---|---|
| TBD-005 | Future template schema evolution and compatibility detail beyond current baseline | `docs/01-requirements/srs.md`, `docs/01-requirements/template-schema.md`, `docs/00-overview/glossary.md` | Deferred | Revisit when schema evolution work goes beyond the current versioned-template baseline. |
| TBD-006 | Base disk OS classification policy | `docs/01-requirements/srs.md` | Deferred | Revisit when catalog classification becomes operationally important to selection, validation, or filtering. |
| TBD-007 | Naming collision policy for deployments and related retries | `docs/01-requirements/non-functional-requirements.md`, `docs/01-requirements/user-stories.md`, `docs/01-requirements/acceptance-criteria.md` | Deferred | Revisit before collision-handling behavior is expanded or standardized in implementation. |
| TBD-008 | Performance baselines, numeric targets, and baseline hardware definition | `docs/01-requirements/non-functional-requirements.md` | Deferred | Revisit when performance measurement is scheduled on a representative workstation. |
| TBD-009 | Supported Windows version matrix | `docs/01-requirements/non-functional-requirements.md` | Deferred | Revisit before packaging, distribution, or explicit compatibility validation work. |
| TBD-010 | Log and diagnostics retention policy | `docs/01-requirements/non-functional-requirements.md`, `docs/01-requirements/logging-contract.md` | Deferred | Revisit when diagnostics storage policy becomes a release or support concern. |
| TBD-011 | Resume or retry UX after failed deployment beyond current cleanup-and-report flow | `docs/01-requirements/srs.md` | Deferred | Revisit only if product scope approves post-failure continuation flows. |
| TBD-012 | RDP readiness detection beyond current v1 host-observable checks | `docs/01-requirements/srs.md`, `docs/01-requirements/acceptance-criteria.md` | Deferred | Revisit when guest-observable readiness or richer RDP guidance becomes an approved slice. |
| TBD-013 | Machines VM origin/status labeling details | `docs/01-requirements/machines-capability-contract.md` | Deferred | Revisit when Machines v1 presentation is refined beyond the current baseline. |
| TBD-014 | Machines advanced-settings handoff to Hyper-V MMC in v1 or later | `docs/01-requirements/machines-capability-contract.md` | Deferred | Revisit when Machines editing scope is reopened. |
| TBD-015 | Mapping policy when multiple compatible base disks exist for the same deployment | `docs/01-requirements/user-stories.md` | Deferred | Revisit when base-disk selection behavior is standardized beyond the current validation-and-selection baseline. |
| TBD-016 | Timeout strategy for long-running external commands during cancellation | `docs/01-requirements/cleanup-cancellation-policy.md` | Deferred | Revisit when cancellation handling is tightened beyond the current safe-boundary cleanup model. |
| TBD-017 | Whether cleanup-failure UX should offer an explicit retry-cleanup action | `docs/01-requirements/cleanup-cancellation-policy.md` | Deferred | Revisit when post-failure remediation UX is scoped beyond the current cleanup-and-report baseline. |
| TBD-018 | Whether to enforce strict structured-event schema validation at runtime | `docs/01-requirements/logging-contract.md` | Backlog | Revisit when logging schema/tooling hardens enough to justify runtime enforcement. |
| TBD-019 | Exact migration flow UX for template schema upgrades | `docs/01-requirements/template-schema.md` | Deferred | Revisit when schema-upgrade flows become a user-visible product slice beyond the current import/save contract. |

## WinUI Architecture

| ID | Topic | Source | Status | Trigger / Next step |
|---|---|---|---|---|
| TBD-020 | Exact compact-width breakpoint values beyond the current compact fallback rule | `docs/03-architecture/winui-shell-navigation-layout.md` | Deferred | Revisit only if current compact behavior proves insufficient on real devices. |
| TBD-021 | Whether compact child-route reveal should stay built-in or adopt a stronger custom affordance | `docs/03-architecture/winui-shell-navigation-layout.md` | Deferred | Revisit when compact-route discoverability becomes a validated usability problem. |
| TBD-022 | Which capabilities bootstrap eagerly at startup versus lazily on first activation | `docs/03-architecture/winui-shell-bootstrap-runtime.md` | Next contract | Revisit when typed runtime implementation work begins for a real capability. |
| TBD-023 | Whether any capability needs a second runtime boundary inside the same capability | `docs/03-architecture/winui-shell-bootstrap-runtime.md` | Deferred | Revisit only if a future capability proves too large for the current runtime/lane split. |
| TBD-024 | Which currently extracted lanes should remain approved simple-lane variants | `docs/03-architecture/winui-lane-architecture.md` | Next contract | Revisit when extracted lanes are reviewed against the canonical lane standard. |
| TBD-025 | Whether future work needs a recurring fifth lane seam beyond the current four-role pattern | `docs/03-architecture/winui-lane-architecture.md` | Deferred | Revisit only if multiple lanes show the same missing boundary. |
| TBD-026 | Whether a future typed `DeployCapabilityRuntime` should own all current Deploy shared seams directly | `docs/03-architecture/winui-shared-seam-ownership.md` | Next contract | Revisit when typed Deploy runtime implementation is actually scoped. |
| TBD-027 | Which non-Deploy shared seams are mature enough for promotion into the canonical shared-seam doc | `docs/03-architecture/winui-shared-seam-ownership.md` | Backlog | Revisit after another capability accumulates a stable shared seam worth canonizing. |

## Testing And Process

| ID | Topic | Source | Status | Trigger / Next step |
|---|---|---|---|---|
| TBD-028 | Whether to define explicit automated coverage thresholds | `docs/07-testing/test-strategy.md` | Deferred | Revisit after CI and the main test suites stabilize further. |
| TBD-029 | Whether Hyper-V smoke or regression validation needs its own dedicated checklist model beyond current test-plan coverage | `docs/07-testing/test-strategy.md` | Deferred | Revisit when manual host validation expands beyond the current milestone-history evidence. |
| TBD-030 | Whether `test-plan.md` should split smoke coverage from milestone regression coverage | `docs/07-testing/test-plan.md` | Backlog | Revisit when the test-plan file becomes harder to maintain as one document. |
| TBD-031 | Whether explicit Windows-version-specific pass/fail checklists are needed once compatibility targets are finalized | `docs/07-testing/test-plan.md` | Deferred | Revisit after the supported Windows matrix is defined. |
| TBD-032 | Whether a later cleanup pass should normalize existing WinUI folder layouts to the newer organization standard | `docs/03-architecture/code-organization.md` | Backlog | Revisit when a targeted code-organization cleanup slice is approved. |
| TBD-033 | Whether XML-documentation analyzer enforcement is worth adding for selected projects or folders | `docs/03-architecture/code-documentation.md` | Backlog | Revisit when comment coverage standards are stable enough for automated enforcement. |
| TBD-034 | Whether deployment history records should be stored locally | `docs/01-requirements/acceptance-criteria.md` | Deferred | Revisit when deployment history becomes an approved product slice rather than an implied future behavior. |
| TBD-035 | Deferred Assets inner layout details beyond the current `assets.base_disks` baseline | `docs/01-requirements/acceptance-criteria.md` | Backlog | Revisit when the next Assets layout or route-contract slice is approved. |
| TBD-036 | Whether rotated structured log files should be included in the Phase 1 viewer | `docs/01-requirements/acceptance-criteria.md` | Deferred | Revisit when Diagnostics log-viewer scope expands beyond the current primary structured log file. |
| TBD-037 | Template deletion retention policy | `docs/01-requirements/acceptance-criteria.md` | Deferred | Revisit before delete behavior is standardized as hard delete, soft delete, or another retained-record model. |
| TBD-039 | Whether future switch management and guest configuration work should introduce new business workflow coordinators | `docs/03-architecture/architecture.md` | Deferred | Revisit when switch management or guest configuration becomes an approved feature slice. |
| TBD-040 | Whether remaining non-Machines read call sites such as `VirtualSwitchProvider` should converge onto the explicit Hyper-V query seam | `docs/03-architecture/hyperv-powershell-interaction.md` | Backlog | Revisit when the remaining read-only Hyper-V call sites are cleaned up or removed in a narrow follow-up slice. |
| TBD-041 | Exact numeric scheduler caps for V2 deployment profiles on representative host hardware | `docs/03-architecture/v2-unified-orchestration.md`, `docs/01-requirements/non-functional-requirements.md` | Deferred | Revisit when the first V2 runtime slice can be measured on real Hyper-V hosts under storage, CPU, and RAM pressure. |
| TBD-042 | Whether V2 should expose deterministic auto-allocation for guest IPs in addition to explicit per-NIC addressing | `docs/01-requirements/template-schema.md`, `docs/04-data/v2-template-planning-contract.md` | Deferred | Revisit only if repeated manual addressing proves too costly for real template authors. |
| TBD-043 | Exact import/edit UX for unresolved V2 credential slots after template sharing | `docs/01-requirements/acceptance-criteria.md`, `docs/04-data/v2-template-planning-contract.md` | Next contract | Revisit when V2 review-and-resolve UX becomes an executable product slice. |
| TBD-044 | Exact V2 child-domain and extra-forest schema fields beyond the reserved extension seam | `docs/01-requirements/template-schema.md`, `docs/04-data/v2-template-planning-contract.md` | Deferred | Revisit when child-domain implementation is approved as an active milestone. |
