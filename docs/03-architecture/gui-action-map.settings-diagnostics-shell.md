# GUI Action Map - Settings, Diagnostics, and Shell (Draft)

**Purpose:** Document current settings, logs/diagnostics, and global shell behaviors (including the error feed overlay) so they can be preserved during future UI migration and navigation redesign.

**Status:** Draft (Phase 3D of UI migration prep).

**Scope:** Current settings/diagnostics/shell-related surfaces:
- `SettingsPage`
- `LogsPage` (transitional deployment-debug surface)
- `MainWindow` shell navigation + global error feed/snack UI
- Diagnostics export capability status (service exists, no dedicated GUI entry point yet)

**Related:**
- `docs/02-ux/capability-taxonomy.md`
- `docs/02-ux/navigation-ia-draft.md`
- `docs/02-ux/current-ui-capability-audit.md`
- `docs/03-architecture/gui-action-map.deploy.md`
- `docs/03-architecture/gui-action-map.templates.md`
- `docs/03-architecture/gui-action-map.assets.md`

---

## 1. Why These Surfaces Are Mapped Together

Current UI mixes three different concerns:
- **Settings** (persisted application and deployment policy configuration)
- **Logs** (a legacy/transitional deployment-debug page)
- **Global shell notifications** (error feed summary + snack cards)

Meanwhile, **Diagnostics** (structured logs + diagnostics export) is a real capability in Services but is not represented as a cohesive user-facing area yet.

This map documents:
- what these surfaces actually do today
- which behaviors are shell-global vs page-local
- what should be preserved when the future `Diagnostics` and `Settings` areas are redesigned

---

## 2. Current UI Surfaces (Settings / Diagnostics / Shell)

## 2.1 `SettingsPage` (direct settings CRUD, code-behind driven)

**Files**
- `LabAssistant/Views/SettingsPage.xaml`
- `LabAssistant/Views/SettingsPage.xaml.cs`

**Primary role**
- edit persisted app settings and deployment policy flags
- change template/log folder paths
- save/reload settings

**Important note**
- This page currently uses **code-behind** directly against `IAppSettingsStore` (not a ViewModel).

## 2.2 `LogsPage` (transitional deployment-debug surface)

**Files**
- `LabAssistant/Views/LogsPage.xaml`
- `LabAssistant/Views/LogsPage.xaml.cs`

**DataContext**
- `DeploymentViewModel` (same singleton used by `DeployPage`)

**Primary role (current reality)**
- alternate/legacy deployment interaction surface bound to deploy state
- basic VM list/edit + deploy + log list

**Important note**
- This is **not** a full diagnostics center.
- It reuses deployment workflow state and commands rather than surfacing canonical diagnostics export/structured log features.

## 2.3 `MainWindow` shell (navigation + global error feed/snacks)

**Files**
- `LabAssistant/MainWindow.xaml`
- `LabAssistant/MainWindow.xaml.cs`

**Primary role**
- top-level page navigation (`Frame`)
- global error feed summary and active snack cards
- snack actions (`View details`, `Dismiss`)

## 2.4 Diagnostics export capability (no dedicated GUI entry point yet)

**Service path (implemented)**
- `LabAssistant.Services/Diagnostics/IDiagnosticsExportService.cs`
- `LabAssistant.Services/Diagnostics/DiagnosticsExportService.cs`

**Current UI status**
- diagnostics export is implemented in Services, but there is **no dedicated page/button flow** in the current shell for a user-facing Diagnostics area.
- this is a known IA mismatch and migration target (`Diagnostics` category in future IA).

---

## 3. Core Code Paths (Settings / Diagnostics / Shell)

## 3.1 Settings persistence and side effects

**Interfaces / storage**
- `LabAssistant.Models/Configuration/IAppSettingsStore.cs`
- `LabAssistant.Data/Configuration/AppSettingsStore.cs`

**Behavior that matters for migration**
- `LoadOrCreate()` validates settings, creates missing directories, and ensures catalog file exists
- `Save()` writes `settings.json`
- settings include:
  - paths (templates, logs, VM base, differencing disk base, catalog)
  - deployment failure policy (`PerVmFailFast`, `StopAllOnAnyVmFailure`)
  - non-blocking optional steps list

**Migration implication**
- A future Settings UX can move to a modern UI pattern, but it must preserve these side effects (especially path initialization and persisted policy flags).

## 3.2 Global error feed service (shell-level notifications)

**Files**
- `LabAssistant/ViewModels/IErrorFeedService.cs`
- `LabAssistant/ViewModels/ErrorFeedService.cs`
- `LabAssistant/ViewModels/ErrorFeedItem.cs`

**Behavior**
- `Publish(...)` inserts new item into:
  - `ActiveItems` (snack cards)
  - `RecentItems` (summary list)
