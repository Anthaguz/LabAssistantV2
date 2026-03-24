namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployOverviewWorkspaceViewModel
{
    public string QuickDeploySummaryText { get; private set; } = "Open Quick Deploy to configure VM entries and run deployment.";

    public string FromTemplateSummaryText { get; private set; } = "Open From Template to load template inventory and review readiness.";

    public void RefreshSummary(int quickDeployDraftCount, bool isLoadingTemplates, int availableTemplateCount)
    {
        QuickDeploySummaryText = quickDeployDraftCount > 0
            ? $"{quickDeployDraftCount} VM entries currently staged in the Quick Deploy draft."
            : "Open Quick Deploy to configure VM entries and run deployment.";

        FromTemplateSummaryText = isLoadingTemplates
            ? "Template inventory is loading."
            : availableTemplateCount > 0
                ? $"{availableTemplateCount} templates currently available for From Template."
                : "Open From Template to load template inventory and review readiness.";
    }
}
