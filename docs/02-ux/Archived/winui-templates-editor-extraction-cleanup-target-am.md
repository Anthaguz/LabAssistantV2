# WinUI Templates Editor Extraction Cleanup Target (AM68)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Templates Editor` cleanup target so Editor runtime extraction starts from an explicit ownership boundary inside the refined shared Templates composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Templates Editor` extraction cleanup target only.

**Out of scope:** Templates Library extraction details, runtime implementation, Editor behavior redesign, performance redesign, and per-navigation workspace recreation.

**Related:**
- `docs/01-requirements/srs.md` (`FR-152`..`FR-154`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-035`)
- `docs/02-ux/Archived/winui-templates-capability-contract-ad.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-templates-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-templates-library-extraction-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Templates Editor needs its own cleanup target

AM56 through AM67 already define:
- the shared Templates composition cleanup target
- the Templates-local composition owner direction
- reduction of shared Templates shell/host/test drift
- the Templates Library-local ownership target

That is still not narrow enough for Editor runtime work.

Without an Editor-specific cleanup target, later slices could drift into:
- leaving Editor-specific state or orchestration trapped in shared Templates composition
- reintroducing `MainWindow` as an Editor host hub
- letting shared `TemplatesWorkspaceComposition` become the de facto Editor workflow owner
- letting Editor absorb Library ownership because `templates.editor` is the active workflow-state destination

AM68 exists to prevent that drift before Editor runtime extraction begins.

---

## 2) Desired end-state for Templates Editor

The target is:
- `Templates Editor` remains under shared `TemplatesWorkspaceComposition`
- shared Templates composition remains responsible only for shared capability-level composition concerns
- an Editor-local seam becomes the long-term home for Editor-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Editor-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Editor-specific coordination should converge behind an Editor-local seam, not stay in shared Templates composition and not move back to `MainWindow`.

---

## 3) What stays in shared Templates composition

Shared `TemplatesWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Templates`
- shared route activation handoff across `templates.library` and `templates.editor`
- shared capability composition and wiring that spans more than one Templates child surface
- shared lifetime participation for the long-lived Templates workspace
- shared coordination between Library and Editor only where the concern is genuinely capability-level rather than Editor-local

This issue does not redefine the shared Templates owner created by AM56.
It narrows what should not remain trapped there once Editor extraction begins.

---

## 4) What becomes Editor-local

Editor-local ownership should include:
- selected template editing context, draft, validation, and feedback state for the editor surface
- Editor-specific orchestration for open/load, create-new, edit, save, save-as-needed, discard/reset, and editor-surface VM-entry editing flows
- Editor-local composition and interaction coordination used by the `templates.editor` surface
- Editor-specific refresh or reconcile behavior triggered by `templates.editor` activation

This keeps the Editor slice narrow:
- editor workflow surface
- Editor-local ownership cleanup
- route-activation refresh within the existing Templates workspace

It does not turn Editor into the shared owner for all Templates behavior.

---

## 5) Editor composition and host cleanup expectation

AM68 is also the cleanup target for Editor-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Editor-specific host bridges remain migration scaffolding only
- Editor-specific control exposure or host-driven UI coordination should reduce behind the Editor-local seam
- shared `TemplatesWorkspaceComposition` must not become a permanent host proxy for Editor-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Templates Editor` becoming a new shared Templates god object
- shared `TemplatesWorkspaceComposition` becoming the Editor workflow owner
- Editor taking ownership of Library-specific state, orchestration, or composition
- direct `MainWindow` injection into Editor views
- shell-owned host growth that treats Editor as a shell surface instead of a Templates-local surface

Editor is a workflow-state destination.
It is not the semantic owner of shared Templates composition or Library workflow concerns.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `templates.library` as the stable/default Templates surface
- existing AD and AL7 Templates navigation behavior where `templates.editor` is entered from explicit actions
- Editor remaining within the long-lived Templates workspace rather than becoming a per-navigation recreated surface
- existing Library ownership remaining outside this Editor cleanup target

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM68 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Templates-local seams are the correct way to cross shell boundaries when needed

For Editor, this means:
- Editor views do not talk to `MainWindow` directly
- shared Templates composition may coordinate with shell-owned behavior only through narrow seams
- Editor-local interaction coordination should not widen shared Templates composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Templates Editor` continues to participate in the long-lived Templates workspace.

That means:
- Editor is not recreated on every navigation
- activation of `templates.editor` refreshes or reconciles Editor state within the existing Templates workspace
- route activation remains the trigger for Editor refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Editor follow-up runtime slices should do this in order:
1. keep Editor under shared `TemplatesWorkspaceComposition`
2. introduce the Editor-local seam
3. move Editor-specific state, orchestration, composition, and interaction coordination behind that seam
4. reduce temporary Editor-specific host bridges or control exposure behind the seam
5. keep Library extraction concerns in their own already-defined narrow slices

This sequencing keeps AM69 through later Editor runtime slices narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM68 does **not** define:
- Templates Library extraction details
- runtime implementation
- Editor behavior redesign
- performance redesign

---

## 12) Traceability

- `FR-152`
  - Templates Editor stays under shared Templates composition while converging Editor-specific ownership behind an Editor-local seam
- `FR-153`
  - behavior-preserving `templates.editor` role with long-lived Templates workspace participation and route-activation refresh
- `FR-154`
  - no direct `MainWindow` view dependency, no shared Templates workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-035`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM56` defined the shared Templates composition cleanup target
- `AM61` through `AM67` remain the Templates Library refinement context that this narrower Editor target must inherit and not override
- `AD` and `AL7` remain the behavioral baseline for Templates routing, unified workflow, and Library-first navigation

---

## 13) Open Questions / TBDs

- `TBD:` Exact Editor-local seam type name during implementation.
- `TBD:` Whether Editor route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Editor-specific dialog or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.

