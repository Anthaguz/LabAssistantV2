using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Builds the long-lived Deploy capability runtime from shell-owned hosts, adapters, and app services.
/// </summary>
internal static class DeployCapabilityBootstrap
{
    public static DeployCapabilityRuntime Bootstrap(IServiceProvider services, DeployCapabilityBootstrapContext context)
    {
        ArgumentNullException.ThrowIfNull(services);
        ValidateContext(context);

        var serviceBundle = ResolveServices(services);
        var sharedSeams = BuildSharedDeploySeams(context, serviceBundle);
        var quickDeployLane = BuildQuickDeployLane(context, serviceBundle, sharedSeams);
        var fromTemplateLane = BuildFromTemplateLane(context, serviceBundle, sharedSeams);
        var workspace = BuildWorkspace(context, quickDeployLane, fromTemplateLane);
        var resultsPanelCoordinator = BuildResultsPanelCoordinator(context, quickDeployLane, fromTemplateLane);

        sharedSeams.UiHooks.AttachSharedUiRefresh(workspace.RefreshSharedUiState);
        sharedSeams.UiHooks.AttachResultsPanelRefresh(context.ShellBridge.RefreshResultsPanelState);

        return BuildRuntime(context, fromTemplateLane, workspace, resultsPanelCoordinator);
    }

