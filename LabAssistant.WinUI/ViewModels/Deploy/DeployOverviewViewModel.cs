using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Deploy;

public partial class DeployOverviewViewModel : ViewModelBase
{
    public event EventHandler? OpenQuickDeployRequested;

    public event EventHandler? OpenFromTemplateRequested;

    [ObservableProperty]
    private string _quickDeploySummary = "Open Quick Deploy to configure VM entries and run deployment.";

    [ObservableProperty]
    private string _fromTemplateSummary = "Open From Template to review a template and resolve readiness blockers before deploy.";

    public string QuickDeploySummaryText => QuickDeploySummary;

    public string FromTemplateSummaryText => FromTemplateSummary;

    public IRelayCommand OpenQuickDeployAction => OpenQuickDeployCommand;

    public IRelayCommand OpenFromTemplateAction => OpenFromTemplateCommand;

    public void RefreshSummary(int quickDeployDraftCount, bool isLoadingTemplates, int availableTemplateCount)
    {
        QuickDeploySummary = quickDeployDraftCount > 0
            ? $"{quickDeployDraftCount} VM entr{(quickDeployDraftCount == 1 ? "y" : "ies")} currently staged in the Quick Deploy draft."
            : "Open Quick Deploy to configure VM entries and run deployment.";

        FromTemplateSummary = isLoadingTemplates
            ? "Template inventory is loading."
            : availableTemplateCount > 0
                ? $"{availableTemplateCount} template{(availableTemplateCount == 1 ? string.Empty : "s")} currently available for From Template."
                : "Open From Template to review a template and resolve readiness blockers before deploy.";
    }

    [RelayCommand]
    private void OpenQuickDeploy() => OpenQuickDeployRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenFromTemplate() => OpenFromTemplateRequested?.Invoke(this, EventArgs.Empty);

    partial void OnQuickDeploySummaryChanged(string value) => OnPropertyChanged(nameof(QuickDeploySummaryText));

    partial void OnFromTemplateSummaryChanged(string value) => OnPropertyChanged(nameof(FromTemplateSummaryText));
}
