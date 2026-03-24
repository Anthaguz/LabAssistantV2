namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployOverviewWorkspaceHost
{
    int QuickDeployDraftCount { get; }

    bool IsLoadingTemplates { get; }

    int AvailableTemplateCount { get; }
}