    private static void ValidateContext(DeployCapabilityBootstrapContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ShellBridge);
        ArgumentNullException.ThrowIfNull(context.ShellViewHosts);
        ArgumentNullException.ThrowIfNull(context.ShellViewHosts.QuickDeploy);
        ArgumentNullException.ThrowIfNull(context.ShellViewHosts.FromTemplate);
        ArgumentNullException.ThrowIfNull(context.ShellViewHosts.Navigation);
        ArgumentNullException.ThrowIfNull(context.Templates);
    }

    private static DeployServiceBundle ResolveServices(IServiceProvider services)
    {
        return new DeployServiceBundle(
            services.GetRequiredService<IMachinesCapabilityService>(),
            services.GetRequiredService<ITemplatesCapabilityService>(),
            services.GetRequiredService<IDeploymentPreflightService>(),
            services.GetRequiredService<IDeploymentCoordinator>(),
            services.GetRequiredService<IDeploymentOutcomeSummaryBuilder>(),
            services.GetRequiredService<IAppSettingsStore>(),
            services.GetRequiredService<IVhdxCatalogStore>());
    }

    private static DeploySharedSeams BuildSharedDeploySeams(
        DeployCapabilityBootstrapContext context,
        DeployServiceBundle services)
    {
        var referenceDataService = new DeployReferenceDataService(
            services.SettingsStore,
            services.VhdxCatalogStore,
            services.MachinesCapabilityService,
            services.TemplatesCapabilityService);

        return new DeploySharedSeams(
            referenceDataService,
            new DeployResolveSuggestionsService(),
            new DeployTemplateEditorLauncher(context.Templates),
            new DeployCapabilityUiHooks());
    }

    private static IDeployQuickDeployLane BuildQuickDeployLane(
        DeployCapabilityBootstrapContext context,
        DeployServiceBundle services,
        DeploySharedSeams sharedSeams)
    {
        return new DeployOnTheFlyWorkspaceOwner(
            context.ShellViewHosts.QuickDeploy.View,
            context.ShellViewHosts.QuickDeploy.RightPanelView,
            sharedSeams.ReferenceDataService,
            sharedSeams.ResolveSuggestionsService,
            sharedSeams.TemplateEditorLauncher,
            new DeployOnTheFlyWorkspaceShellBridge(
                context.ShellBridge.DispatcherQueue,
                () => context.ShellBridge.XamlRoot,
                context.ShellBridge.RequestResultsPanelToggle,
                sharedSeams.UiHooks.RefreshResultsPanelState),
            services.DeploymentPreflightService,
            services.DeploymentCoordinator,
            services.DeploymentOutcomeSummaryBuilder);
    }

    private static IDeployFromTemplateLane BuildFromTemplateLane(
        DeployCapabilityBootstrapContext context,
        DeployServiceBundle services,
        DeploySharedSeams sharedSeams)
    {
        return new DeployFromTemplateWorkspaceComposition(
            context.ShellViewHosts.FromTemplate.View,
            context.ShellViewHosts.FromTemplate.RightPanelView,
            context.Templates.ItemsSource,
            new DeployFromTemplateWorkspaceHost(
                sharedSeams.ReferenceDataService,
                sharedSeams.ResolveSuggestionsService,
                context.Templates,
                sharedSeams.UiHooks,
                (deploymentContext, mode) => services.DeploymentPreflightService.RunAsync(deploymentContext, mode),
                async deploymentContext =>
                {
                    await services.DeploymentCoordinator.DeployAllAsync(deploymentContext);
                    return services.DeploymentOutcomeSummaryBuilder.Build(deploymentContext);
                },
                context.ShellBridge.AttachProgressCallbacks,
                context.ShellBridge.RequestResultsPanelToggle));
    }

    private static DeployWorkspaceComposition BuildWorkspace(
        DeployCapabilityBootstrapContext context,
        IDeployQuickDeployLane quickDeployLane,
        IDeployFromTemplateLane fromTemplateLane)
    {
        return new DeployWorkspaceComposition(
            context.ShellViewHosts.LocalNavigationHost,
            context.ShellViewHosts.OverviewView,
            quickDeployLane,
            context.ShellViewHosts.Navigation.SubviewTabView,
            context.ShellViewHosts.Navigation.OverviewTabViewItem,
            context.ShellViewHosts.Navigation.QuickDeployTabViewItem,
            context.ShellViewHosts.Navigation.FromTemplateTabViewItem,
            () => new DeployWorkspaceUiState(
                QuickDeployDraftCount: quickDeployLane.DraftCount,
                IsLoadingTemplates: fromTemplateLane.IsLoadingTemplates,
                AvailableTemplateCount: context.Templates.GetLibraryItems().Count),
            fromTemplateLane,
            new DeployWorkspaceShellBridge(
                () => context.ShellBridge.IsDeployCapabilityActive,
                () => context.ShellBridge.IsDeployOverviewActive,
                () => context.ShellBridge.IsDeployOnTheFlyActive,
                () => context.ShellBridge.IsDeployFromTemplateActive,
                context.ShellBridge.NavigateToRoute));
    }

    private static DeployResultsPanelCoordinator BuildResultsPanelCoordinator(
        DeployCapabilityBootstrapContext context,
        IDeployQuickDeployLane quickDeployLane,
        IDeployFromTemplateLane fromTemplateLane)
    {
        return new DeployResultsPanelCoordinator(
            quickDeployLane,
            fromTemplateLane,
            () => context.ShellBridge.IsDeployOverviewActive,
            () => context.ShellBridge.IsDeployOnTheFlyActive,
            () => context.ShellBridge.IsDeployFromTemplateActive);
    }

    private static DeployCapabilityRuntime BuildRuntime(
        DeployCapabilityBootstrapContext context,
        IDeployFromTemplateLane fromTemplateLane,
        DeployWorkspaceComposition workspace,
        DeployResultsPanelCoordinator resultsPanelCoordinator)
    {
        return new DeployCapabilityRuntime(
            context.ShellBridge,
            fromTemplateLane,
            workspace,
            resultsPanelCoordinator);
    }

    private sealed record DeployServiceBundle(
        IMachinesCapabilityService MachinesCapabilityService,
        ITemplatesCapabilityService TemplatesCapabilityService,
        IDeploymentPreflightService DeploymentPreflightService,
        IDeploymentCoordinator DeploymentCoordinator,
        IDeploymentOutcomeSummaryBuilder DeploymentOutcomeSummaryBuilder,
        IAppSettingsStore SettingsStore,
        IVhdxCatalogStore VhdxCatalogStore);

    private sealed record DeploySharedSeams(
        DeployReferenceDataService ReferenceDataService,
        DeployResolveSuggestionsService ResolveSuggestionsService,
        DeployTemplateEditorLauncher TemplateEditorLauncher,
        DeployCapabilityUiHooks UiHooks);
}
