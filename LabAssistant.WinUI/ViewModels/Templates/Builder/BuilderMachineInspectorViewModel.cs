using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Runtime-independent presentation for the selected Level 2 machine inspector.
/// It carries only display state and injected commands; all draft mutation stays on the builder view model.
/// </summary>
public sealed class BuilderMachineInspectorViewModel
{
    /// <summary>Simple view item for the base-disk catalog ComboBox.</summary>
    public sealed class BaseDiskOption
    {
        /// <summary>Initializes one base-disk option row.</summary>
        public BaseDiskOption(string id, string displayLabel)
        {
            Id = id ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
        }

        /// <summary>Gets the catalog id committed back into the draft.</summary>
        public string Id { get; }

        /// <summary>Gets the user-facing label shown in the ComboBox.</summary>
        public string DisplayLabel { get; }
    }

    /// <summary>Initializes the selected machine inspector.</summary>
    public BuilderMachineInspectorViewModel(
        string machineTitle,
        string roleLabel,
        string subtext,
        string searchText,
        bool isRolesPanelExpanded,
        bool isFeaturesPanelExpanded,
        string machineNameText,
        string machineCpuCountText,
        string machineMemoryMbText,
        IReadOnlyList<BaseDiskOption> baseDiskOptions,
        string selectedBaseDiskId,
        string bootstrapAccountText,
        bool isHostAddressEditable,
        string hostAddressFixedOctetPrefix,
        bool hasSecondHostOctet,
        string hostAddressOctetOneText,
        string hostAddressOctetTwoText,
        string hostAddressSubnetCidr,
        MachineHostAddressStatus hostAddressStatus,
        IReadOnlyList<BuilderRoleRowViewModel> roleRows,
        IReadOnlyList<BuilderRoleRowViewModel> featureRows,
        IRelayCommand<string?>? commitMachineNameCommand,
        IRelayCommand<string?>? commitMachineCpuCountCommand,
        IRelayCommand<string?>? commitMachineMemoryMbCommand,
        IRelayCommand<string?>? commitMachineBaseDiskCommand,
        IRelayCommand<IReadOnlyList<int>>? commitMachineHostOctetsCommand,
        IRelayCommand? toggleRolesPanelCommand,
        IRelayCommand? toggleFeaturesPanelCommand)
    {
        MachineTitle = machineTitle;
        RoleLabel = roleLabel;
        Subtext = subtext;
        SearchText = searchText;
        IsRolesPanelExpanded = isRolesPanelExpanded;
        IsFeaturesPanelExpanded = isFeaturesPanelExpanded;
        MachineNameText = machineNameText;
        MachineCpuCountText = machineCpuCountText;
        MachineMemoryMbText = machineMemoryMbText;
        BaseDiskOptions = new ObservableCollection<BaseDiskOption>(baseDiskOptions ?? []);
        SelectedBaseDiskId = selectedBaseDiskId ?? string.Empty;
        BootstrapAccountText = bootstrapAccountText ?? string.Empty;
        IsHostAddressEditable = isHostAddressEditable;
        HostAddressFixedOctetPrefix = hostAddressFixedOctetPrefix ?? string.Empty;
        HasSecondHostOctet = hasSecondHostOctet;
        HostAddressOctetOneText = hostAddressOctetOneText ?? string.Empty;
        HostAddressOctetTwoText = hostAddressOctetTwoText ?? string.Empty;
        HostAddressSubnetCidr = hostAddressSubnetCidr ?? string.Empty;
        HostAddressStatus = hostAddressStatus;
        HostAddressValidationMessage = BuildHostAddressValidationMessage(HostAddressStatus);
        RoleRows = new ObservableCollection<BuilderRoleRowViewModel>(roleRows);
        FeatureRows = new ObservableCollection<BuilderRoleRowViewModel>(featureRows);
        CommitMachineNameCommand = commitMachineNameCommand;
        CommitMachineCpuCountCommand = commitMachineCpuCountCommand;
        CommitMachineMemoryMbCommand = commitMachineMemoryMbCommand;
        CommitMachineBaseDiskCommand = commitMachineBaseDiskCommand;
        CommitMachineHostOctetsCommand = commitMachineHostOctetsCommand;
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

    /// <summary>Gets the editable machine name text.</summary>
    public string MachineNameText { get; }

    /// <summary>Gets the editable vCPU count text.</summary>
    public string MachineCpuCountText { get; }

    /// <summary>Gets the editable startup-memory text in MB.</summary>
    public string MachineMemoryMbText { get; }

    /// <summary>Gets the available base-disk catalog options.</summary>
    public ObservableCollection<BaseDiskOption> BaseDiskOptions { get; }

    /// <summary>Gets the selected base-disk catalog id.</summary>
    public string SelectedBaseDiskId { get; }

    /// <summary>Gets the read-only bootstrap (local admin) account baked into the selected base disk, if advertised.</summary>
    public string BootstrapAccountText { get; }

    /// <summary>Gets whether a bootstrap account is known for the selected base disk.</summary>
    public bool HasBootstrapAccount => !string.IsNullOrWhiteSpace(BootstrapAccountText);

    /// <summary>Gets whether host-octet editing is available for the selected machine.</summary>
    public bool IsHostAddressEditable { get; }

    /// <summary>Gets the non-editable host-address prefix.</summary>
    public string HostAddressFixedOctetPrefix { get; }

    /// <summary>Gets whether the host address exposes a second editable octet.</summary>
    public bool HasSecondHostOctet { get; }

    /// <summary>Gets the first editable host octet text.</summary>
    public string HostAddressOctetOneText { get; }

    /// <summary>Gets the second editable host octet text.</summary>
    public string HostAddressOctetTwoText { get; }

    /// <summary>Gets the host subnet hint shown under the octet editors.</summary>
    public string HostAddressSubnetCidr { get; }

    /// <summary>Gets the host-address status projected by the engine.</summary>
    public MachineHostAddressStatus HostAddressStatus { get; }

    /// <summary>Gets the inline host-address validation message.</summary>
    public string HostAddressValidationMessage { get; }

    /// <summary>Gets whether octet editors should show the red error outline.</summary>
    public bool ShowHostAddressErrorOutline =>
        IsHostAddressEditable &&
        !string.IsNullOrWhiteSpace(HostAddressValidationMessage);

    /// <summary>Gets whether an inline host-address validation message should be shown.</summary>
    public bool HasHostAddressValidationMessage => !string.IsNullOrWhiteSpace(HostAddressValidationMessage);

    /// <summary>Gets the projected role rows.</summary>
    public ObservableCollection<BuilderRoleRowViewModel> RoleRows { get; }

    /// <summary>Gets the projected feature rows.</summary>
    public ObservableCollection<BuilderRoleRowViewModel> FeatureRows { get; }

    /// <summary>Gets the command that expands or collapses the Roles panel.</summary>
    public IRelayCommand? ToggleRolesPanelCommand { get; }

    /// <summary>Gets the command that expands or collapses the Features panel.</summary>
    public IRelayCommand? ToggleFeaturesPanelCommand { get; }

    /// <summary>Gets the command that commits the machine name edit.</summary>
    public IRelayCommand<string?>? CommitMachineNameCommand { get; }

    /// <summary>Gets the command that commits the vCPU count edit.</summary>
    public IRelayCommand<string?>? CommitMachineCpuCountCommand { get; }

    /// <summary>Gets the command that commits the startup-memory edit.</summary>
    public IRelayCommand<string?>? CommitMachineMemoryMbCommand { get; }

    /// <summary>Gets the command that commits the base-disk selection.</summary>
    public IRelayCommand<string?>? CommitMachineBaseDiskCommand { get; }

    /// <summary>Gets the command that commits editable host octets.</summary>
    public IRelayCommand<IReadOnlyList<int>>? CommitMachineHostOctetsCommand { get; }

    /// <summary>Gets whether any roles are visible after filtering.</summary>
    public bool HasRoleRows => RoleRows.Count > 0;

    /// <summary>Gets whether any features are visible after filtering.</summary>
    public bool HasFeatures => FeatureRows.Count > 0;

    /// <summary>Gets the chevron glyph for the Roles panel header.</summary>
    public string RolesPanelChevron => IsRolesPanelExpanded ? "\uE70D" : "\uE76C";

    /// <summary>Gets the chevron glyph for the Features panel header.</summary>
    public string FeaturesPanelChevron => IsFeaturesPanelExpanded ? "\uE70D" : "\uE76C";

    private static string BuildHostAddressValidationMessage(MachineHostAddressStatus status)
        => status switch
        {
            MachineHostAddressStatus.Duplicate => "Another machine already uses this address",
            MachineHostAddressStatus.ReservedRouter => "Reserved for the router (.1)",
            MachineHostAddressStatus.ReservedHost => "Reserved for the host",
            MachineHostAddressStatus.OutOfSubnet => "Outside the subnet",
            MachineHostAddressStatus.NetworkOrBroadcast => "Network or broadcast address",
            MachineHostAddressStatus.Invalid => "Not a valid address",
            _ => string.Empty
        };
}
