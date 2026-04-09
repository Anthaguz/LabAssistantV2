# GUI Action Map - Assets (Draft)

> Historical note: this migration/reference doc is retained for code-reading or migration history only and is not authoritative for new work. Current authority lives in AGENTS.md, SRS, Acceptance Criteria, and the canonical docs named in docs/00-overview/authoritative-doc-map.md.

**Purpose:** Document current asset-related UI actions and code paths, including both the dedicated VHDX Catalog page and embedded asset workflows used inside Deploy/Templates. This supports future consolidation into the `Assets` area during UI migration.

**Status:** Historical migration/reference doc (original Phase 3C of UI migration prep). Not authoritative for new work.

**Scope:** Current asset-related surfaces:
- `VhdxCatalogPage`
- `VhdxCatalogEditDialog`
- `VhdxSelectorDialog`
- `MissingVhdxResolutionDialog`
- embedded VHDX catalog actions in `VmConfigPanel`
- `SwitchesPage` (current placeholder)

**Related:**
- `docs/02-ux/Archived/capability-taxonomy.md`
- `docs/02-ux/Archived/navigation-ia-draft.md`
- `docs/02-ux/Archived/current-ui-capability-audit.md`
- `docs/03-architecture/Archived/gui-action-map.deploy.md`
- `docs/03-architecture/Archived/gui-action-map.templates.md`

---

## 1. Why Assets Needs an Action Map

Assets is currently split between:
- a dedicated VHDX catalog CRUD page
- a placeholder Switches page
- embedded asset actions inside Deploy/Templates workflows
- missing-reference repair dialogs that also import/select catalog entries

The future `Assets` area needs to centralize asset management without removing fast in-flow shortcuts. This map identifies what must be preserved.

---

## 2. Current Asset-Related UI Surfaces

## 2.1 `VhdxCatalogPage` (dedicated VHDX catalog CRUD)

**Files**
- `LabAssistant/Views/VhdxCatalogPage.xaml`
- `LabAssistant/Views/VhdxCatalogPage.xaml.cs`

**ViewModel**
- `VhdxCatalogPageViewModel`

**Primary role**
- list catalog entries
- add/edit/delete entries
- reload catalog from disk

## 2.2 `VhdxCatalogEditDialog` (add/edit catalog item)

**Files**
- `LabAssistant/Views/VhdxCatalogEditDialog.xaml`
- `LabAssistant/Views/VhdxCatalogEditDialog.xaml.cs`

**Primary role**
- collect/edit VHDX metadata and path
- validate item shape
- compute size/signature metadata

## 2.3 `VhdxSelectorDialog` (select existing catalog item)

**Files**
- `LabAssistant/Views/VhdxSelectorDialog.xaml`
- `LabAssistant/Views/VhdxSelectorDialog.xaml.cs`

**Primary role**
- choose a catalog item for a VM/template mapping

## 2.4 `MissingVhdxResolutionDialog` (repair missing template references)

**Files**
- `LabAssistant/Views/MissingVhdxResolutionDialog.xaml`
- `LabAssistant/Views/MissingVhdxResolutionDialog.xaml.cs`

**ViewModel**
- `MissingVhdxResolutionDialogViewModel`

**Primary role**
- resolve missing template VHDX references by selecting replacement catalog entries
- import new catalog items inline if needed

## 2.5 Embedded VHDX catalog actions in `VmConfigPanel`

**Files**
- `LabAssistant/Views/Controls/VmConfigPanel.xaml`
- `LabAssistant/Views/Controls/VmConfigPanel.xaml.cs`

**Primary role (inside Deploy/Templates workflows)**
- select VHDX from catalog
- import VHDX into catalog
- apply selected entry to current VM config context

## 2.6 `SwitchesPage` (current placeholder)

**Files**
- `LabAssistant/Views/SwitchesPage.xaml`
- `LabAssistant/Views/SwitchesPage.xaml.cs`

**Current role**
- placeholder surface only (title text, no CRUD behavior)

---