- auto-expiry timer removes non-hovered active items after expiration
- `Dismiss(id)` removes an active item manually
- `RecentItems` is capped at 25
- `TotalErrorCount` increments on publish and drives shell summary

**Publishers (examples)**
- `DeploymentViewModel` (deploy/readiness/runtime failures)
- `TemplatesPage` (template load errors / compatibility warnings)
- `TemplateEditorPage` (template compatibility warnings)

**Migration implication**
- This is a **cross-cutting shell behavior**, not just a page widget.
- Future Diagnostics and page-specific error panels may coexist, but shell-level issue surfacing should be treated as a separate design decision.

## 3.3 Transitional Logs page = shared deploy state

**Key file**
- `LabAssistant/Views/LogsPage.xaml.cs`

**Behavior**
- `LogsPage` resolves the same singleton `DeploymentViewModel` used by `DeployPage`
- actions on `LogsPage` operate on shared deployment state/VM entries/logs

**Migration implication**
- `LogsPage` is not independent; it is an alternate deployment surface.
- Future UI migration should likely reclassify it under `Diagnostics` or retire it after `#215` (structured log viewer) and richer Deploy diagnostics views exist.

---

## 4. User Actions -> Code Paths

## 4.1 Shell navigation buttons (`MainWindow`)

**UI**
- Sidebar buttons in `LabAssistant/MainWindow.xaml`

**Code**
- `DeployButton_Click` -> `MainFrame.Navigate(new Views.DeployPage())`
- `TemplatesButton_Click` -> `MainFrame.Navigate(new Views.TemplatesPage())`
- `TemplateEditorButton_Click` -> `MainFrame.Navigate(new Views.TemplateEditorPage())`
- `VhdxCatalogButton_Click` -> `MainFrame.Navigate(new Views.VhdxCatalogPage())`
- `SwitchesButton_Click` -> `MainFrame.Navigate(new Views.SwitchesPage())`
- `SettingsButton_Click` -> `MainFrame.Navigate(new Views.SettingsPage())`
- `LogsButton_Click` -> `MainFrame.Navigate(new Views.LogsPage())`

**Migration note**
- Current shell nav reflects implementation fragments, not the future entity-based IA.
- This mapping is primarily useful to preserve command targets during migration while regrouping navigation.

## 4.2 Global snack action: `View details`

**UI**
- `View details` button in active snack card template (`MainWindow.xaml`)

**Code**
- `SnackViewDetails_Click(...)` in `MainWindow.xaml.cs`
- Reads `ErrorFeedItem.ViewDetailsAction`
- Executes callback if present

**Behavior**
- Shell does not know the destination page/details target; it delegates to the publisher-provided callback.

**Migration implication**
- This callback-based indirection is a useful preservation point for future shell redesign.
- A future UI can change visuals while keeping the “publisher defines deep-link action” behavior.

## 4.3 Global snack action: `Dismiss`

**UI**
- `Dismiss` button in active snack card template

**Code**
- `SnackDismiss_Click(...)` -> `_errorFeed.Dismiss(item.Id)`

**Behavior**
- Removes item from `ActiveItems`
- `RecentItems` history remains intact

## 4.4 Settings action: Change Template Folder

**UI**
- `Change Template Folder` button on `SettingsPage`

**Code path**
- `SettingsPage.ChangeTemplateFolder_Click(...)`
  - opens `FolderBrowserDialog`
  - on selection:
    - updates `_settingsStore.Settings.TemplateFolder`
    - updates textbox

**Important behavior**
- This action modifies the in-memory settings object but does **not** persist by itself.
- User must click `Save Settings` to persist.

## 4.5 Settings action: Change Logs Path

**UI**
- `Change Logs Path` button on `SettingsPage`

**Code path**
- `SettingsPage.ChangeLogsPath_Click(...)`
  - opens `FolderBrowserDialog`
  - on selection:
    - updates `_settingsStore.Settings.LogFolder`
    - updates textbox
    - calls `DebugLogger.SetLogFolder(dialog.SelectedPath)` immediately

**Important behavior**
- Debug logger path changes immediately in-process (for new debug log writes)
- persisted settings still require `Save Settings`
- structured logging path behavior depends on services initialization/runtime path usage and should be preserved intentionally during migration

## 4.6 Settings action: Save Settings

**UI**
- `Save Settings` button on `SettingsPage`

**Code path**
- `SettingsPage.SaveSettings_Click(...)`
  - reads checkbox states
  - writes:
    - `PerVmFailFast`
    - `StopAllOnAnyVmFailure`
    - `NonBlockingOptionalSteps`
  - `_settingsStore.Save()`
  - shows success message box

**Behavioral note**
- `NonBlockingOptionalSteps` uses deployment step keys (`DeploymentStepKeys.*`) and influences runtime policy for future deploy operations.

## 4.7 Settings action: Reload Settings

**UI**
- `Reload Settings` button on `SettingsPage`

