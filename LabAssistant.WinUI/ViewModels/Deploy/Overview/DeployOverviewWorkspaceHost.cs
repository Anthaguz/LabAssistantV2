namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployOverviewWorkspaceHost : IDeployOverviewWorkspaceHost
{
    private readonly Func<int> _getQuickDeployDraftCount;
    private readonly Func<bool> _isLoadingTemplates;
    private readonly Func<int> _getAvailableTemplateCount;

    public DeployOverviewWorkspaceHost(
        Func<int> getQuickDeployDraftCount,
        Func<bool> isLoadingTemplates,
        Func<int> getAvailableTemplateCount)
    {
        _getQuickDeployDraftCount = getQuickDeployDraftCount;
        _isLoadingTemplates = isLoadingTemplates;
        _getAvailableTemplateCount = getAvailableTemplateCount;
    }

    public int QuickDeployDraftCount => _getQuickDeployDraftCount();

    public bool IsLoadingTemplates => _isLoadingTemplates();

    public int AvailableTemplateCount => _getAvailableTemplateCount();
}
