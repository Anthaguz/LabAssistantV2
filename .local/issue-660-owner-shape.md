## Issue 660 owner shape

- `DeployOnTheFlyWorkspaceOwner` is the single Quick Deploy-local owner.
- The owner holds:
  - `DeployOnTheFlyWorkspaceViewModel`
  - `DeployOnTheFlyWorkspaceController`
  - `DeployOnTheFlyWorkspaceComposition`
  - Quick Deploy route-activation orchestration
  - Quick Deploy reference-data refresh/application
  - Quick Deploy template snapshot / resolve-suggestion / template-editor handoff
  - Quick Deploy lane panel intent
- `DeployOnTheFlyWorkspaceComposition` is reduced to view composition and view-state application only.
- `IDeployOnTheFlyCompositionHost` and `AttachComposition()` are removed.
- `MainWindow` keeps shell route switching, shell panel container state, and narrow bridge wiring only.
- Shared Deploy helpers:
  - `DeployReferenceDataService`
  - `DeployResolveSuggestionsService`
  - `DeployResultsPanelCoordinator`
