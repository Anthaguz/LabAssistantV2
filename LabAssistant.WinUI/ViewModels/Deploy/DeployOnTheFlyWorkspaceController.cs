using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployOnTheFlyWorkspaceControllerHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true);

    void SetActionStatus(string statusText);

    Task EvaluateReadinessAsync(DeploymentPreflightMode mode);

    LabTemplate BuildTemplate();

    void BeginDeployWorkflow();

    void PrepareDeployExecution(MultiVmDeploymentContext context);

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void SetDeployBlocked();

    void ApplyDeploySummary(DeploymentOutcomeSummary summary);

    void SetDeployFailed(string errorMessage);

    void FinalizeDeployWorkflow();
}

internal sealed class DeployOnTheFlyWorkspaceController
{
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace;
    private readonly IDeployOnTheFlyWorkspaceControllerHost _host;

    public DeployOnTheFlyWorkspaceController(
        DeployOnTheFlyWorkspaceViewModel workspace,
        IDeployOnTheFlyWorkspaceControllerHost host)
    {
        _workspace = workspace;
        _host = host;
    }

    public async Task StartDeployAsync()
    {
        if (!_host.TryApplyVmFields(showSuccessStatus: false) && _workspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_workspace.VmEntryCount == 0)
        {
            _host.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        _workspace.BeginStarting();
        _host.BeginDeployWorkflow();
        try
        {
            await _host.EvaluateReadinessAsync(DeploymentPreflightMode.Full);
            if (_workspace.HasBlockingFailures)
            {
                _host.SetDeployBlocked();
                return;
            }

            var template = _host.BuildTemplate();
            var deployContext = DeployContextBuilder.Build(
                template,
                _host.DeploymentSettings,
                _host.LoadCatalogItems(),
                _host.AvailableSwitches);
            _host.PrepareDeployExecution(deployContext.MultiVmContext);
            var summary = await _host.DeployAllAsync(deployContext.MultiVmContext);
            _host.ApplyDeploySummary(summary);
        }
        catch (Exception ex)
        {
            _host.SetDeployFailed(ex.Message);
        }
        finally
        {
            _workspace.EndStarting();
            _host.FinalizeDeployWorkflow();
        }
    }
}
