# Authoritative Document Map

**Purpose:** Define which documents are authoritative now, which ones are historical only, and how durable WinUI rules must be updated so milestone-local contracts do not remain live by accident.

## Current Authority Layers

### 1) Repo workflow and role rules
- `AGENTS.md`

### 2) Product framing and terminology
- `docs/00-overview/scope.md`
- `docs/00-overview/product-vision.md`
- `docs/00-overview/glossary.md`

Use these when the active slice depends on product scope, target users, outcomes, or terminology.
They do not replace the implementation contract in `srs.md` and `acceptance-criteria.md`.

### 3) Product planning and story traceability
- `docs/01-requirements/user-stories.md`

Use this when the active slice depends on story framing, user-observable scope, or story-to-FR/AC/test traceability.
It does not replace the implementation contract in `srs.md` and `acceptance-criteria.md`.
Backlog placeholders or older draft story stubs in that file do not become active implementation authority unless they are mapped into the current requirements set or the active slice explicitly depends on them.

### 4) Product behavior and implementation contract
- `docs/01-requirements/srs.md`
- `docs/01-requirements/acceptance-criteria.md`

### 5) Quality and specialized requirement supplements
Use these only when the active slice directly depends on them or when `srs.md` / `acceptance-criteria.md` explicitly points to them:
- `docs/01-requirements/non-functional-requirements.md`
- `docs/01-requirements/cleanup-cancellation-policy.md`
- `docs/01-requirements/logging-contract.md`
- `docs/01-requirements/machines-capability-contract.md`
- `docs/01-requirements/template-schema.md`
- `docs/vhdx-catalog-schema.md`
- `docs/04-data/v2-template-planning-contract.md`

These define quality bars, specialized requirement boundaries, and domain-specific supporting contracts.
They do not override `srs.md` / `acceptance-criteria.md` on user-visible behavior unless those files explicitly delegate.

### 6) Testing authority
- `docs/07-testing/test-strategy.md`
- `docs/07-testing/test-plan.md`

Use these when the active slice depends on validation strategy, recurring regression coverage, or manual/automated verification expectations.
They do not override product behavior contracts; they define how behavior should be verified.

### 7) Current architecture authority
- `docs/03-architecture/architecture.md`
- `docs/03-architecture/code-documentation.md`
- `docs/03-architecture/code-organization.md`
- `docs/03-architecture/hyperv-powershell-interaction.md`
- `docs/03-architecture/v2-unified-orchestration.md`
- `docs/03-architecture/lab-config-script-behavior-inventory.md`
- `docs/03-architecture/winui-shell-navigation-layout.md`
- `docs/03-architecture/winui-shell-bootstrap-runtime.md`
- `docs/03-architecture/winui-lane-architecture.md`
- `docs/03-architecture/winui-shared-seam-ownership.md`

### 8) Active execution authority
- the current GitHub issue brief or issue body for the active slice

That issue brief may narrow scope and cite exact authoritative sections for the slice.
It must not silently override the repo rules, SRS, AC, or the canonical architecture docs above.

### 9) Active TBD tracking
- `docs/00-overview/tbd-register.md` is the central index of active unresolved decisions.
- The source document listed in the register remains authoritative for the actual rule, contract, or boundary.
- Archived docs, templates, and examples do not become active TBD sources unless the unresolved item is explicitly listed in the register.

## Update And Override Rules

### 1) No silent overrides
- A newer doc does not override an older doc by implication.
- If a durable rule changes, update the canonical file in place.
- If behavior changes, update `srs.md` and `acceptance-criteria.md` in the same slice.

### 2) Canonical docs change in place
- shell navigation/layout changes update `docs/03-architecture/winui-shell-navigation-layout.md`
- lane-local architecture changes update `docs/03-architecture/winui-lane-architecture.md`
- shell/bootstrap/runtime boundary changes update `docs/03-architecture/winui-shell-bootstrap-runtime.md`
- Hyper-V PowerShell interaction model changes update `docs/03-architecture/hyperv-powershell-interaction.md`
- shared seam/helper ownership changes update `docs/03-architecture/winui-shared-seam-ownership.md`
- repo-wide code documentation guidance changes update `docs/03-architecture/code-documentation.md`
- repo-wide code organization guidance changes update `docs/03-architecture/code-organization.md`

Do not create a new milestone-coded architecture contract when the rule is meant to survive beyond that slice.

### 3) Historical docs do not stay live
- once a milestone contract is absorbed into a canonical doc, the milestone doc becomes historical only
- historical docs remain useful for issue, PR, and milestone traceability
- historical docs are not current authority for new work

### 4) Issue briefs must name the active authority set
Each active slice should cite only the docs it actually needs.
If an issue requires reading many milestone docs just to know the current rule, consolidation is overdue.

## Historical Milestone Docs

Milestone-coded docs are not current authority for new work.

That includes:
- milestone-coded WinUI docs under `docs/02-ux/`
- milestone checklists under `docs/07-testing/`
- archived migration aids under `docs/03-architecture/Archived/`

They may remain in the repo for:
- decision history
- implementation history
- milestone closure evidence
- audits or historical reconstruction when explicitly requested

They must not be used as the normal authority chain for new implementation or planning work.

If a historical milestone doc still appears to contain a rule that matters today, the fix is:
1. promote the surviving rule into the current authority docs
2. keep the milestone doc historical

Do not silently reactivate the milestone doc as live authority.

## Trust Rule

If a rule is not in:
- `AGENTS.md`
- the current authority docs named in the `Current Authority Layers` section above
- or the active issue brief

then it should be treated as background or history, not as live authority for new work.
