# V2 Health, Trust Runtime, And Builder Issue Briefs

**Purpose:** Preserve the approved next-issue chain for V2 health reconciliation, trust runtime, and the Templates V2 Builder so future agents can create narrow issues without re-expanding the scope into a broad implementation grab bag.

## Current Health Summary

The V2 backend direction is broadly healthy. The current runtime executes root domains, child domains, tree domains, additional independent root forests, replica promotion, per-domain DNS stabilization, domain joins, router runtime, deploy-time review, and deploy-time base remote access. The main confirmed gap is explicit trust runtime.

The important correction is process and traceability, not a runtime rollback. Some docs and issue metadata still describe earlier boundaries, especially around child/tree/additional-forest execution. Those should be reconciled before new runtime or UI implementation starts.

## PM Issue Brief - Mismatch: docs vs code - V2 extended topology execution boundary

### Mode
PM-only; docs and GitHub metadata reconciliation only. No runtime or UI code edits.

### Authoritative Docs
- `AGENTS.md`
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`
- `docs/04-data/v2-template-planning-contract.md`
- `docs/00-overview/v2-unified-orchestration-roadmap.md`

### Issue
- GitHub: `#750` `Mismatch: docs vs code - V2 extended topology execution boundary`
- Parent context: GitHub issue `#722` for V2 Extended Domain Topologies.
- Status target: create a docs-only reconciliation issue and close it only after current docs match backend reality.

### Goal
Bring the V2 current-authority docs and active epic notes into alignment with merged code: child domains, tree domains, and additional independent root forests are executable; trusts remain non-executable.

### Why this issue exists
The backend moved faster than the durable docs. Leaving the old boundary in place would cause future agents to plan from stale assumptions and could duplicate already-completed topology work.

### In Scope
- Update current authority/support docs that still say child/tree/additional forests are deferred or non-executable.
- Note that trusts remain known-but-non-executable until the trust-runtime slice.
- Prepare labels and status notes for recently completed V2 issues and PRs where metadata is missing.
- Recommend whether epics `#716`, `#720`, `#721`, and `#722` should remain open or receive progress comments.
- Retire stale TBDs around child/tree/extra-forest schema fields, first review-surface visuals, and base remote-access return-to-runtime wording.

### Out of Scope
- Trust runtime behavior.
- Templates V2 Builder UX.
- Code changes.
- Broad historical cleanup outside the recent V2 chain.

### Likely Files
- `docs/01-requirements/acceptance-criteria.md`
- `docs/01-requirements/template-schema.md`
- `docs/03-architecture/v2-unified-orchestration.md`
- `docs/04-data/v2-template-planning-contract.md`
- `docs/00-overview/v2-unified-orchestration-roadmap.md`
- `docs/00-overview/tbd-register.md`
- GitHub issue/PR metadata only after user approval.

### Validation required
- Documentation review only.
- Confirm no docs still state that child or tree domains are non-executable in the current backend boundary.

### Repo hygiene requirement
Use a dedicated docs branch from updated `origin/master`. Do not include local-only files such as `dotnet-install.ps1`.

### PR Requirements
- Label: `docs`
- Link the driving issue with `Closes #<issue>`.
- State explicitly that this is docs-only and does not change runtime behavior.

### Definition of Done
- Current V2 docs accurately describe executable topology state.
- Trusts are still clearly deferred as runtime behavior.
- Completed V2 work has a clear traceability note or metadata recommendation.

## PM Issue Brief - Contract: Define first V2 forest-trust runtime slice

### Mode
PM-only; contract and acceptance criteria definition. No runtime or UI code edits.

### Authoritative Docs
- `AGENTS.md`
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`
- `docs/01-requirements/cleanup-cancellation-policy.md`
- `docs/01-requirements/logging-contract.md`
- `docs/04-data/v2-template-planning-contract.md`
- `docs/03-architecture/v2-unified-orchestration.md`

### Issue
- GitHub: `#751` `Contract: Define first V2 forest-trust runtime slice`
- Parent context: GitHub issue `#722`.
- Dependency baseline: docs/code reconciliation issue completed.

### Goal
Define the first executable trust-runtime contract before any implementation.

### In Scope
- First supported shape: bidirectional forest trust between two managed V2 domains/forests.
- Credentials: use existing per-domain domain-admin credential slots.
- DNS prerequisite: configure cross-forest DNS forwarding/reachability before trust creation.
- Readiness: trust creation waits until both participating domains are domain-ready.
- Validation: validate the trust from both source and target sides before marking trust ready.
- Cleanup: if LabAssistant creates a trust and the operation later fails or is cancelled, it attempts to delete created trust objects; DNS forwarders remain in place for retry/diagnosis.
- Planning context: introduce resolved trust context instead of executing from raw template trust rows.
- Runtime shape: define whether trust work is source-DC anchored or a new non-VM/global runtime node path before implementation.
- Review/update SRS and Acceptance Criteria before the Dev issue starts.

