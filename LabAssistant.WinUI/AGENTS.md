# AGENTS.md - LabAssistant.WinUI

Scope-local notes for the WinUI project.
The root `AGENTS.md` still governs; this file only adds UI-specific context.
It is descriptive, not a mandate.
It tells you what the current shell looks like and what pattern to reach for first, so you do not accidentally rebuild an older model.

## Status: navigation migration complete

The app has moved from a "every capability view is always instantiated and toggled by `Visibility`" shell to on-demand capability pages hosted in a `Frame`.
As of now, every capability - `Machines`, `Diagnostics`, `Assets`, `Settings`, `Deploy`, and `Templates` - is page-hosted through its capability page in the `Frame`.
All three `Templates` subviews (Library, Editor, and the 6-step Builder wizard) are on full `x:Bind` MVVM: transient view models resolved from DI, reached through injected host seams, with no delegate-bag workspace composition/controller glue remaining.

No capability is left running inline on the old always-instantiated shell.
Prefer the migrated pattern below for any new capability.

There is no non-migrated fallback left: `ShellNavigationCoordinator` always resolves the active capability to a page type from the `capabilityPageTypes` map (`MainWindow.xaml.cs`) and navigates the `Frame` to it, tearing down the outgoing page on leave.
A capability added without a `capabilityPageTypes` entry fails loudly with an `InvalidOperationException` rather than falling back to legacy inline content.
The pure route-resolution and state-transition logic lives in `Shell/ShellNavigationState`, kept free of WinUI types so it is unit-testable without a runtime; the coordinator is a thin adapter that applies its transitions to the real `Frame`/`NavigationView`/header text.

## Reference implementations

If you are adding a new capability, or migrating an existing one, read these first and follow their shape unless you have a concrete reason not to.

- `Views/Machines/MachinesPage` and its subview view models.
- `Views/Diagnostics/DiagnosticsPage`, `ViewModels/Diagnostics/*`, and the rewritten `Views/Diagnostics/*View`.

Diagnostics is the smaller and more self-contained of the two, so it is usually the easier reference to copy from.

## The migrated capability pattern (recommended, not required)

- A capability is a `Page` (for example `DiagnosticsPage`) that the shell frame navigates to on route entry and tears down on leave.
  The page is constructed on demand, so its state starts fresh each visit.
- `MainWindow` owns shell chrome only: routing, the header title/description, the nav pane, and the right panel.
  Capability workflow state and cross-subview coordination live on the page, not in `MainWindow`.
  If you find yourself adding capability orchestration to `MainWindow`, that is the signal to push it onto the page instead.
- The page implements `ICapabilityPage.ShowSubview(routeKey)` so the shell can switch subviews without rebuilding the page.
  Any richer page-to-shell contract (for example the Diagnostics "show logs / report support status" seam) is expressed as a small internal interface the page implements, not as event wiring in `MainWindow`.
- Register a migrated capability by adding `["<key>"] = typeof(<Capability>Page)` to `capabilityPageTypes` in `MainWindow.xaml.cs`.
  That single entry is what flips the coordinator into frame mode for that capability.

## View and view model conventions

- A view resolves its own view model from DI in its constructor, sets `DataContext`, calls `InitializeComponent()`, and hooks `Loaded -> InitializeAsync` / `Unloaded -> CleanupAsync`.
- Views bind with `x:Bind` against a typed `ViewModel` property, including `TwoWay` for editable inputs and `x:DataType` on item templates.
  Prefer this over classic `Binding` and over code-behind that forwards control events.
- A page hosts its subviews in XAML by `x:Name` and reaches them via their `ViewModel` property.
  The page is the right place for the one or two genuine cross-subview links (for example keeping an overview summary in sync with a logs load), by observing the other view model rather than pushing state through the shell.
- View models derive from `ViewModelBase` and use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`).
  Gate commands with `CanExecute` tied to busy/idle state instead of manually toggling control `IsEnabled`.

## Docs governance

Requirement clauses (FR/AC/TC) sometimes name specific implementation classes.
When a migration renames or removes those classes, honor the behavioral intent and reconcile the doc to match the code, per the root `AGENTS.md` ("code is reality, docs are intent").
Do not preserve a dead class name in a doc just because a clause quoted it.
