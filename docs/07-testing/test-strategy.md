# Test Strategy

**Purpose:** Define what to test and where, so quality is consistent.

## Test Levels
- **Unit tests (primary):**
  - Business logic (deployment orchestration, cleanup, cancellation states, summary builders)
  - Validation logic (templates, catalog, compatibility gates)
  - Data mapping/normalization rules (template load/save/migration)
  - Structured logging and diagnostics export behavior
- **Integration-style tests (with fakes/mocks):**
  - Services and business coordination using mocked Hyper-V/PowerShell/filesystem abstractions
  - JSON persistence and diagnostics bundle generation against temp filesystem artifacts
  - Hyper-V execution-pattern seams (workflow session vs reusable query session vs one-shot admin command path)
- **End-to-end/manual validation (required on Hyper-V host):**
  - Real Hyper-V deployment and failure injection
  - Cleanup/cancellation behavior on real resources
  - Diagnostics export and structured log inspection in a real environment

## Test Projects (current repo)
- `LabAssistant.Business.Tests`
  - deployment pipeline/coordinator behavior
  - cleanup/cancellation/orchestration logic
  - summary builders and compatibility gates
- `LabAssistant.Data.Tests`
  - stores/loaders/JSON persistence and migration behavior
- `LabAssistant.Services.Tests`
  - structured logging foundation and diagnostics export services
- `LabAssistant.UI.Tests`
  - ViewModel workflow behavior and command-state UI logic

## What Must Be Mocked (or faked)
- Hyper-V calls
- PowerShell execution/session behavior
- File system operations where deterministic tests are needed
- Logging sinks when asserting structured event emission

## Tooling
- **Test framework:** xUnit
- **Mocking/fakes:** lightweight in-test fakes and interface-based test doubles (no single mandatory mocking library)
- **Static analysis in tests:** xUnit analyzers (warnings should be reviewed, not ignored by default)
- **Coverage target:** No numeric gate defined yet; prioritize coverage of workflow decisions, validation, cleanup/cancellation, and logging contracts

## Validation Expectations
- PRs should include automated tests for new decision logic and workflow changes.
- Builder authoring/navigation smoke tests should exercise route semantics, draft preservation, accessible navigator targets, and layout safety without requiring Hyper-V.
- New Builder tests should use behavior or surface names instead of issue-numbered class names; renaming existing issue-numbered tests can happen in a later targeted test slice.
- Manual Hyper-V validation remains required for runtime behaviors that cannot be proven with mocks (especially deployment, cleanup, and host-specific failures).
- Build/test commands are documented in `AGENTS.md`; local machine/CI are the source of truth when sandboxed environments are unreliable.

## Open Questions / TBDs
- Whether to define explicit coverage thresholds once CI and test suites stabilize further.
- Whether to add a dedicated manual test checklist doc for Hyper-V smoke/regression validation per milestone.
