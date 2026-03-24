# Code Organization

**Purpose:** Define how large seam-heavy files should be organized so extracted WinUI workflows remain navigable and maintainable without relying on historical context.

## Why This Exists
Ownership extraction has reduced the amount of logic left in `MainWindow.xaml.cs` and similar files, but maintainability still depends on predictable structure inside the files that remain.

This standard exists to keep:
- seam-heavy files easy to scan,
- related methods grouped together,
- interfaces and classes easy to locate,
- and lane-local code separated from capability-shared code.

The goal is not to force every file into the same template. The goal is to make organization predictable where complexity actually exists.

## Core Rule
For large seam-heavy files, especially in `LabAssistant.WinUI`, organize members using a stable section order and split files before they become navigation-hostile.

Only use the sections that actually apply to the file.

Do not add empty placeholders.

## Canonical Section Order For Seam-Heavy Files
When the file contains these kinds of members, use this order:
1. fields
2. constructor
3. public API
4. interface implementation
5. event wiring / event handlers
6. core workflow methods
7. UI/application helpers
8. small private parsing/format helpers

This is a navigation rule, not a requirement to invent sections that do not belong in a file.

## Type Placement Rules
- Use one top-level class per file.
- Use one top-level interface per file.
- Do not keep multiple unrelated top-level seam contracts in one file just because they were created together during extraction.

Small private helpers may stay local only when they are truly private to that file and separating them would make the code harder to follow rather than easier.

## Capability Folder Rules
Within `LabAssistant.WinUI/ViewModels/<Capability>/`:
- capability-shared files stay at the capability root,
- lane-local files should live in PascalCase folders named for the intended route/workflow surface.

Examples:
- `Overview/`
- `QuickDeploy/`
- `FromTemplate/`
- `Logs/`

Use the intended current route/workflow name, not a legacy transitional name.

## File Size Guidance
Files should be as short as practically possible while keeping cohesive logic together.

For seam-heavy files, prefer splitting before they become navigation-hostile.

As a practical guide, aim to stay below roughly 300-500 lines when the responsibility can be separated cleanly without scattering tightly coupled logic.

This is not a hard line-count rule. Cohesion still matters.

## `#region` Rule
Do not use `#region`.

The repository standard is to solve navigability through:
- correct extraction boundaries,
- stable member ordering,
- one type per file,
- and clear file placement.

`#region` should not be used as a substitute for splitting or grouping code correctly.

## Where This Rule Applies Most Strongly
This standard is especially important in:
- workspace composition classes,
- workflow controllers,
- shell bridges,
- host interfaces,
- and large viewmodels in `LabAssistant.WinUI`.

Other files do not need to be forced into a fake seam-heavy template if it does not fit their shape. The important rule there is still:
- keep one top-level type per file,
- keep related methods together,
- and avoid oversized files.

## Rollout Strategy
Apply this standard incrementally:
- first in touched files,
- then in active cleanup/refactor slices,
- and only later as opportunistic maintenance elsewhere.

Do not use this standard as justification for broad repo-wide file churn in unrelated work.

## Open Questions / TBDs
- TBD: whether a later cleanup pass should normalize existing WinUI capability folder layouts to this standard once the current post-AM extraction cleanup chain stabilizes.
