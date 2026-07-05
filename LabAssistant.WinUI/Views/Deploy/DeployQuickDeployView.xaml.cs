using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

/// <summary>
/// Quick Deploy subview. Under the MVVM rewrite this is a thin x:Bind host: it exposes the
/// <see cref="DeployQuickDeployViewModel"/> assigned by <see cref="DeployPage"/>, drives the
/// view-model lifecycle from Loaded/Unloaded, and keeps only the responsive layout switch that
/// is genuinely control-specific. All workflow state and commands live on the view model.
/// </summary>
public sealed partial class DeployQuickDeployView : UserControl
{
    private const double CompactLayoutThreshold = 1120;

    private DeployQuickDeployViewModel? _viewModel;

    public DeployQuickDeployView()
    {
        InitializeComponent();
        SizeChanged += DeployQuickDeployView_SizeChanged;
        Loaded += DeployQuickDeployView_Loaded;
        Unloaded += DeployQuickDeployView_Unloaded;
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    /// <summary>
    /// The lane view model, assigned by the host page. Setting it refreshes the compiled bindings.
    /// </summary>
    internal DeployQuickDeployViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            Bindings.Update();
        }
    }

    private void DeployQuickDeployView_Loaded(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.InitializeAsync();
    }

    private void DeployQuickDeployView_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup/cancellation policy: navigate-away cancels any in-flight deploy so the coordinator
        // tears down resources it created rather than leaking VMs, disks, or switches.
        _ = _viewModel?.CleanupAsync();
    }

    private void DeployQuickDeployView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    // Per-item remove buttons live inside data templates, where compiled bindings cannot reach the
    // lane view model. Forwarding the realized item into the view-model command keeps the workflow
    // logic on the view model while honoring the control-specific binding boundary.
    private void OnRemoveVmRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DeployQuickDeployVmEntryRow row } &&
            _viewModel?.RemoveVmRowCommand.CanExecute(row) == true)
        {
            _viewModel.RemoveVmRowCommand.Execute(row);
        }
    }

    private void OnRemoveSwitchRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DeployQuickDeploySwitchRowItem row } &&
            _viewModel?.RemoveSwitchRowCommand.CanExecute(row) == true)
        {
            _viewModel.RemoveSwitchRowCommand.Execute(row);
        }
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        DeployQuickDeployListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        DeployQuickDeployEditorColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.25, GridUnitType.Star);
        DeployQuickDeployPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        DeployQuickDeployEditorRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(DeployQuickDeployVmEntriesPanel, 0);
        Grid.SetColumn(DeployQuickDeployVmEntriesPanel, 0);

        Grid.SetRow(DeployQuickDeployVmEditorPanel, useStackedLayout ? 1 : 0);
        Grid.SetColumn(DeployQuickDeployVmEditorPanel, useStackedLayout ? 0 : 1);
    }
}
