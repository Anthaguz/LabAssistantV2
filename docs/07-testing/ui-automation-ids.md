# UI AutomationId Convention

**Purpose:** Define how interactive controls in the WinUI app are made addressable so the UI automation harness (`LabAssistant.UITesting`) can drive them reliably.
This is the contract the `automation-id-coverage` audit enforces.

## Why this exists

The harness drives the real app through Microsoft UI Automation (UIA), the same accessibility layer screen readers use.
To click a button or type into a field, it must first find that element in the live UIA tree.
The only stable, language-independent way to find a specific control is its **AutomationId**.
Finding controls by their visible `Name` is brittle: names change with copy edits, localization, and data binding, and several controls can share a name.
So every authored, interactive control the harness may drive must carry a stable AutomationId.

## How AutomationId is set in WinUI

A control's effective AutomationId comes from one of two places, in priority order:

1. An explicit `AutomationProperties.AutomationId="..."` attached property.
2. The control's `x:Name`, which WinUI promotes to the AutomationId when no explicit id is set.

FlaUI (and any UIA client) reads the single effective value, so from the harness's point of view an `x:Name` and an explicit `AutomationProperties.AutomationId` are equivalent.
If a control has neither, its AutomationId is empty and the harness cannot address it by id.

This is why the app already works with the harness despite having no `AutomationProperties.AutomationId` in XAML today: shell controls such as `HamburgerButton`, `CurrentRouteTextBlock`, and `QuickDeployStartButton` are reached through their `x:Name` fallback.

### Which one to use

- Prefer a plain `x:Name` when the control already needs a code-behind or binding handle, or when a short local name reads well.
  The `x:Name` doubles as the AutomationId at no extra cost.
- Use an explicit `AutomationProperties.AutomationId` when the control does not otherwise need an `x:Name`, or when the desired stable id differs from a name the code-behind wants.
- Do not set both to different values on the same control; the explicit AutomationId wins and the mismatch is confusing.

## Naming convention

Use PascalCase in the form `{Capability}{Role}{Type}`.

- **Capability** prefix keeps ids unique across screens: `Machines`, `QuickDeploy`, `Templates`, `Assets`, `Diagnostics`, `Settings`, or `Shell` for chrome.
- **Role** is what the control does or targets: `Start`, `Rename`, `BaseDisk`, `VmName`, `AddSwitch`.
- **Type** is the control kind: `Button`, `TextBox` (for `Edit`), `ComboBox`, `CheckBox`, `RadioButton`, `Slider`, `Link`, `SplitButton`, `Spinner`.

Examples that match controls already in the app:

- `QuickDeployStartButton`
- `QuickDeployVmNameTextBox`
- `QuickDeployBaseDiskComboBox`
- `QuickDeployAddSwitchButton`

Drop the `Type` suffix only when the role already ends in that word, to avoid stutter (for example `MachinesConsoleButton`, not `MachinesConsoleButtonButton`).

## What needs an id, and what does not

**Give a stable id to** every authored, individually operable control the harness clicks or types into:
buttons, checkboxes, radio buttons, combo boxes, text/number inputs (`Edit`), sliders, hyperlinks, split buttons, and spinners.
These are the fixed chrome of a screen and the `automation-id-coverage` audit reports any that lack an id.

**Do not force ids onto** dynamic data items in a collection - the individual rows of a VM list, template list, or catalog.
Those are generated per data item, so a single static id is meaningless and a per-item id is not unique.
Address a specific row by scoping into its container (select the row, then find controls within it) rather than by a top-level id.

**Per-row action buttons** inside a data template (for example a Rename button on each machine row) are a middle case.
Give them a stable id that is constant across rows (for example `MachineRowRenameButton`); the harness disambiguates by first scoping to the selected row and then finding that id within it.

## The coverage audit

`LabAssistant.UITesting` ships an `AutomationIdCoverageScenario` (`Requirements = None`, so it needs no Hyper-V).
For each capability it navigates the shell, walks the live UIA subtree under the `CapabilityFrame` content host, and reports every top-most interactive control that lacks a stable AutomationId.
Composite controls are collapsed to a single row (the edit and button inside a ComboBox are treated as parts of the ComboBox, not separate gaps), and controls that surface on multiple tabs are counted once.

Run it on demand:

```
dotnet run --project LabAssistant.UITesting -- coverage
```

It also runs as part of the standard suite (`... -- run`).

Output lands under `LabAssistant.UITesting/runs/<timestamp>/`:

- A per-capability `Info` finding with a coverage number, for example `QuickDeploy: 12/15 interactive controls carry a stable AutomationId (80% covered)`.
- A `Warning` finding per missing control, naming its type and visible name and proposing an id, for example `Suggested id: MachinesResolveButton`.
- A screenshot of each capability that has gaps.

Coverage gaps are **advisory**: they are recorded as `Warning`/`Info`, never `Error`/`Crash`, so the nightly run surfaces them without failing.
The goal is to trend each capability toward 100% as screens are built and edited.

### Fixing a finding

Add the suggested id to the control in XAML, then re-run the audit to confirm the gap is closed:

```xml
<Button x:Name="MachinesResolveButton" Content="Resolve suggestions" ... />
```

or, when the control does not need an `x:Name`:

```xml
<Button AutomationProperties.AutomationId="MachinesResolveButton" Content="Resolve suggestions" ... />
```

## Scope and limits

The audit reflects the controls currently rendered when it walks a capability.
It activates each tab it can find under the content host, but controls that appear only after a specific interaction (a selection-driven right panel, an expanded flyout, a dialog) are not yet covered.
Growing that reach - driving the app into more states before auditing - is a natural next increment for the harness.
