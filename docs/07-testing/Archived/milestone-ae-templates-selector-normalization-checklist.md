# Milestone AE Templates Selector and Normalization Checklist

> Historical note: this milestone doc is retained for decision or verification history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.


**Purpose:** Manual closure checklist for Milestone AE Templates UX/data-binding hardening (`#312`, `#313`, `#314`, `#315`, `#316`).

**Scope:** WinUI Templates selector and normalization workflows only (`templates.library`, `templates.editor`) plus contract-level route/context sanity.

---

## 1) Routing and Context Sanity

- [ ] Launch WinUI and open Templates from global navigation.
- [ ] Confirm Templates parent still lands on `templates.library`.
- [ ] Navigate to `templates.editor` and back to `templates.library`.
- [ ] Confirm selected-template context stays coherent across Library <-> Editor navigation.

## 2) Switch Selector Workflow

- [ ] VM switch rows support add/remove.
- [ ] Zero switch rows remains a valid optional state.
- [ ] Duplicate switch selections are blocked with actionable guidance.
- [ ] Non-empty rows require explicit selection before Apply/Save.
- [ ] Empty host-switch inventory shows explicit guidance (no silent failure).

## 3) VHDX Selector Workflow

- [ ] Catalog-backed VHDX selector is available in VM configuration.
- [ ] Selecting a catalog entry updates effective VHD identity guidance.
- [ ] Path-first legacy template state remains loadable/editable.
- [ ] Missing/stale catalog reference surfaces explicit user guidance.

## 4) Normalization and Conflict Workflow

- [ ] Effective source is visible (`vhdxId`, `vhdxSignature`, `vhdPath`, unresolved/legacy).
- [ ] Conflicting identity states are surfaced explicitly.
- [ ] Save is blocked until conflict is resolved by catalog selection.
- [ ] Ambiguous signature matches require user selection before save.

## 5) Save/Reload Compatibility Verification

- [ ] Switch values persist canonically (`switchNames`) with legacy fallback compatibility.
- [ ] VHD identity remains deterministic after save/reload.
- [ ] Legacy/path-first templates remain usable without schema changes.

## 6) Layout and Usability Sanity

- [ ] Compact width: selector controls and action buttons remain reachable.
- [ ] Normal width: status/guidance messaging remains visible and readable.
- [ ] Wide width: editor remains scroll-safe without overflow regression.
- [ ] No hidden critical actions due to layout clipping.

## 7) Recording Template

- **Build/commit tested:** `<commit>`
- **Environment:** `<OS version / WinAppSDK / .NET SDK>`
- **Result by section:**
  - Routing and Context Sanity: `Pass | Fail`
  - Switch Selector Workflow: `Pass | Fail`
  - VHDX Selector Workflow: `Pass | Fail`
  - Normalization and Conflict Workflow: `Pass | Fail`
  - Save/Reload Compatibility Verification: `Pass | Fail`
  - Layout and Usability Sanity: `Pass | Fail`
- **Findings / limitations:**
  - `<item>`

---

## Open Questions / TBDs

- `TBD`: whether AE closure should require a standard test dataset with known vhdxId/vhdxSignature conflict fixtures for repeatable manual verification.

