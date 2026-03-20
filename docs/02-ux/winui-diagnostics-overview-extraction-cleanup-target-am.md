# WinUI Diagnostics Overview Extraction Cleanup Target (AM110)

**Purpose:** Define the narrow `Diagnostics Overview` cleanup target so Overview runtime extraction starts from an explicit ownership boundary inside the refined shared Diagnostics composition model.

**Status:** Approved implementation contract for the `Diagnostics Overview` post-AM105 cleanup sequence.

**Scope:** WinUI `Diagnostics Overview` extraction cleanup target only.

**Out of scope:** Diagnostics Logs extraction details, runtime implementation, Diagnostics behavior redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-170`..`FR-172`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-041`)
- `docs/02-ux/winui-diagnostics-composition-cleanup-target-am.md`
- `docs/02-ux/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/ui-migration-execution-plan.md`

---

## 1) Why Diagnostics Overview needs its own cleanup target

AM105 already defines:
- the shared Diagnostics composition cleanup target

That is still not narrow enough for Overview runtime work.

Without an Overview-specific cleanup target, later slices could drift into:
- leaving Overview-specific coordination in shared Diagnostics composition
- reintroducing `MainWindow` as an Overview host hub
- letting Overview absorb Diagnostics Logs concerns because it is the route-entry surface
- letting shared `DiagnosticsWorkspaceComposition` become the de facto Overview workflow owner

AM110 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Diagnostics Overview

The target is:
- `Diagnostics Overview` remains under shared `DiagnosticsWorkspaceComposition`
- shared Diagnostics composition remains responsible only for shared capability-level composition concerns
- an Overview-local seam becomes the long-term home for Overview-specific state and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Overview-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Overview-specific coordination should converge behind an Overview-local seam, not stay in shared Diagnostics composition and not move back to `MainWindow`.

---

## 3) What stays in shared Diagnostics composition

Shared `DiagnosticsWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Diagnostics`
- shared route activation handoff across `diagnostics.overview` and `diagnostics.logs`
- shared capability composition and wiring that spans more than one Diagnostics child surface
- shared lifetime participation for the long-lived Diagnostics workspace

This issue does not redefine the shared Diagnostics owner created by AM105.
It narrows what should not remain trapped there once Overview extraction begins.

---

## 4) What becomes Overview-local

Overview-local ownership should include:
- Overview summary or entry-surface state
- Overview-local navigation coordination
- Overview-local interaction boundaries used by the Overview surface
- Overview-local composition and host cleanup expectations
- Overview-specific refresh or reconcile behavior triggered by `diagnostics.overview` activation

This keeps the Overview slice narrow:
- summary
- entry/index behavior
- local interaction coordination

It does not turn Overview into the shared owner for all Diagnostics behavior.

---

## 5) Overview host cleanup expectation

AM110 is also the cleanup target for Overview-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Overview-specific host bridges remain migration scaffolding only
- Overview-specific control exposure or host-driven UI coordination should reduce behind the Overview-local seam
- shared `DiagnosticsWorkspaceComposition` must not become a permanent host proxy for Overview-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Diagnostics Overview` becoming a new shared Diagnostics god object
- shared `DiagnosticsWorkspaceComposition` becoming the Overview workflow owner
- Overview taking ownership of Diagnostics Logs semantics
- direct `MainWindow` injection into Overview views
- shell-owned host growth that treats Overview as a shell surface instead of a Diagnostics-local surface

Overview is a Diagnostics child surface with a summary/navigation role.
It is not the semantic owner of Diagnostics Logs or shared Diagnostics composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `diagnostics.overview` as the route-entry and index surface for `Diagnostics`
- Overview as primarily a summary and navigation surface
- approved AL shell, header, navigation, and layout behavior around the Diagnostics surface
- approved AM105 shared Diagnostics composition boundary and long-lived workspace behavior
- Diagnostics Logs semantic ownership outside Overview cleanup

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM110 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Diagnostics-local seams are the correct way to cross shell boundaries when needed

For Overview, this means:
- Overview views do not talk to `MainWindow` directly
- shared Diagnostics composition may coordinate with shell-owned behavior only through narrow seams
- Overview-local interaction coordination should not widen shared Diagnostics composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Diagnostics Overview` continues to participate in the long-lived Diagnostics workspace.

That means:
- Overview is not recreated on every navigation
- activation of `diagnostics.overview` refreshes or reconciles Overview state within the existing Diagnostics workspace
- route activation remains the trigger for Overview refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Overview follow-up runtime slices should do this in order:
1. keep Overview under shared `DiagnosticsWorkspaceComposition`
2. introduce the Overview-local seam
3. move Overview-specific state and UI coordination behind that seam
4. reduce temporary Overview-specific host bridges or control exposure behind the seam
5. keep Diagnostics Logs extraction concerns in a later narrow slice

This sequencing keeps AM111-AM113 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM110 does **not** define:
- Diagnostics Logs extraction details
- runtime implementation
- Diagnostics behavior redesign
- performance redesign

---

## 12) Traceability

- `FR-170`
  - Overview stays under shared Diagnostics composition while converging Overview-specific ownership behind an Overview-local seam
- `FR-171`
  - behavior-preserving Overview role with long-lived workspace participation and `diagnostics.overview` route-activation refresh or reconcile behavior
- `FR-172`
  - no direct `MainWindow` view dependency, no shared Diagnostics workflow-owner drift, no Overview absorption of Diagnostics Logs semantics, and explicit non-goals

Mapped acceptance criteria:
- `AC-041`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM105` defined the shared Diagnostics composition cleanup target
- `AL` remains the behavioral baseline for Diagnostics shell, routing, and view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact Overview-local seam type name during implementation.
- `TBD:` Whether Overview route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Overview-specific summary action or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.
