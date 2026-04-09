# WinUI Diagnostics Logs Extraction Cleanup Target (AM114)

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Define the narrow `Diagnostics Logs` cleanup target so Logs runtime extraction starts from an explicit ownership boundary inside the refined shared Diagnostics composition model.

**Status:** Historical milestone doc. Not authoritative for new work.

**Scope:** WinUI `Diagnostics Logs` extraction cleanup target only.

**Out of scope:** Diagnostics Overview extraction details, runtime implementation, Diagnostics behavior redesign, per-navigation workspace recreation, and performance redesign.

**Related:**
- `docs/01-requirements/srs.md` (`FR-173`..`FR-175`)
- `docs/01-requirements/acceptance-criteria.md` (`AC-042`)
- `docs/02-ux/Archived/winui-diagnostics-composition-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-diagnostics-overview-extraction-cleanup-target-am.md`
- `docs/02-ux/Archived/winui-shell-view-consistency-contract-al.md`
- `docs/02-ux/Archived/winui-capability-workspace-composition-contract-am.md`
- `docs/02-ux/Archived/winui-shell-composition-boundary-contract-am.md`
- `docs/02-ux/Archived/ui-migration-execution-plan.md`

---

## 1) Why Diagnostics Logs needs its own cleanup target

AM105 already defines:
- the shared Diagnostics composition cleanup target

AM110 already defines:
- the narrow Diagnostics Overview cleanup target

That is still not narrow enough for Logs runtime work.

Without a Logs-specific cleanup target, later slices could drift into:
- leaving Logs-specific state and orchestration in shared Diagnostics composition
- reintroducing `MainWindow` as a Logs host hub
- letting `Diagnostics Logs` absorb Overview route-entry or index semantics because Logs is a dense troubleshooting surface
- letting shared `DiagnosticsWorkspaceComposition` become the de facto Logs workflow owner

AM114 exists to prevent that drift before runtime extraction begins.

---

## 2) Desired end-state for Diagnostics Logs

The target is:
- `Diagnostics Logs` remains under shared `DiagnosticsWorkspaceComposition`
- shared Diagnostics composition remains responsible only for shared capability-level composition concerns
- a Logs-local seam becomes the long-term home for Logs-specific state, orchestration, composition, and UI coordination
- `MainWindow` remains limited to shell composition and app-level workspace lifetime

The exact Logs-local seam type is not mandated here.
Examples:
- workspace section
- presenter
- coordinator
- viewmodel plus coordinator pair

What matters is ownership. Logs-specific coordination should converge behind a Logs-local seam, not stay in shared Diagnostics composition and not move back to `MainWindow`.

---

## 3) What stays in shared Diagnostics composition

Shared `DiagnosticsWorkspaceComposition` remains responsible for shared capability-level concerns only:
- route-level workspace participation for `Diagnostics`
- shared route activation handoff across `diagnostics.overview` and `diagnostics.logs`
- shared capability composition and wiring that spans more than one Diagnostics child surface
- shared lifetime participation for the long-lived Diagnostics workspace
- shared cross-surface coordination only where it genuinely spans Overview and Logs

This issue does not redefine the shared Diagnostics owner created by AM105.
It narrows what should not remain trapped there once Logs extraction begins.

---

## 4) What becomes Logs-local

Logs-local ownership should include:
- Logs-specific state used for log exploration and troubleshooting
- Logs-specific orchestration for filtering, selection, refresh, and local workflow coordination
- Logs-local interaction boundaries used by the Logs surface
- Logs-local composition and host cleanup expectations
- Logs-specific refresh or reconcile behavior triggered by `diagnostics.logs` activation

This keeps the Logs slice narrow:
- troubleshooting
- log exploration
- local workflow coordination

It does not turn Logs into the shared owner for all Diagnostics behavior.

---

## 5) Logs host cleanup expectation

AM114 is also the cleanup target for Logs-specific composition and host coupling that should not survive runtime extraction.

That means:
- temporary Logs-specific host bridges remain migration scaffolding only
- Logs-specific control exposure or host-driven UI coordination should reduce behind the Logs-local seam
- shared `DiagnosticsWorkspaceComposition` must not become a permanent host proxy for Logs-local workflow concerns

