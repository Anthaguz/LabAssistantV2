using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LabAssistant.WinUI.Views.Templates;

internal readonly record struct TemplatesBuilderViewState(
    string ContextText,
    string ReferenceText,
    string StatusText,
    bool IsStatusVisible,
    bool HasActiveDraft,
    TemplatesBuilderDraftSnapshot Draft);

internal readonly record struct TemplatesBuilderActionState(
    bool CanApplySuggestions,
    bool CanValidate,
    bool CanSave,
    bool CanSaveAs,
    bool CanBackToLibrary);

public sealed partial class TemplatesBuilderView : UserControl
{
    private bool _isUpdatingDraft;
    private BuilderWorkflowStep _selectedStep = BuilderWorkflowStep.General;
    private TemplatesBuilderDraftSnapshot _draft = CreateEmptyDraft();
    private int _selectedNetworkIndex;
    private int _selectedCredentialSlotIndex;
    private int _selectedVmIndex;
    private BuilderForestDomainResourceKind _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
    private int _selectedForestDomainIndex;

    public event EventHandler? DraftChanged;
    public event EventHandler? ApplySuggestionsRequested;
    public event EventHandler? ValidateRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? BackToLibraryRequested;

    public TemplatesBuilderView()
    {
        InitializeComponent();
        BuilderDeploymentProfileComboBox.SelectedIndex = 1;
        BuilderTemplateNameTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderTemplateDescriptionTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderDeploymentProfileComboBox.SelectionChanged += BuilderSelectionControl_Changed;
        BuilderConfirmSaveCheckBox.Checked += BuilderConfirmSaveCheckBox_Changed;
        BuilderConfirmSaveCheckBox.Unchecked += BuilderConfirmSaveCheckBox_Changed;
        BuilderGeneralStepButton.Click += (_, _) => SelectStep(BuilderWorkflowStep.General);
        BuilderNetworksStepButton.Click += (_, _) => SelectStep(BuilderWorkflowStep.Networks);
        BuilderForestsDomainsStepButton.Click += (_, _) => SelectStep(BuilderWorkflowStep.ForestsDomains);
        BuilderCredentialsStepButton.Click += (_, _) => SelectStep(BuilderWorkflowStep.Credentials);
        BuilderVmsStepButton.Click += (_, _) => SelectStep(BuilderWorkflowStep.Vms);
        BuilderReviewStepButton.Click += (_, _) => SelectStep(BuilderWorkflowStep.Review);
        BuilderAddNetworkButton.Click += BuilderAddNetworkButton_Click;
        BuilderAddCredentialSlotButton.Click += BuilderAddCredentialSlotButton_Click;
        BuilderAddForestButton.Click += BuilderAddForestButton_Click;
        BuilderAddDomainButton.Click += BuilderAddDomainButton_Click;
        BuilderAddVmButton.Click += BuilderAddVmButton_Click;
        BuilderApplySuggestionsButton.Click += BuilderApplySuggestionsButton_Click;
        BuilderValidateButton.Click += BuilderValidateButton_Click;
        BuilderSaveButton.Click += BuilderSaveButton_Click;
        BuilderSaveAsButton.Click += BuilderSaveAsButton_Click;
        BuilderBackToLibraryButton.Click += BuilderBackToLibraryButton_Click;
        RenderSelectedStep();
    }

    internal TemplatesBuilderDraftSnapshot CaptureDraft()
    {
        UpdateWorkingDraftFromVisibleControls();
        return _draft;
    }

    internal void UpdateViewState(TemplatesBuilderViewState state)
    {
        _isUpdatingDraft = true;
        try
        {
            _draft = state.Draft;
            EnsureSelectedResourcesInBounds();

            SetTextIfChanged(BuilderContextTextBlock, state.ContextText);
            SetTextIfChanged(BuilderReferenceTextBlock, state.ReferenceText);
            SetTextIfChanged(BuilderStatusTextBlock, state.StatusText);
            BuilderStatusTextBlock.Visibility = state.IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;

            SetTextIfChanged(BuilderTemplateNameTextBox, _draft.TemplateName);
            SetTextIfChanged(BuilderTemplateDescriptionTextBox, _draft.TemplateDescription);
            SetSelectedProfile(_draft.DeploymentProfile);
            BuilderConfirmSaveCheckBox.IsChecked = _draft.IsSaveConfirmed;
            RenderDraftResources();
        }
        finally
        {
            _isUpdatingDraft = false;
        }
    }