## 3. Core ViewModel / Business / Data Paths (Assets)

## 3.1 Dedicated VHDX catalog page model
- `LabAssistant/ViewModels/VhdxCatalogPageViewModel.cs`

Owns:
- in-memory `Items` list (`ObservableCollection<VhdxCatalogItem>`)
- add/update/delete operations with rollback-on-save-failure behavior
- catalog load/reload

## 3.2 Business catalog orchestration
- `LabAssistant.Business/Catalog/CatalogService.cs`

Owns:
- catalog load/save orchestration
- structured logging (`CatalogLoaded`, `CatalogSaved`)
- VHDX integrity validation on save (via `IVhdxIntegrityValidator`)
- scoped validation via `itemsToValidate` (important post-`#219` behavior)

## 3.3 Data persistence
- `IVhdxCatalogStore` implementations (load/save catalog JSON)

## 3.4 Shared dialogs / repair workflows
- `MissingVhdxResolutionDialogViewModel`
- `CatalogService`
- `VhdxCatalogEditDialog` / `VhdxSelectorDialog`

---

## 4. User Actions -> Code Paths (Dedicated VHDX Catalog Page)

## 4.1 Action: Open `VHDX Catalog` page

**UI entry**
- `MainWindow.VhdxCatalogButton_Click()` -> `MainFrame.Navigate(new Views.VhdxCatalogPage())`

**Page constructor behavior**
- Resolves `VhdxCatalogPageViewModel`
- Binds `CatalogListView.ItemsSource` to `_viewModel.Items`
- Shows current catalog path text
- Calls `LoadCatalog()`

**`LoadCatalog()` behavior**
- Refreshes displayed catalog path
- Calls `_viewModel.LoadCatalog()`
- Shows message box if errors returned

**Migration preservation notes**
- Opening the page performs a real catalog load, not just UI binding
- Error visibility on load matters (currently message box)

---

## 4.2 Action: `Add` catalog item

**UI control**
- `VhdxCatalogPage.xaml` button -> `Add_Click`

**Entry point**
- `VhdxCatalogPage.Add_Click(...)`

**Flow**
1. Open `VhdxCatalogEditDialog`
2. On dialog success, call `_viewModel.AddItem(dialog.Item)`
3. Show warning message box on failure

**`VhdxCatalogPageViewModel.AddItem(...)` behavior**
- Checks duplicate ID in current list
- Adds item to `Items`
- Saves catalog via `CatalogService.SaveCatalog(Items, [item])`
  - validates only the newly added item (subset save validation)
- Rolls back in-memory add if save fails

**Downstream `CatalogService.SaveCatalog(...)` behavior**
- Runs catalog VHDX integrity validation (if validator configured)
- Emits structured log `CatalogSaved`
- Persists via data store on success

**Migration preservation notes**
- Add is not just local list mutation; it includes immediate persistence + integrity validation + rollback behavior

---

## 4.3 Action: `Edit` catalog item

**UI control**
- `VhdxCatalogPage.xaml` button -> `Edit_Click`

**Entry point**
- `VhdxCatalogPage.Edit_Click(...)`

**Flow**
1. Requires selected item; otherwise message box
2. Opens `VhdxCatalogEditDialog` with cloned item copy (`CloneItem`)
3. On dialog success:
   - `_viewModel.UpdateItem(selected, dialog.Item)`
   - refreshes list view items
   - shows warning message box on failure

**`VhdxCatalogPageViewModel.UpdateItem(...)` behavior**
- Checks duplicate ID (excluding selected item)
- Applies edits to existing item object
- Saves catalog validating only edited item (`SaveCatalog([selected])`)
- Rolls back edited fields if save fails

**Migration preservation notes**
- Edit has transactional/rollback semantics at ViewModel level
- Preserve "edit copy -> apply -> save -> rollback on error" behavior

---

## 4.4 Action: `Delete` catalog item

**UI control**
- `VhdxCatalogPage.xaml` button -> `Delete_Click`

**Entry point**
- `VhdxCatalogPage.Delete_Click(...)`

