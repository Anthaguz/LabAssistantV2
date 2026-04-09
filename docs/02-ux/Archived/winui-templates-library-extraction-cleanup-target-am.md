# WinUI Templates Library Extraction Cleanup Target (AM61)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Templates Library` cleanup target so Library runtime extraction starts from an explicit ownership boundary inside the refined shared Templates composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Templates Library` extraction cleanup target only.

**Out of scope:** Templates Editor extraction details, runtime implementation, Library behavior redesign, performance redesign, and per-navigation workspace recreation.

**Related:**
- `docs/01-requirements/srs.md` (`FR-149`..`FR-151`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-034`)
- `docs/02-ux/Archived/winui-templates-capability-contract-ad.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-templates-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Templates Library needs its own cleanup target

AM56 through AM60 already define:
- the shared Templates composition cleanup target
- the Templates-local composition owner direction
- reduction of shared Templates shell/host/test drift

That is still not narrow enough for Library runtime work.

Without a Library-specific cleanup target, later slices could drift into:
- leaving Library-specific state or orchestration trapped in shared Templates composition
- reintroducing `MainWindow` as a Library host hub
- letting shared `TemplatesWorkspaceComposition` become the de facto Library workflow owner
- letting Library absorb Editor ownership because `templates.library` is the stable/default surface

AM61 exists to prevent that drift before Library runtime extraction begins.

---

## 2) Desired end-state for Templates Library

The target is:
- `Templates Library` remains under shared `TemplatesWorkspaceComposition`
- shared Templates composition remains responsible only for shared capability-level composition concerns
- a Library-local seam becomes the long-term home for Library-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Library-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Library-specific coordination should converge behind a Library-local seam, not stay in shared Templates composition and not move back to `MainWindow`.

---

## 3) What stays in shared Templates composition

Shared `TemplatesWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Templates`
- shared route activation handoff across `templates.library` and `templates.editor`
- shared capability composition and wiring that spans more than one Templates child surface
- shared lifetime participation for the long-lived Templates workspace

This issue does not redefine the shared Templates owner created by AM56.
It narrows what should not remain trapped there once Library extraction begins.

---

## 4) What becomes Library-local

Library-local ownership should include:
- Library list, selection, filter, and feedback state
- Library-specific orchestration for refresh, load/open, create-entry, import, export, delete, and library-surface selection flows
- Library-local composition and interaction coordination used by the `templates.library` surface
- Library-specific refresh or reconcile behavior triggered by `templates.library` activation

This keeps the Library slice narrow:
- primary Templates library surface
- Library-local ownership cleanup
- route-activation refresh within the existing Templates workspace

It does not turn Library into the shared owner for all Templates behavior.

---

## 5) Library composition and host cleanup expectation

AM61 is also the cleanup target for Library-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Library-specific host bridges remain migration scaffolding only
- Library-specific control exposure or host-driven UI coordination should reduce behind the Library-local seam
- shared `TemplatesWorkspaceComposition` must not become a permanent host proxy for Library-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Templates Library` becoming a new shared Templates god object
- shared `TemplatesWorkspaceComposition` becoming the Library workflow owner
- Library taking ownership of Editor-specific state, orchestration, or composition
- direct `MainWindow` injection into Library views
- shell-owned host growth that treats Library as a shell surface instead of a Templates-local surface

Library is the stable/default Templates surface.
It is not the semantic owner of shared Templates composition or Editor workflow concerns.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `templates.library` as the stable/default Templates surface
- existing AD and AL7 Library-first Templates navigation behavior
- Library remaining within the long-lived Templates workspace rather than becoming a per-navigation recreated surface
- existing Editor ownership remaining outside this Library cleanup target

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM61 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Templates-local seams are the correct way to cross shell boundaries when needed

For Library, this means:
- Library views do not talk to `MainWindow` directly
- shared Templates composition may coordinate with shell-owned behavior only through narrow seams
- Library-local interaction coordination should not widen shared Templates composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Templates Library` continues to participate in the long-lived Templates workspace.

That means:
- Library is not recreated on every navigation
- activation of `templates.library` refreshes or reconciles Library state within the existing Templates workspace
- route activation remains the trigger for Library refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Library follow-up runtime slices should do this in order:
1. keep Library under shared `TemplatesWorkspaceComposition`
2. introduce the Library-local seam
3. move Library-specific state, orchestration, composition, and interaction coordination behind that seam
4. reduce temporary Library-specific host bridges or control exposure behind the seam
5. keep Editor extraction concerns in their own later narrow slices

This sequencing keeps AM62-AM67 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM61 does **not** define:
- Templates Editor extraction details
- runtime implementation
- Library behavior redesign
- performance redesign

---

## 12) Traceability

- `FR-149`
  - Templates Library stays under shared Templates composition while converging Library-specific ownership behind a Library-local seam
- `FR-150`
  - behavior-preserving `templates.library` role with long-lived Templates workspace participation and route-activation refresh
- `FR-151`
  - no direct `MainWindow` view dependency, no shared Templates workflow-owner drift, and explicit non-goals

Mapped acceptance criteria:
- `AC-034`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM56` defined the shared Templates composition cleanup target
- `AM57` through `AM60` remain the shared Templates refinement context that this narrower Library target must inherit and not override
- `AD` and `AL7` remain the behavioral baseline for Templates routing, unified workflow, and Library-first navigation

---

## 13) Open Questions / TBDs

- `TBD:` Exact Library-local seam type name during implementation.
- `TBD:` Whether Library route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Library-specific dialog or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.

