# Milestone AM Templates Extraction Checklist

## Purpose

Manual runtime verification checklist for Milestone AM (`Templates extraction closure`).

Use this checklist after AM automation passes to confirm the final shared Templates workspace model, Library lane, and Editor lane remain usable on a real Windows machine without regressing long-lived workspace behavior.

## Scope

- Shared Templates workspace ownership and Library-first routing
- Library search/filter/list/selection/action sanity
- Editor explicit-entry, document/header, and VM workflow sanity
- Shell/title ownership sanity for Templates
- Long-lived Templates workspace and Library/Editor navigation sanity

## Out of Scope

- Runtime redesign
- Business/domain semantic changes
- WPF behavior
- New CI/workflow behavior
- Logging redesign

## Preconditions

- Build under test is from the target AM77 closure commit/branch
- WinUI app launches successfully
- At least one template exists if Library open/edit/export/delete flows are to be exercised fully
- Template reference data is available enough to exercise Editor VM-entry interactions if practical

## 1. Templates Library Default-Route Behavior

- Open parent `Templates` from the global navigation
- Confirm it resolves to `templates.library`
- Confirm the shell title/description represent the capability context rather than a child view owning a duplicate page banner
- Confirm Library appears as the default Templates surface when entering the capability from outside Templates

## 2. Library Search / Filter / List / Selection / Action Sanity

- Stay on `Templates > Library`
- Confirm the search box, list, status text, and primary Library actions are present without a redundant nested page banner
- Enter a search/filter term and confirm the Library inventory/status updates coherently
- Clear or change the search term and confirm the list/state refresh remains coherent
- Select an existing template and confirm selection-dependent actions update coherently
- If practical, exercise open/create/import/export/delete entry points and confirm each remains reachable from the Library workflow surface

## 3. Editor Explicit-Entry Behavior Sanity

- Enter the editor explicitly by opening an existing template from Library
- Confirm navigation moves to `templates.editor`
- Return to Library, then enter the editor by creating a new template if practical
- Confirm the editor remains an explicit workflow-state destination rather than the default Templates landing surface

## 4. Editor Document / Header Behavior Sanity

- Stay on `Templates > Editor`
- Confirm template name, description, context, template id, file path, and VM count surfaces render coherently
- Edit the template name and description fields and confirm header/document state updates coherently
- Confirm save, save-as, and validate actions remain visible and stateful without requiring shell-owned wiring

## 5. Editor VM List / Selection / Draft / Apply / Save / Save-As / Validate Sanity

- With an editor document open, confirm the VM list surface is present and selectable
- Select an existing VM entry and confirm the draft editor loads the matching VM state coherently
- Edit draft fields and confirm apply-state affordances update coherently
- Apply VM changes and confirm status/selection/document state remain coherent
- Trigger `Save`, `Save As`, and `Validate` as practical and confirm each path remains reachable with explicit status feedback

## 6. Editor VM-Entry Add / Remove Behavior Sanity

- With an editor document open, trigger `Add VM`
- Confirm a new VM-entry workflow is created and selection/draft state moves to the new entry coherently
- If a removable VM entry is available, trigger `Remove VM`
- Confirm the remove path requires confirmation before the VM entry is removed

## 7. Library / Editor Navigation and Long-Lived Workspace Sanity

- From `Templates > Library`, open a template in the editor
- Return to `Templates > Library`
- Re-enter `Templates > Editor`
- Confirm the capability behaves like one long-lived Templates workspace rather than a newly constructed surface on each route change
- Confirm route activation visibly refreshes/reconciles the active lane without collapsing Templates workflow/state back into `MainWindow`

## 8. Shell / Title Ownership Sanity for Templates

- Navigate across:
  - `Templates > Library`
  - `Templates > Editor`
- Confirm the shell remains the owner of page-level title/description context
- Confirm child views do not reintroduce large duplicate page-title bands
- Confirm local operational section headings still exist where needed for Library and Editor clarity

## Result Record

- Commit tested:
- Environment:
  - Windows version:
  - App build type:
  - Hyper-V available:
  - Templates library scenario available:
  - Templates editor VM-entry scenario available:
- Section 1 Templates Library Default-Route Behavior: Pass / Fail
- Section 2 Library Search / Filter / List / Selection / Action Sanity: Pass / Fail
- Section 3 Editor Explicit-Entry Behavior Sanity: Pass / Fail
- Section 4 Editor Document / Header Behavior Sanity: Pass / Fail
- Section 5 Editor VM List / Selection / Draft / Apply / Save / Save-As / Validate Sanity: Pass / Fail
- Section 6 Editor VM-Entry Add / Remove Behavior Sanity: Pass / Fail
- Section 7 Library / Editor Navigation and Long-Lived Workspace Sanity: Pass / Fail
- Section 8 Shell / Title Ownership Sanity for Templates: Pass / Fail
- Findings / follow-up observations:

## Open Questions / TBDs

- Whether a future closure pass should add a dedicated compact-width exploratory checklist for Templates Library and Editor surfaces beyond the current extraction-closure scope
- Whether later milestones should add one cross-capability workspace-lifetime checklist once more extracted capabilities share the same long-lived workspace model