**Code path**
- `SettingsPage.ReloadSettings_Click(...)`
  - `_settingsStore.Reload()`
  - `LoadSettingsIntoUI()`

**Behavior**
- Discards unsaved in-page edits and reloads persisted values/defaults.

## 4.8 Logs page actions (transitional deployment actions)

**UI**
- `Add VM`, `Deploy`, and per-VM edit controls on `LogsPage`

**DataContext**
- shared `DeploymentViewModel`

**Code path**
- WPF commands bind directly into `DeploymentViewModel` commands/properties already mapped in:
  - `docs/03-architecture/gui-action-map.deploy.md`

**Migration note**
- `LogsPage` should not be treated as a separate deployment implementation.
- It is a second UI shell over the same deploy behavior and state.

---

## 5. Automatic / Background Behaviors (Migration Risks)

## 5.1 Error feed auto-expiry and hover pause

**Implementation**
- `ErrorFeedService` starts a `DispatcherTimer` (1s tick)
- Expired items are removed only when `IsHovered == false`
- Hover state is controlled by shell mouse enter/leave handlers in `MainWindow.xaml.cs`

**Migration risk**
- A visual redesign can easily remove hover semantics and accidentally make transient errors disappear too quickly (or never expire).

## 5.2 Recent error summary and active snack lists are separate views over shared events

**Behavior**
- `Publish()` inserts same item into both `ActiveItems` and `RecentItems`
- Active list is transient; Recent list is capped history (25)
- `TotalErrorCount` is cumulative

**Migration risk**
- Future Diagnostics area may subsume some of this, but the shell summary and active snack behavior should be treated as distinct UX responsibilities.

## 5.3 Settings store reload/create side effects

**Behavior in `AppSettingsStore.LoadOrCreate()`**
- validates empty/invalid settings values
- creates missing configured directories
- ensures catalog file exists

**Migration risk**
- A future Settings UI that “previews” or batches changes must preserve when these side effects occur (on save vs on app startup vs on path change).

## 5.4 Logs page shares singleton deployment state

**Behavior**
- `LogsPage` and `DeployPage` read/write the same `DeploymentViewModel`
- changes in one surface can affect the other if both are visited in the same app session

**Migration risk**
- If `LogsPage` is repurposed/replaced, make sure state ownership remains explicit and not duplicated.

## 5.5 Diagnostics export capability is implemented but not discoverable in current shell

**Behavior**
- Diagnostics export exists in Services and is testable/service-callable
- current shell provides no dedicated route/button for it

**Migration opportunity**
- Future `Diagnostics` area should expose diagnostics export explicitly without coupling it to the legacy `LogsPage`.

---

## 6. Current Mismatch Themes (Settings / Diagnostics / Shell)

## 6.1 Diagnostics capability is stronger than its UI representation
- Canonical diagnostics path (structured JSONL + export bundle) exists
- Top-level UI exposes `LogsPage`, which is a deployment-centric debug page, not a diagnostics center

## 6.2 Shell error feed acts like a global alert system, but is not categorized in IA
- It is present and useful
- It has no formal place in the current navigation model
- It overlaps conceptually with future Diagnostics and contextual page validation/error panels

## 6.3 Settings mixes app paths, deployment policy, and optional-step behavior
- Functionally correct today
- Future IA may split Settings into subcategories, but persistence semantics must remain stable

---

## 7. Migration Preservation Checklist (Settings / Diagnostics / Shell)

Preserve these behaviors during UI migration:
- `Settings` edits must still support:
  - template folder path
  - logs path
  - deployment failure policy
  - non-blocking optional-step policy
- `Save Settings` vs `Reload Settings` semantics (persist vs discard unsaved edits)
- Debug logger path update behavior when logs folder changes (or a consciously redesigned equivalent)
- Global shell error feed capabilities:
  - publish transient active items
  - dismiss
  - hover pause for expiry (or a consciously redesigned equivalent timing policy)
  - recent history summary
  - view-details callback deep-link behavior
- `LogsPage` transitional behavior must either:
  - remain intentionally available as a deployment-debug surface, or
  - be replaced with explicit `Diagnostics` + deploy diagnostics views without losing access to deployment logs/state insights
- Diagnostics export must become a discoverable user action in the future `Diagnostics` area

---

## 8. Open Questions / Migration Design Prompts

- Should the global shell error feed remain a shell-level element in the future UI, or be reduced in favor of page-local error/readiness surfaces plus a Diagnostics history view?
- Should `LogsPage` be preserved temporarily as a "Deploy Debug" subview during migration, or retired once `Diagnostics` and deploy outcome/log drill-down are improved?
- Should settings be split into multiple subpages (for example General / Paths / Deploy Policies / Diagnostics), or kept in one consolidated Settings surface?
- Where should diagnostics export live in the future `Diagnostics` area:
  - primary action on the Diagnostics landing page
  - context action in Deploy outcomes
  - both?
