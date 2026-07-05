# AGENTS.md - LabAssistant.WinUI

Scope-local notes for the WinUI project.
The root `AGENTS.md` still governs; this file only adds UI-specific context.
It is descriptive, not a mandate.
It tells you what the current shell looks like and what pattern to reach for first, so you do not accidentally rebuild an older model.

## Status: navigation is mid-migration

The app is moving from a "every capability view is always instantiated and toggled by `Visibility`" shell toward on-demand capability pages hosted in a `Frame`.
This is partial.
As of now, `Machines`, `Diagnostics`, `Assets`, `Settings`, and `Deploy` are migrated; the remaining capability (`Templates`) still uses the older long-lived-workspace/runtime model.

Because of that, do not assume the whole app already works the frame way, and do not assume the older capabilities are wrong.
When you touch a capability, prefer the migrated pattern below, but treating a not-yet-migrated capability as a bug (rather than as pending work) is itself a mistake.

`ShellNavigationCoordinator` makes this explicit: `IsActiveCapabilityMigrated` is true only for capabilities present in the `capabilityPageTypes` map (`MainWindow.xaml.cs`).
Migrated capabilities navigate the `Frame` to a page and tear it down on leave; non-migrated ones fall back to the older behavior.

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