The target is narrower ownership, not a new shared host layer.

---

## 6) What must not happen

The following outcomes are explicitly rejected:
- `Diagnostics Logs` becoming a new shared Diagnostics god object
- shared `DiagnosticsWorkspaceComposition` becoming the Logs workflow owner
- Logs taking ownership of Diagnostics Overview route-entry or index semantics
- direct `MainWindow` injection into Logs views
- shell-owned host growth that treats Logs as a shell surface instead of a Diagnostics-local surface

Logs is a Diagnostics child troubleshooting surface.
It is not the semantic owner of Diagnostics Overview or shared Diagnostics composition.

---

## 7) Behavior that must remain unchanged

The cleanup target must preserve:
- `diagnostics.logs` as the distinct troubleshooting and log-exploration surface for `Diagnostics`
- Diagnostics Overview as the route-entry and index surface for the capability
- approved AL shell, header, navigation, and layout behavior around the Diagnostics surface
- approved AM105 shared Diagnostics composition boundary and long-lived workspace behavior
- approved AM110 boundary that keeps Overview ownership outside Logs cleanup

This remains an ownership cleanup issue, not a redesign issue.

---

## 8) MainWindow and interaction boundary rule

AM114 inherits the AM interaction rules:
- views must not depend on or receive `MainWindow` directly
- bindings/commands-first remains the default interaction model
- narrow abstractions or Diagnostics-local seams are the correct way to cross shell boundaries when needed

For Logs, this means:
- Logs views do not talk to `MainWindow` directly
- shared Diagnostics composition may coordinate with shell-owned behavior only through narrow seams
- Logs-local interaction coordination should not widen shared Diagnostics composition or shell host responsibilities

---

## 9) Workspace lifetime and route activation rule

`Diagnostics Logs` continues to participate in the long-lived Diagnostics workspace.

That means:
- Logs is not recreated on every navigation
- activation of `diagnostics.logs` refreshes or reconciles Logs state within the existing Diagnostics workspace
- route activation remains the trigger for Logs refresh or reconcile behavior where needed

Any future move to per-navigation recreation remains a `TBD` and would require explicit re-contracting.

---

## 10) Sequencing implication

Logs follow-up runtime slices should do this in order:
1. keep Logs under shared `DiagnosticsWorkspaceComposition`
2. introduce the Logs-local seam
3. move Logs-specific state, orchestration, composition, and UI coordination behind that seam
4. reduce temporary Logs-specific host bridges or control exposure behind the seam
5. keep Diagnostics Overview ownership outside the Logs slice

This sequencing keeps AM115-AM120 narrow and avoids reintroducing shared-owner ambiguity.

---

## 11) Non-goals

AM114 does **not** define:
- Diagnostics Overview extraction details
- runtime implementation
- Diagnostics behavior redesign
- performance redesign

---

## 12) Traceability

- `FR-173`
  - Logs stays under shared Diagnostics composition while converging Logs-specific ownership behind a Logs-local seam
- `FR-174`
  - behavior-preserving Logs role with long-lived workspace participation and `diagnostics.logs` route-activation refresh or reconcile behavior
- `FR-175`
  - no direct `MainWindow` view dependency, no shared Diagnostics workflow-owner drift, no Logs absorption of Overview semantics, and explicit non-goals

Mapped acceptance criteria:
- `AC-042`

Aligned milestone context:
- `AM33` refined long-lived capability workspace composition
- `AM105` defined the shared Diagnostics composition cleanup target
- `AM110` defined the narrow Diagnostics Overview cleanup target
- `AM111` through `AM113` kept Overview extraction/runtime/test convergence narrow without redefining Logs ownership
- `AL` remains the behavioral baseline for Diagnostics shell, routing, and view consistency

---

## 13) Open Questions / TBDs

- `TBD:` Exact Logs-local seam type name during implementation.
- `TBD:` Whether Logs route-activation refresh remains entirely route-driven or later needs a narrower explicit local refresh trigger.
- `TBD:` Whether any Logs-specific troubleshooting helper or shell-facing helper still needs a temporary narrow abstraction during runtime extraction.