    internal void UpdateActionState(TemplatesBuilderActionState state)
    {
        BuilderAddNetworkButton.IsEnabled = state.CanValidate;
        BuilderAddCredentialSlotButton.IsEnabled = state.CanValidate;
        BuilderAddForestButton.IsEnabled = state.CanValidate;
        BuilderAddDomainButton.IsEnabled = state.CanValidate;
        BuilderAddVmButton.IsEnabled = state.CanValidate;
        BuilderApplySuggestionsButton.IsEnabled = state.CanApplySuggestions;
        BuilderValidateButton.IsEnabled = state.CanValidate;
        BuilderSaveButton.IsEnabled = state.CanSave;
        BuilderSaveAsButton.IsEnabled = state.CanSaveAs;
        BuilderBackToLibraryButton.IsEnabled = state.CanBackToLibrary;
        BuilderGeneralStepButton.IsEnabled = state.CanValidate;
        BuilderNetworksStepButton.IsEnabled = state.CanValidate;
        BuilderForestsDomainsStepButton.IsEnabled = state.CanValidate;
        BuilderCredentialsStepButton.IsEnabled = state.CanValidate;
        BuilderVmsStepButton.IsEnabled = state.CanValidate;
        BuilderReviewStepButton.IsEnabled = state.CanValidate;
    }

    internal void UpdateConfirmationState(bool isSaveConfirmed)
    {
        _isUpdatingDraft = true;
        try
        {
            BuilderConfirmSaveCheckBox.IsChecked = isSaveConfirmed;
            _draft = _draft with { IsSaveConfirmed = isSaveConfirmed };
        }
        finally
        {
            _isUpdatingDraft = false;
        }
    }

    private void SelectStep(BuilderWorkflowStep step)
    {
        UpdateWorkingDraftFromVisibleControls();
        _selectedStep = step;
        RenderSelectedStep();
        RenderDraftResources();
    }

    private void RenderSelectedStep()
    {
        BuilderGeneralSection.Visibility = _selectedStep == BuilderWorkflowStep.General ? Visibility.Visible : Visibility.Collapsed;
        BuilderNetworksSection.Visibility = _selectedStep == BuilderWorkflowStep.Networks ? Visibility.Visible : Visibility.Collapsed;
        BuilderForestsDomainsSection.Visibility = _selectedStep == BuilderWorkflowStep.ForestsDomains ? Visibility.Visible : Visibility.Collapsed;
        BuilderCredentialsSection.Visibility = _selectedStep == BuilderWorkflowStep.Credentials ? Visibility.Visible : Visibility.Collapsed;
        BuilderVmsSection.Visibility = _selectedStep == BuilderWorkflowStep.Vms ? Visibility.Visible : Visibility.Collapsed;
        BuilderReviewSection.Visibility = _selectedStep == BuilderWorkflowStep.Review ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderDraftResources()
    {
        EnsureSelectedResourcesInBounds();
        RenderNetworkList();
        RenderCredentialSlotList();
        RenderForestDomainList();
        RenderVmNameList();
        RenderSelectedNetworkDetail();
        RenderSelectedCredentialSlotDetail();
        RenderSelectedForestDomainDetail();
        RenderSelectedVmDetail();
        BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(_draft);
    }

    private void RenderNetworkList()
    {
        BuilderNetworksListPanel.Children.Clear();
        for (var i = 0; i < _draft.LabNetworks.Count; i++)
        {
            var index = i;
            var network = _draft.LabNetworks[i];
            BuilderNetworksListPanel.Children.Add(CreateResourceButton(
                FormatResourceName(network.Name, network.NetworkId),
                index == _selectedNetworkIndex,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedNetworkIndex = index;
                    RenderNetworkList();
                    RenderSelectedNetworkDetail();
                }));
        }
    }

