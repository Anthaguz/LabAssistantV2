# Sequence Diagrams (Text-Based)

> Historical note: this placeholder diagram doc is retained for reference only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.

**Purpose:** Show how components interact for key flows.

**Status:** Historical placeholder doc. Not authoritative for new work.

## How to fill this
Use a simple bullet sequence:
1. Actor → Component: action
2. Component → Component: call
3. etc.

---

## Deploy Lab (example format)
1. User → UI: Click “Deploy”
2. UI → Business: DeployLab(request)
3. Business → Services: ValidateEnvironment()
4. Services → Hyper-V/PowerShell: CreateVM(...)
5. Services → Business: result
6. Business → UI: progress updates
7. UI → User: Completed

## Template Import
TBD

## Open Questions / TBDs
- TBD