### Out of Scope
- External trusts, realm trusts, one-way directions, selective authentication, SID filtering, and unmanaged external domains.
- Trust authoring UI.
- Runtime implementation.
- Dedicated trust credential fields.

### Tests
Define required tests for the Dev slice:
- trust nodes wait for both domains to be ready
- missing domain-admin slots block before runtime
- DNS preparation is represented before trust creation
- trust validation requires both sides
- created trust cleanup is attempted on failure/cancel

### Validation required
- PM review against cleanup policy and logging contract.
- No active TBD required for first-slice implementation.

### PR Requirements
- Label: `docs`
- Link the driving issue with `Closes #<issue>`.
- Include SRS and Acceptance Criteria updates.

### Definition of Done
- The Dev implementer does not need to decide trust type, direction, credential source, DNS prerequisite behavior, validation depth, or cleanup policy.

## PM Issue Brief - Runtime: Execute bidirectional managed-forest V2 trust path

### Mode
Dev-only after the trust contract issue is approved and merged.

### Authoritative Docs
- `AGENTS.md`
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`
- `docs/01-requirements/cleanup-cancellation-policy.md`
- `docs/01-requirements/logging-contract.md`
- `docs/04-data/v2-template-planning-contract.md`
- `docs/03-architecture/v2-unified-orchestration.md`
- active trust contract issue brief

### Issue
- GitHub: `#753` `Runtime: Execute bidirectional managed-forest V2 trust path`
- Parent context: GitHub issue `#722`.
- Dependency baseline: trust contract issue completed.

### Goal
Make `directoryTopology.trusts` executable for the first supported trust shape without widening into all trust variants or UI authoring.

### In Scope
- Add trust-ready planning/runtime nodes after both participating domains are ready.
- Add DNS forwarding/reachability preparation before trust creation.
- Add a trust-specific coordinator/script builder seam rather than growing the central V2 runtime service with large inline scripts.
- Use existing source/target domain-admin credential slots.
- Derive source and target credentials from each participating domain context and block if either side is unresolved.
- Emit structured logs with `operationId`, trust id, source domain id, target domain id, result, and error context.
- Attempt trust-object deletion for LabAssistant-created trusts on failure/cancel; leave DNS forwarders in place.

### Out of Scope
- V2 Builder or any trust authoring UI.
- External/realm trusts.
- One-way trust directions.
- Dedicated trust credential fields.
- Capability-role runtime.

### Tests
- Planner emits trust/DNS/trust-ready nodes in the correct order.
- Runtime blocks when either domain-admin credential slot is unresolved.
- DNS prep runs before trust creation.
- Successful trust creation validates both sides.
- Validation failure marks runtime failed and triggers trust cleanup when a trust was created.
- Cancellation after trust creation attempts trust cleanup.

### Validation required
- `dotnet test C:\src\LabAssistant\LabAssistant.Business.Tests\LabAssistant.Business.Tests.csproj`
- Add Data tests only if persisted model shape changes.

### PR Requirements
- Label: `feature`
- Link the driving issue with `Closes #<issue>`.
- State that trust authoring UI is intentionally out of scope.

### Definition of Done
- Bidirectional forest trusts between two managed V2 forests/domains execute from the V2 runtime path.
- Cleanup/cancellation/logging standards are preserved.
- The central runtime service does not become the long-term owner of trust script construction.

## PM Issue Brief - Contract: Define Templates V2 Builder workflow

### Mode
PM-only; UX/product contract and architecture seam definition. No UI implementation code.

### Authoritative Docs
- `AGENTS.md`
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`
- `docs/01-requirements/template-schema.md`
- `docs/03-architecture/winui-lane-architecture.md`
- `docs/03-architecture/winui-shared-seam-ownership.md`
- `docs/03-architecture/v2-unified-orchestration.md`
- `docs/04-data/v2-template-planning-contract.md`

### Issue
- GitHub: `#754` `Contract: Define Templates V2 Builder workflow`
- Parent context: Templates capability plus V2 unified orchestration.
- Dependency baseline: docs/code reconciliation issue completed.

### Goal
Define a new V2 Builder workflow under Templates so V2 authoring is not forced through the current VM-entry-centric Templates Editor.

