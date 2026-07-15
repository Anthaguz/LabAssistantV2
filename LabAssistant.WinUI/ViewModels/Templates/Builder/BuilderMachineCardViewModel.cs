using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A single machine card in the Level 2 grid (the zoomed-in view of one domain or the Standalone container).
/// Immutable presentation (label, role, subtext, selection/role accents) plus the select and delete commands
/// the card surfaces. Purely runtime-independent so the Level 2 surface is unit-testable without a XAML host.
/// </summary>
public sealed partial class BuilderMachineCardViewModel : ObservableObject
{
    public BuilderMachineCardViewModel(
        string nodeId,
        int vmIndex,
        string label,
        string roleLabel,
        string subtext,
        bool isDomainController,
        bool isSelected,
        IRelayCommand? selectCommand,
        IRelayCommand? deleteCommand)
    {
        NodeId = nodeId;
        VmIndex = vmIndex;
        Label = label;
        RoleLabel = roleLabel;
        Subtext = subtext;
        IsDomainController = isDomainController;
        IsSelected = isSelected;
        SelectCommand = selectCommand;
        DeleteCommand = deleteCommand;
    }

    public string NodeId { get; }

    /// <summary>Index into <c>draft.Vms</c> so selection and edits route straight to the owning machine.</summary>
    public int VmIndex { get; }

    public string Label { get; }

    public string RoleLabel { get; }

    public string Subtext { get; }

    /// <summary>True for a domain controller; drives the directory-anchor accent on the card.</summary>
    public bool IsDomainController { get; }

    public bool IsSelected { get; }

    public IRelayCommand? SelectCommand { get; }

    /// <summary>Delete affordance: remove this machine. Null when the machine cannot be removed.</summary>
    public IRelayCommand? DeleteCommand { get; }

    /// <summary>True when the delete affordance should be offered on the card.</summary>
    public bool CanDelete => DeleteCommand is not null;
}
