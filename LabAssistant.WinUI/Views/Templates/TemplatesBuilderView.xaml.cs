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
    TemplatesBuilderDraftSnapshot Draft,
    TemplatesBuilderValidationState ValidationState);

internal readonly record struct TemplatesBuilderActionState(
    bool CanNavigate,
    bool CanSave,
    bool CanSaveAs,
    bool CanBackToLibrary);

public sealed partial class TemplatesBuilderView : UserControl
{
    private const string ButtonBackgroundPointerOverResource = "ButtonBackgroundPointerOver";
    private const string ButtonBackgroundPressedResource = "ButtonBackgroundPressed";
    private const string ButtonBorderBrushPointerOverResource = "ButtonBorderBrushPointerOver";
    private const string ButtonBorderBrushPressedResource = "ButtonBorderBrushPressed";
    private const string ConservativeDeploymentProfile = "Conservative";
    private const string BalancedDeploymentProfile = "Balanced";
    private const string AggressiveDeploymentProfile = "Aggressive";

    private bool _isUpdatingDraft;
    private bool _canNavigateWorkflow;
    private readonly TemplatesBuilderWorkflowNavigation _workflowNavigation = new();
    private TemplatesBuilderDraftSnapshot _draft = CreateEmptyDraft();
    private TemplatesBuilderValidationState _validationState = TemplatesBuilderValidationState.Empty;
    private TemplatesBuilderActionState _actionState;
    private string _selectedDeploymentProfile = BalancedDeploymentProfile;
    private int _selectedNetworkIndex;
    private int _selectedCredentialSlotIndex;
    private BuilderForestDomainResourceKind _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
    private int _selectedForestDomainIndex;

    public event EventHandler? DraftChanged;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? BackToLibraryRequested;