**Flow**
1. Requires selected item; otherwise message box
2. Confirmation dialog
3. `_viewModel.DeleteItem(selected)`
4. Warning message box on save failure

**`VhdxCatalogPageViewModel.DeleteItem(...)` behavior**
- Removes item from in-memory list
- Saves catalog with empty validation subset (`SaveCatalog([])`) [no VHDX integrity validation needed]
- Re-adds item if save fails

**Migration preservation notes**
- Delete is catalog-entry delete only (not file delete)
- Save is still immediate and rollback-aware

---

## 4.5 Action: `Reload`

**UI control**
- `VhdxCatalogPage.xaml` button -> `Reload_Click`

**Entry point**
- `VhdxCatalogPage.Reload_Click(...)` -> `LoadCatalog()`

**Behavior**
- Re-loads catalog and refreshes list/path display

---

## 5. User Actions -> Code Paths (VHDX Catalog Edit Dialog)

## 5.1 Action: `Browse...`

**UI control**
- `VhdxCatalogEditDialog.xaml` button -> `Browse_Click`

**Behavior**
- OpenFileDialog filtered to `*.vhdx`
- Sets `PathBox.Text`

## 5.2 Action: `OK`

**UI control**
- `VhdxCatalogEditDialog.xaml` button -> `Ok_Click`

**Behavior**
- `TryUpdateItem(out errorMessage)`:
  - parses generation
  - generates ID if missing (new item)
  - copies UI values into `Item`
  - computes file size if accessible
  - computes signature (`VhdxSignature.Build`)
  - validates with `VhdxCatalogValidator`
- On validation failure: message box, dialog stays open
- On success: `DialogResult = true`

**Migration preservation notes**
- Dialog does local item-shape validation and metadata enrichment before page-level add/edit save path

---

## 6. User Actions -> Code Paths (Embedded Asset Actions in `VmConfigPanel`)

These are critical because they let users manage/select VHDX assets without leaving Deploy/Templates workflows.

## 6.1 Action: `Select VHDX`

**UI control**
- `VmConfigPanel.xaml` button -> `SelectVhdx_Click`

**Used in**
- Deploy VM detail (`DeployVmConfigContext`)
- Template VM detail (`TemplateVmConfigContext`)

**Behavior**
1. Load catalog via `CatalogService.LoadCatalog()`
2. If empty, prompt user to add catalog item first
3. Open `VhdxSelectorDialog`
4. Apply selected catalog entry to current VM config context:
   - `VhdxId`
   - `BaseVhdPath` / `VhdPath` (context-dependent property mapping)
   - `VhdxSignature`
5. Update selected VHDX display and validation indicators

**Migration preservation notes**
- This is a high-value in-flow shortcut; future Assets consolidation should not force users to leave their current task just to select a disk

---

## 6.2 Action: `Import VHDX` (from `VmConfigPanel`)

**UI control**
- `VmConfigPanel.xaml` button -> `AddVhdx_Click`

**Behavior**
1. Open `VhdxCatalogEditDialog`
2. Reload catalog
3. Check duplicate path
   - if duplicate path exists: prompt to reuse existing entry
4. Check duplicate ID
5. Temporarily add new item to local catalog list
6. Save catalog via `CatalogService.SaveCatalog(_catalogItems, [dialog.Item])`
   - subset validation only
7. Roll back local list if save fails
8. Apply the selected/imported item to current VM config context

**Migration preservation notes**
- This is more than "open asset dialog":
  - duplicate path reuse flow
  - duplicate ID validation
  - immediate catalog persistence
  - automatic selection into current VM config

---

## 7. User Actions -> Code Paths (Missing VHDX Resolution Dialog)

This dialog is a major cross-capability repair workflow used by Templates details/editor paths.

## 7.1 Dialog initialization (automatic behavior)

**Entry path**
- Opened from `TemplateDetailsPage` or `TemplateEditorPage`

