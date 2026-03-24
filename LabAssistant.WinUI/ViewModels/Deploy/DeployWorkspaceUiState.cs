namespace LabAssistant.WinUI.ViewModels.Deploy;

internal readonly record struct DeployWorkspaceUiState(
    int QuickDeployDraftCount,
    bool IsLoadingTemplates,
    int AvailableTemplateCount);