    private void RenderCredentialSlotList()
    {
        BuilderCredentialSlotsListPanel.Children.Clear();
        for (var i = 0; i < _draft.CredentialSlots.Count; i++)
        {
            var index = i;
            var slot = _draft.CredentialSlots[i];
            BuilderCredentialSlotsListPanel.Children.Add(CreateResourceButton(
                FormatResourceName(slot.Label, slot.SlotKey),
                index == _selectedCredentialSlotIndex,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedCredentialSlotIndex = index;
                    RenderCredentialSlotList();
                    RenderSelectedCredentialSlotDetail();
                }));
        }
    }

    private void RenderForestDomainList()
    {
        BuilderForestDomainResourcesListPanel.Children.Clear();
        for (var i = 0; i < _draft.Forests.Count; i++)
        {
            var index = i;
            var forest = _draft.Forests[i];
            BuilderForestDomainResourcesListPanel.Children.Add(CreateResourceButton(
                $"Forest: {FormatResourceName(forest.ForestId, forest.RootDomainId)}",
                _selectedForestDomainKind == BuilderForestDomainResourceKind.Forest && index == _selectedForestDomainIndex,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
                    _selectedForestDomainIndex = index;
                    RenderForestDomainList();
                    RenderSelectedForestDomainDetail();
                }));
        }

        for (var i = 0; i < _draft.Domains.Count; i++)
        {
            var index = i;
            var domain = _draft.Domains[i];
            BuilderForestDomainResourcesListPanel.Children.Add(CreateResourceButton(
                $"Domain: {FormatResourceName(domain.DnsName, domain.DomainId)}",
                _selectedForestDomainKind == BuilderForestDomainResourceKind.Domain && index == _selectedForestDomainIndex,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedForestDomainKind = BuilderForestDomainResourceKind.Domain;
                    _selectedForestDomainIndex = index;
                    RenderForestDomainList();
                    RenderSelectedForestDomainDetail();
                }));
        }
    }

    private void RenderVmNameList()
    {
        BuilderVmNameListPanel.Children.Clear();
        for (var i = 0; i < _draft.Vms.Count; i++)
        {
            var index = i;
            var vm = _draft.Vms[i];
            BuilderVmNameListPanel.Children.Add(CreateResourceButton(
                FormatResourceName(vm.Name, vm.VmId),
                index == _selectedVmIndex,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedVmIndex = index;
                    RenderVmNameList();
                    RenderSelectedVmDetail();
                }));
        }
    }

    private void RenderSelectedNetworkDetail()
    {
        BuilderSelectedNetworkDetailPanel.Children.Clear();
        if (_draft.LabNetworks.Count == 0)
        {
            BuilderSelectedNetworkDetailPanel.Children.Add(CreateEmptyDetailText("No network selected."));
            return;
        }

        var network = _draft.LabNetworks[_selectedNetworkIndex];
        BuilderSelectedNetworkDetailPanel.Children.Add(CreateRowTitle("Selected Network Detail"));
        BuilderSelectedNetworkDetailPanel.Children.Add(CreateFieldGrid(
            CreateTextBox("Network ID", "network.NetworkId", network.NetworkId),
            CreateTextBox("Name", "network.Name", network.Name),
            CreateTextBox("Switch", "network.SwitchName", network.SwitchName),
            CreateTextBox("Subnet", "network.Subnet", network.Subnet),
            CreateTextBox("Notes", "network.Notes", network.Notes)));
    }

    private void RenderSelectedCredentialSlotDetail()
    {
        BuilderSelectedCredentialSlotDetailPanel.Children.Clear();
        if (_draft.CredentialSlots.Count == 0)
        {
            BuilderSelectedCredentialSlotDetailPanel.Children.Add(CreateEmptyDetailText("No credential slot selected."));
            return;
        }

        var slot = _draft.CredentialSlots[_selectedCredentialSlotIndex];
        BuilderSelectedCredentialSlotDetailPanel.Children.Add(CreateRowTitle("Selected Slot Detail"));
        BuilderSelectedCredentialSlotDetailPanel.Children.Add(CreateFieldGrid(
            CreateTextBox("Slot Key", "credential.SlotKey", slot.SlotKey),
            CreateTextBox("Label", "credential.Label", slot.Label),
            CreateTextBox("Scope", "credential.ScopeHint", slot.ScopeHint)));
    }

    private void RenderSelectedForestDomainDetail()
    {
        BuilderSelectedForestDomainDetailPanel.Children.Clear();
        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest && _draft.Forests.Count > 0)
        {
            var forest = _draft.Forests[_selectedForestDomainIndex];
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateRowTitle("Selected Forest Detail"));
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateFieldGrid(
                CreateTextBox("Forest ID", "forest.ForestId", forest.ForestId),
                CreateTextBox("Root Domain ID", "forest.RootDomainId", forest.RootDomainId)));
            return;
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain && _draft.Domains.Count > 0)
        {
            var domain = _draft.Domains[_selectedForestDomainIndex];
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateRowTitle("Selected Domain Detail"));
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateFieldGrid(
                CreateTextBox("Domain ID", "domain.DomainId", domain.DomainId),
                CreateTextBox("DNS Name", "domain.DnsName", domain.DnsName),
                CreateTextBox("NetBIOS", "domain.NetBiosName", domain.NetBiosName),
                CreateTextBox("Forest ID", "domain.ForestId", domain.ForestId),
                CreateComboBox("Relation", "domain.RelationKind", domain.RelationKind, nameof(V2DomainRelationKind.Root), nameof(V2DomainRelationKind.Child), nameof(V2DomainRelationKind.Tree)),
                CreateTextBox("Parent Domain ID", "domain.ParentDomainId", domain.ParentDomainId)));
            return;
        }

        BuilderSelectedForestDomainDetailPanel.Children.Add(CreateEmptyDetailText("No forest or domain selected."));
    }

    private void RenderSelectedVmDetail()
    {
        BuilderSelectedVmDetailPanel.Children.Clear();
        if (_draft.Vms.Count == 0)
        {
            BuilderSelectedVmDetailPanel.Children.Add(CreateEmptyDetailText("No VM selected."));
            return;
        }

        var vm = _draft.Vms[_selectedVmIndex];
        BuilderSelectedVmDetailPanel.Children.Add(CreateRowTitle("Selected VM Detail"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead("Basics"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
            CreateTextBox("VM ID", "vm.VmId", vm.VmId),
            CreateTextBox("Name", "vm.Name", vm.Name),
            CreateTextBox("VHDX ID", "vm.VhdxId", vm.VhdxId)));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead("Compute"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
            CreateTextBox("Memory MB", "vm.MemoryMb", vm.MemoryMb.ToString()),
            CreateTextBox("CPU Count", "vm.CpuCount", vm.CpuCount.ToString())));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead("Membership"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
            CreateComboBox("Membership", "vm.MembershipMode", vm.MembershipMode, V2MembershipModeCatalog.DomainMember, V2MembershipModeCatalog.Standalone),
            CreateTextBox("Domain ID", "vm.DomainId", vm.DomainId)));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead("Roles"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateCheckBox("Active Directory Domain Controller", "vm.IsActiveDirectoryDomainController", vm.IsActiveDirectoryDomainController));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead("Networking"));
        var addNicButton = new Button { Content = "Add NIC", Tag = _selectedVmIndex };
        addNicButton.Click += BuilderAddNicButton_Click;
        BuilderSelectedVmDetailPanel.Children.Add(addNicButton);
        var nicsPanel = new StackPanel { Spacing = 6, Tag = "vm.NicsPanel" };
        foreach (var nic in vm.Nics)
        {
            nicsPanel.Children.Add(CreateNicRow(nic));
        }

        BuilderSelectedVmDetailPanel.Children.Add(nicsPanel);
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead("Credentials"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
            CreateTextBox("Local Bootstrap Slot", "vm.LocalBootstrap", vm.CredentialSlots.LocalBootstrap),
            CreateTextBox("Domain Admin Slot", "vm.DomainAdmin", vm.CredentialSlots.DomainAdmin),
            CreateTextBox("Domain Join Slot", "vm.DomainJoin", vm.CredentialSlots.DomainJoin),
            CreateTextBox("DSRM Slot", "vm.Dsrm", vm.CredentialSlots.Dsrm),
            CreateTextBox("Parent Domain Admin Slot", "vm.ParentDomainAdmin", vm.CredentialSlots.ParentDomainAdmin)));
    }

    private StackPanel CreateNicRow(TemplatesBuilderNicDraft nic)
    {
        var row = CreateRow();
        row.Tag = "nic.Row";
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("NIC ID", "nic.NicId", nic.NicId),
            CreateTextBox("Name", "nic.Name", nic.Name),
            CreateTextBox("Network ID", "nic.NetworkId", nic.NetworkId),
            CreateTextBox("Switch", "nic.SwitchName", nic.SwitchName),
            CreateTextBox("IP Address", "nic.IpAddress", nic.IpAddress),
            CreateTextBox("Prefix", "nic.PrefixLength", nic.PrefixLength?.ToString() ?? string.Empty),
            CreateTextBox("Gateway", "nic.DefaultGateway", nic.DefaultGateway),
            CreateTextBox("DNS Servers", "nic.DnsServers", string.Join(", ", nic.DnsServers))));
        return row;
    }

    private void UpdateWorkingDraftFromVisibleControls()
    {
        if (_isUpdatingDraft)
        {
            return;
        }

        var draft = _draft with
        {
            TemplateName = BuilderTemplateNameTextBox.Text,
            TemplateDescription = BuilderTemplateDescriptionTextBox.Text,
            DeploymentProfile = GetSelectedProfile(),
            IsSaveConfirmed = BuilderConfirmSaveCheckBox.IsChecked == true
        };

        draft = UpdateSelectedNetwork(draft);
        draft = UpdateSelectedCredentialSlot(draft);
        draft = UpdateSelectedForestOrDomain(draft);
        draft = UpdateSelectedVm(draft);
        _draft = draft;
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedNetwork(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedNetworkIndex < 0 ||
            _selectedNetworkIndex >= draft.LabNetworks.Count ||
            FindDescendants<TextBox>(BuilderSelectedNetworkDetailPanel).All(textBox => !string.Equals(textBox.Tag as string, "network.NetworkId", StringComparison.Ordinal)))
        {
            return draft;
        }

        var networks = draft.LabNetworks.ToList();
        networks[_selectedNetworkIndex] = new TemplatesBuilderLabNetworkDraft(
            GetText(BuilderSelectedNetworkDetailPanel, "network.NetworkId"),
            GetText(BuilderSelectedNetworkDetailPanel, "network.Name"),
            GetText(BuilderSelectedNetworkDetailPanel, "network.SwitchName"),
            GetText(BuilderSelectedNetworkDetailPanel, "network.Subnet"),
            GetText(BuilderSelectedNetworkDetailPanel, "network.Notes"));
        return draft with { LabNetworks = networks };
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedCredentialSlot(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedCredentialSlotIndex < 0 ||
            _selectedCredentialSlotIndex >= draft.CredentialSlots.Count ||
            FindDescendants<TextBox>(BuilderSelectedCredentialSlotDetailPanel).All(textBox => !string.Equals(textBox.Tag as string, "credential.SlotKey", StringComparison.Ordinal)))
        {
            return draft;
        }

        var slots = draft.CredentialSlots.ToList();
        slots[_selectedCredentialSlotIndex] = new TemplatesBuilderCredentialSlotDraft(
            GetText(BuilderSelectedCredentialSlotDetailPanel, "credential.SlotKey"),
            GetText(BuilderSelectedCredentialSlotDetailPanel, "credential.Label"),
            GetText(BuilderSelectedCredentialSlotDetailPanel, "credential.ScopeHint"));
        return draft with { CredentialSlots = slots };
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedForestOrDomain(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Forests.Count &&
            FindDescendants<TextBox>(BuilderSelectedForestDomainDetailPanel).Any(textBox => string.Equals(textBox.Tag as string, "forest.ForestId", StringComparison.Ordinal)))
        {
            var forests = draft.Forests.ToList();
            forests[_selectedForestDomainIndex] = new TemplatesBuilderForestDraft(
                GetText(BuilderSelectedForestDomainDetailPanel, "forest.ForestId"),
                GetText(BuilderSelectedForestDomainDetailPanel, "forest.RootDomainId"));
            return draft with { Forests = forests };
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Domains.Count &&
            FindDescendants<TextBox>(BuilderSelectedForestDomainDetailPanel).Any(textBox => string.Equals(textBox.Tag as string, "domain.DomainId", StringComparison.Ordinal)))
        {
            var domains = draft.Domains.ToList();
            domains[_selectedForestDomainIndex] = new TemplatesBuilderDomainDraft(
                GetText(BuilderSelectedForestDomainDetailPanel, "domain.DomainId"),
                GetText(BuilderSelectedForestDomainDetailPanel, "domain.DnsName"),
                GetText(BuilderSelectedForestDomainDetailPanel, "domain.NetBiosName"),
                GetText(BuilderSelectedForestDomainDetailPanel, "domain.ForestId"),
                GetComboValue(BuilderSelectedForestDomainDetailPanel, "domain.RelationKind"),
                GetText(BuilderSelectedForestDomainDetailPanel, "domain.ParentDomainId"));
            return draft with { Domains = domains };
        }

        return draft;
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedVm(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedVmIndex < 0 ||
            _selectedVmIndex >= draft.Vms.Count ||
            FindDescendants<TextBox>(BuilderSelectedVmDetailPanel).All(textBox => !string.Equals(textBox.Tag as string, "vm.VmId", StringComparison.Ordinal)))
        {
            return draft;
        }

        var vms = draft.Vms.ToList();
        vms[_selectedVmIndex] = ReadVm(BuilderSelectedVmDetailPanel);
        return draft with { Vms = vms };
    }

    private TemplatesBuilderVmDraft ReadVm(DependencyObject root)
    {
        var nicsPanel = FindDescendants<StackPanel>(root).FirstOrDefault(panel => string.Equals(panel.Tag as string, "vm.NicsPanel", StringComparison.Ordinal));
        var nics = nicsPanel?.Children.OfType<StackPanel>().Select(ReadNic).ToList() ?? [];

        return new TemplatesBuilderVmDraft(
            GetText(root, "vm.VmId"),
            GetText(root, "vm.Name"),
            ParsePositiveInt(GetText(root, "vm.MemoryMb")),
            ParsePositiveInt(GetText(root, "vm.CpuCount")),
            GetText(root, "vm.VhdxId"),
            GetComboValue(root, "vm.MembershipMode"),
            GetText(root, "vm.DomainId"),
            GetCheckBoxValue(root, "vm.IsActiveDirectoryDomainController"),
            new TemplatesBuilderVmCredentialSlotDraft(
                GetText(root, "vm.LocalBootstrap"),
                GetText(root, "vm.DomainAdmin"),
                GetText(root, "vm.DomainJoin"),
                GetText(root, "vm.Dsrm"),
                GetText(root, "vm.ParentDomainAdmin")),
            nics);
    }

    private TemplatesBuilderNicDraft ReadNic(StackPanel row)
        => new(
            GetText(row, "nic.NicId"),
            GetText(row, "nic.Name"),
            GetText(row, "nic.NetworkId"),
            GetText(row, "nic.SwitchName"),
            GetText(row, "nic.IpAddress"),
            ParseNullableInt(GetText(row, "nic.PrefixLength")),
            GetText(row, "nic.DefaultGateway"),
            SplitList(GetText(row, "nic.DnsServers")));

    private void BuilderDraftControl_Changed(object sender, TextChangedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderSelectionControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderConfirmSaveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderAddNetworkButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        var networks = draft.LabNetworks
            .Append(new TemplatesBuilderLabNetworkDraft($"lab-network-{draft.LabNetworks.Count + 1}", "Network", string.Empty, string.Empty, string.Empty))
            .ToList();
        _selectedNetworkIndex = networks.Count - 1;
        RenderAndNotify(draft with { LabNetworks = networks, IsSaveConfirmed = false });
    }

    private void BuilderAddCredentialSlotButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        var slots = draft.CredentialSlots
            .Append(new TemplatesBuilderCredentialSlotDraft($"slot-{draft.CredentialSlots.Count + 1}", "Credential slot", "template reference"))
            .ToList();
        _selectedCredentialSlotIndex = slots.Count - 1;
        RenderAndNotify(draft with { CredentialSlots = slots, IsSaveConfirmed = false });
    }

    private void BuilderAddForestButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        var forests = draft.Forests
            .Append(new TemplatesBuilderForestDraft($"forest-{draft.Forests.Count + 1}", string.Empty))
            .ToList();
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
        _selectedForestDomainIndex = forests.Count - 1;
        RenderAndNotify(draft with { Forests = forests, IsSaveConfirmed = false });
    }

    private void BuilderAddDomainButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        var forestId = draft.Forests.FirstOrDefault().ForestId;
        var domains = draft.Domains
            .Append(new TemplatesBuilderDomainDraft($"domain-{draft.Domains.Count + 1}", "example.local", "EXAMPLE", forestId, nameof(V2DomainRelationKind.Root), string.Empty))
            .ToList();
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Domain;
        _selectedForestDomainIndex = domains.Count - 1;
        RenderAndNotify(draft with { Domains = domains, IsSaveConfirmed = false });
    }

    private void BuilderAddVmButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        var vms = draft.Vms
            .Append(new TemplatesBuilderVmDraft(
                $"vm-{draft.Vms.Count + 1}",
                "new-vm",
                4096,
                2,
                string.Empty,
                V2MembershipModeCatalog.Standalone,
                string.Empty,
                false,
                new TemplatesBuilderVmCredentialSlotDraft(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                []))
            .ToList();
        _selectedVmIndex = vms.Count - 1;
        RenderAndNotify(draft with { Vms = vms, IsSaveConfirmed = false });
    }

    private void BuilderAddNicButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int vmIndex })
        {
            return;
        }

        var draft = CaptureDraft();
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count)
        {
            return;
        }

        var vms = draft.Vms.ToList();
        var vm = vms[vmIndex];
        var nics = vm.Nics.ToList();
        nics.Add(new TemplatesBuilderNicDraft($"nic-{nics.Count + 1}", "Lab", draft.LabNetworks.FirstOrDefault().NetworkId, string.Empty, string.Empty, null, string.Empty, []));
        vms[vmIndex] = vm with { Nics = nics };
        RenderAndNotify(draft with { Vms = vms, IsSaveConfirmed = false });
    }

    private void BuilderApplySuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySuggestionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderValidateButton_Click(object sender, RoutedEventArgs e)
    {
        ValidateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderSaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderSaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderBackToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        BackToLibraryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RenderAndNotify(TemplatesBuilderDraftSnapshot draft)
    {
        _isUpdatingDraft = true;
        try
        {
            _draft = draft;
            SetTextIfChanged(BuilderTemplateNameTextBox, _draft.TemplateName);
            SetTextIfChanged(BuilderTemplateDescriptionTextBox, _draft.TemplateDescription);
            SetSelectedProfile(_draft.DeploymentProfile);
            BuilderConfirmSaveCheckBox.IsChecked = _draft.IsSaveConfirmed;
            RenderDraftResources();
        }
        finally
        {
            _isUpdatingDraft = false;
        }

        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyDraftChanged()
    {
        if (_isUpdatingDraft)
        {
            return;
        }

        UpdateWorkingDraftFromVisibleControls();
        BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(_draft);
        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureSelectedResourcesInBounds()
    {
        _selectedNetworkIndex = ClampIndex(_selectedNetworkIndex, _draft.LabNetworks.Count);
        _selectedCredentialSlotIndex = ClampIndex(_selectedCredentialSlotIndex, _draft.CredentialSlots.Count);
        _selectedVmIndex = ClampIndex(_selectedVmIndex, _draft.Vms.Count);

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest && _draft.Forests.Count == 0 && _draft.Domains.Count > 0)
        {
            _selectedForestDomainKind = BuilderForestDomainResourceKind.Domain;
        }
        else if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain && _draft.Domains.Count == 0 && _draft.Forests.Count > 0)
        {
            _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
        }

        var forestDomainCount = _selectedForestDomainKind == BuilderForestDomainResourceKind.Forest
            ? _draft.Forests.Count
            : _draft.Domains.Count;
        _selectedForestDomainIndex = ClampIndex(_selectedForestDomainIndex, forestDomainCount);
    }

    private string GetSelectedProfile()
    {
        if (BuilderDeploymentProfileComboBox.SelectedItem is ComboBoxItem item &&
            item.Content is string content)
        {
            return content;
        }

        return "Balanced";
    }

    private void SetSelectedProfile(string profile)
    {
        var normalized = string.IsNullOrWhiteSpace(profile) ? "Balanced" : profile.Trim();
        foreach (var item in BuilderDeploymentProfileComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content is string content &&
                string.Equals(content, normalized, StringComparison.OrdinalIgnoreCase))
            {
                BuilderDeploymentProfileComboBox.SelectedItem = item;
                return;
            }
        }

        BuilderDeploymentProfileComboBox.SelectedIndex = 1;
    }

    private TextBox CreateTextBox(string header, string tag, string value)
    {
        var textBox = new TextBox
        {
            Header = header,
            Tag = tag,
            Text = value,
            MinWidth = 150
        };
        textBox.TextChanged += BuilderDraftControl_Changed;
        return textBox;
    }

    private ComboBox CreateComboBox(string header, string tag, string value, params string[] options)
    {
        var comboBox = new ComboBox
        {
            Header = header,
            Tag = tag,
            MinWidth = 150
        };

        foreach (var option in options)
        {
            comboBox.Items.Add(new ComboBoxItem { Content = option });
        }

        SetComboBoxValue(comboBox, value);
        comboBox.SelectionChanged += BuilderSelectionControl_Changed;
        return comboBox;
    }

    private CheckBox CreateCheckBox(string content, string tag, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Content = content,
            Tag = tag,
            IsChecked = isChecked
        };
        checkBox.Checked += BuilderConfirmSaveCheckBox_Changed;
        checkBox.Unchecked += BuilderConfirmSaveCheckBox_Changed;
        return checkBox;
    }

    private Button CreateResourceButton(string content, bool isSelected, Action select)
    {
        var button = new Button
        {
            Content = isSelected ? $"> {content}" : content,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        button.Click += (_, _) => select();
        return button;
    }

    private static StackPanel CreateRow()
        => new()
        {
            Spacing = 6,
            Padding = new Thickness(8),
            Background = Application.Current.Resources["ShellBackgroundBrush"] as Brush
        };

    private static TextBlock CreateRowTitle(string text)
        => new()
        {
            Foreground = Application.Current.Resources["ShellTextPrimaryBrush"] as Brush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = text
        };

    private static TextBlock CreateSubhead(string text)
        => new()
        {
            Foreground = Application.Current.Resources["ShellTextSecondaryBrush"] as Brush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = text
        };

    private static TextBlock CreateEmptyDetailText(string text)
        => new()
        {
            Foreground = Application.Current.Resources["ShellTextSecondaryBrush"] as Brush,
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };

    private static Grid CreateFieldGrid(params FrameworkElement[] fields)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        for (var i = 0; i < fields.Length; i++)
        {
            var row = i / 2;
            if (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            Grid.SetRow(fields[i], row);
            Grid.SetColumn(fields[i], i % 2);
            grid.Children.Add(fields[i]);
        }

        return grid;
    }

    private static void SetTextIfChanged(TextBox textBox, string value)
    {
        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
        {
            textBox.Text = value;
        }
    }

    private static void SetTextIfChanged(TextBlock textBlock, string value)
    {
        if (!string.Equals(textBlock.Text, value, StringComparison.Ordinal))
        {
            textBlock.Text = value;
        }
    }

    private static string GetText(DependencyObject root, string tag)
        => FindDescendants<TextBox>(root).FirstOrDefault(textBox => string.Equals(textBox.Tag as string, tag, StringComparison.Ordinal))?.Text ?? string.Empty;

    private static string GetComboValue(DependencyObject root, string tag)
    {
        var comboBox = FindDescendants<ComboBox>(root).FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
        if (comboBox?.SelectedItem is ComboBoxItem { Content: string content })
        {
            return content;
        }

        return string.Empty;
    }

    private static bool GetCheckBoxValue(DependencyObject root, string tag)
        => FindDescendants<CheckBox>(root).FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal))?.IsChecked == true;

    private static void SetComboBoxValue(ComboBox comboBox, string value)
    {
        var normalized = value.Trim();
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content is string content &&
                string.Equals(content, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Math.Clamp(index, 0, count - 1);
    }

    private static int ParsePositiveInt(string value)
        => int.TryParse(value, out var parsed) ? parsed : 0;

    private static int? ParseNullableInt(string value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static IReadOnlyList<string> SplitList(string value)
        => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

    private static string BuildReviewSummary(TemplatesBuilderDraftSnapshot draft)
    {
        var dcCount = draft.Vms.Count(vm => vm.IsActiveDirectoryDomainController);
        var nicCount = draft.Vms.Sum(vm => vm.Nics.Count);
        return $"{draft.LabNetworks.Count} networks, {draft.CredentialSlots.Count} credential slot references, {draft.Forests.Count} forests, {draft.Domains.Count} domains, {draft.Vms.Count} VMs, {dcCount} Active Directory Domain Controller role assignments, {nicCount} NICs.";
    }

    private static string FormatResourceName(string primary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "(unnamed)" : fallback.Trim();
    }

    private static TemplatesBuilderDraftSnapshot CreateEmptyDraft()
        => new(
            string.Empty,
            string.Empty,
            "Balanced",
            [],
            [],
            [],
            [],
            [],
            false);

    private enum BuilderWorkflowStep
    {
        General,
        Networks,
        ForestsDomains,
        Credentials,
        Vms,
        Review
    }

    private enum BuilderForestDomainResourceKind
    {
        Forest,
        Domain
    }
}
