# GUI Action Map - Templates (Draft)

> Historical note: this migration/reference doc is retained for code-reading or migration history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.

**Purpose:** Document what happens behind the current Templates-related UI actions (list/details/editor) and the hidden/background behaviors that must be preserved during future UI migration and Templates workflow consolidation.

**Status:** Historical migration/reference doc (original Phase 3B of UI migration prep). Not authoritative for new work.

**Scope:** Current template lifecycle surfaces:
- `TemplatesPage`
- `TemplateDetailsPage`
- `TemplateEditorPage`
- `TemplateVmDetailPage`
- shared `VmConfigPanel` behavior in template context
- related dialogs used in template workflows

**Related:**
- `docs/02-ux/Archived/capability-taxonomy.md`
- `docs/02-ux/Archived/navigation-ia-draft.md`
- `docs/02-ux/Archived/current-ui-capability-audit.md`
- `docs/03-architecture/Archived/gui-action-map.deploy.md`

---

## 1. Why Templates Is Mapped Next

Templates is the biggest current IA fragmentation point:
- Template list/details exist in one top-level page
- Template editing exists in a separate top-level page
- Both surfaces perform overlapping validation and missing-VHDX resolution behaviors
- Users can end up opening template files manually from disk to edit them

This action map is required before consolidating Templates into one future workflow.

---

## 2. Current Templates Workflow Surfaces

## 2.1 `TemplatesPage` (template list)

**Files**
- `LabAssistant/Views/TemplatesPage.xaml`
- `LabAssistant/Views/TemplatesPage.xaml.cs`

**Primary role**
- Load templates from configured template folder
- Display template list
- Publish load warnings/errors to global error feed
- Navigate to template details on double-click

## 2.2 `TemplateDetailsPage` (template inspection + VHDX mapping)

**Files**
- `LabAssistant/Views/TemplateDetailsPage.xaml`
- `LabAssistant/Views/TemplateDetailsPage.xaml.cs`

**ViewModel**
- `TemplateDetailsViewModel`

**Primary role**
- Show template metadata and VM rows
- Select/map VHDX for selected VM
- Resolve missing VHDX references via dialog
- Apply persisted selections to template instance

## 2.3 `TemplateEditorPage` (authoring/editor workflow)

**Files**
- `LabAssistant/Views/TemplateEditorPage.xaml`
- `LabAssistant/Views/TemplateEditorPage.xaml.cs`

**ViewModel**
- `TemplateEditorViewModel`

**Primary role**
- Open/save/save-as template JSON
- Add/remove/edit template VMs
- Template-level and VM-level validation before save
- Missing VHDX resolution before save/load
- Template VM detail editing via `TemplateVmDetailPage`

## 2.4 `TemplateVmDetailPage` + shared `VmConfigPanel`

