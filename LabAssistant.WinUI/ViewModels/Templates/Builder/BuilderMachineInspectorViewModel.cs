using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Runtime-independent presentation for the selected Level 2 machine inspector.
/// It carries only display state and injected commands; all draft mutation stays on the builder view model.
/// </summary>
public sealed class BuilderMachineInspectorViewModel
{
    /// <summary>Initializes the selected machine inspector.</summary>
    public BuilderMachineInspectorViewModel(
        string machineTitle,
        string roleLabel,
        string subtext,
        string searchText,
        bool isRolesPanelExpanded,
        bool isFeaturesPanelExpanded,
        IReadOnlyList<BuilderRoleRowViewModel> roleRows,
        IReadOnlyList<BuilderRoleRowViewModel> featureRows,
        IRelayCommand? toggleRolesPanelCommand,
        IRelayCommand? toggleFeaturesPanelCommand)
    {
        MachineTitle = machineTitle;
        RoleLabel = roleLabel;
        Subtext = subtext;
        SearchText = searchText;
        IsRolesPanelExpanded = isRolesPanelExpanded;
        IsFeaturesPanelExpanded = isFeaturesPanelExpanded;
        RoleRows = new ObservableCollection<BuilderRoleRowViewModel>(roleRows);
        FeatureRows = new ObservableCollection<BuilderRoleRowViewModel>(featureRows);
        ToggleRolesPanelCommand = toggleRolesPanelCommand;
        ToggleFeaturesPanelCommand = toggleFeaturesPanelCommand;
    }

    /// <summary>Gets the selected machine display name.</summary>
    public string MachineTitle { get; }

    /// <summary>Gets the selected machine role label.</summary>
    public string RoleLabel { get; }

    /// <summary>Gets the selected machine summary text.</summary>
    public string Subtext { get; }

    /// <summary>Gets the search text used to build the current row projection.</summary>
    public string SearchText { get; }

    /// <summary>Gets whether the Roles panel is expanded.</summary>
    public bool IsRolesPanelExpanded { get; }

    /// <summary>Gets whether the Features panel is expanded.</summary>
    public bool IsFeaturesPanelExpanded { get; }

    /// <summary>Gets the projected role rows.</summary>
    public ObservableCollection<BuilderRoleRowViewModel> RoleRows { get; }

    /// <summary>Gets the projected feature rows.</summary>
    public ObservableCollection<BuilderRoleRowViewModel> FeatureRows { get; }

    /// <summary>Gets the command that expands or collapses the Roles panel.</summary>
    public IRelayCommand? ToggleRolesPanelCommand { get; }

    /// <summary>Gets the command that expands or collapses the Features panel.</summary>
    public IRelayCommand? ToggleFeaturesPanelCommand { get; }

    /// <summary>Gets whether any roles are visible after filtering.</summary>
    public bool HasRoleRows => RoleRows.Count > 0;

    /// <summary>Gets whether any features are visible after filtering.</summary>
    public bool HasFeatures => FeatureRows.Count > 0;

    /// <summary>Gets the chevron glyph for the Roles panel header.</summary>
    public string RolesPanelChevron => IsRolesPanelExpanded ? "\uE70D" : "\uE76C";

    /// <summary>Gets the chevron glyph for the Features panel header.</summary>
    public string FeaturesPanelChevron => IsFeaturesPanelExpanded ? "\uE70D" : "\uE76C";
}
