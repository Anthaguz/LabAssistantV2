# Shared Shell Spike Fixture (Y2)

Both framework prototypes must implement the same interaction fixture to keep comparison fair.

## Capability Scope (Hamburger/Menu mode)

- Capabilities list:
  - Machines
  - Deploy
  - Templates
  - Assets
  - Diagnostics
  - Settings
- Hamburger toggles left pane between:
  - Capability Scope
  - Context Scope
- Selecting a capability:
  - updates active capability
  - returns left pane to Context Scope

## Context Scope (Normal left panel mode)

Per selected capability, show contextual items (stub data acceptable):
- Deploy: VM entries + readiness summary shortcut
- Templates: template list + sections
- Assets: asset categories
- Diagnostics: recent operations / filters
- Settings: categories
- Machines: machine list

## Shell-level error feed placement concept

- Global shell region (not page-local) with:
  - compact summary count
  - list of active issues/snacks concept
  - dismiss action
- Stub issues acceptable

## Dense content placeholder region

Main content area should simulate workflow density with:
- compact readiness summary
- constrained readiness details list
- VM cards/list area
- log panel placeholder area

This is a layout/ergonomics fixture only, not real Deploy implementation.

## Minimum state interactions

- Toggle capability/context scope
- Select capability
- Toggle dense content mode (normal/many entries)
- Add/dismiss shell issue

## Evidence capture expectations

For each framework prototype, record:
- what was easy/awkward
- how state ownership felt
- layout density observations
- any blockers/tooling issues