**Files**
- `LabAssistant/Views/TemplateVmDetailPage.xaml`
- `LabAssistant/Views/TemplateVmDetailPage.xaml.cs`
- `LabAssistant/ViewModels/TemplateVmConfigContext.cs`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml(.cs)` [shared]

**Primary role**
- Reuse the shared VM config UI in template-editing context
- Bind to `VmTemplate` via `TemplateVmConfigContext`
- Support VHDX catalog selection/import and guest-step toggles in template context

---

## 3. Core ViewModel / Service Paths (Templates)

## 3.1 Editor workflow hub
- `LabAssistant/ViewModels/TemplateEditorViewModel.cs`

Owns:
- template in-memory model (`LabTemplate`)
- VM list (`VmTemplates`)
- template load/save via `ILabTemplateStore`
- template validation and missing-VHDX resolution
- switch list loading/default application
- structured logging for template import/save operations
- VM issue tracking for editor validation panel

## 3.2 Template details workflow hub
- `LabAssistant/ViewModels/TemplateDetailsViewModel.cs`

Owns:
- catalog load access for details page
- persisted VHDX selection application/saving
- missing VHDX resolution dialog state
- compatibility warning generation for substituted VHDX mappings

## 3.3 Data and business services used in templates workflows
- `ILabTemplateStore` (load/save/import/export of templates)
- `IVhdxCatalogStore` / `CatalogService` (catalog load/save + validation)
- `TemplateValidationService`
- `MissingVhdxResolutionService`
- `TemplateSelectionService`
- `VirtualSwitchProvider` (editor-side switch availability/defaults)
- `IStructuredLogger` (TemplateImported / TemplateSaved events)

---

## 4. User Actions -> Code Paths (TemplatesPage + TemplateDetailsPage)

## 4.1 Action: Open `Templates` top-level page (page load)

**UI surface**
- `TemplatesPage`

**Entry path**
- `MainWindow.TemplatesButton_Click()` -> `MainFrame.Navigate(new Views.TemplatesPage())`
- `TemplatesPage` constructor -> `LoadTemplates()`

**What `LoadTemplates()` does**
- Reads template folder from `IAppSettingsStore`
- Loads catalog (`IVhdxCatalogStore.Load`)
- Loads templates from folder (`ILabTemplateStore.LoadFromFolder(folder, catalogItems)`)
- Collects:
  - catalog errors
  - template load errors
  - template compatibility warnings
- Publishes warnings/errors to global `IErrorFeedService`
- Binds `_templates` list to `TemplatesListBox`
- Toggles empty-state text visibility

**Important behavior**
- Loading templates depends on catalog availability because compatibility/missing-reference checks are part of load behavior
- Warnings are surfaced globally via error feed, not only in-page

**Migration preservation notes**
- "Open Templates" is not just list rendering; it performs a real load+compatibility pass and emits warnings

---

## 4.2 Action: `Reload Templates`

**UI control**
- `TemplatesPage.xaml` button -> `ReloadTemplates_Click`

**Entry point**
- `TemplatesPage.ReloadTemplates_Click()` -> `LoadTemplates()`

**Behavior**
- Same as page-load behavior above

**Migration preservation notes**
- Reload should preserve warning/error feed publication behavior (or intentionally replace it with a better equivalent)

---

## 4.3 Action: Open template details (double-click template in list)

**UI control**
- `TemplatesListBox` double-click

**Entry point**
- `TemplatesPage.TemplatesListBox_MouseDoubleClick(...)`
- Navigates to `TemplateDetailsPage(template)`

**What happens in `TemplateDetailsPage` constructor**
- Initializes `TemplateDetailsViewModel`
- Captures template key and applies persisted selections (`TemplateDetailsViewModel.Initialize`)
- Sets visible template metadata text
- Calls `ResolveMissingVhdxSelections()` automatically
- Binds VM list to `template.VmTemplates`

**Automatic/background behavior**
- Missing VHDX resolution prompt may appear immediately on page open if unresolved references exist

**Migration preservation notes**
- Template details page has implicit auto-resolution behavior on load; this must be preserved or redesigned intentionally

---

## 4.4 Action: `Select VHDX for Selected VM` (TemplateDetails)

**UI control**
- `TemplateDetailsPage.xaml` button -> `SelectVhdx_Click`

**Entry point**
- `TemplateDetailsPage.SelectVhdx_Click(...)`

**What it does**
1. Requires a selected VM in `VmListView`
2. Loads catalog items via `TemplateDetailsViewModel.LoadCatalogItems()`
3. Shows catalog errors (message box) if any
4. Blocks if no catalog items
5. Opens `VhdxSelectorDialog`
6. On selection:
   - updates `VmTemplate` VHDX fields
   - persists mapping via `TemplateSelectionService.SaveSelection(...)`
   - refreshes VM list display

**Capability crossing**
- `Templates` workflow invoking `Assets` (catalog) selection

**Migration preservation notes**
- Persisted per-template selection behavior is hidden but important; not just a visual mapping change

---

## 4.5 Automatic behavior: Missing VHDX resolution prompt in TemplateDetails

**Entry point**
- `TemplateDetailsPage.ResolveMissingVhdxSelections()` called during page construction

**What it does**
- Builds missing-reference dialog state via `TemplateDetailsViewModel.ResolveMissingVhdxForDialog(...)`
- Optionally shows catalog errors
- Opens `MissingVhdxResolutionDialog`
- If user confirms:
  - applies resolved selections via `TemplateDetailsViewModel.ApplyResolvedMissingVhdx(...)`
  - persists selections through `TemplateSelectionService`
- If user cancels:
  - shows warning message box explaining save/deploy impact

**Migration preservation notes**
- This is a critical background workflow that can be easy to lose in a redesigned details page
- It should remain a guided repair path in future Templates UX

---

## 5. User Actions -> Code Paths (TemplateEditorPage)

## 5.1 Action: Open `Template Editor` top-level page

**UI surface**
- `TemplateEditorPage`

**Entry path**
- `MainWindow.TemplateEditorButton_Click()` -> `MainFrame.Navigate(new Views.TemplateEditorPage())`
- Page constructor resolves `TemplateEditorViewModel` and sets `DataContext`

**Automatic/background behavior in editor ViewModel**
- Initializes canonical template defaults on a new `LabTemplate`
- Syncs `VmTemplates` collection to template model
- Begins switch discovery (`LoadAvailableSwitchesAsync`) if provider exists
  - updates `AvailableSwitches`
  - applies default switch to template VMs lacking switch assignment

**Migration preservation notes**
- Editor startup includes non-trivial model normalization and switch loading behavior

---

## 5.2 Action: `Open` template file (editor)

**UI control**
- `TemplateEditorPage.xaml` -> `OpenTemplate_Click`

**Entry point**
- `TemplateEditorPage.OpenTemplate_Click(...)`

**Flow**
1. OpenFileDialog rooted at template directory (`settings.TemplateFolder` or app default)
2. `_viewModel.LoadFromFile(filePath)`
3. Publish template compatibility warnings from `_viewModel.LastLoadWarnings` to global error feed
4. Call `ResolveMissingVhdxReferences()` automatically

**`TemplateEditorViewModel.LoadFromFile()` behavior**
- Loads template via `ILabTemplateStore.LoadFromFile`
- Ensures canonical defaults (schema fields)
- Captures store warnings (`LastLoadWarnings`)
- Replaces active `Template` and `VmTemplates`
- Syncs VM list (including vmId backfill if missing)
- Applies default switch to template VMs if available
- Sets `CurrentTemplatePath`
- Emits structured log event `TemplateImported` with `operationId`

**Migration preservation notes**
- Editor "Open" is not just file load; it includes compatibility warning publication, canonical normalization, missing-VHDX resolution, and structured logging

---

## 5.3 Action: `Save` template (editor)

**UI control**
- `TemplateEditorPage.xaml` -> `SaveTemplate_Click`

**Entry point**
- `TemplateEditorPage.SaveTemplate_Click(...)`

**Behavior**
- If no current path: delegates to `Save As`
- Runs `ValidateBeforeSave()`
- On pass: `_viewModel.SaveToFile(CurrentTemplatePath)`
- On exception: shows message box

**`ValidateBeforeSave()` behavior (page-level orchestration)**
- Builds composite save validation state (`BuildSaveValidationState`)
- Blocks on:
  - VM field issues
  - missing VHDX references
  - template validation errors
- Shows warnings message box for template validation warnings (non-blocking)
- Opens/updates validation panel for VM-level issues

**`TemplateEditorViewModel.SaveToFile()` behavior**
- Ensures canonical template defaults
- Syncs VM list into template model (including vmId backfill/clone behavior)
- Saves via `ILabTemplateStore.SaveToFile`
- Updates `CurrentTemplatePath`
- Emits structured log event `TemplateSaved`

**Migration preservation notes**
- Save behavior is a composition of:
  - UI-level multi-source validation orchestration
  - ViewModel normalization/sync
  - Data-layer persistence
- Validation panel and missing-reference gating are key behaviors, not just visuals

---

## 5.4 Action: `Save As` template (editor)

**UI control**
- `TemplateEditorPage.xaml` -> `SaveTemplateAs_Click`

**Entry point**
- `TemplateEditorPage.SaveTemplateAs_Click(...)`

**Flow**
1. SaveFileDialog rooted at template directory
2. Default filename derived from:
   - current path, or
   - template id, or
   - `lab-template.json`
3. Runs `ValidateBeforeSave()`
4. Calls `_viewModel.SaveToFile(newPath)`

**Migration preservation notes**
- Preserve default file-name selection logic; it reflects canonical template id usage and user convenience

---

## 5.5 Action: Add VM (editor)

**UI control**
- `TemplateEditorPage.xaml` -> `AddVm_Click`

**Entry point**
- `TemplateEditorPage.AddVm_Click(...)` -> `_viewModel.AddVm()`

**Behavior**
- Adds a new `VmTemplate` with default values
- Uses editor default switch (if available)
- UI list updates via bound `VmTemplates`

**Migration preservation notes**
- Default values and switch auto-application are behavior, not just placeholder UI state

---

## 5.6 Action: Remove VM (editor)

**UI control**
- VM row trash button in editor list -> `RemoveVm_Click`

**Entry point**
- `TemplateEditorPage.RemoveVm_Click(...)`

**Behavior**
- Requires a valid row (`VmTemplate` in button tag)
- Shows confirmation message box
- Removes VM via `_viewModel.RemoveVm(...)`

**Migration preservation notes**
- Standard confirm-delete behavior; low complexity but part of unified template CRUD flow to preserve

---

## 5.7 Action: Open VM detail (editor)

**UI control**
- VM row button in editor list -> `OpenVmDetail_Click`

**Entry point**
- `TemplateEditorPage.OpenVmDetail_Click(...)`

**Behavior**
- Updates validation panel state
- Stores list scroll position
- Hides VM list panel, shows `VmDetailFrame`
- Navigates to `TemplateVmDetailPage`
- Supports pending field focus callback (from validation item click flow)

**Migration preservation notes**
- Current editor is effectively a two-state workspace (VM list panel vs VM detail panel)
- This can be redesigned, but the workflow state preservation (selected VM, scroll/focus) matters

---

## 5.8 Action: Validation panel interactions (editor)

**UI controls**
- Validation item click -> `ValidationItem_Click`
- Validation collapse toggle -> `ValidationToggle_Click`
- Validation filter box -> `ValidationFilter_TextChanged`

**Behavior**
- `UpdateValidationPanel()` rebuilds VM issue display list from ViewModel validation issues
- Filter is applied in ViewModel (`FilterVmValidationDisplayItems`)
- Clicking a validation item navigates directly to the target VM detail and focuses the relevant field

**Why this matters**
- This is a high-value UX behavior for template editing
- It is easy to lose in a redesign if validation is treated as only a message list

**Migration preservation notes**
- Preserve "click validation issue -> open VM -> focus field" behavior (even if implemented differently)

---

## 5.9 Automatic behavior: Missing VHDX resolution in editor after load

**Entry point**
- `TemplateEditorPage.OpenTemplate_Click()` -> `ResolveMissingVhdxReferences()`

**Behavior**
- Similar to TemplateDetails flow:
  - build missing-reference dialog state
  - show catalog errors if present
  - open `MissingVhdxResolutionDialog`
  - apply resolved mappings via `_viewModel.ApplyResolvedMissingVhdx(...)`
- On cancel, warns user that save/deploy will be blocked until resolved

**Migration preservation notes**
- This repair workflow appears in both TemplateDetails and TemplateEditor; future unified Templates UX should consolidate it but not lose the guided path

---

## 6. Shared `VmConfigPanel` in Template Context (Important Cross-Capability Behavior)

`TemplateVmDetailPage` uses `VmConfigPanel` with `TemplateVmConfigContext`.

### What this means in practice
- The same UI component used for Deploy VM config also edits template VM config
- In template context, bindings target `VmTemplate` fields and guest-step config objects
- `VmConfigPanel` still allows embedded VHDX catalog actions:
  - select VHDX
  - import VHDX

### Migration implication
- The future Templates workflow should preserve in-context asset actions (VHDX catalog shortcuts) even after Assets is consolidated
- Shared VM config behavior should be treated as a reusable component contract, not copied UI

---

## 7. Hidden / Background Behaviors (Templates)

These are easy to miss if only the visible pages are mapped.

## 7.1 Canonical template normalization on load/save

Handled in `TemplateEditorViewModel` and `ILabTemplateStore` paths:
- schema defaults ensured
- vmId backfill/immutability behavior preserved
- canonical save shape maintained

This is a key Milestone Q behavior and must survive UI migration.

## 7.2 Switch discovery/default application in editor

Editor loads available switches asynchronously and applies a default switch to template VMs missing one.

This is not a visible "button action" but it changes user-visible state.

## 7.3 Structured logging for template import/save

Template editor actions emit structured events:
- `TemplateImported`
- `TemplateSaved`

Migration note:
- Preserve operation logging even if dialogs/pages are consolidated.

## 7.4 Global error feed publication from templates flows

Both `TemplatesPage` and `TemplateEditorPage` publish warnings/errors to the global error feed.

Migration note:
- Template workflows currently use both local message boxes and global feed. This may be redesigned later, but behavior visibility must remain intentional.

---

## 8. Current Fragmentation Summary (Templates)

The current Templates capability is split into two overlapping user journeys:

### Journey A: Templates list/details path
- Open `Templates`
- Browse templates
- Inspect template details
- Resolve/select VHDX mappings
- (No integrated edit transition)

### Journey B: Template editor path
- Open `Template Editor`
- Open file from disk
- Edit/save template
- Resolve missing VHDX mappings

### User impact
- The user can inspect templates from the library but may need filesystem access to edit them
- Missing-VHDX resolution exists in multiple paths with similar behavior

### Migration target implication
- Future `Templates` area must unify:
  - library
  - details
  - editor
  - validation
  - missing-reference resolution

---

## 9. Migration Preservation Checklist (Templates)

Preserve these behaviors explicitly during Templates workflow consolidation:

- Opening Templates library loads templates with catalog-aware compatibility processing
- Template load warnings/errors surface (currently via error feed and/or dialogs)
- Template details auto-triggers missing VHDX resolution when needed
- VHDX selection in template details persists per-template selections
- Template editor open performs canonical normalization + warning capture + missing-VHDX resolution + structured logging
- Save/save-as performs composite validation gating before persistence
- Validation panel supports click-to-VM and field focus navigation
- Shared `VmConfigPanel` behavior works in template context
- Embedded VHDX catalog actions remain available in template VM editing
- Template import/save structured logging remains intact

---

## 10. Open Questions / TBDs (Templates Action Map)

- In the future unified Templates area, should selecting a template open details first or editor-first (with details as a tab/panel)?
- Should missing VHDX resolution run automatically on template open in the future UI, or move to an explicit prompt/action with a visible unresolved-state banner?
- Should template compatibility warnings remain global error-feed items, or become local template status badges/panels in the unified Templates workflow?
