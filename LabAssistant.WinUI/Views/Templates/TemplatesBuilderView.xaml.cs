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
    private static readonly BuilderWorkflowStep[] WorkflowStepOrder =
    [
        BuilderWorkflowStep.General,
        BuilderWorkflowStep.Networks,
        BuilderWorkflowStep.ForestsDomains,
        BuilderWorkflowStep.Credentials,
        BuilderWorkflowStep.Vms,
        BuilderWorkflowStep.Review
    ];

    private const string ButtonBackgroundPointerOverResource = "ButtonBackgroundPointerOver";
    private const string ButtonBackgroundPressedResource = "ButtonBackgroundPressed";
    private const string ButtonBorderBrushPointerOverResource = "ButtonBorderBrushPointerOver";
    private const string ButtonBorderBrushPressedResource = "ButtonBorderBrushPressed";

    private bool _isUpdatingDraft;
    private bool _canNavigateWorkflow;
    private BuilderWorkflowStep _selectedStep = BuilderWorkflowStep.General;
    private BuilderVmDetailCategory _selectedVmDetailCategory = BuilderVmDetailCategory.Basics;
    private TemplatesBuilderDraftSnapshot _draft = CreateEmptyDraft();
    private int _selectedNetworkIndex;
    private int _selectedCredentialSlotIndex;
    private int _selectedVmIndex;
    private bool _isVmOverviewSelected = true;
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
        BuilderPreviousStepButton.Click += (_, _) => SelectAdjacentStep(-1);
        BuilderNextStepButton.Click += (_, _) => SelectAdjacentStep(1);
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
        _canNavigateWorkflow = state.CanValidate;
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
        RenderWorkflowTreeState();
        UpdateStepCommandState();
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
        _isVmOverviewSelected = step == BuilderWorkflowStep.Vms;
        RenderSelectedStep();
        RenderDraftResources();
    }

    private void SelectVmOverview()
    {
        UpdateWorkingDraftFromVisibleControls();
        _selectedStep = BuilderWorkflowStep.Vms;
        _isVmOverviewSelected = true;
        RenderSelectedStep();
        RenderDraftResources();
    }

    private void SelectVmChild(int index)
    {
        _selectedStep = BuilderWorkflowStep.Vms;
        _selectedVmIndex = index;
        _isVmOverviewSelected = false;
        _selectedVmDetailCategory = BuilderVmDetailCategory.Basics;
        RenderSelectedStep();
        RenderVmNavChildren();
        RenderSelectedVmDetail();
    }

    private void SelectVmDetailCategory(BuilderVmDetailCategory category)
    {
        _selectedVmDetailCategory = category;
        RenderSelectedVmDetail();
    }

    private void SelectAdjacentStep(int offset)
    {
        var currentIndex = Array.IndexOf(WorkflowStepOrder, _selectedStep);
        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= WorkflowStepOrder.Length)
        {
            return;
        }

        SelectStep(WorkflowStepOrder[targetIndex]);
    }

    private void RenderSelectedStep()
    {
        BuilderGeneralSection.Visibility = _selectedStep == BuilderWorkflowStep.General ? Visibility.Visible : Visibility.Collapsed;
        BuilderNetworksSection.Visibility = _selectedStep == BuilderWorkflowStep.Networks ? Visibility.Visible : Visibility.Collapsed;
        BuilderForestsDomainsSection.Visibility = _selectedStep == BuilderWorkflowStep.ForestsDomains ? Visibility.Visible : Visibility.Collapsed;
        BuilderCredentialsSection.Visibility = _selectedStep == BuilderWorkflowStep.Credentials ? Visibility.Visible : Visibility.Collapsed;
        BuilderVmsSection.Visibility = _selectedStep == BuilderWorkflowStep.Vms ? Visibility.Visible : Visibility.Collapsed;
        BuilderReviewSection.Visibility = _selectedStep == BuilderWorkflowStep.Review ? Visibility.Visible : Visibility.Collapsed;
        BuilderVmOverviewPanel.Visibility = _selectedStep == BuilderWorkflowStep.Vms && _isVmOverviewSelected ? Visibility.Visible : Visibility.Collapsed;
        BuilderSelectedVmDetailHost.Visibility = _selectedStep == BuilderWorkflowStep.Vms && !_isVmOverviewSelected ? Visibility.Visible : Visibility.Collapsed;

        RenderWorkflowTreeState();
        UpdateStepCommandState();
    }

    private void RenderWorkflowTreeState()
    {
        BuilderWorkflowTreePanel.Children.Clear();
        BuilderWorkflowTreePanel.Children.Add(CreateResourceButton("General", _selectedStep == BuilderWorkflowStep.General, () => SelectStep(BuilderWorkflowStep.General), _canNavigateWorkflow));
        BuilderWorkflowTreePanel.Children.Add(CreateResourceButton("Networks", _selectedStep == BuilderWorkflowStep.Networks, () => SelectStep(BuilderWorkflowStep.Networks), _canNavigateWorkflow));
        BuilderWorkflowTreePanel.Children.Add(CreateResourceButton("Forests & Domains", _selectedStep == BuilderWorkflowStep.ForestsDomains, () => SelectStep(BuilderWorkflowStep.ForestsDomains), _canNavigateWorkflow));
        BuilderWorkflowTreePanel.Children.Add(CreateResourceButton("Credentials", _selectedStep == BuilderWorkflowStep.Credentials, () => SelectStep(BuilderWorkflowStep.Credentials), _canNavigateWorkflow));
        BuilderWorkflowTreePanel.Children.Add(CreateVmWorkflowNavRow());
        BuilderWorkflowTreePanel.Children.Add(BuilderVmNavChildrenPanel);
        BuilderWorkflowTreePanel.Children.Add(CreateResourceButton("Review", _selectedStep == BuilderWorkflowStep.Review, () => SelectStep(BuilderWorkflowStep.Review), _canNavigateWorkflow));
        RenderVmNavChildren();
    }

    private Grid CreateVmWorkflowNavRow()
    {
        var row = new Grid
        {
            ColumnSpacing = 4
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var vmButton = CreateResourceButton("VMs", _selectedStep == BuilderWorkflowStep.Vms, SelectVmOverview, _canNavigateWorkflow);
        Grid.SetColumn(vmButton, 0);
        row.Children.Add(vmButton);

        var addButton = new Button
        {
            MinWidth = 28,
            Padding = new Thickness(6, 0, 6, 0),
            Background = CreateTransparentBrush(),
            BorderThickness = new Thickness(0),
            Content = "+",
            IsEnabled = _canNavigateWorkflow
        };
        ToolTipService.SetToolTip(addButton, "Add VM");
        addButton.Click += BuilderAddVmButton_Click;
        Grid.SetColumn(addButton, 1);
        row.Children.Add(addButton);

        return row;
    }

    private void UpdateStepCommandState()
    {
        BuilderPreviousStepButton.IsEnabled = _canNavigateWorkflow && _selectedStep != BuilderWorkflowStep.General;
        BuilderNextStepButton.IsEnabled = _canNavigateWorkflow && _selectedStep != BuilderWorkflowStep.Review;
    }

    private void RenderDraftResources()
    {
        EnsureSelectedResourcesInBounds();
        RenderResourceLists();
        RenderSelectedNetworkDetail();
        RenderSelectedCredentialSlotDetail();
        RenderSelectedForestDomainDetail();
        RenderVmOverview();
        RenderSelectedVmDetail();
        BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(_draft);
    }

    private void RenderVmOverview()
    {
        BuilderVmTotalCountTextBlock.Text = _draft.Vms.Count.ToString();
        var standaloneCount = _draft.Vms.Count(vm => string.Equals(vm.MembershipMode, V2MembershipModeCatalog.Standalone, StringComparison.OrdinalIgnoreCase));
        var domainMemberCount = _draft.Vms.Count(vm => string.Equals(vm.MembershipMode, V2MembershipModeCatalog.DomainMember, StringComparison.OrdinalIgnoreCase));
        BuilderVmMembershipCountsTextBlock.Text = $"Standalone {standaloneCount} / Domain {domainMemberCount}";
        BuilderVmAdDcCountTextBlock.Text = _draft.Vms.Count(vm => vm.IsActiveDirectoryDomainController).ToString();

        BuilderVmOverviewListPanel.Children.Clear();
        if (_draft.Vms.Count == 0)
        {
            BuilderVmOverviewListPanel.Children.Add(CreateEmptyDetailText("No VMs in this draft."));
            return;
        }

        foreach (var vm in _draft.Vms)
        {
            var role = vm.IsActiveDirectoryDomainController ? ", AD DC" : string.Empty;
            BuilderVmOverviewListPanel.Children.Add(CreateEmptyDetailText(
                $"{FormatResourceName(vm.Name, vm.VmId)} - {FormatResourceName(vm.MembershipMode, V2MembershipModeCatalog.Standalone)}, {vm.Nics.Count} NICs{role}"));
        }
    }

    private void RenderResourceLists()
    {
        RenderNetworkList();
        RenderCredentialSlotList();
        RenderForestDomainList();
        RenderVmNavChildren();
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

    private void RenderVmNavChildren()
    {
        BuilderVmNavChildrenPanel.Children.Clear();
        for (var i = 0; i < _draft.Vms.Count; i++)
        {
            var index = i;
            var vm = _draft.Vms[i];
            var button = CreateResourceButton(
                FormatResourceName(vm.Name, vm.VmId),
                _selectedStep == BuilderWorkflowStep.Vms && !_isVmOverviewSelected && index == _selectedVmIndex,
                () => SelectVmChild(index));
            BuilderVmNavChildrenPanel.Children.Add(button);
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
        BuilderVmDetailCategoryNavPanel.Children.Clear();
        if (_draft.Vms.Count == 0)
        {
            BuilderSelectedVmDetailPanel.Children.Add(CreateEmptyDetailText("No VM selected."));
            return;
        }

        var vm = _draft.Vms[_selectedVmIndex];
        RenderVmDetailCategoryNav();
        BuilderSelectedVmDetailPanel.Children.Add(CreateRowTitle($"Selected VM Detail: {FormatResourceName(vm.Name, vm.VmId)}"));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead(GetVmDetailCategoryLabel(_selectedVmDetailCategory)));

        switch (_selectedVmDetailCategory)
        {
            case BuilderVmDetailCategory.Basics:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateTextBox("VM ID", "vm.VmId", vm.VmId),
                    CreateTextBox("Name", "vm.Name", vm.Name)));
                break;
            case BuilderVmDetailCategory.Resources:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateTextBox("Memory MB", "vm.MemoryMb", vm.MemoryMb),
                    CreateTextBox("CPU Count", "vm.CpuCount", vm.CpuCount),
                    CreateTextBox("Base Disk / VHDX ID", "vm.VhdxId", vm.VhdxId)));
                break;
            case BuilderVmDetailCategory.Membership:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateComboBox("Membership", "vm.MembershipMode", vm.MembershipMode, V2MembershipModeCatalog.DomainMember, V2MembershipModeCatalog.Standalone),
                    CreateTextBox("Domain ID", "vm.DomainId", vm.DomainId)));
                break;
            case BuilderVmDetailCategory.Roles:
                BuilderSelectedVmDetailPanel.Children.Add(CreateCheckBox("Active Directory Domain Controller", "vm.IsActiveDirectoryDomainController", vm.IsActiveDirectoryDomainController));
                break;
            case BuilderVmDetailCategory.Networking:
                var addNicButton = new Button { Content = "Add NIC", Tag = _selectedVmIndex };
                addNicButton.Click += BuilderAddNicButton_Click;
                BuilderSelectedVmDetailPanel.Children.Add(addNicButton);
                var nicsPanel = new StackPanel { Spacing = 6, Tag = "vm.NicsPanel" };
                foreach (var nic in vm.Nics)
                {
                    nicsPanel.Children.Add(CreateNicRow(nic));
                }

                BuilderSelectedVmDetailPanel.Children.Add(nicsPanel);
                break;
            case BuilderVmDetailCategory.Credentials:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateTextBox("Local Bootstrap Slot", "vm.LocalBootstrap", vm.CredentialSlots.LocalBootstrap),
                    CreateTextBox("Domain Admin Slot", "vm.DomainAdmin", vm.CredentialSlots.DomainAdmin),
                    CreateTextBox("Domain Join Slot", "vm.DomainJoin", vm.CredentialSlots.DomainJoin),
                    CreateTextBox("DSRM Slot", "vm.Dsrm", vm.CredentialSlots.Dsrm),
                    CreateTextBox("Parent Domain Admin Slot", "vm.ParentDomainAdmin", vm.CredentialSlots.ParentDomainAdmin)));
                break;
        }
    }

    private void RenderVmDetailCategoryNav()
    {
        foreach (var category in Enum.GetValues<BuilderVmDetailCategory>())
        {
            var selectedCategory = category;
            BuilderVmDetailCategoryNavPanel.Children.Add(CreateResourceButton(
                GetVmDetailCategoryLabel(selectedCategory),
                selectedCategory == _selectedVmDetailCategory,
                () => SelectVmDetailCategory(selectedCategory)));
        }
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
            CreateTextBox("Prefix", "nic.PrefixLength", nic.PrefixLength),
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
        if (_isVmOverviewSelected)
        {
            return draft;
        }

        if (_selectedVmIndex < 0 ||
            _selectedVmIndex >= draft.Vms.Count)
        {
            return draft;
        }

        var vms = draft.Vms.ToList();
        var vm = vms[_selectedVmIndex];
        vms[_selectedVmIndex] = _selectedVmDetailCategory switch
        {
            BuilderVmDetailCategory.Basics when HasTextBox(BuilderSelectedVmDetailPanel, "vm.VmId") => vm with
            {
                VmId = GetText(BuilderSelectedVmDetailPanel, "vm.VmId"),
                Name = GetText(BuilderSelectedVmDetailPanel, "vm.Name")
            },
            BuilderVmDetailCategory.Resources when HasTextBox(BuilderSelectedVmDetailPanel, "vm.MemoryMb") => vm with
            {
                MemoryMb = GetText(BuilderSelectedVmDetailPanel, "vm.MemoryMb"),
                CpuCount = GetText(BuilderSelectedVmDetailPanel, "vm.CpuCount"),
                VhdxId = GetText(BuilderSelectedVmDetailPanel, "vm.VhdxId")
            },
            BuilderVmDetailCategory.Membership when HasComboBox(BuilderSelectedVmDetailPanel, "vm.MembershipMode") => vm with
            {
                MembershipMode = GetComboValue(BuilderSelectedVmDetailPanel, "vm.MembershipMode"),
                DomainId = GetText(BuilderSelectedVmDetailPanel, "vm.DomainId")
            },
            BuilderVmDetailCategory.Roles when HasCheckBox(BuilderSelectedVmDetailPanel, "vm.IsActiveDirectoryDomainController") => vm with
            {
                IsActiveDirectoryDomainController = GetCheckBoxValue(BuilderSelectedVmDetailPanel, "vm.IsActiveDirectoryDomainController")
            },
            BuilderVmDetailCategory.Networking when HasNicsPanel(BuilderSelectedVmDetailPanel) => vm with
            {
                Nics = ReadNics(BuilderSelectedVmDetailPanel)
            },
            BuilderVmDetailCategory.Credentials when HasTextBox(BuilderSelectedVmDetailPanel, "vm.LocalBootstrap") => vm with
            {
                CredentialSlots = new TemplatesBuilderVmCredentialSlotDraft(
                    GetText(BuilderSelectedVmDetailPanel, "vm.LocalBootstrap"),
                    GetText(BuilderSelectedVmDetailPanel, "vm.DomainAdmin"),
                    GetText(BuilderSelectedVmDetailPanel, "vm.DomainJoin"),
                    GetText(BuilderSelectedVmDetailPanel, "vm.Dsrm"),
                    GetText(BuilderSelectedVmDetailPanel, "vm.ParentDomainAdmin"))
            },
            _ => vm
        };
        return draft with { Vms = vms };
    }

    private List<TemplatesBuilderNicDraft> ReadNics(DependencyObject root)
    {
        var nicsPanel = FindDescendants<StackPanel>(root).FirstOrDefault(panel => string.Equals(panel.Tag as string, "vm.NicsPanel", StringComparison.Ordinal));
        return nicsPanel?.Children.OfType<StackPanel>().Select(ReadNic).ToList() ?? [];
    }

    private TemplatesBuilderNicDraft ReadNic(StackPanel row)
        => new(
            GetText(row, "nic.NicId"),
            GetText(row, "nic.Name"),
            GetText(row, "nic.NetworkId"),
            GetText(row, "nic.SwitchName"),
            GetText(row, "nic.IpAddress"),
            GetText(row, "nic.PrefixLength"),
            GetText(row, "nic.DefaultGateway"),
            SplitList(GetText(row, "nic.DnsServers")));

    private void BuilderDraftControl_Changed(object sender, TextChangedEventArgs e)
    {
        NotifyDraftChanged(sender);
    }

    private void BuilderSelectionControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        NotifyDraftChanged(sender);
    }

    private void BuilderConfirmSaveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        NotifyDraftChanged(sender);
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
        var nextVmNumber = FindNextVmNumber(draft.Vms);
        var vms = draft.Vms
            .Append(new TemplatesBuilderVmDraft(
                $"vm-{nextVmNumber}",
                $"VM {nextVmNumber}",
                "4096",
                "2",
                string.Empty,
                V2MembershipModeCatalog.Standalone,
                string.Empty,
                false,
                new TemplatesBuilderVmCredentialSlotDraft(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                []))
            .ToList();
        _selectedVmIndex = vms.Count - 1;
        _selectedStep = BuilderWorkflowStep.Vms;
        _isVmOverviewSelected = false;
        _selectedVmDetailCategory = BuilderVmDetailCategory.Basics;
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
        nics.Add(new TemplatesBuilderNicDraft($"nic-{nics.Count + 1}", "Lab", draft.LabNetworks.FirstOrDefault().NetworkId, string.Empty, string.Empty, string.Empty, string.Empty, []));
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
            RenderSelectedStep();
        }
        finally
        {
            _isUpdatingDraft = false;
        }

        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyDraftChanged(object sender)
    {
        if (_isUpdatingDraft)
        {
            return;
        }

        UpdateWorkingDraftFromChangedControl(sender);
        RenderResourceLists();
        RenderVmOverview();
        BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(_draft);
        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateWorkingDraftFromChangedControl(object sender)
    {
        if (_isUpdatingDraft)
        {
            return;
        }

        _draft = _draft with
        {
            TemplateName = BuilderTemplateNameTextBox.Text,
            TemplateDescription = BuilderTemplateDescriptionTextBox.Text,
            DeploymentProfile = GetSelectedProfile(),
            IsSaveConfirmed = BuilderConfirmSaveCheckBox.IsChecked == true
        };

        if (sender is not FrameworkElement element ||
            element.Tag is not string tag)
        {
            return;
        }

        if (tag.StartsWith("network.", StringComparison.Ordinal))
        {
            _draft = UpdateSelectedNetwork(_draft);
        }
        else if (tag.StartsWith("credential.", StringComparison.Ordinal))
        {
            _draft = UpdateSelectedCredentialSlot(_draft);
        }
        else if (tag.StartsWith("forest.", StringComparison.Ordinal) ||
                 tag.StartsWith("domain.", StringComparison.Ordinal))
        {
            _draft = UpdateSelectedForestOrDomain(_draft);
        }
        else if (tag.StartsWith("vm.", StringComparison.Ordinal) ||
                 tag.StartsWith("nic.", StringComparison.Ordinal))
        {
            _draft = UpdateSelectedVm(_draft);
        }
    }

    private void EnsureSelectedResourcesInBounds()
    {
        _selectedNetworkIndex = ClampIndex(_selectedNetworkIndex, _draft.LabNetworks.Count);
        _selectedCredentialSlotIndex = ClampIndex(_selectedCredentialSlotIndex, _draft.CredentialSlots.Count);
        _selectedVmIndex = ClampIndex(_selectedVmIndex, _draft.Vms.Count);
        if (_draft.Vms.Count == 0)
        {
            _isVmOverviewSelected = true;
            _selectedVmDetailCategory = BuilderVmDetailCategory.Basics;
        }

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

    private Button CreateResourceButton(string content, bool isSelected, Action select, bool isEnabled = true)
    {
        var button = new Button
        {
            Background = CreateTransparentBrush(),
            BorderThickness = new Thickness(0),
            Content = CreateNavButtonContent(content, isSelected),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = isEnabled,
            Padding = new Thickness(0)
        };
        ConfigureNavButtonChrome(button);
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

    private static bool HasTextBox(DependencyObject root, string tag)
        => FindDescendants<TextBox>(root).Any(textBox => string.Equals(textBox.Tag as string, tag, StringComparison.Ordinal));

    private static bool HasComboBox(DependencyObject root, string tag)
        => FindDescendants<ComboBox>(root).Any(comboBox => string.Equals(comboBox.Tag as string, tag, StringComparison.Ordinal));

    private static bool HasCheckBox(DependencyObject root, string tag)
        => FindDescendants<CheckBox>(root).Any(checkBox => string.Equals(checkBox.Tag as string, tag, StringComparison.Ordinal));

    private static bool HasNicsPanel(DependencyObject root)
        => FindDescendants<StackPanel>(root).Any(panel => string.Equals(panel.Tag as string, "vm.NicsPanel", StringComparison.Ordinal));

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

    private static Border CreateNavButtonContent(string content, bool isSelected)
        => new()
        {
            Padding = new Thickness(8, 5, 8, 5),
            Background = GetBrush(isSelected ? "ShellBackgroundBrush" : "ShellContentBackgroundBrush"),
            BorderBrush = GetBrush(isSelected ? "ShellAccentBrush" : "ShellBorderBrush"),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Foreground = GetBrush(isSelected ? "ShellAccentBrush" : "ShellTextPrimaryBrush"),
                Text = content,
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

    private static void ConfigureNavButtonChrome(Button button)
    {
        var transparent = CreateTransparentBrush();
        button.Resources[ButtonBackgroundPointerOverResource] = transparent;
        button.Resources[ButtonBackgroundPressedResource] = transparent;
        button.Resources[ButtonBorderBrushPointerOverResource] = transparent;
        button.Resources[ButtonBorderBrushPressedResource] = transparent;
    }

    private static SolidColorBrush CreateTransparentBrush()
        => new(Microsoft.UI.Colors.Transparent);

    private static Brush? GetBrush(string resourceKey)
        => Application.Current.Resources[resourceKey] as Brush;

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

    private static IReadOnlyList<string> SplitList(string value)
        => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

    private static int FindNextVmNumber(IReadOnlyList<TemplatesBuilderVmDraft> vms)
    {
        var largestNumber = 0;
        foreach (var vm in vms)
        {
            largestNumber = Math.Max(largestNumber, ExtractTrailingVmNumber(vm.VmId, "vm-"));
            largestNumber = Math.Max(largestNumber, ExtractTrailingVmNumber(vm.Name, "VM "));
        }

        return largestNumber + 1;
    }

    private static int ExtractTrailingVmNumber(string value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(value[prefix.Length..].Trim(), out var number) ? number : 0;
    }

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

    private static string GetVmDetailCategoryLabel(BuilderVmDetailCategory category)
        => category switch
        {
            BuilderVmDetailCategory.Basics => "Basics",
            BuilderVmDetailCategory.Resources => "Resources",
            BuilderVmDetailCategory.Membership => "Membership",
            BuilderVmDetailCategory.Roles => "Roles",
            BuilderVmDetailCategory.Networking => "Networking",
            BuilderVmDetailCategory.Credentials => "Credentials",
            _ => category.ToString()
        };

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

    private enum BuilderVmDetailCategory
    {
        Basics,
        Resources,
        Membership,
        Roles,
        Networking,
        Credentials
    }
}
