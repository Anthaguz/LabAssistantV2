# Documentation Scaffold (LabAssistantV2)

This `/docs` system is the **documentation backbone** for LabAssistantV2.
It exists to:
- reduce rework by making requirements explicit,
- keep agents aligned with your real product goals,
- preserve architecture boundaries and quality bars as the code grows.

---

## 1) What to read first (recommended order)

### A) Product intent (why / what matters)
**/docs/00-overview/**
- `product-vision.md` — why this exists, target users, outcomes, success metrics
- `scope.md` — what is in/out, constraints, assumptions, milestones
- `glossary.md` — shared vocabulary (use this to avoid mismatched terminology)

### B) What must be built (contract)
**/docs/01-requirements/**
- `srs.md` — functional requirements (FRs) and system constraints (what the system shall do)
- `user-stories.md` — user-level backlog (what users need, ordered by priority)
- `acceptance-criteria.md` — implementation contract (testable behavior, edge cases, cleanup policy)
- `non-functional-requirements.md` — quality bars (performance, reliability, security, usability)

### C) How it is built (implementation structure)
**/docs/03-architecture/**
- `architecture.md` — system boundaries, layers, workflow orchestration
- `sequence-diagrams.md` — key flows (deploy lab, template import/export, base disk mapping, cancellation)
- `deployment.md` — runtime prerequisites and environment expectations

### D) UX (how users experience it)
**/docs/02-ux/**
- `user-flows.md` — major workflows (Deploy, Templates, Base Disks, Settings)
- `ui-inventory.md` — screens/components and what each one is responsible for
- `accessibility.md` — minimum UX/accessibility expectations

### E) Everything else (fill only when needed)
- **Data** (`/docs/04-data/`) — template schema, registries, storage layout, migrations if needed
- **Security** (`/docs/06-security/`) — threat model and checklists (especially file/path safety)
- **Testing** (`/docs/07-testing/`) — test strategy and plan; trace to acceptance criteria
- **DevOps/Ops** — CI/CD, release process, runbooks, monitoring, DR (only as maturity requires)

---

## 2) “Where do I put this info?” (fast map)

- **If it’s about what the system does:** `srs.md` (FRs)
- **If it’s about user value / backlog planning:** `user-stories.md`
- **If it’s about exact behavior + edge cases + cleanup:** `acceptance-criteria.md`
- **If it’s about speed/reliability/security expectations:** `non-functional-requirements.md`
- **If it’s about layers/classes/flows:** `architecture.md` + `sequence-diagrams.md`
- **If it’s about UI layout/screens:** `ui-inventory.md` and `user-flows.md`

---

## 3) Change control rules (prevents doc drift)

When updating the product, follow this order:

1) **Acceptance Criteria first** (behavior contract)
2) Update `srs.md` if it adds/removes an FR
3) Update `user-stories.md` if it changes priority/scope or adds backlog items
4) Update `architecture.md` if structure/flow changed
5) Update NFR if quality bars changed

**Rule:** If code changes behavior, docs must be updated in the same PR.

---

## 4) Traceability rule (mandatory for agent work)

Each significant feature must have traceability:
**User Story → Acceptance Criteria → Tests**  
And should map back to:
**FR → AC → Test** (when applicable)

---

## 5) How to handle unknowns

- Write `TBD` directly in the most relevant doc section.
- Add it under **Open Questions / TBDs**.
- Open an issue titled: `TBD: <topic>`.

---

Generated scaffold date: 2026-02-10