**Constructor behavior**
- Creates `MissingVhdxResolutionDialogViewModel`
- Loads catalog via `_viewModel.LoadCatalog()`
- Shows catalog errors if present
- Updates resolve button enabled state and empty-catalog hint

**Migration preservation notes**
- This dialog performs immediate catalog load and state calculation; not passive UI

---

## 7.2 Action: Select replacements in grid

**UI surface**
- DataGrid rows bound to `MissingVhdxResolutionItem`
- ComboBox bound to `CatalogOptions`

**Behavior**
- ViewModel tracks row selections
- `CanResolve` is computed from whether all items have selections
- Resolve button enablement updates via `StateChanged`

## 7.3 Action: `Import VHDX` (from missing-resolution dialog)

**UI control**
- Dialog button -> `ImportVhdx_Click`

**Behavior**
1. Open `VhdxCatalogEditDialog`
2. Call `_viewModel.ImportCatalogItem(dialog.Item)`
3. ViewModel checks duplicate path and duplicate ID
4. Save catalog via `CatalogService.SaveCatalog(_catalogItems, [item])`
5. Refresh catalog options and resolve state
6. Show errors if import/save fails

**Migration preservation notes**
- This is another embedded asset action path that must survive Templates consolidation and Assets centralization

## 7.4 Action: `Resolve`

**UI control**
- Dialog button -> `Resolve_Click`

**Behavior**
- Only succeeds if `CanResolve == true`
- Dialog returns selected replacements
- Caller applies mappings to template VM entries

---

## 8. Switches Page (Current Placeholder Surface)

## 8.1 Current state

**UI surface**
- `SwitchesPage.xaml`

**Behavior**
- Displays only a title (`Virtual Switch Manager`)
- No CRUD/listing/actions yet

**IA implication**
- Current top-level `Switches` overstates capability maturity
- In future IA this should move under `Assets` and remain clearly labeled as partial/placeholder until implemented

---

## 9. Hidden / Background Behaviors (Assets)

## 9.1 Catalog save integrity validation (high-value)

`CatalogService.SaveCatalog(...)` can validate VHDX integrity before saving catalog changes:
- missing/unreadable/invalid VHDX can block catalog save
- validation is scoped by `itemsToValidate` where provided

This is a major post-Milestone U behavior and should be preserved.

## 9.2 Structured logging for catalog operations

`CatalogService` emits structured logs:
- `CatalogLoaded`
- `CatalogSaved`

These logs include operationId and catalog path/item/error counts.

## 9.3 Subset validation behavior (important UX/performance behavior)

Catalog save callers now often pass only the changed items for validation (instead of validating whole catalog), especially in embedded flows.

This behavior was introduced to prevent unrelated invalid catalog entries from breaking single-item edits/imports.

---

## 10. Migration Preservation Checklist (Assets)

Preserve these behaviors explicitly during Assets consolidation:

- VHDX Catalog CRUD remains immediate-persist + rollback-on-failure
- Catalog item add/edit validates shape locally in dialog and integrity on save (via catalog service)
- Embedded `Select VHDX` and `Import VHDX` shortcuts remain available inside Deploy/Templates VM config flows
- Duplicate path reuse prompt behavior in `VmConfigPanel` import flow is preserved (or replaced intentionally with equivalent UX)
- Missing VHDX resolution dialog supports inline catalog import and replacement selection
- Catalog save subset validation behavior is preserved (single-item edits/imports should not validate unrelated entries)
- Catalog operations continue emitting structured logs
- `Switches` stays clearly labeled as partial/placeholder until real functionality exists

---

## 11. Open Questions / TBDs (Assets Action Map)

- When Assets is consolidated, should the VHDX catalog and switch management share one page shell with tabs/segments, or separate subpages under Assets?
- Should embedded asset actions (select/import VHDX) open modal dialogs (current pattern) or a side panel/inline picker in the future UI?
- Should the missing-VHDX resolution flow eventually become a reusable "asset repair" experience shared by Templates and Deploy readiness flows?
- When switch CRUD is implemented, how much should be exposed in Deploy/Templates inline flows vs requiring navigation to Assets?