### In Scope
- Builder lives under Templates as a workflow-state destination.
- Recommended route shape: add a Templates-local route such as `templates.builder` inside the long-lived Templates workspace.
- Recommended ownership shape: add Builder-specific workspace, controller, composition, and view rather than basing the Builder on the current Editor implementation.
- Current Templates Editor remains for V1/simple/legacy editing.
- Builder must create and edit V2 templates.
- Builder uses topology-first panels: lab networks and directory topology are defined before VM assignment.
- First Builder contract includes schema/profile, lab networks, forests/domains, VM topology role, membership mode, domain assignment, credential slot references, and per-VM NIC/IP/DNS/gateway authoring.
- Deterministic suggestions are allowed, but user confirmation is required before save.

### Out of Scope
- Trust authoring in the first Builder slice.
- Deploy workflow redesign.
- Runtime implementation.
- Replacing the current Templates Editor for all templates.

### Tests
Define test targets for the Dev slice:
- route/workspace boundary for new Templates V2 Builder destination
- V1 Templates Editor remains available
- V2 Builder can create and edit V2 topology basics plus NICs
- V1 templates are not forced through V2-only requirements
- Deploy review can consume templates produced by the Builder

### Validation required
- PM review against Templates and WinUI lane ownership docs.
- No implementation before SRS and AC are updated.

### PR Requirements
- Label: `docs`
- Link the driving issue with `Closes #<issue>`.

### Definition of Done
- The Dev implementer knows the Builder location, coexistence model, first field set, non-goals, and required test shape.

## PM Issue Brief - UX: Add Templates V2 Builder topology-first authoring surface

### Mode
Dev-only after the Builder contract issue is approved and merged.

### Authoritative Docs
- `AGENTS.md`
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`
- `docs/01-requirements/template-schema.md`
- `docs/03-architecture/winui-lane-architecture.md`
- `docs/03-architecture/winui-shared-seam-ownership.md`
- `docs/04-data/v2-template-planning-contract.md`
- active Builder contract issue brief

### Issue
- GitHub: `#755` `UX: Add Templates V2 Builder topology-first authoring surface`
- Dependency baseline: Builder contract issue completed.

### Goal
Add the first usable V2 Builder surface under Templates for creating and editing V2 templates.

### In Scope
- Add a Templates workflow-state destination for V2 Builder.
- Use a Templates-local route such as `templates.builder`.
- Add Builder-specific workspace/viewmodel/controller/composition/view seams.
- Keep the current Templates Editor available for V1/simple/legacy editing.
- Implement topology-first authoring for lab networks, forests/domains, and VM assignments.
- Implement VM authoring for topology role, membership mode, domain assignment, credential slot references, and per-VM NIC/IP/DNS/gateway.
- Provide deterministic suggestions but require explicit user confirmation before save.
- Save valid V2 templates that can be reviewed by the existing Deploy From Template V2 review flow.

### Out of Scope
- Trust authoring.
- Trust runtime.
- Replacing the current Templates Editor for every template type.
- Deploy workflow redesign.
- Broad visual redesign outside the Builder surface.

### Tests
- UI route/workspace tests for Builder destination.
- ViewModel/controller tests for V2 field persistence.
- V1 non-regression tests for current Templates Editor.
- Builder output can be planned by V2 planner in Business tests or integration-style UI tests.

### Validation required
- `dotnet test C:\src\LabAssistant\LabAssistant.UI.Tests\LabAssistant.UI.Tests.csproj`
- `dotnet test C:\src\LabAssistant\LabAssistant.Business.Tests\LabAssistant.Business.Tests.csproj` if planner-facing generated templates are covered there.

### PR Requirements
- Label: `feature`
- Link the driving issue with `Closes #<issue>`.
- State that trust authoring is intentionally out of scope.

### Definition of Done
- Users can create and edit V2 templates for topology basics plus NICs without hand-authoring JSON.
- V1 compatibility is preserved.
- Deploy From Template can review the Builder-produced V2 template.

## Subagent Coordination Notes

- Docs/Traceability agent should audit stale V2 docs and GitHub metadata only.
- Trust Runtime Research agent should inspect planner/runtime seams and test risks only.
- V2 Builder UX Contract agent should inspect Templates seams and contract boundaries only.
- The orchestrator resolves conflicts and turns outputs into issue briefs.
- No subagent should implement runtime or UI code before its approved issue exists.

## Open Questions / TBDs

- TBD: whether V2 Builder eventually replaces the current Templates Editor for all template types or remains V2-specific.
- TBD: which GitHub milestone should own the V2 Builder work if it does not naturally belong under the existing V2 backend epics.
