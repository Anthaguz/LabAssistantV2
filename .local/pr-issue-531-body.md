## What
- Remove the remaining shared Diagnostics-specific UI coordination from `MainWindow`
- Move shared Diagnostics log loading, filter management, selection details, and support export/open-location coordination into `DiagnosticsWorkspaceComposition`
- Update direct-impact AL/AM structural tests for the shared Diagnostics UI coordination boundary

## Why
- AM106 introduced a Diagnostics composition owner and AM107 reduced the shared host bridge, but shared Diagnostics-local UI coordination still lived in the shell
- This slice finishes the shared Diagnostics coordination cleanup so `MainWindow` stays shell-owned while the Diagnostics composition layer becomes the practical local coordination home

## Scope
- Runtime refactor only for the shared Diagnostics layer
- Touched files:
  - `LabAssistant.WinUI/MainWindow.xaml.cs`
  - `LabAssistant.WinUI/ViewModels/Diagnostics/DiagnosticsWorkspaceComposition.cs`
  - `LabAssistant.UI.Tests/Tests/MilestoneALScenarioMatrixTests.cs`
  - `LabAssistant.UI.Tests/Tests/MilestoneAMScenarioMatrixTests.cs`
- Shared Diagnostics UI coordination cleanup only
- No Overview or Logs runtime extraction yet
- No Diagnostics behavior redesign

## Out of scope
- No shared Diagnostics AM test convergence beyond direct impact
- No Diagnostics Overview runtime extraction yet
- No Diagnostics Logs runtime extraction yet
- No domain or business semantic changes
- No WPF changes
- No performance redesign

## Validation
- `dotnet build .\LabAssistant.sln -c Debug`
- `dotnet test .\LabAssistant.UI.Tests\LabAssistant.UI.Tests.csproj -c Debug --no-build`

## Traceability
- Issue: #531
- Closes #531
- Milestone: [Milestone AM - WinUI Composition and Workspace Extraction](https://github.com/Anthaguz/LabAssistantV2/milestone/39)