    public TemplatesBuilderView()
    {
        InitializeComponent();
        BuilderTemplateNameTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderTemplateDescriptionTextBox.TextChanged += BuilderDraftControl_Changed;
        ConfigureDeploymentProfileButton(BuilderDeploymentProfileConservativeButton, ConservativeDeploymentProfile);
        ConfigureDeploymentProfileButton(BuilderDeploymentProfileBalancedButton, BalancedDeploymentProfile);
        ConfigureDeploymentProfileButton(BuilderDeploymentProfileAggressiveButton, AggressiveDeploymentProfile);
        SetSelectedProfile(BalancedDeploymentProfile);
        BuilderAddNetworkButton.Click += BuilderAddNetworkButton_Click;
        BuilderAddCredentialSlotButton.Click += BuilderAddCredentialSlotButton_Click;
        BuilderAddForestButton.Click += BuilderAddForestButton_Click;
        BuilderAddDomainButton.Click += BuilderAddDomainButton_Click;
        BuilderAddVmButton.Click += BuilderAddVmButton_Click;
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
            _validationState = state.ValidationState;
            RenderDraftResources(refreshWorkflowTreeChildren: false);
            RenderSelectedStep();
        }
        finally
        {
            _isUpdatingDraft = false;
        }
    }

    internal void UpdateActionState(TemplatesBuilderActionState state)
    {
        var canNavigateChanged = _canNavigateWorkflow != state.CanNavigate;
        _actionState = state;
        _canNavigateWorkflow = state.CanNavigate;
        BuilderAddNetworkButton.IsEnabled = state.CanNavigate;
        BuilderAddCredentialSlotButton.IsEnabled = state.CanNavigate;
        BuilderAddForestButton.IsEnabled = state.CanNavigate;
        BuilderAddDomainButton.IsEnabled = state.CanNavigate;
        BuilderAddVmButton.IsEnabled = state.CanNavigate;
        BuilderBackToLibraryButton.IsEnabled = state.CanBackToLibrary;
        if (canNavigateChanged)
        {
            UpdateWorkflowTreeActionState();
        }

        UpdateFooterCommandState(state);
    }

    internal void UpdateValidationState(TemplatesBuilderValidationState validationState)
    {
        _validationState = validationState;
        RenderReviewValidationState();
    }

    private void SelectStep(BuilderWorkflowStep step)
    {
        UpdateWorkingDraftFromVisibleControls();
        _workflowNavigation.SelectStep(step, _draft);
        RenderSelectedStep();
        RenderDraftResources(refreshWorkflowTreeChildren: false);
    }

    private void SelectVmOverview()
    {
        UpdateWorkingDraftFromVisibleControls();
        _workflowNavigation.SelectVmOverview(_draft);
        RenderSelectedStep();
        RenderDraftResources(refreshWorkflowTreeChildren: false);
    }

    private void SelectVmChild(int index)
    {
        _workflowNavigation.SelectVmChild(index, _draft);
        RenderSelectedStep();
        RenderSelectedVmDetail();
    }

    private void SelectVmDetailCategory(BuilderVmDetailCategory category)
    {
        _workflowNavigation.SelectVmDetailCategory(category, _draft);
        RenderSelectedStep();
        RenderSelectedVmDetail();
    }

    private void SelectVmDetailCategory(int vmIndex, BuilderVmDetailCategory category)
    {
        _workflowNavigation.SelectRoute(BuilderWorkflowRoute.ForVmCategory(vmIndex, category), _draft);
        RenderSelectedStep();
        RenderSelectedVmDetail();
    }

    private void SelectAdjacentStep(int offset)
    {
        UpdateWorkingDraftFromVisibleControls();
        if (!_workflowNavigation.SelectAdjacent(offset, _draft))
        {
            return;
        }

        RenderSelectedStep();
        RenderDraftResources(refreshWorkflowTreeChildren: false);
    }

    private void RenderSelectedStep()
    {
        var projection = _workflowNavigation.Project(_draft, _canNavigateWorkflow);
        BuilderGeneralSection.Visibility = projection.ActiveStep == BuilderWorkflowStep.General ? Visibility.Visible : Visibility.Collapsed;
        BuilderNetworksSection.Visibility = projection.ActiveStep == BuilderWorkflowStep.Networks ? Visibility.Visible : Visibility.Collapsed;
        BuilderForestsDomainsSection.Visibility = projection.ActiveStep == BuilderWorkflowStep.ForestsDomains ? Visibility.Visible : Visibility.Collapsed;
        BuilderCredentialsSection.Visibility = projection.ActiveStep == BuilderWorkflowStep.Credentials ? Visibility.Visible : Visibility.Collapsed;
        BuilderVmsSection.Visibility = projection.ActiveStep == BuilderWorkflowStep.Vms ? Visibility.Visible : Visibility.Collapsed;
        BuilderReviewSection.Visibility = projection.ActiveStep == BuilderWorkflowStep.Review ? Visibility.Visible : Visibility.Collapsed;
        BuilderVmOverviewPanel.Visibility = projection.ActiveStep == BuilderWorkflowStep.Vms && projection.IsVmOverviewSelected ? Visibility.Visible : Visibility.Collapsed;
        BuilderSelectedVmDetailHost.Visibility = projection.ActiveStep == BuilderWorkflowStep.Vms && !projection.IsVmOverviewSelected ? Visibility.Visible : Visibility.Collapsed;

        RenderWorkflowTreeState(projection);
        UpdateFooterCommandState();
    }

    private void RenderWorkflowTreeState()
    {
        RenderWorkflowTreeState(_workflowNavigation.Project(_draft, _canNavigateWorkflow));
    }

    private void RenderWorkflowTreeState(BuilderWorkflowProjection projection)
    {
        BuilderWorkflowTreePanel.Children.Clear();
        foreach (var row in projection.StepRows.Take(4))
        {
            BuilderWorkflowTreePanel.Children.Add(CreateResourceButton(row.Label, row.IsSelected, () => SelectStep(row.Route.Step), row.IsEnabled));
        }

        BuilderWorkflowTreePanel.Children.Add(CreateVmWorkflowNavRow(projection.VmOverviewRow));
        BuilderWorkflowTreePanel.Children.Add(BuilderVmNavChildrenPanel);
        var reviewRow = projection.StepRows.Last();
        BuilderWorkflowTreePanel.Children.Add(CreateResourceButton(reviewRow.Label, reviewRow.IsSelected, () => SelectStep(reviewRow.Route.Step), reviewRow.IsEnabled));
        RenderVmNavChildren(projection);
    }

    private Grid CreateVmWorkflowNavRow(BuilderWorkflowNavigationRow vmOverviewRow)
    {
        var row = new Grid
        {
            ColumnSpacing = 4
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var vmButton = CreateResourceButton(vmOverviewRow.Label, vmOverviewRow.IsSelected, SelectVmOverview, vmOverviewRow.IsEnabled);
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

    private void UpdateFooterCommandState()
    {
        UpdateFooterCommandState(_actionState);
    }

    private void UpdateFooterCommandState(TemplatesBuilderActionState state)
    {
        var projection = _workflowNavigation.ProjectFooter(_draft, _canNavigateWorkflow, state.CanSave, state.CanSaveAs);

        BuilderPreviousStepButton.IsEnabled = projection.CanGoPrevious;
        BuilderNextStepButton.IsEnabled = projection.CanGoNext;
        BuilderNextStepButton.Visibility = projection.IsReview ? Visibility.Collapsed : Visibility.Visible;
        BuilderSaveAsButton.IsEnabled = projection.CanSaveAs;
        BuilderSaveButton.IsEnabled = projection.CanSave;
        BuilderSaveAsButton.Visibility = projection.IsReview ? Visibility.Visible : Visibility.Collapsed;
        BuilderSaveButton.Visibility = projection.IsReview ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWorkflowTreeActionState()
    {
        foreach (var button in FindDescendants<Button>(BuilderWorkflowTreePanel))
        {
            button.IsEnabled = _canNavigateWorkflow;
        }
    }

    private void RenderDraftResources(bool refreshWorkflowTreeChildren = true)
    {
        EnsureSelectedResourcesInBounds();
        RenderResourceLists(refreshWorkflowTreeChildren);
        RenderSelectedNetworkDetail();
        RenderSelectedCredentialSlotDetail();
        RenderSelectedForestDomainDetail();
        RenderVmOverview();
        RenderSelectedVmDetail();
        RenderReviewValidationState();
    }

    private void RenderVmOverview()
    {
        var projection = TemplatesBuilderSectionProjections.ProjectVmOverview(_draft);
        BuilderVmTotalCountTextBlock.Text = projection.TotalVmCount.ToString();
        BuilderVmMembershipCountsTextBlock.Text = $"Standalone {projection.StandaloneVmCount} / Domain {projection.DomainMemberVmCount}";
        BuilderVmAdDcCountTextBlock.Text = projection.ActiveDirectoryDomainControllerCount.ToString();

        BuilderVmOverviewListPanel.Children.Clear();
        if (projection.TotalVmCount == 0)
        {
            BuilderVmOverviewListPanel.Children.Add(CreateEmptyDetailText("No VMs in this draft."));
            return;
        }

        foreach (var row in projection.SummaryRows)
        {
            BuilderVmOverviewListPanel.Children.Add(CreateEmptyDetailText(row));
        }
    }

    private void RenderResourceLists(bool refreshWorkflowTreeChildren = true)
    {
        RenderNetworkList();
        RenderCredentialSlotList();
        RenderForestDomainList();
        if (refreshWorkflowTreeChildren)
        {
            RenderVmNavChildren();
        }
    }

    private void RenderNetworkList()
    {
        BuilderNetworksListPanel.Children.Clear();
        foreach (var row in TemplatesBuilderSectionProjections.ProjectNetworkRows(_draft, _selectedNetworkIndex))
        {
            BuilderNetworksListPanel.Children.Add(CreateResourceButton(
                row.Label,
                row.IsSelected,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedNetworkIndex = row.Index;
                    RenderNetworkList();
                    RenderSelectedNetworkDetail();
                }));
        }
    }

    private void RenderCredentialSlotList()
    {
        BuilderCredentialSlotsListPanel.Children.Clear();
        foreach (var row in TemplatesBuilderSectionProjections.ProjectCredentialSlotRows(_draft, _selectedCredentialSlotIndex))
        {
            BuilderCredentialSlotsListPanel.Children.Add(CreateResourceButton(
                row.Label,
                row.IsSelected,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedCredentialSlotIndex = row.Index;
                    RenderCredentialSlotList();
                    RenderSelectedCredentialSlotDetail();
                }));
        }
    }

    private void RenderForestDomainList()
    {
        BuilderForestDomainResourcesListPanel.Children.Clear();
        foreach (var row in TemplatesBuilderSectionProjections.ProjectForestDomainRows(_draft, _selectedForestDomainKind, _selectedForestDomainIndex))
        {
            BuilderForestDomainResourcesListPanel.Children.Add(CreateResourceButton(
                row.Label,
                row.IsSelected,
                () =>
                {
                    UpdateWorkingDraftFromVisibleControls();
                    _selectedForestDomainKind = row.Kind == TemplatesBuilderResourceKind.Forest
                        ? BuilderForestDomainResourceKind.Forest
                        : BuilderForestDomainResourceKind.Domain;
                    _selectedForestDomainIndex = row.Index;
                    RenderForestDomainList();
                    RenderSelectedForestDomainDetail();
                }));
        }
    }

    private void RenderVmNavChildren()
    {
        RenderVmNavChildren(_workflowNavigation.Project(_draft, _canNavigateWorkflow));
    }

    private void RenderVmNavChildren(BuilderWorkflowProjection projection)
    {
        BuilderVmNavChildrenPanel.Children.Clear();
        foreach (var group in projection.VmRows)
        {
            var row = group.VmRow;
            var button = CreateResourceButton(
                row.Label,
                row.IsSelected,
                () => SelectVmChild(row.Route.VmIndex),
                row.IsEnabled,
                isNested: true);
            BuilderVmNavChildrenPanel.Children.Add(button);

            var categoryPanel = new StackPanel
            {
                Margin = new Thickness(14, 0, 0, 0),
                Spacing = 4
            };
            foreach (var categoryRow in group.CategoryRows)
            {
                categoryPanel.Children.Add(CreateResourceButton(
                    categoryRow.Label,
                    categoryRow.IsSelected,
                    () => SelectVmDetailCategory(categoryRow.Route.VmIndex, categoryRow.Route.VmDetailCategory),
                    categoryRow.IsEnabled,
                    isNested: true));
            }

            BuilderVmNavChildrenPanel.Children.Add(categoryPanel);
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
            CreateTextBox("Network ID", TemplatesBuilderFieldKeys.NetworkId, network.NetworkId),
            CreateTextBox("Name", TemplatesBuilderFieldKeys.NetworkName, network.Name),
            CreateTextBox("Switch", TemplatesBuilderFieldKeys.NetworkSwitchName, network.SwitchName),
            CreateTextBox("Subnet", TemplatesBuilderFieldKeys.NetworkSubnet, network.Subnet),
            CreateTextBox("Notes", TemplatesBuilderFieldKeys.NetworkNotes, network.Notes)));
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
            CreateTextBox("Slot Key", TemplatesBuilderFieldKeys.CredentialSlotKey, slot.SlotKey),
            CreateTextBox("Label", TemplatesBuilderFieldKeys.CredentialSlotLabel, slot.Label),
            CreateTextBox("Scope", TemplatesBuilderFieldKeys.CredentialSlotScopeHint, slot.ScopeHint)));
    }

    private void RenderSelectedForestDomainDetail()
    {
        BuilderSelectedForestDomainDetailPanel.Children.Clear();
        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest && _draft.Forests.Count > 0)
        {
            var forest = _draft.Forests[_selectedForestDomainIndex];
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateRowTitle("Selected Forest Detail"));
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateFieldGrid(
                CreateTextBox("Forest ID", TemplatesBuilderFieldKeys.ForestId, forest.ForestId),
                CreateTextBox("Root Domain ID", TemplatesBuilderFieldKeys.ForestRootDomainId, forest.RootDomainId)));
            return;
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain && _draft.Domains.Count > 0)
        {
            var domain = _draft.Domains[_selectedForestDomainIndex];
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateRowTitle("Selected Domain Detail"));
            BuilderSelectedForestDomainDetailPanel.Children.Add(CreateFieldGrid(
                CreateTextBox("Domain ID", TemplatesBuilderFieldKeys.DomainId, domain.DomainId),
                CreateTextBox("DNS Name", TemplatesBuilderFieldKeys.DomainDnsName, domain.DnsName),
                CreateTextBox("NetBIOS", TemplatesBuilderFieldKeys.DomainNetBiosName, domain.NetBiosName),
                CreateTextBox("Forest ID", TemplatesBuilderFieldKeys.DomainForestId, domain.ForestId),
                CreateComboBox("Relation", TemplatesBuilderFieldKeys.DomainRelationKind, domain.RelationKind, nameof(V2DomainRelationKind.Root), nameof(V2DomainRelationKind.Child), nameof(V2DomainRelationKind.Tree)),
                CreateTextBox("Parent Domain ID", TemplatesBuilderFieldKeys.DomainParentDomainId, domain.ParentDomainId)));
            return;
        }

        BuilderSelectedForestDomainDetailPanel.Children.Add(CreateEmptyDetailText("No forest or domain selected."));
    }

    private void RenderSelectedVmDetail()
    {
        var projection = _workflowNavigation.Project(_draft, _canNavigateWorkflow);
        BuilderSelectedVmDetailPanel.Children.Clear();
        var detail = TemplatesBuilderSectionProjections.ProjectSelectedVmDetail(_draft, projection);
        if (detail is null)
        {
            BuilderSelectedVmDetailPanel.Children.Add(CreateEmptyDetailText("No VM selected."));
            return;
        }

        var vm = detail.Value.Vm;
        BuilderSelectedVmDetailPanel.Children.Add(CreateRowTitle(detail.Value.Title));
        BuilderSelectedVmDetailPanel.Children.Add(CreateSubhead(detail.Value.CategoryLabel));

        switch (detail.Value.Category)
        {
            case BuilderVmDetailCategory.Basics:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateTextBox("VM ID", TemplatesBuilderFieldKeys.VmId, vm.VmId),
                    CreateTextBox("Name", TemplatesBuilderFieldKeys.VmName, vm.Name)));
                break;
            case BuilderVmDetailCategory.Resources:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateTextBox("Memory MB", TemplatesBuilderFieldKeys.VmMemoryMb, vm.MemoryMb),
                    CreateTextBox("CPU Count", TemplatesBuilderFieldKeys.VmCpuCount, vm.CpuCount),
                    CreateTextBox("Base Disk / VHDX ID", TemplatesBuilderFieldKeys.VmVhdxId, vm.VhdxId)));
                break;
            case BuilderVmDetailCategory.Membership:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateComboBox("Membership", TemplatesBuilderFieldKeys.VmMembershipMode, vm.MembershipMode, V2MembershipModeCatalog.DomainMember, V2MembershipModeCatalog.Standalone),
                    CreateTextBox("Domain ID", TemplatesBuilderFieldKeys.VmDomainId, vm.DomainId)));
                break;
            case BuilderVmDetailCategory.Roles:
                foreach (var role in detail.Value.Roles.Where(role => role.IsAuthorable))
                {
                    BuilderSelectedVmDetailPanel.Children.Add(CreateCheckBox(role.DisplayName, TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController, role.IsAssigned));
                }

                break;
            case BuilderVmDetailCategory.Networking:
                var addNicButton = new Button { Content = "Add NIC", Tag = detail.Value.VmIndex };
                addNicButton.Click += BuilderAddNicButton_Click;
                BuilderSelectedVmDetailPanel.Children.Add(addNicButton);
                var nicsPanel = new StackPanel { Spacing = 6, Tag = TemplatesBuilderFieldKeys.VmNicsPanel };
                foreach (var nic in detail.Value.Nics)
                {
                    nicsPanel.Children.Add(CreateNicRow(nic));
                }

                BuilderSelectedVmDetailPanel.Children.Add(nicsPanel);
                break;
            case BuilderVmDetailCategory.Credentials:
                BuilderSelectedVmDetailPanel.Children.Add(CreateFieldGrid(
                    CreateTextBox("Local Bootstrap Slot", TemplatesBuilderFieldKeys.VmLocalBootstrap, vm.CredentialSlots.LocalBootstrap),
                    CreateTextBox("Domain Admin Slot", TemplatesBuilderFieldKeys.VmDomainAdmin, vm.CredentialSlots.DomainAdmin),
                    CreateTextBox("Domain Join Slot", TemplatesBuilderFieldKeys.VmDomainJoin, vm.CredentialSlots.DomainJoin),
                    CreateTextBox("DSRM Slot", TemplatesBuilderFieldKeys.VmDsrm, vm.CredentialSlots.Dsrm),
                    CreateTextBox("Parent Domain Admin Slot", TemplatesBuilderFieldKeys.VmParentDomainAdmin, vm.CredentialSlots.ParentDomainAdmin)));
                break;
        }
    }

    private StackPanel CreateNicRow(TemplatesBuilderNicRowProjection projection)
    {
        var nic = projection.Draft;
        var row = CreateRow();
        row.Tag = TemplatesBuilderFieldKeys.NicRow;
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("NIC ID", TemplatesBuilderFieldKeys.NicId, nic.NicId),
            CreateTextBox("Name", TemplatesBuilderFieldKeys.NicName, nic.Name),
            CreateTextBox("Network ID", TemplatesBuilderFieldKeys.NicNetworkId, nic.NetworkId),
            CreateTextBox("Switch", TemplatesBuilderFieldKeys.NicSwitchName, nic.SwitchName),
            CreateTextBox("IP Address", TemplatesBuilderFieldKeys.NicIpAddress, nic.IpAddress),
            CreateTextBox("Prefix", TemplatesBuilderFieldKeys.NicPrefixLength, nic.PrefixLength),
            CreateTextBox("Gateway", TemplatesBuilderFieldKeys.NicDefaultGateway, nic.DefaultGateway),
            CreateTextBox("DNS Servers", TemplatesBuilderFieldKeys.NicDnsServers, string.Join(", ", nic.DnsServers))));
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
            DeploymentProfile = GetSelectedProfile()
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
            !HasTextBox(BuilderSelectedNetworkDetailPanel, TemplatesBuilderFieldKeys.NetworkId))
        {
            return draft;
        }

        var networks = draft.LabNetworks.ToList();
        networks[_selectedNetworkIndex] = new TemplatesBuilderLabNetworkDraft(
            GetText(BuilderSelectedNetworkDetailPanel, TemplatesBuilderFieldKeys.NetworkId),
            GetText(BuilderSelectedNetworkDetailPanel, TemplatesBuilderFieldKeys.NetworkName),
            GetText(BuilderSelectedNetworkDetailPanel, TemplatesBuilderFieldKeys.NetworkSwitchName),
            GetText(BuilderSelectedNetworkDetailPanel, TemplatesBuilderFieldKeys.NetworkSubnet),
            GetText(BuilderSelectedNetworkDetailPanel, TemplatesBuilderFieldKeys.NetworkNotes));
        return draft with { LabNetworks = networks };
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedCredentialSlot(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedCredentialSlotIndex < 0 ||
            _selectedCredentialSlotIndex >= draft.CredentialSlots.Count ||
            !HasTextBox(BuilderSelectedCredentialSlotDetailPanel, TemplatesBuilderFieldKeys.CredentialSlotKey))
        {
            return draft;
        }

        var slots = draft.CredentialSlots.ToList();
        slots[_selectedCredentialSlotIndex] = new TemplatesBuilderCredentialSlotDraft(
            GetText(BuilderSelectedCredentialSlotDetailPanel, TemplatesBuilderFieldKeys.CredentialSlotKey),
            GetText(BuilderSelectedCredentialSlotDetailPanel, TemplatesBuilderFieldKeys.CredentialSlotLabel),
            GetText(BuilderSelectedCredentialSlotDetailPanel, TemplatesBuilderFieldKeys.CredentialSlotScopeHint));
        return draft with { CredentialSlots = slots };
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedForestOrDomain(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Forests.Count &&
            HasTextBox(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.ForestId))
        {
            var forests = draft.Forests.ToList();
            forests[_selectedForestDomainIndex] = new TemplatesBuilderForestDraft(
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.ForestId),
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.ForestRootDomainId));
            return draft with { Forests = forests };
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Domains.Count &&
            HasTextBox(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainId))
        {
            var domains = draft.Domains.ToList();
            domains[_selectedForestDomainIndex] = new TemplatesBuilderDomainDraft(
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainId),
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainDnsName),
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainNetBiosName),
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainForestId),
                GetComboValue(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainRelationKind),
                GetText(BuilderSelectedForestDomainDetailPanel, TemplatesBuilderFieldKeys.DomainParentDomainId));
            return draft with { Domains = domains };
        }

        return draft;
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedVm(TemplatesBuilderDraftSnapshot draft)
    {
        var projection = _workflowNavigation.Project(draft, _canNavigateWorkflow);
        if (!projection.IsVmDetailSelected)
        {
            return draft;
        }

        if (projection.SelectedVmIndex < 0 ||
            projection.SelectedVmIndex >= draft.Vms.Count)
        {
            return draft;
        }

        var vms = draft.Vms.ToList();
        var vm = vms[projection.SelectedVmIndex];
        vms[projection.SelectedVmIndex] = projection.SelectedVmDetailCategory switch
        {
            BuilderVmDetailCategory.Basics when HasTextBox(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmId) => vm with
            {
                VmId = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmId),
                Name = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmName)
            },
            BuilderVmDetailCategory.Resources when HasTextBox(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmMemoryMb) => vm with
            {
                MemoryMb = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmMemoryMb),
                CpuCount = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmCpuCount),
                VhdxId = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmVhdxId)
            },
            BuilderVmDetailCategory.Membership when HasComboBox(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmMembershipMode) => vm with
            {
                MembershipMode = GetComboValue(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmMembershipMode),
                DomainId = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmDomainId)
            },
            BuilderVmDetailCategory.Roles when HasCheckBox(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController) => vm with
            {
                IsActiveDirectoryDomainController = GetCheckBoxValue(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController)
            },
            BuilderVmDetailCategory.Networking when HasNicsPanel(BuilderSelectedVmDetailPanel) => vm with
            {
                Nics = ReadNics(BuilderSelectedVmDetailPanel)
            },
            BuilderVmDetailCategory.Credentials when HasTextBox(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmLocalBootstrap) => vm with
            {
                CredentialSlots = new TemplatesBuilderVmCredentialSlotDraft(
                    GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmLocalBootstrap),
                    GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmDomainAdmin),
                    GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmDomainJoin),
                    GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmDsrm),
                    GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmParentDomainAdmin))
            },
            _ => vm
        };
        return draft with { Vms = vms };
    }

    private List<TemplatesBuilderNicDraft> ReadNics(DependencyObject root)
    {
        var nicsPanel = FindDescendants<StackPanel>(root).FirstOrDefault(panel => Equals(panel.Tag, TemplatesBuilderFieldKeys.VmNicsPanel));
        return nicsPanel?.Children.OfType<StackPanel>().Select(ReadNic).ToList() ?? [];
    }

    private TemplatesBuilderNicDraft ReadNic(StackPanel row)
        => new(
            GetText(row, TemplatesBuilderFieldKeys.NicId),
            GetText(row, TemplatesBuilderFieldKeys.NicName),
            GetText(row, TemplatesBuilderFieldKeys.NicNetworkId),
            GetText(row, TemplatesBuilderFieldKeys.NicSwitchName),
            GetText(row, TemplatesBuilderFieldKeys.NicIpAddress),
            GetText(row, TemplatesBuilderFieldKeys.NicPrefixLength),
            GetText(row, TemplatesBuilderFieldKeys.NicDefaultGateway),
            SplitList(GetText(row, TemplatesBuilderFieldKeys.NicDnsServers)));

    private void BuilderDraftControl_Changed(object sender, TextChangedEventArgs e)
    {
        NotifyDraftChanged(sender);
    }

    private void BuilderSelectionControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        NotifyDraftChanged(sender);
    }

    private void BuilderCheckBox_Changed(object sender, RoutedEventArgs e)
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
        var updatedDraft = draft with { Vms = vms, IsSaveConfirmed = false };
        _workflowNavigation.SelectVmChild(vms.Count - 1, updatedDraft);
        RenderAndNotify(updatedDraft);
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
            RenderDraftResources(refreshWorkflowTreeChildren: false);
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
        RenderReviewValidationState();
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
            DeploymentProfile = GetSelectedProfile()
        };

        if (sender is not FrameworkElement { Tag: BuilderDraftFieldKey fieldKey })
        {
            return;
        }

        switch (fieldKey.Scope)
        {
            case BuilderDraftFieldScope.Network:
                _draft = UpdateSelectedNetwork(_draft);
                break;
            case BuilderDraftFieldScope.CredentialSlot:
                _draft = UpdateSelectedCredentialSlot(_draft);
                break;
            case BuilderDraftFieldScope.Forest:
            case BuilderDraftFieldScope.Domain:
                _draft = UpdateSelectedForestOrDomain(_draft);
                break;
            case BuilderDraftFieldScope.Vm:
            case BuilderDraftFieldScope.Nic:
                _draft = UpdateSelectedVm(_draft);
                break;
        }
    }

    private void EnsureSelectedResourcesInBounds()
    {
        _selectedNetworkIndex = ClampIndex(_selectedNetworkIndex, _draft.LabNetworks.Count);
        _selectedCredentialSlotIndex = ClampIndex(_selectedCredentialSlotIndex, _draft.CredentialSlots.Count);
        _workflowNavigation.EnsureCurrentRouteInBounds(_draft);

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
        => _selectedDeploymentProfile;

    private void SetSelectedProfile(string profile)
    {
        _selectedDeploymentProfile = NormalizeDeploymentProfile(profile);
        UpdateDeploymentProfileButtonState();
    }

    private void ConfigureDeploymentProfileButton(Button button, string profile)
    {
        button.Tag = profile;
        ConfigureNavButtonChrome(button);
        button.Click += BuilderDeploymentProfileButton_Click;
    }

    private void BuilderDeploymentProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string profile })
        {
            return;
        }

        SetSelectedProfile(profile);
        NotifyDraftChanged((Button)sender);
    }

    private void UpdateDeploymentProfileButtonState()
    {
        UpdateDeploymentProfileButton(BuilderDeploymentProfileConservativeButton, ConservativeDeploymentProfile);
        UpdateDeploymentProfileButton(BuilderDeploymentProfileBalancedButton, BalancedDeploymentProfile);
        UpdateDeploymentProfileButton(BuilderDeploymentProfileAggressiveButton, AggressiveDeploymentProfile);
    }

    private void UpdateDeploymentProfileButton(Button button, string profile)
    {
        var isSelected = string.Equals(_selectedDeploymentProfile, profile, StringComparison.OrdinalIgnoreCase);
        button.Content = CreateNavButtonContent(profile, isSelected);
    }

    private static string NormalizeDeploymentProfile(string profile)
    {
        if (string.Equals(profile, ConservativeDeploymentProfile, StringComparison.OrdinalIgnoreCase))
        {
            return ConservativeDeploymentProfile;
        }

        if (string.Equals(profile, AggressiveDeploymentProfile, StringComparison.OrdinalIgnoreCase))
        {
            return AggressiveDeploymentProfile;
        }

        return BalancedDeploymentProfile;
    }

    private TextBox CreateTextBox(string header, BuilderDraftFieldKey fieldKey, string value)
    {
        var textBox = new TextBox
        {
            Header = header,
            Tag = fieldKey,
            Text = value,
            MinWidth = 150
        };
        textBox.TextChanged += BuilderDraftControl_Changed;
        return textBox;
    }

    private ComboBox CreateComboBox(string header, BuilderDraftFieldKey fieldKey, string value, params string[] options)
    {
        var comboBox = new ComboBox
        {
            Header = header,
            Tag = fieldKey,
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

    private CheckBox CreateCheckBox(string content, BuilderDraftFieldKey fieldKey, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Content = content,
            Tag = fieldKey,
            IsChecked = isChecked
        };
        checkBox.Checked += BuilderCheckBox_Changed;
        checkBox.Unchecked += BuilderCheckBox_Changed;
        return checkBox;
    }

    private Button CreateResourceButton(string content, bool isSelected, Action select, bool isEnabled = true, bool isNested = false)
    {
        var button = new Button
        {
            Background = CreateTransparentBrush(),
            BorderThickness = new Thickness(0),
            Content = CreateNavButtonContent(content, isSelected, isNested),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsEnabled = isEnabled,
            Padding = new Thickness(0)
        };
        ConfigureNavButtonChrome(button);
        ToolTipService.SetToolTip(button, content);
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

    private static string GetText(DependencyObject root, BuilderDraftFieldKey fieldKey)
        => FindDescendants<TextBox>(root).FirstOrDefault(textBox => Equals(textBox.Tag, fieldKey))?.Text ?? string.Empty;

    private static bool HasTextBox(DependencyObject root, BuilderDraftFieldKey fieldKey)
        => FindDescendants<TextBox>(root).Any(textBox => Equals(textBox.Tag, fieldKey));

    private static bool HasComboBox(DependencyObject root, BuilderDraftFieldKey fieldKey)
        => FindDescendants<ComboBox>(root).Any(comboBox => Equals(comboBox.Tag, fieldKey));

    private static bool HasCheckBox(DependencyObject root, BuilderDraftFieldKey fieldKey)
        => FindDescendants<CheckBox>(root).Any(checkBox => Equals(checkBox.Tag, fieldKey));

    private static bool HasNicsPanel(DependencyObject root)
        => FindDescendants<StackPanel>(root).Any(panel => Equals(panel.Tag, TemplatesBuilderFieldKeys.VmNicsPanel));

    private static string GetComboValue(DependencyObject root, BuilderDraftFieldKey fieldKey)
    {
        var comboBox = FindDescendants<ComboBox>(root).FirstOrDefault(item => Equals(item.Tag, fieldKey));
        if (comboBox?.SelectedItem is ComboBoxItem { Content: string content })
        {
            return content;
        }

        return string.Empty;
    }

    private static bool GetCheckBoxValue(DependencyObject root, BuilderDraftFieldKey fieldKey)
        => FindDescendants<CheckBox>(root).FirstOrDefault(item => Equals(item.Tag, fieldKey))?.IsChecked == true;

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

    private static Border CreateNavButtonContent(string content, bool isSelected, bool isNested = false)
        => new()
        {
            Padding = isNested ? new Thickness(7, 4, 7, 4) : new Thickness(8, 5, 8, 5),
            Background = GetBrush(isSelected ? "ShellBackgroundBrush" : "ShellContentBackgroundBrush"),
            BorderBrush = GetBrush(isSelected ? "ShellAccentBrush" : "ShellBorderBrush"),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = GetBrush(isSelected ? "ShellAccentBrush" : "ShellTextPrimaryBrush"),
                MaxLines = 1,
                Text = content,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
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

    private void RenderReviewValidationState()
    {
        BuilderReviewSummaryTextBlock.Text = $"{BuildReviewSummary(_draft)} {_validationState.BuildReviewSummary()}";
        BuilderReviewBlockerTextBlock.Text = _validationState.HasBlockers
            ? string.Join(Environment.NewLine, _validationState.Blockers.Select(issue => issue.Message))
            : _validationState.Warnings.Count > 0
                ? string.Join(Environment.NewLine, _validationState.Warnings.Select(issue => issue.Message))
                : "Builder validation has no current blockers or warnings.";
        BuilderReviewBlockerTextBlock.Visibility = _validationState.HasBlockers || _validationState.Warnings.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
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
}
