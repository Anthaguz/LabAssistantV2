using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Templates.Builder;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// V2 Template Builder subview model. Full x:Bind MVVM: it owns the draft, the workflow navigation
/// state, per-step/section field view models, and the create/show/save/save-as/navigation commands,
/// folding the logic of the dissolved builder workspace view model + controller + composition into a
/// single testable view model. All draft/topology/section/validation shaping stays in the untouched
/// pure Builder helpers; the contractual scoped-revalidation merge lives in
/// <see cref="TemplatesBuilderValidationMerger"/>. Reference data, library reload, the Save As file
/// picker, and cross-subview navigation are injected via <see cref="Attach"/>, so the model is
/// unit-testable without a dispatcher or Hyper-V. Registered transient; created and torn down with the
/// hosting <c>TemplatesPage</c>.
/// </summary>
public partial class TemplatesBuilderViewModel : ViewModelBase
{
    private const string ConservativeDeploymentProfile = "Conservative";
    private const string BalancedDeploymentProfile = "Balanced";
    private const string AggressiveDeploymentProfile = "Aggressive";

    private static readonly BuilderDraftFieldKey DisplayOnlyFieldKey = new(BuilderDraftFieldScope.Network, "__display_only__");

    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly TemplatesBuilderWorkflowNavigation _navigation = new();

    private ITemplatesBuilderHost? _host;

    // The working draft (what the visible fields describe) and the last-validated snapshot the scoped
    // revalidation diffs against. Both are updated together at the end of every apply so a subsequent
    // edit diffs against the previously validated state, not the just-mutated working draft.
    private TemplatesBuilderDraftSnapshot _draft = CreateEmptyDraft();
    private TemplatesBuilderDraftSnapshot _validatedDraft = CreateEmptyDraft();
    private TemplatesBuilderValidationState _validationState = TemplatesBuilderValidationState.Empty;
    private IReadOnlyList<V2AvailableSwitchInfo> _availableSwitchInventory = Array.Empty<V2AvailableSwitchInfo>();
    private IReadOnlyList<TemplateVhdxCatalogOption> _vhdxCatalogOptions = Array.Empty<TemplateVhdxCatalogOption>();

    private string _templateId = Guid.NewGuid().ToString("N");
    private int _templateRevision = 1;
    private string _createdWithAppVersion = "1.0.0";
    private string? _sourceFilePath;

    private bool _hasActiveDraft;
    private bool _canNavigate;
    private string _selectedDeploymentProfile = BalancedDeploymentProfile;
    private int _selectedNetworkIndex;
    private int _selectedCredentialSlotIndex;
    private BuilderForestDomainResourceKind _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
    private int _selectedForestDomainIndex;

    // Level 2 (machine authoring) state. When zoomed in, the active container is either a domain
    // (_machineContainerId = domain id, _isStandaloneContainer = false) or the Standalone container
    // (_isStandaloneContainer = true). _selectedMachineVmIndex indexes draft.Vms for the highlighted card.
    private string _machineContainerId = string.Empty;
    private bool _isStandaloneContainer;
    private int _selectedMachineVmIndex = -1;
    private readonly HashSet<string> _expandedRoleConfigKeys = new(StringComparer.OrdinalIgnoreCase);

    // Persistent trailing tile for the Level 2 machine grid. Its IsEnabled tracks AddVmEnabled so the
    // placeholder can always be present in MachineGridItems and simply disable when adding is not allowed.
    private readonly BuilderAddMachinePlaceholder _machinePlaceholder;

    // Guards programmatic field/observable writes so refreshing the forms does not re-enter the edit
    // pipeline (the imperative view guarded the same recompute with _isUpdatingDraft).
    private bool _isApplyingDraft;

    // True only while RebuildSelectedMachineInspector is assigning SelectedMachineInspector. That assignment
    // re-binds the base-disk ComboBox, which raises a synchronous SelectionChanged echo on the same call stack;
    // suppressing commits during the window stops the echo from re-entering the render pipeline and overflowing
    // the stack. This is the render-boundary backstop to the value guard in CommitSelectedMachineBaseDisk.
    private bool _isRebuildingMachineInspector;

    // Live field view models per detail scope; the edit pipeline reads working values straight off
    // these instead of scanning the visual tree by field key.
    private IReadOnlyList<BuilderFieldViewModel> _networkFields = Array.Empty<BuilderFieldViewModel>();
    private IReadOnlyList<BuilderFieldViewModel> _credentialFields = Array.Empty<BuilderFieldViewModel>();
    private IReadOnlyList<BuilderFieldViewModel> _forestDomainFields = Array.Empty<BuilderFieldViewModel>();
    private IReadOnlyList<BuilderFieldViewModel> _vmDetailFields = Array.Empty<BuilderFieldViewModel>();

    public TemplatesBuilderViewModel(ITemplatesCapabilityService templatesCapabilityService)
    {
        _templatesCapabilityService = templatesCapabilityService;
        _machinePlaceholder = new BuilderAddMachinePlaceholder("+ Add computer", AddComputerCommand);
        PropertyChanged += OnSelfPropertyChanged;
        RebuildDeploymentProfileRows();
        RenderAll();
    }

    // Header.
    [ObservableProperty] private string _contextText = "No Builder draft loaded.";
    [ObservableProperty] private string _referenceText = "No reference data loaded.";
    [ObservableProperty] private string _statusText = "Create or open a V2 Builder draft.";
    [ObservableProperty] private bool _isStatusVisible;
    [ObservableProperty] private string _validationChipText = "Valid";
    [ObservableProperty] private bool _validationChipIsOk = true;
    [ObservableProperty] private string _builderBreadcrumbText = "Topology";
    [ObservableProperty] private ObservableCollection<BuilderValidationIssueRow> _validationIssues = [];
    [ObservableProperty] private bool _hasValidationIssues;

    // General.
    [ObservableProperty] private string _templateName = string.Empty;
    [ObservableProperty] private string _templateDescription = string.Empty;
    [ObservableProperty] private ObservableCollection<BuilderNavRowViewModel> _deploymentProfileRows = [];

    // Step visibility.
    [ObservableProperty] private bool _isGeneralVisible = true;
    [ObservableProperty] private bool _isNetworksVisible;
    [ObservableProperty] private bool _isForestsDomainsVisible;
    [ObservableProperty] private bool _isCredentialsVisible;
    [ObservableProperty] private bool _isVmsVisible;
    [ObservableProperty] private bool _isReviewVisible;
    [ObservableProperty] private bool _isVmOverviewVisible;
    [ObservableProperty] private bool _isVmDetailVisible;

    // Navigator.
    [ObservableProperty] private bool _isNavigatorHeaderVisible;
    [ObservableProperty] private string _navigatorTitle = "Builder";
    [ObservableProperty] private string _navigatorBackTooltip = "Back to Builder";
    [ObservableProperty] private bool _navigatorBackEnabled;
    [ObservableProperty] private ObservableCollection<BuilderNavRowViewModel> _navigatorRows = [];

    // Networks.
    [ObservableProperty] private bool _addNetworkEnabled;
    [ObservableProperty] private ObservableCollection<BuilderNavRowViewModel> _networkRows = [];
    [ObservableProperty] private string _networkDetailTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<BuilderFieldRowViewModel> _networkDetailRows = [];
    [ObservableProperty] private bool _hasNetworkDetail;
    [ObservableProperty] private bool _networkDetailEmpty = true;

    // Forests & Domains.
    [ObservableProperty] private bool _addForestEnabled;
    [ObservableProperty] private bool _addTreeEnabled;
    [ObservableProperty] private bool _addStandaloneMachineEnabled;
    [ObservableProperty] private BuilderTopologyCanvasViewModel? _topologyCanvas;
    [ObservableProperty] private bool _hasTopologyForests;
    [ObservableProperty] private string _forestDomainDetailTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<BuilderFieldRowViewModel> _forestDomainDetailRows = [];
    [ObservableProperty] private bool _hasForestDomainDetail;
    [ObservableProperty] private bool _forestDomainDetailEmpty = true;
    [ObservableProperty] private string _domainSubnetValue = string.Empty;
    [ObservableProperty] private bool _showDomainSubnetEditor;
    [ObservableProperty] private string _domainSubnetValidationMessage = string.Empty;
    [ObservableProperty] private bool _hasDomainSubnetValidationMessage;
    [ObservableProperty] private bool _showDomainSubnetErrorOutline;

    // Forest trust authoring (Level 1 detail panel, only when a forest is selected and >=2 forests exist).
    [ObservableProperty] private bool _canAuthorForestTrust;
    [ObservableProperty] private ObservableCollection<BuilderForestTrustTargetViewModel> _forestTrustTargets = [];
    [ObservableProperty] private BuilderForestTrustTargetViewModel? _selectedForestTrustTarget;
    [ObservableProperty] private ObservableCollection<BuilderForestTrustRowViewModel> _forestTrustRows = [];

    // Forests & Domains - Level 2 (machines inside one domain or the Standalone container).
    [ObservableProperty] private bool _isMachineLevelVisible;
    [ObservableProperty] private string _machineLevelTitle = string.Empty;
    [ObservableProperty] private string _machineLevelSubtitle = string.Empty;
    [ObservableProperty] private ObservableCollection<BuilderMachineCardViewModel> _machineCards = [];
    // View-only grid source: the machine cards followed by the trailing add-computer placeholder tile.
    // Kept separate from MachineCards (which stays machine-only) so the placeholder can flow in the same wrap.
    [ObservableProperty] private ObservableCollection<object> _machineGridItems = [];
    [ObservableProperty] private bool _hasMachineCards;
    [ObservableProperty] private string _machineLevelEmptyText = "No machines yet.";
    [ObservableProperty] private string _addComputerLabel = "+ Add computer";
    [ObservableProperty] private BuilderMachineInspectorViewModel? _selectedMachineInspector;
    [ObservableProperty] private bool _hasSelectedMachineInspector;
    [ObservableProperty] private string _roleSearchText = string.Empty;
    [ObservableProperty] private bool _isRolesPanelExpanded = true;
    [ObservableProperty] private bool _isFeaturesPanelExpanded;

    // Credentials.
    [ObservableProperty] private bool _addCredentialEnabled;
    [ObservableProperty] private ObservableCollection<BuilderNavRowViewModel> _credentialRows = [];
    [ObservableProperty] private string _credentialDetailTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<BuilderFieldRowViewModel> _credentialDetailRows = [];
    [ObservableProperty] private bool _hasCredentialDetail;
    [ObservableProperty] private bool _credentialDetailEmpty = true;

    // VM overview.
    [ObservableProperty] private bool _addVmEnabled;
    [ObservableProperty] private string _vmTotalCountText = "0";
    [ObservableProperty] private string _vmMembershipCountsText = "Standalone 0 / Domain 0";
    [ObservableProperty] private string _vmAdDcCountText = "0";
    [ObservableProperty] private ObservableCollection<string> _vmOverviewRows = [];
    [ObservableProperty] private bool _hasVmOverviewRows;

    // VM detail.
    [ObservableProperty] private string _vmDetailTitle = string.Empty;
    [ObservableProperty] private bool _hasVmDetailTitle;
    [ObservableProperty] private string _vmDetailCategoryLabel = string.Empty;
    [ObservableProperty] private bool _hasVmDetailCategoryLabel;
    [ObservableProperty] private ObservableCollection<BuilderFieldRowViewModel> _vmDetailFieldRows = [];
    [ObservableProperty] private bool _hasVmDetailFieldRows;
    [ObservableProperty] private string _vmDetailNicSubhead = string.Empty;
    [ObservableProperty] private bool _hasVmDetailNicSubhead;
    [ObservableProperty] private string _vmDetailInfoText = string.Empty;
    [ObservableProperty] private bool _hasVmDetailInfoText;

    // Review.
    [ObservableProperty] private string _reviewSummaryText = string.Empty;
    [ObservableProperty] private string _reviewBlockerText = string.Empty;
    [ObservableProperty] private bool _isReviewBlockerVisible;

    // Footer.
    [ObservableProperty] private bool _backToLibraryEnabled = true;
    [ObservableProperty] private bool _previousStepEnabled;
    [ObservableProperty] private string _previousStepTooltip = "Previous";
    [ObservableProperty] private bool _nextStepEnabled;
    [ObservableProperty] private bool _nextStepVisible = true;
    [ObservableProperty] private string _nextStepTooltip = "Next";
    [ObservableProperty] private bool _saveEnabled;
    [ObservableProperty] private bool _saveVisible;
    [ObservableProperty] private bool _saveAsEnabled;
    [ObservableProperty] private bool _saveAsVisible;

    /// <summary>Gets whether a draft is currently active. Exposed for tests and gating.</summary>
    public bool HasActiveDraft => _hasActiveDraft;

    /// <summary>Gets whether the current validation state has blockers. Exposed for tests.</summary>
    public bool HasValidationBlockers => _validationState.HasBlockers;

    /// <summary>Attaches the cross-subview host. Called by the page on navigation.</summary>
    internal void Attach(ITemplatesBuilderHost host) => _host = host;

    /// <summary>Detaches the host. Called by the page on leave.</summary>
    internal void Detach() => _host = null;

    /// <summary>Test seam: exposes the current draft snapshot.</summary>
    internal TemplatesBuilderDraftSnapshot CaptureDraft() => _draft;

    /// <summary>Test seam: exposes the current validation state.</summary>
    internal TemplatesBuilderValidationState ValidationState => _validationState;

    public override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        _host = null;
        IsLoading = false;
        IsInitialized = false;
    }

    /// <summary>
    /// Ensures reference data, seeds a deterministic suggested draft, routes to the Builder subview,
    /// and renders. Called by the Library subview's "create in builder" affordance.
    /// </summary>
    public async Task CreateDraftAsync()
    {
        var referenceData = await EnsureReferenceDataAsync(forceRefresh: false);
        LoadNewDraft(TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData));
        _host?.NavigateToBuilder();
        RenderAll();
    }

    /// <summary>
    /// Ensures reference data, loads <paramref name="document"/> into a Builder draft, routes to the
    /// Builder subview, and renders. Called by the Library subview's "edit in builder" affordance.
    /// </summary>
    public async Task ShowDocumentAsync(TemplateEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        await EnsureReferenceDataAsync(forceRefresh: false);
        LoadDocument(document);
        _host?.NavigateToBuilder();
        RenderAll();
    }

    private async Task<TemplatesBuilderReferenceData> EnsureReferenceDataAsync(bool forceRefresh)
    {
        var referenceData = _host is null
            ? new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>())
            : await _host.LoadBuilderReferenceDataAsync(forceRefresh);
        SetReferenceData(referenceData);
        return referenceData;
    }

    #region Commands

    [RelayCommand]
    private void BackToLibrary() => _host?.NavigateToLibrary();

    [RelayCommand]
    private void PreviousStep() => SelectAdjacentStep(-1);

    [RelayCommand]
    private void NextStep() => SelectAdjacentStep(1);

    [RelayCommand]
    private void NavigatorBack()
    {
        if (!_navigation.MoveNavigatorBack())
        {
            return;
        }

        RenderNavigator();
    }

    [RelayCommand]
    private void SetDeploymentProfile(string? profile)
    {
        if (_isApplyingDraft || profile is null)
        {
            return;
        }

        _selectedDeploymentProfile = NormalizeDeploymentProfile(profile);
        RebuildDeploymentProfileRows();
        HandleGeneralEdit();
    }

    [RelayCommand]
    private void AddNetwork()
    {
        var draft = CaptureWorkingDraft();
        var networks = draft.LabNetworks
            .Append(new TemplatesBuilderLabNetworkDraft($"lab-network-{draft.LabNetworks.Count + 1}", "Network", string.Empty, string.Empty, string.Empty, string.Empty))
            .ToList();
        _selectedNetworkIndex = networks.Count - 1;
        RenderAndNotify(draft with { LabNetworks = networks, IsSaveConfirmed = false });
    }

    [RelayCommand]
    private void AddCredentialSlot()
    {
        var draft = CaptureWorkingDraft();
        var slots = draft.CredentialSlots
            .Append(new TemplatesBuilderCredentialSlotDraft($"slot-{draft.CredentialSlots.Count + 1}", "Credential slot", "template reference"))
            .ToList();
        _selectedCredentialSlotIndex = slots.Count - 1;
        RenderAndNotify(draft with { CredentialSlots = slots, IsSaveConfirmed = false });
    }

    [RelayCommand]
    private void AddForest()
    {
        var draft = CaptureWorkingDraft();
        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.AddForest(draft));
    }

    [RelayCommand]
    private void AddTree()
    {
        var draft = CaptureWorkingDraft();
        var forestId = ResolveActiveForestId(draft);
        if (string.IsNullOrWhiteSpace(forestId))
        {
            return;
        }

        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.AddTree(draft, forestId));
    }

    // Level 1 detail-panel affordance: author a forest trust from the selected forest to the forest chosen in
    // the "Add forest trust" combo box. No-ops unless a forest is selected and a distinct target is picked.
    [RelayCommand]
    private void AddForestTrust()
    {
        if (_selectedForestDomainKind != BuilderForestDomainResourceKind.Forest)
        {
            return;
        }

        var target = SelectedForestTrustTarget;
        if (target is null)
        {
            return;
        }

        var draft = CaptureWorkingDraft();
        var sourceIndex = _selectedForestDomainIndex;
        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.AddForestTrust(draft, sourceIndex, target.ForestIndex));
    }

    // Level 1 detail-panel affordance: remove the selected forest's trust identified by trustId. Invoked by the
    // per-row remove command on each existing-trust row.
    private void RemoveForestTrustById(string? trustId)
    {
        if (string.IsNullOrWhiteSpace(trustId))
        {
            return;
        }

        var draft = CaptureWorkingDraft();
        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.RemoveForestTrust(draft, trustId));
    }

    // Canvas hover-+ affordance: add a child domain under the domain the pointer is over.
    private void AddChildDomainAt(int domainIndex)
    {
        var draft = CaptureWorkingDraft();
        if (domainIndex < 0 || domainIndex >= draft.Domains.Count)
        {
            return;
        }

        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.AddChildDomain(draft, draft.Domains[domainIndex].DomainId));
    }

    // Canvas forest header +tree affordance: add a tree domain to the hovered forest.
    private void AddTreeAt(int forestIndex)
    {
        var draft = CaptureWorkingDraft();
        if (forestIndex < 0 || forestIndex >= draft.Forests.Count)
        {
            return;
        }

        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.AddTree(draft, draft.Forests[forestIndex].ForestId));
    }

    // Canvas delete affordance: remove a forest (and its whole tree) or a domain subtree.
    private void DeleteForestDomainAt(BuilderForestDomainResourceKind kind, int index)
    {
        var draft = CaptureWorkingDraft();
        if (kind == BuilderForestDomainResourceKind.Forest)
        {
            if (index < 0 || index >= draft.Forests.Count)
            {
                return;
            }

            ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.DeleteForest(draft, draft.Forests[index].ForestId));
            return;
        }

        if (index < 0 || index >= draft.Domains.Count)
        {
            return;
        }

        ApplyTopologyResult(TemplatesBuilderTopologyAuthoring.DeleteDomain(draft, draft.Domains[index].DomainId));
    }

    // Prefers the currently-selected forest so toolbar "Add Tree" targets what the user is looking at; falls
    // back to the forest owning the selected domain, then the first forest.
    private string ResolveActiveForestId(TemplatesBuilderDraftSnapshot draft)
    {
        if (draft.Forests.Count == 0)
        {
            return string.Empty;
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Forests.Count)
        {
            return draft.Forests[_selectedForestDomainIndex].ForestId;
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Domains.Count)
        {
            var owningForestId = draft.Domains[_selectedForestDomainIndex].ForestId;
            if (draft.Forests.Any(forest => string.Equals(forest.ForestId, owningForestId, StringComparison.OrdinalIgnoreCase)))
            {
                return owningForestId;
            }
        }

        return draft.Forests[0].ForestId;
    }

    private void ApplyTopologyResult(TopologyAuthoringResult result)
    {
        _selectedForestDomainKind = result.SelectedKind;
        _selectedForestDomainIndex = result.SelectedIndex;
        RenderAndNotify(result.Draft with { IsSaveConfirmed = false });
    }

    /// <summary>Commits the selected domain subnet after the dedicated editor loses focus.</summary>
    public void CommitSelectedDomainSubnet(string rawText)
    {
        if (_selectedForestDomainKind != BuilderForestDomainResourceKind.Domain ||
            _selectedForestDomainIndex < 0 ||
            _selectedForestDomainIndex >= _draft.Domains.Count)
        {
            return;
        }

        var domain = _draft.Domains[_selectedForestDomainIndex];
        var networkIndex = FindDomainNetworkIndex(_draft, domain.DomainId);
        if (networkIndex < 0)
        {
            return;
        }

        var trimmed = (rawText ?? string.Empty).Trim();
        var current = _draft.LabNetworks[networkIndex];
        var validation = EvaluateDomainSubnet(_draft, current.NetworkId, domain.DomainId, trimmed);

        // An unparseable subnet is never written into the draft. Persisting it would blank the domain's on-canvas
        // subnet label (an empty or non-CIDR value drops out of the topology projection) and would let a garbage
        // subnet reach Save. Instead the raw text stays in the editor field with a soft-red message so the user
        // can correct it in place, while the network keeps its last valid subnet. This mirrors the machine
        // inspector's octet editor, which likewise no-ops on invalid input rather than corrupting the draft.
        if (!validation.IsCidrValid)
        {
            DomainSubnetValue = trimmed;
            ApplyDomainSubnetValidation(validation.Message);
            return;
        }

        if (string.Equals(trimmed, current.Subnet ?? string.Empty, StringComparison.Ordinal))
        {
            ApplyDomainSubnetValidation(validation.Message);
            return;
        }

        var networks = _draft.LabNetworks.ToList();
        networks[networkIndex] = current with { Subnet = trimmed };
        _draft = _draft with { LabNetworks = networks, IsSaveConfirmed = false };

        // A parseable subnet (including one that overlaps another domain - a soft warning, not a hard block) is
        // committed and the layout reconciled so VM addresses reflow onto the new block.
        RenderAndNotify(_draft, reconcileNetworkLayout: true);
        RefreshSelectedDomainSubnetValidation();
    }

    // ----- Level 2: machine authoring inside a domain or the Standalone container -----

    // Canvas manage-machines affordance (double-tap or the hover button): zoom from Level 1 into the machines
    // of a domain, or of the Standalone container.
    private void ManageMachinesAt(BuilderForestDomainResourceKind kind, int index)
    {
        var draft = CaptureWorkingDraft();
        if (kind == BuilderForestDomainResourceKind.Standalone)
        {
            ZoomIntoStandalone(draft);
            return;
        }

        if (kind != BuilderForestDomainResourceKind.Domain || index < 0 || index >= draft.Domains.Count)
        {
            return;
        }

        ZoomIntoDomain(draft.Domains[index].DomainId);
    }

    private void ZoomIntoDomain(string domainId)
    {
        _isStandaloneContainer = false;
        _machineContainerId = domainId;
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Domain;
        var projection = TemplatesBuilderMachineProjector.ProjectDomainMachines(_draft, domainId, -1);
        _selectedMachineVmIndex = projection.Machines.Count > 0 ? projection.Machines[0].VmIndex : -1;
        IsMachineLevelVisible = true;
        RenderMachineLevel();
    }

    private void ZoomIntoStandalone(TemplatesBuilderDraftSnapshot draft)
    {
        _isStandaloneContainer = true;
        _machineContainerId = TemplatesBuilderMachineProjector.StandaloneContainerId;
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Standalone;
        var projection = TemplatesBuilderMachineProjector.ProjectStandaloneMachines(draft, -1);
        _selectedMachineVmIndex = projection.Machines.Count > 0 ? projection.Machines[0].VmIndex : -1;
        IsMachineLevelVisible = true;
        RenderMachineLevel();
    }

    [RelayCommand]
    private void BackToTopology()
    {
        IsMachineLevelVisible = false;
        // RenderMachineLevel clears the (now hidden) machine collections and rebuilds the inspector, so leaving
        // Level 2 does not strand stale machine cards or the add-computer placeholder behind the topology view.
        RenderMachineLevel();
    }

    // Level 1 entry point for standalone machines: there is no Standalone box until one exists, so this creates
    // the first standalone machine (a workgroup box: router, root CA, ...) and zooms straight into it.
    [RelayCommand]
    private void AddStandaloneMachine()
    {
        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineAuthoring.AddStandaloneComputer(draft);
        _isStandaloneContainer = true;
        _machineContainerId = TemplatesBuilderMachineProjector.StandaloneContainerId;
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Standalone;
        _selectedMachineVmIndex = result.SelectedVmIndex;
        IsMachineLevelVisible = true;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void AddComputer()
    {
        var draft = CaptureWorkingDraft();
        var result = _isStandaloneContainer
            ? TemplatesBuilderMachineAuthoring.AddStandaloneComputer(draft)
            : TemplatesBuilderMachineAuthoring.AddDomainComputer(draft, _machineContainerId);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    private void SelectMachineCard(int vmIndex)
    {
        _selectedMachineVmIndex = vmIndex;
        RenderMachineLevel();
    }

    private void DeleteMachineCard(int vmIndex)
    {
        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineAuthoring.DeleteComputer(draft, vmIndex);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void ToggleSelectedMachineRole(string? roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return;
        }

        var draft = CaptureWorkingDraft();
        if (_selectedMachineVmIndex < 0 || _selectedMachineVmIndex >= draft.Vms.Count)
        {
            return;
        }

        var definition = TemplatesBuilderRoleProjectionCatalog.FindRole(roleKey);
        if (definition is null || TemplatesBuilderRoleProjectionCatalog.IsStructuralRole(definition.Value.Key))
        {
            return;
        }

        var projected = TemplatesBuilderRoleProjectionCatalog.ProjectRole(draft.Vms[_selectedMachineVmIndex], definition.Value);
        if (projected.IsLocked)
        {
            return;
        }

        var result = TemplatesBuilderRoleAuthoring.SetAdditionalRole(
            draft,
            _selectedMachineVmIndex,
            definition.Value.Key,
            enabled: !projected.IsAssigned);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void CommitSelectedMachineName(string? name)
    {
        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineName(draft, _selectedMachineVmIndex, name);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void CommitSelectedMachineCpuCount(string? cpuCount)
    {
        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineCpuCount(draft, _selectedMachineVmIndex, cpuCount);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void CommitSelectedMachineMemoryMb(string? memoryMb)
    {
        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineMemoryMb(draft, _selectedMachineVmIndex, memoryMb);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void CommitSelectedMachineBaseDisk(string? vhdxId)
    {
        // Re-entrancy guard for the base-disk ComboBox. Rebuilding the inspector hands the ComboBox a fresh
        // ItemsSource and re-applies SelectedValue, which WinUI reports back through SelectionChanged as if the
        // user had picked a disk. Committing that echo would re-render, rebuild the inspector, and fire again -
        // an unbounded recursion that overflows the stack. _isRebuildingMachineInspector drops the echo raised
        // synchronously while we assign the inspector; the value checks drop any later stray echo. A null/empty id
        // is only ever the transient value while the ItemsSource is being swapped (the options list is the
        // non-empty VHDX catalog, with no empty entry), and an id equal to the current base disk is our own render.
        if (_isRebuildingMachineInspector ||
            string.IsNullOrWhiteSpace(vhdxId) ||
            _selectedMachineVmIndex < 0 ||
            _selectedMachineVmIndex >= _draft.Vms.Count ||
            string.Equals(_draft.Vms[_selectedMachineVmIndex].VhdxId ?? string.Empty, vhdxId.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineBaseDisk(draft, _selectedMachineVmIndex, vhdxId);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft);
    }

    [RelayCommand]
    private void CommitSelectedMachineHostOctets(IReadOnlyList<int> octets)
    {
        var draft = CaptureWorkingDraft();
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, _selectedMachineVmIndex, octets);
        _selectedMachineVmIndex = result.SelectedVmIndex;
        RenderAndNotify(result.Draft, reconcileNetworkLayout: false);
    }

    [RelayCommand]
    private void ToggleRolesPanel()
    {
        IsRolesPanelExpanded = !IsRolesPanelExpanded;
        RebuildSelectedMachineInspector();
    }

    [RelayCommand]
    private void ToggleFeaturesPanel()
    {
        IsFeaturesPanelExpanded = !IsFeaturesPanelExpanded;
        RebuildSelectedMachineInspector();
    }

    [RelayCommand]
    private void ToggleRoleConfiguration(string? roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return;
        }

        if (!_expandedRoleConfigKeys.Remove(roleKey))
        {
            _expandedRoleConfigKeys.Add(roleKey);
        }

        RebuildSelectedMachineInspector();
    }

    private void RenderMachineLevel()
    {
        if (!IsMachineLevelVisible)
        {
            MachineCards = [];
            MachineGridItems = [];
            HasMachineCards = false;
            RebuildSelectedMachineInspector();
            RefreshTopbarState();
            return;
        }

        var projection = _isStandaloneContainer
            ? TemplatesBuilderMachineProjector.ProjectStandaloneMachines(_draft, _selectedMachineVmIndex)
            : TemplatesBuilderMachineProjector.ProjectDomainMachines(_draft, _machineContainerId, _selectedMachineVmIndex);

        MachineLevelTitle = projection.Title;
        MachineLevelSubtitle = _isStandaloneContainer
            ? "Standalone machines - no domain membership"
            : "Machines in this domain";
        MachineLevelEmptyText = _isStandaloneContainer
            ? "No standalone machines yet. Add a computer to get started."
            : "No machines in this domain yet. Add a computer to get started.";
        AddComputerLabel = "+ Add computer";

        var cards = new ObservableCollection<BuilderMachineCardViewModel>();
        foreach (var card in projection.Machines)
        {
            var vmIndex = card.VmIndex;
            // A domain's only domain controller cannot be deleted, so it offers no delete affordance.
            var canDelete = !(card.IsDomainController && IsOnlyDomainControllerInContainer(vmIndex));
            cards.Add(new BuilderMachineCardViewModel(
                card.NodeId,
                card.VmIndex,
                card.Label,
                card.RoleLabel,
                card.Subtext,
                card.IsDomainController,
                card.IsSelected,
                new RelayCommand(() => SelectMachineCard(vmIndex)),
                canDelete ? new RelayCommand(() => DeleteMachineCard(vmIndex)) : null));
        }

        MachineCards = cards;
        HasMachineCards = cards.Count > 0;

        // The grid source mirrors the machine cards and appends the persistent add-computer placeholder so it
        // wraps as the trailing tile. MachineCards itself stays machine-only for the interaction tests.
        var gridItems = new ObservableCollection<object>();
        foreach (var card in cards)
        {
            gridItems.Add(card);
        }

        gridItems.Add(_machinePlaceholder);
        MachineGridItems = gridItems;
        RebuildSelectedMachineInspector();
        RefreshTopbarState();
    }

    private void RebuildSelectedMachineInspector()
    {
        // The flag is read by CommitSelectedMachineBaseDisk to drop the ComboBox SelectionChanged echo that fires
        // synchronously while we assign SelectedMachineInspector below.
        _isRebuildingMachineInspector = true;
        try
        {
            if (!IsMachineLevelVisible || _selectedMachineVmIndex < 0 || _selectedMachineVmIndex >= _draft.Vms.Count)
            {
                SelectedMachineInspector = null;
                HasSelectedMachineInspector = false;
                return;
            }

            var projection = _isStandaloneContainer
                ? TemplatesBuilderMachineProjector.ProjectStandaloneMachines(_draft, _selectedMachineVmIndex)
                : TemplatesBuilderMachineProjector.ProjectDomainMachines(_draft, _machineContainerId, _selectedMachineVmIndex);
            var matchingCards = projection.Machines.Where(machine => machine.VmIndex == _selectedMachineVmIndex).ToList();
            if (matchingCards.Count == 0)
            {
                SelectedMachineInspector = null;
                HasSelectedMachineInspector = false;
                return;
            }

            var card = matchingCards[0];
            var vm = _draft.Vms[_selectedMachineVmIndex];
            var hostAddress = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(_draft, _selectedMachineVmIndex);
            var baseDiskOptions = _vhdxCatalogOptions
                .Select(option => new BuilderMachineInspectorViewModel.BaseDiskOption(option.Id, option.DisplayLabel))
                .ToList();
            var bootstrapAccountText = _vhdxCatalogOptions
                .FirstOrDefault(option => string.Equals(option.Id, vm.VhdxId, StringComparison.OrdinalIgnoreCase))
                ?.BootstrapLocalUser ?? string.Empty;
            var roleRows = new List<BuilderRoleRowViewModel>();
            var featureRows = new List<BuilderRoleRowViewModel>();
            foreach (var role in TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles(vm).Where(MatchesRoleSearch))
            {
                var row = BuildRoleRow(role);
                if (role.Category == TemplatesBuilderRoleCategory.Feature)
                {
                    featureRows.Add(row);
                }
                else
                {
                    roleRows.Add(row);
                }
            }

            SelectedMachineInspector = new BuilderMachineInspectorViewModel(
                card.Label,
                card.RoleLabel,
                card.Subtext,
                RoleSearchText,
                IsRolesPanelExpanded,
                IsFeaturesPanelExpanded,
                vm.Name,
                vm.CpuCount,
                vm.MemoryMb,
                baseDiskOptions,
                vm.VhdxId,
                bootstrapAccountText,
                hostAddress.IsEditable,
                hostAddress.FixedOctetPrefix,
                hostAddress.EditableOctetCount > 1,
                hostAddress.Octets.Count > 0 ? hostAddress.Octets[0].ToString() : string.Empty,
                hostAddress.Octets.Count > 1 ? hostAddress.Octets[1].ToString() : string.Empty,
                hostAddress.SubnetCidr,
                hostAddress.Status,
                roleRows,
                featureRows,
                CommitSelectedMachineNameCommand,
                CommitSelectedMachineCpuCountCommand,
                CommitSelectedMachineMemoryMbCommand,
                CommitSelectedMachineBaseDiskCommand,
                CommitSelectedMachineHostOctetsCommand,
                ToggleRolesPanelCommand,
                ToggleFeaturesPanelCommand);
            HasSelectedMachineInspector = true;
        }
        finally
        {
            _isRebuildingMachineInspector = false;
        }
    }

    private BuilderRoleRowViewModel BuildRoleRow(TemplatesBuilderVmRoleProjection role)
    {
        var isStructural = TemplatesBuilderRoleProjectionCatalog.IsStructuralRole(role.RoleKey);
        var canToggle = !isStructural && !role.IsLocked;
        var statusNote = isStructural
            ? "Domain controllers are managed from the machine list."
            : role.StatusNote;
        var roleKey = role.RoleKey;
        return new BuilderRoleRowViewModel(
            role.RoleKey,
            role.DisplayName,
            role.Description,
            role.Category,
            role.IsAssigned,
            role.IsInstallOnly,
            role.IsLocked,
            role.HasConfiguration,
            statusNote,
            _expandedRoleConfigKeys.Contains(role.RoleKey),
            canToggle,
            canToggle ? new RelayCommand(() => ToggleSelectedMachineRole(roleKey)) : null,
            role.HasConfiguration ? new RelayCommand(() => ToggleRoleConfiguration(roleKey)) : null);
    }

    private bool MatchesRoleSearch(TemplatesBuilderVmRoleProjection role)
    {
        var search = RoleSearchText?.Trim();
        return string.IsNullOrWhiteSpace(search) ||
               role.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               role.Description.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOnlyDomainControllerInContainer(int vmIndex)
    {
        if (vmIndex < 0 || vmIndex >= _draft.Vms.Count)
        {
            return false;
        }

        var target = _draft.Vms[vmIndex];
        if (!target.IsActiveDirectoryDomainController || string.IsNullOrWhiteSpace(target.DomainId))
        {
            return false;
        }

        return _draft.Vms.Count(vm =>
            vm.IsActiveDirectoryDomainController &&
            string.Equals(vm.DomainId?.Trim(), target.DomainId.Trim(), StringComparison.OrdinalIgnoreCase)) <= 1;
    }

    [RelayCommand]
    private void AddVm()
    {
        var draft = CaptureWorkingDraft();
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
        _navigation.SelectVmChild(vms.Count - 1, updatedDraft);
        RenderAndNotify(updatedDraft);
    }

    private void AddNic(int vmIndex)
    {
        var draft = CaptureWorkingDraft();
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count)
        {
            return;
        }

        var vms = draft.Vms.ToList();
        var vm = vms[vmIndex];
        var nics = (vm.Nics ?? Array.Empty<TemplatesBuilderNicDraft>()).ToList();
        nics.Add(new TemplatesBuilderNicDraft($"nic-{nics.Count + 1}", "Lab", draft.LabNetworks.FirstOrDefault().NetworkId, string.Empty, string.Empty, string.Empty, string.Empty, []));
        vms[vmIndex] = vm with { Nics = nics };
        var updatedDraft = draft with { Vms = vms, IsSaveConfirmed = false };
        _navigation.SelectRoute(BuilderWorkflowRoute.ForVmNic(vmIndex, nics.Count - 1), updatedDraft);
        RenderAndNotify(updatedDraft);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!_hasActiveDraft)
        {
            SetStatus("Create or open a V2 Builder draft first.");
            RefreshActionState();
            return;
        }

        if (_validationState.HasBlockers)
        {
            SetStatus("Save blocked: " + string.Join(" ", _validationState.Blockers.Select(issue => issue.Message)));
            RefreshActionState();
            return;
        }

        var build = BuildCurrentDocument();
        if (build.Document is null)
        {
            SetStatus("Save blocked: " + string.Join(" ", build.Errors));
            RefreshActionState();
            return;
        }

        IsLoading = true;
        RefreshActionState();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(build.Document);
            SetStatus(result.UserMessage);
            if (result.Success && !string.IsNullOrWhiteSpace(result.FilePath))
            {
                SetSavedDocument(build.Document, result.FilePath);
                if (_host is not null)
                {
                    await _host.ReloadLibraryAsync(forceRefresh: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Benign navigate-away cancellation; nothing to surface.
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshActionState();
        }
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        if (!_hasActiveDraft)
        {
            SetStatus("Create or open a V2 Builder draft first.");
            RefreshActionState();
            return;
        }

        if (_validationState.HasBlockers)
        {
            SetStatus("Save As blocked: " + string.Join(" ", _validationState.Blockers.Select(issue => issue.Message)));
            RefreshActionState();
            return;
        }

        var build = BuildCurrentDocument();
        if (build.Document is null)
        {
            SetStatus("Save As blocked: " + string.Join(" ", build.Errors));
            RefreshActionState();
            return;
        }

        var suggestedName = string.IsNullOrWhiteSpace(build.Document.Template.Name)
            ? "v2-lab-template"
            : build.Document.Template.Name;
        var destinationPath = _host is null ? null : await _host.PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            SetStatus("Save As cancelled.");
            RefreshActionState();
            return;
        }

        IsLoading = true;
        RefreshActionState();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(build.Document, destinationPath, saveAs: true);
            SetStatus(result.UserMessage);
            if (result.Success && !string.IsNullOrWhiteSpace(result.FilePath))
            {
                SetSavedDocument(build.Document, result.FilePath);
                if (_host is not null)
                {
                    await _host.ReloadLibraryAsync(forceRefresh: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Benign navigate-away cancellation; nothing to surface.
        }
        catch (Exception ex)
        {
            SetStatus($"Save As failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshActionState();
        }
    }

    private TemplatesBuilderDraftBuildResult BuildCurrentDocument()
        => TemplatesBuilderDraftMapper.BuildDocument(
            CaptureDraft(),
            _templateId,
            _templateRevision,
            _createdWithAppVersion,
            _sourceFilePath);

    #endregion

    #region Draft lifecycle (folded workspace view model)

    internal void LoadNewDraft(TemplatesBuilderDraftSnapshot draft)
    {
        _templateId = Guid.NewGuid().ToString("N");
        _templateRevision = 1;
        _createdWithAppVersion = "1.0.0";
        _sourceFilePath = null;
        ApplyDraft(draft with { IsSaveConfirmed = false }, TemplatesBuilderValidationRequest.All());
        _hasActiveDraft = true;
        // A draft is now active, so the authoring gates (add forest/tree/network/credential/vm/standalone and the
        // machine add-computer placeholder) must enable immediately instead of staying dead until the first edit.
        RefreshActionState();
        ContextText = "Editing new V2 Builder draft.";
        SetStatus("Review the suggested topology, then save.");
    }

    private void LoadDocument(TemplateEditorDocument document)
    {
        _templateId = document.Template.Id;
        _templateRevision = document.Template.TemplateRevision;
        _createdWithAppVersion = document.Template.CreatedWithAppVersion;
        _sourceFilePath = document.SourceFilePath;
        ApplyDraft(TemplatesBuilderDraftMapper.FromTemplate(document.Template), TemplatesBuilderValidationRequest.All());
        _hasActiveDraft = true;
        // A draft is now active, so the authoring gates must enable immediately (see LoadNewDraft).
        RefreshActionState();
        ContextText = string.IsNullOrWhiteSpace(_sourceFilePath)
            ? "Editing V2 Builder draft."
            : $"Editing V2 template: {_sourceFilePath}";
        SetStatus("V2 template loaded in Builder.");
    }

    private void SetSavedDocument(TemplateEditorDocument document, string filePath)
    {
        _sourceFilePath = filePath;
        _templateId = document.Template.Id;
        _templateRevision = document.Template.TemplateRevision;
        _createdWithAppVersion = document.Template.CreatedWithAppVersion;
        _draft = _draft with { IsSaveConfirmed = false };
        _validatedDraft = _validatedDraft with { IsSaveConfirmed = false };
        ContextText = $"Editing V2 template: {filePath}";
    }

    private void SetReferenceData(TemplatesBuilderReferenceData referenceData)
    {
        var availableSwitches = CopyList(referenceData.AvailableVmSwitches);
        _availableSwitchInventory = CopyList(referenceData.AvailableSwitchInventory);
        _vhdxCatalogOptions = CopyList(referenceData.VhdxCatalogOptions);
        var vhdxCatalogOptions = _vhdxCatalogOptions;
        var switchText = availableSwitches.Count == 0
            ? "switches: none loaded"
            : $"switches: {string.Join(", ", availableSwitches)}";
        var diskText = vhdxCatalogOptions.Count == 0
            ? "catalog disks: none loaded"
            : $"catalog disks: {string.Join(", ", vhdxCatalogOptions.Select(option => option.Id))}";
        ReferenceText = $"{switchText}; {diskText}";
    }

    private void SetStatus(string statusText)
    {
        StatusText = statusText;
        IsStatusVisible = !string.IsNullOrWhiteSpace(statusText);
    }

    internal void ApplyDraft(TemplatesBuilderDraftSnapshot draft)
        => ApplyDraft(draft, TemplatesBuilderValidationRequest.DetectChangedScopes(_validatedDraft, draft));

    private void ApplyDraft(TemplatesBuilderDraftSnapshot draft, TemplatesBuilderValidationRequest validationRequest)
    {
        var labNetworks = CopyList(draft.LabNetworks);
        var credentialSlots = CopyList(draft.CredentialSlots);
        var forests = CopyList(draft.Forests);
        var domains = CopyList(draft.Domains);
        var vms = CopyList(draft.Vms);
        var trusts = CopyList(draft.Trusts);
        var editableContentChanged =
            !string.Equals(_validatedDraft.TemplateName, draft.TemplateName ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(_validatedDraft.TemplateDescription, draft.TemplateDescription ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(_validatedDraft.DeploymentProfile, draft.DeploymentProfile ?? string.Empty, StringComparison.Ordinal) ||
            !_validatedDraft.LabNetworks.SequenceEqual(labNetworks) ||
            !_validatedDraft.CredentialSlots.SequenceEqual(credentialSlots) ||
            !_validatedDraft.Forests.SequenceEqual(forests) ||
            !_validatedDraft.Domains.SequenceEqual(domains) ||
            !_validatedDraft.Vms.SequenceEqual(vms) ||
            !(_validatedDraft.Trusts ?? []).SequenceEqual(trusts);

        var isSaveConfirmed = editableContentChanged && _validatedDraft.IsSaveConfirmed
            ? false
            : draft.IsSaveConfirmed;

        var applied = new TemplatesBuilderDraftSnapshot(
            draft.TemplateName ?? string.Empty,
            draft.TemplateDescription ?? string.Empty,
            draft.DeploymentProfile ?? string.Empty,
            labNetworks,
            credentialSlots,
            forests,
            domains,
            vms,
            isSaveConfirmed)
        {
            // Trusts is an init-only property, so the positional ctor above drops it: copy it across
            // explicitly or authored trusts vanish the instant any unrelated edit is applied.
            Trusts = trusts
        };

        if (validationRequest.Categories.Count > 0)
        {
            _validationState = TemplatesBuilderValidationMerger.Merge(
                _validationState,
                TemplatesBuilderDraftValidator.Validate(applied, validationRequest));
        }

        _draft = applied;
        _validatedDraft = applied;
    }

    #endregion

    #region Edit pipeline

    private void OnFieldEdited(BuilderFieldViewModel field)
    {
        if (_isApplyingDraft)
        {
            return;
        }

        var isSwitchSelection = Equals(field.FieldKey, TemplatesBuilderFieldKeys.NetworkSwitchSelection);
        var draft = _draft with
        {
            TemplateName = TemplateName,
            TemplateDescription = TemplateDescription,
            DeploymentProfile = _selectedDeploymentProfile
        };

        draft = field.FieldKey.Scope switch
        {
            BuilderDraftFieldScope.Network => isSwitchSelection ? ApplySelectedNetworkSwitchOption(draft) : UpdateSelectedNetwork(draft),
            BuilderDraftFieldScope.CredentialSlot => UpdateSelectedCredentialSlot(draft),
            BuilderDraftFieldScope.Forest or BuilderDraftFieldScope.Domain => UpdateSelectedForestOrDomain(draft),
            BuilderDraftFieldScope.Vm or BuilderDraftFieldScope.Nic => UpdateSelectedVm(draft),
            _ => draft
        };
        _draft = draft;

        RenderResourceLists(refreshNavigator: true);
        if (isSwitchSelection)
        {
            RenderNetworkDetail();
        }

        // A relation change can coerce the relation and seed/clear the parent, so refresh the detail panel to
        // reflect the engine's decision (mirrors the switch-selection re-render above).
        if (Equals(field.FieldKey, TemplatesBuilderFieldKeys.DomainRelationKind))
        {
            RenderForestDomainDetail();
        }

        RenderVmOverview();
        ApplyDraft(_draft);
        RenderReview();
        RefreshTopbarState();
        RefreshActionState();
    }

    private void HandleGeneralEdit()
    {
        if (_isApplyingDraft)
        {
            return;
        }

        _draft = _draft with
        {
            TemplateName = TemplateName,
            TemplateDescription = TemplateDescription,
            DeploymentProfile = _selectedDeploymentProfile
        };
        RenderResourceLists(refreshNavigator: true);
        RenderVmOverview();
        ApplyDraft(_draft);
        RenderReview();
        RefreshTopbarState();
        RefreshActionState();
    }

    private TemplatesBuilderDraftSnapshot CaptureWorkingDraft()
    {
        if (_isApplyingDraft)
        {
            return _draft;
        }

        var draft = _draft with
        {
            TemplateName = TemplateName,
            TemplateDescription = TemplateDescription,
            DeploymentProfile = _selectedDeploymentProfile
        };
        draft = UpdateSelectedNetwork(draft);
        draft = UpdateSelectedCredentialSlot(draft);
        draft = UpdateSelectedForestOrDomain(draft);
        draft = UpdateSelectedVm(draft);
        _draft = draft;
        return draft;
    }

    private void RenderAndNotify(TemplatesBuilderDraftSnapshot draft, bool reconcileNetworkLayout = true)
    {
        // Structural authoring (add/remove a domain or a machine) all funnels through here, so this is the single
        // point where the network layout is reconciled back to the one-switch-per-domain + required-router model.
        // Machine inspector host-IP edits opt out so their soft-validation statuses (reserved/duplicate/...) can
        // surface immediately instead of being auto-normalized away.
        if (reconcileNetworkLayout)
        {
            draft = TemplatesBuilderNetworkReconciler.Reconcile(draft);
        }

        _isApplyingDraft = true;
        try
        {
            _draft = draft;
            SyncGeneralFromDraft();
            RenderDraftResources(refreshNavigator: false);
            RenderSelectedStep();
        }
        finally
        {
            _isApplyingDraft = false;
        }

        ApplyDraft(_draft);
        RenderReview();
        RefreshTopbarState();
        RefreshActionState();
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedNetwork(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedNetworkIndex < 0 ||
            _selectedNetworkIndex >= draft.LabNetworks.Count ||
            !FieldHas(_networkFields, TemplatesBuilderFieldKeys.NetworkName))
        {
            return draft;
        }

        var networks = draft.LabNetworks.ToList();
        var current = networks[_selectedNetworkIndex];
        // Rebuild via `with` so the in-memory-only DomainId (the reconciler's domain<->switch link that drives the
        // subnet subtext) survives a field edit. A positional re-construction would silently drop it, unhoming the
        // switch from its domain and blanking the node's subnet until the next structural reconcile.
        networks[_selectedNetworkIndex] = current with
        {
            Name = FieldText(_networkFields, TemplatesBuilderFieldKeys.NetworkName),
            SwitchName = FieldHas(_networkFields, TemplatesBuilderFieldKeys.NetworkSwitchName)
                ? FieldText(_networkFields, TemplatesBuilderFieldKeys.NetworkSwitchName)
                : current.SwitchName,
            SwitchType = FieldHas(_networkFields, TemplatesBuilderFieldKeys.NetworkSwitchType)
                ? FieldText(_networkFields, TemplatesBuilderFieldKeys.NetworkSwitchType)
                : current.SwitchType,
            Subnet = FieldText(_networkFields, TemplatesBuilderFieldKeys.NetworkSubnet),
            Notes = FieldText(_networkFields, TemplatesBuilderFieldKeys.NetworkNotes)
        };
        return draft with { LabNetworks = networks };
    }

    private TemplatesBuilderDraftSnapshot ApplySelectedNetworkSwitchOption(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedNetworkIndex < 0 ||
            _selectedNetworkIndex >= draft.LabNetworks.Count ||
            !TryGetSelectedSwitchOption(out var selectedOption))
        {
            return draft;
        }

        var networks = draft.LabNetworks.ToList();
        networks[_selectedNetworkIndex] = TemplatesBuilderNetworkSwitchIntent.ApplySelectedOption(
            networks[_selectedNetworkIndex],
            selectedOption);
        return draft with { LabNetworks = networks };
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedCredentialSlot(TemplatesBuilderDraftSnapshot draft)
    {
        if (_selectedCredentialSlotIndex < 0 ||
            _selectedCredentialSlotIndex >= draft.CredentialSlots.Count ||
            !FieldHas(_credentialFields, TemplatesBuilderFieldKeys.CredentialSlotKey))
        {
            return draft;
        }

        var slots = draft.CredentialSlots.ToList();
        slots[_selectedCredentialSlotIndex] = new TemplatesBuilderCredentialSlotDraft(
            FieldText(_credentialFields, TemplatesBuilderFieldKeys.CredentialSlotKey),
            FieldText(_credentialFields, TemplatesBuilderFieldKeys.CredentialSlotLabel),
            FieldText(_credentialFields, TemplatesBuilderFieldKeys.CredentialSlotScopeHint));
        return draft with { CredentialSlots = slots };
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedForestOrDomain(TemplatesBuilderDraftSnapshot draft)
    {
        // Forests are non-editable: the forest name follows its root domain and its id/linkage are derived,
        // so there are no writable forest fields to fold back here.
        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain &&
            _selectedForestDomainIndex >= 0 &&
            _selectedForestDomainIndex < draft.Domains.Count &&
            FieldHas(_forestDomainFields, TemplatesBuilderFieldKeys.DomainDnsName))
        {
            var domains = draft.Domains.ToList();
            var existing = domains[_selectedForestDomainIndex];

            // Merge only the editable fields onto the existing record so the read-only identity/linkage
            // fields (DomainId, ForestId, ParentDomainId) are never wiped by the write-back.
            var merged = existing with
            {
                DnsName = FieldText(_forestDomainFields, TemplatesBuilderFieldKeys.DomainDnsName),
                NetBiosName = FieldHas(_forestDomainFields, TemplatesBuilderFieldKeys.DomainNetBiosName)
                    ? FieldText(_forestDomainFields, TemplatesBuilderFieldKeys.DomainNetBiosName)
                    : existing.NetBiosName
            };
            domains[_selectedForestDomainIndex] = merged;
            draft = draft with { Domains = domains };

            // The relation field is only present for non-root domains and is limited to Tree/Child; route it
            // through the authoring engine so one-root-per-forest and parent seeding/clearing stay invariant.
            if (FieldHas(_forestDomainFields, TemplatesBuilderFieldKeys.DomainRelationKind))
            {
                draft = TemplatesBuilderTopologyAuthoring.ApplyDomainRelationEdit(
                    draft,
                    _selectedForestDomainIndex,
                    FieldText(_forestDomainFields, TemplatesBuilderFieldKeys.DomainRelationKind));
            }

            return draft;
        }

        return draft;
    }

    private TemplatesBuilderDraftSnapshot UpdateSelectedVm(TemplatesBuilderDraftSnapshot draft)
    {
        var projection = _navigation.Project(draft, _canNavigate);
        if (!projection.IsVmDetailSelected ||
            projection.SelectedVmIndex < 0 ||
            projection.SelectedVmIndex >= draft.Vms.Count)
        {
            return draft;
        }

        var vms = draft.Vms.ToList();
        var vm = vms[projection.SelectedVmIndex];
        vms[projection.SelectedVmIndex] = projection.SelectedVmDetailCategory switch
        {
            BuilderVmDetailCategory.Basics when FieldHas(_vmDetailFields, TemplatesBuilderFieldKeys.VmId) => vm with
            {
                VmId = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmId),
                Name = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmName)
            },
            BuilderVmDetailCategory.Resources when FieldHas(_vmDetailFields, TemplatesBuilderFieldKeys.VmMemoryMb) => vm with
            {
                MemoryMb = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmMemoryMb),
                CpuCount = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmCpuCount),
                VhdxId = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmVhdxId)
            },
            BuilderVmDetailCategory.Membership when FieldHas(_vmDetailFields, TemplatesBuilderFieldKeys.VmMembershipMode) => vm with
            {
                MembershipMode = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmMembershipMode),
                DomainId = FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmDomainId)
            },
            BuilderVmDetailCategory.Roles when FieldHas(_vmDetailFields, TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController) => vm with
            {
                IsActiveDirectoryDomainController = FieldChecked(_vmDetailFields, TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController)
            },
            BuilderVmDetailCategory.Networking when projection.IsNicDetailSelected &&
                FieldHas(_vmDetailFields, TemplatesBuilderFieldKeys.NicId) => vm with
            {
                Nics = UpdateSelectedNic(vm.Nics, projection.SelectedNicIndex)
            },
            BuilderVmDetailCategory.Credentials when FieldHas(_vmDetailFields, TemplatesBuilderFieldKeys.VmLocalBootstrap) => vm with
            {
                CredentialSlots = new TemplatesBuilderVmCredentialSlotDraft(
                    FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmLocalBootstrap),
                    FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmDomainAdmin),
                    FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmDomainJoin),
                    FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmDsrm),
                    FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.VmParentDomainAdmin))
            },
            _ => vm
        };
        return draft with { Vms = vms };
    }

    private IReadOnlyList<TemplatesBuilderNicDraft> UpdateSelectedNic(IReadOnlyList<TemplatesBuilderNicDraft> existingNics, int selectedNicIndex)
    {
        existingNics ??= [];
        if (selectedNicIndex < 0 || selectedNicIndex >= existingNics.Count)
        {
            return existingNics;
        }

        var nics = existingNics.ToList();
        nics[selectedNicIndex] = new TemplatesBuilderNicDraft(
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicId),
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicName),
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicNetworkId),
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicSwitchName),
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicIpAddress),
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicPrefixLength),
            FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicDefaultGateway),
            SplitList(FieldText(_vmDetailFields, TemplatesBuilderFieldKeys.NicDnsServers)));
        return nics;
    }

    #endregion

    #region Navigation (folded workspace controller navigation)

    private void SelectStep(BuilderWorkflowStep step)
    {
        CaptureWorkingDraft();
        _navigation.SelectStep(step, _draft);
        RenderSelectedStep();
        RenderDraftResources(refreshNavigator: false);
    }

    private void SelectVmChild(int index)
    {
        _navigation.SelectVmChild(index, _draft);
        RenderSelectedStep();
        RenderVmDetail();
    }

    private void SelectVmDetailCategory(BuilderVmDetailCategory category)
    {
        _navigation.SelectVmDetailCategory(category, _draft);
        RenderSelectedStep();
        RenderVmDetail();
    }

    private void SelectVmNic(BuilderWorkflowRoute route)
    {
        CaptureWorkingDraft();
        if (!_navigation.SelectRoute(route, _draft))
        {
            return;
        }

        RenderSelectedStep();
        RenderVmDetail();
    }

    private void SelectAdjacentStep(int offset)
    {
        CaptureWorkingDraft();
        if (!_navigation.SelectAdjacent(offset, _draft))
        {
            return;
        }

        RenderSelectedStep();
        RenderDraftResources(refreshNavigator: false);
    }

    private void SelectNetwork(int index)
    {
        CaptureWorkingDraft();
        _selectedNetworkIndex = index;
        RenderNetworkList();
        RenderNetworkDetail();
    }

    private void SelectCredentialSlot(int index)
    {
        CaptureWorkingDraft();
        _selectedCredentialSlotIndex = index;
        RenderCredentialList();
        RenderCredentialDetail();
    }

    private void SelectForest(int forestIndex)
    {
        CaptureWorkingDraft();
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Forest;
        _selectedForestDomainIndex = forestIndex;
        RenderForestDomainList();
        RenderForestDomainDetail();
    }

    private void SelectDomain(int domainIndex)
    {
        CaptureWorkingDraft();
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Domain;
        _selectedForestDomainIndex = domainIndex;
        RenderForestDomainList();
        RenderForestDomainDetail();
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        EnsureSelectedResourcesInBounds();
        RenderDraftResources(refreshNavigator: false);
        RenderSelectedStep();
        RefreshActionState();
    }

    private void SyncGeneralFromDraft()
    {
        _selectedDeploymentProfile = NormalizeDeploymentProfile(_draft.DeploymentProfile);
        TemplateName = _draft.TemplateName;
        TemplateDescription = _draft.TemplateDescription;
        RebuildDeploymentProfileRows();
    }

    private void RenderSelectedStep()
    {
        _canNavigate = _hasActiveDraft && !IsLoading;
        var projection = _navigation.Project(_draft, _canNavigate);
        IsGeneralVisible = projection.ActiveStep == BuilderWorkflowStep.General;
        IsNetworksVisible = projection.ActiveStep == BuilderWorkflowStep.Networks;
        IsForestsDomainsVisible = projection.ActiveStep == BuilderWorkflowStep.ForestsDomains;
        IsCredentialsVisible = projection.ActiveStep == BuilderWorkflowStep.Credentials;
        IsVmsVisible = projection.ActiveStep == BuilderWorkflowStep.Vms;
        IsReviewVisible = projection.ActiveStep == BuilderWorkflowStep.Review;
        IsVmOverviewVisible = projection.ActiveStep == BuilderWorkflowStep.Vms && projection.IsVmOverviewSelected;
        IsVmDetailVisible = projection.ActiveStep == BuilderWorkflowStep.Vms && !projection.IsVmOverviewSelected;

        RenderNavigator(projection);
        UpdateFooter(projection);
    }

    private void RenderNavigator() => RenderNavigator(_navigation.Project(_draft, _canNavigate));

    private void RenderNavigator(BuilderWorkflowProjection projection)
    {
        IsNavigatorHeaderVisible = projection.CanNavigateBack;
        NavigatorTitle = projection.NavigatorTitle;
        NavigatorBackTooltip = projection.NavigatorBackTargetLabel;
        NavigatorBackEnabled = _canNavigate && projection.CanNavigateBack;

        var rows = new ObservableCollection<BuilderNavRowViewModel>();
        switch (projection.NavigatorDepth)
        {
            case BuilderNavigatorDepth.Root:
                foreach (var row in projection.RootRows)
                {
                    var step = row.Route.Step;
                    rows.Add(BuilderNavRowViewModel.Button(row.Label, row.IsSelected, row.IsEnabled, new RelayCommand(() => SelectStep(step))));
                }

                break;
            case BuilderNavigatorDepth.VmList:
                rows.Add(BuilderNavRowViewModel.Button("+ Add VM", isSelected: false, _canNavigate, AddVmCommand, "Add VM"));
                if (projection.VmRows.Count == 0)
                {
                    rows.Add(BuilderNavRowViewModel.Placeholder("No VMs in this draft."));
                }
                else
                {
                    foreach (var row in projection.VmRows)
                    {
                        var vmIndex = row.Route.VmIndex;
                        rows.Add(BuilderNavRowViewModel.Button(row.Label, row.IsSelected, row.IsEnabled, new RelayCommand(() => SelectVmChild(vmIndex))));
                    }
                }

                break;
            case BuilderNavigatorDepth.VmSections:
                foreach (var row in projection.SelectedVmSectionRows)
                {
                    var category = row.Route.VmDetailCategory;
                    rows.Add(BuilderNavRowViewModel.Button(row.Label, row.IsSelected, row.IsEnabled, new RelayCommand(() => SelectVmDetailCategory(category))));
                }

                break;
            case BuilderNavigatorDepth.NicList:
                var selectedVmIndex = projection.SelectedVmIndex;
                rows.Add(BuilderNavRowViewModel.Button("+ Add NIC", isSelected: false, _canNavigate, new RelayCommand(() => AddNic(selectedVmIndex)), "Add NIC"));
                if (projection.SelectedVmNicRows.Count == 0)
                {
                    rows.Add(BuilderNavRowViewModel.Placeholder("No NICs in this VM."));
                }
                else
                {
                    foreach (var row in projection.SelectedVmNicRows)
                    {
                        var route = row.Route;
                        rows.Add(BuilderNavRowViewModel.Button(row.Label, row.IsSelected, row.IsEnabled, new RelayCommand(() => SelectVmNic(route))));
                    }
                }

                break;
        }

        NavigatorRows = rows;
    }

    private void RenderDraftResources(bool refreshNavigator)
    {
        EnsureSelectedResourcesInBounds();
        RenderResourceLists(refreshNavigator);
        RenderNetworkDetail();
        RenderCredentialDetail();
        RenderForestDomainDetail();
        RenderMachineLevel();
        RenderVmOverview();
        RenderVmDetail();
        RenderReview();
    }

    private void RenderResourceLists(bool refreshNavigator)
    {
        RenderNetworkList();
        RenderCredentialList();
        RenderForestDomainList();
        if (refreshNavigator)
        {
            RenderNavigator();
        }
    }

    private void RenderNetworkList()
    {
        var rows = new ObservableCollection<BuilderNavRowViewModel>();
        foreach (var row in TemplatesBuilderSectionProjections.ProjectNetworkRows(_draft, _selectedNetworkIndex))
        {
            var index = row.Index;
            rows.Add(BuilderNavRowViewModel.Button(row.Label, row.IsSelected, isEnabled: true, new RelayCommand(() => SelectNetwork(index))));
        }

        NetworkRows = rows;
    }

    private void RenderCredentialList()
    {
        var rows = new ObservableCollection<BuilderNavRowViewModel>();
        foreach (var row in TemplatesBuilderSectionProjections.ProjectCredentialSlotRows(_draft, _selectedCredentialSlotIndex))
        {
            var index = row.Index;
            rows.Add(BuilderNavRowViewModel.Button(row.Label, row.IsSelected, isEnabled: true, new RelayCommand(() => SelectCredentialSlot(index))));
        }

        CredentialRows = rows;
    }

    private void RenderForestDomainList()
    {
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(_draft, _selectedForestDomainKind, _selectedForestDomainIndex);
        if (TopologyCanvas is null)
        {
            TopologyCanvas = new BuilderTopologyCanvasViewModel(
                projection,
                OnTopologyCanvasSelect,
                AddChildDomainAt,
                AddTreeAt,
                DeleteForestDomainAt,
                ManageMachinesAt);
        }
        else
        {
            TopologyCanvas.Rebuild(projection);
        }

        HasTopologyForests = projection.Forests.Count > 0;
    }

    private void OnTopologyCanvasSelect(BuilderForestDomainResourceKind kind, int index)
    {
        if (kind == BuilderForestDomainResourceKind.Forest)
        {
            SelectForest(index);
        }
        else if (kind == BuilderForestDomainResourceKind.Standalone)
        {
            SelectStandaloneContainer();
        }
        else
        {
            SelectDomain(index);
        }
    }

    // Selecting the Standalone container highlights it at Level 1 (its detail panel is intentionally empty -
    // standalone machines are authored at Level 2 by managing the container, not by editing directory fields).
    private void SelectStandaloneContainer()
    {
        _selectedForestDomainKind = BuilderForestDomainResourceKind.Standalone;
        _selectedForestDomainIndex = -1;
        RenderDraftResources(refreshNavigator: false);
    }

    private void RenderNetworkDetail()
    {
        // Guard the full index range, not just emptiness: the selection index is a mutable field that can lag a
        // collection shrink on paths that render before EnsureSelectedResourcesInBounds runs, so an out-of-range
        // index must fall back to the same empty-detail state rather than throw IndexOutOfRangeException.
        if (_selectedNetworkIndex < 0 || _selectedNetworkIndex >= _draft.LabNetworks.Count)
        {
            _networkFields = Array.Empty<BuilderFieldViewModel>();
            NetworkDetailRows = [];
            HasNetworkDetail = false;
            NetworkDetailEmpty = true;
            return;
        }

        var network = _draft.LabNetworks[_selectedNetworkIndex];
        var switchIntent = TemplatesBuilderNetworkSwitchIntent.Project(network, _availableSwitchInventory);
        var fields = new List<BuilderFieldViewModel>
        {
            Text(TemplatesBuilderFieldKeys.NetworkName, "Name", network.Name),
            SwitchPicker(switchIntent)
        };
        if (switchIntent.IsExistingSwitchSelected)
        {
            fields.Add(ReadOnly("Switch Type", switchIntent.SwitchType));
        }
        else
        {
            fields.Add(Text(TemplatesBuilderFieldKeys.NetworkSwitchName, "New Switch Name", switchIntent.SwitchName));
            fields.Add(Combo(TemplatesBuilderFieldKeys.NetworkSwitchType, "Switch Type", switchIntent.SwitchType, selectFirstWhenMissing: false, V2SwitchTypeCatalog.External, V2SwitchTypeCatalog.Internal, V2SwitchTypeCatalog.Private));
        }

        fields.Add(Text(TemplatesBuilderFieldKeys.NetworkSubnet, "Subnet", network.Subnet));
        fields.Add(Text(TemplatesBuilderFieldKeys.NetworkNotes, "Notes", network.Notes));
        fields.Add(ReadOnly("Network ID (advanced)", network.NetworkId));

        _networkFields = fields;
        NetworkDetailTitle = "Selected Network Detail";
        NetworkDetailRows = PairRows(fields);
        HasNetworkDetail = true;
        NetworkDetailEmpty = false;
    }

    private void RenderCredentialDetail()
    {
        // See RenderNetworkDetail: range-guard the mutable selection index, not just the empty case.
        if (_selectedCredentialSlotIndex < 0 || _selectedCredentialSlotIndex >= _draft.CredentialSlots.Count)
        {
            _credentialFields = Array.Empty<BuilderFieldViewModel>();
            CredentialDetailRows = [];
            HasCredentialDetail = false;
            CredentialDetailEmpty = true;
            return;
        }

        var slot = _draft.CredentialSlots[_selectedCredentialSlotIndex];
        var fields = new List<BuilderFieldViewModel>
        {
            Text(TemplatesBuilderFieldKeys.CredentialSlotKey, "Slot Key", slot.SlotKey),
            Text(TemplatesBuilderFieldKeys.CredentialSlotLabel, "Label", slot.Label),
            Text(TemplatesBuilderFieldKeys.CredentialSlotScopeHint, "Scope", slot.ScopeHint)
        };
        _credentialFields = fields;
        CredentialDetailTitle = "Selected Slot Detail";
        CredentialDetailRows = PairRows(fields);
        HasCredentialDetail = true;
        CredentialDetailEmpty = false;
    }

    private void RenderForestDomainDetail()
    {
        RenderForestTrustAffordance();
        // Range-guard the mutable selection index (not just Count > 0): a stale index paired with a matching kind
        // would otherwise index past the end and throw. Out-of-range falls through to the cleared-detail state.
        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Forest &&
            _selectedForestDomainIndex >= 0 && _selectedForestDomainIndex < _draft.Forests.Count)
        {
            ClearDomainSubnetEditor();
            var forest = _draft.Forests[_selectedForestDomainIndex];
            var forestName = TemplatesBuilderTopologyAuthoring.ResolveForestName(_draft, forest);
            var fields = new List<BuilderFieldViewModel>
            {
                // The forest is named for its root domain and cannot be renamed here; both fields are derived.
                ReadOnly("Forest Name", forestName),
                ReadOnly("Root Domain ID", forest.RootDomainId)
            };
            _forestDomainFields = fields;
            ForestDomainDetailTitle = "Selected Forest Detail";
            ForestDomainDetailRows = PairRows(fields);
            HasForestDomainDetail = true;
            ForestDomainDetailEmpty = false;
            return;
        }

        if (_selectedForestDomainKind == BuilderForestDomainResourceKind.Domain &&
            _selectedForestDomainIndex >= 0 && _selectedForestDomainIndex < _draft.Domains.Count)
        {
            var domain = _draft.Domains[_selectedForestDomainIndex];
            RenderSelectedDomainSubnetEditor(domain);
            var isRoot = TemplatesBuilderTopologyAuthoring.IsForestRoot(_draft, domain.DomainId);
            var fields = new List<BuilderFieldViewModel>
            {
                ReadOnly("Domain ID", domain.DomainId),
                Text(TemplatesBuilderFieldKeys.DomainDnsName, "DNS Name", domain.DnsName),
                Text(TemplatesBuilderFieldKeys.DomainNetBiosName, "NetBIOS", domain.NetBiosName),
                ReadOnly("Forest ID", domain.ForestId)
            };

            if (isRoot)
            {
                // The forest root is locked to Root: there can be exactly one root per forest, so the relation
                // is shown read-only rather than as an editable choice.
                fields.Add(ReadOnly("Relation", nameof(V2DomainRelationKind.Root)));
            }
            else
            {
                // A non-root domain may only ever be a Tree or a Child, never a second Root.
                fields.Add(Combo(TemplatesBuilderFieldKeys.DomainRelationKind, "Relation", domain.RelationKind, selectFirstWhenMissing: true, nameof(V2DomainRelationKind.Tree), nameof(V2DomainRelationKind.Child)));
            }

            fields.Add(ReadOnly("Parent Domain ID", domain.ParentDomainId));

            _forestDomainFields = fields;
            ForestDomainDetailTitle = "Selected Domain Detail";
            ForestDomainDetailRows = PairRows(fields);
            HasForestDomainDetail = true;
            ForestDomainDetailEmpty = false;
            return;
        }

        ClearDomainSubnetEditor();
        _forestDomainFields = Array.Empty<BuilderFieldViewModel>();
        ForestDomainDetailRows = [];
        HasForestDomainDetail = false;
        ForestDomainDetailEmpty = true;
    }

    private void RenderSelectedDomainSubnetEditor(TemplatesBuilderDomainDraft domain)
    {
        var network = FindDomainNetwork(_draft, domain.DomainId);
        DomainSubnetValue = network.Subnet ?? string.Empty;
        ShowDomainSubnetEditor = true;

        var validation = EvaluateDomainSubnet(
            _draft,
            network.NetworkId ?? string.Empty,
            domain.DomainId,
            DomainSubnetValue);
        ApplyDomainSubnetValidation(validation.Message);
    }

    private void RefreshSelectedDomainSubnetValidation()
    {
        if (_selectedForestDomainKind != BuilderForestDomainResourceKind.Domain ||
            _selectedForestDomainIndex < 0 ||
            _selectedForestDomainIndex >= _draft.Domains.Count)
        {
            ClearDomainSubnetEditor();
            return;
        }

        var domain = _draft.Domains[_selectedForestDomainIndex];
        var network = FindDomainNetwork(_draft, domain.DomainId);
        var subnet = network.Subnet ?? string.Empty;
        var validation = EvaluateDomainSubnet(
            _draft,
            network.NetworkId ?? string.Empty,
            domain.DomainId,
            subnet);
        ApplyDomainSubnetValidation(validation.Message);
    }

    private void ClearDomainSubnetEditor()
    {
        DomainSubnetValue = string.Empty;
        ShowDomainSubnetEditor = false;
        ApplyDomainSubnetValidation(string.Empty);
    }

    private void ApplyDomainSubnetValidation(string message)
    {
        DomainSubnetValidationMessage = message ?? string.Empty;
        HasDomainSubnetValidationMessage = !string.IsNullOrWhiteSpace(DomainSubnetValidationMessage);
        ShowDomainSubnetErrorOutline = HasDomainSubnetValidationMessage;
    }

    private static (bool IsCidrValid, string Message) EvaluateDomainSubnet(
        TemplatesBuilderDraftSnapshot draft,
        string networkId,
        string domainId,
        string subnetText)
    {
        if (!BuilderLabSubnet.TryParseCidr(subnetText, out var parsed))
        {
            return (false, "Enter a valid CIDR such as 10.0.0.0/24");
        }

        var collision = FindCollidingDomainNetwork(draft, networkId, domainId, parsed.NetworkAddress);
        if (!string.IsNullOrWhiteSpace(collision.DomainId))
        {
            return (true, $"Subnet overlaps domain {ResolveDomainDisplayName(draft, collision.DomainId)}");
        }

        return (true, string.Empty);
    }

    private static TemplatesBuilderLabNetworkDraft FindDomainNetwork(
        TemplatesBuilderDraftSnapshot draft,
        string domainId)
        => draft.LabNetworks.FirstOrDefault(network =>
            !string.IsNullOrWhiteSpace(network.DomainId) &&
            string.Equals(network.DomainId, domainId, StringComparison.OrdinalIgnoreCase));

    private static int FindDomainNetworkIndex(TemplatesBuilderDraftSnapshot draft, string domainId)
    {
        for (var index = 0; index < draft.LabNetworks.Count; index++)
        {
            var network = draft.LabNetworks[index];
            if (!string.IsNullOrWhiteSpace(network.DomainId) &&
                string.Equals(network.DomainId, domainId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static TemplatesBuilderLabNetworkDraft FindCollidingDomainNetwork(
        TemplatesBuilderDraftSnapshot draft,
        string networkId,
        string domainId,
        uint networkAddress)
    {
        foreach (var network in draft.LabNetworks)
        {
            if (string.IsNullOrWhiteSpace(network.DomainId) ||
                string.Equals(network.NetworkId, networkId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(network.DomainId, domainId, StringComparison.OrdinalIgnoreCase) ||
                !BuilderLabSubnet.TryParseCidr(network.Subnet, out var otherSubnet))
            {
                continue;
            }

            if (otherSubnet.NetworkAddress == networkAddress)
            {
                return network;
            }
        }

        return default;
    }

    private static string ResolveDomainDisplayName(TemplatesBuilderDraftSnapshot draft, string domainId)
    {
        var domain = draft.Domains.FirstOrDefault(candidate =>
            string.Equals(candidate.DomainId, domainId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(domain.DnsName))
        {
            return domain.DnsName;
        }

        if (!string.IsNullOrWhiteSpace(domain.NetBiosName))
        {
            return domain.NetBiosName;
        }

        return domainId;
    }

    // Populates the "Add forest trust" affordance shown only when a forest is selected and the draft has at
    // least two forests: the candidate target forests (the other forests not already trusted to this one) and
    // the selected forest's existing trusts (each removable). Clears the state for any other selection.
    private void RenderForestTrustAffordance()
    {
        if (_selectedForestDomainKind != BuilderForestDomainResourceKind.Forest ||
            _selectedForestDomainIndex < 0 ||
            _selectedForestDomainIndex >= _draft.Forests.Count ||
            _draft.Forests.Count < 2)
        {
            CanAuthorForestTrust = false;
            ForestTrustTargets = [];
            SelectedForestTrustTarget = null;
            ForestTrustRows = [];
            return;
        }

        var selectedForest = _draft.Forests[_selectedForestDomainIndex];
        var selectedRoot = selectedForest.RootDomainId?.Trim() ?? string.Empty;
        var existingTrusts = _draft.Trusts ?? [];

        var targets = new List<BuilderForestTrustTargetViewModel>();
        for (var index = 0; index < _draft.Forests.Count; index++)
        {
            if (index == _selectedForestDomainIndex)
            {
                continue;
            }

            var forest = _draft.Forests[index];
            var root = forest.RootDomainId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root) ||
                existingTrusts.Any(trust => TrustJoinsRoots(trust, selectedRoot, root)))
            {
                continue;
            }

            targets.Add(new BuilderForestTrustTargetViewModel(
                index,
                TemplatesBuilderTopologyAuthoring.ResolveForestName(_draft, forest)));
        }

        ForestTrustTargets = new ObservableCollection<BuilderForestTrustTargetViewModel>(targets);
        SelectedForestTrustTarget = targets.FirstOrDefault();
        CanAuthorForestTrust = true;

        var rows = new List<BuilderForestTrustRowViewModel>();
        foreach (var trust in existingTrusts)
        {
            var source = trust.SourceDomainId?.Trim() ?? string.Empty;
            var target = trust.TargetDomainId?.Trim() ?? string.Empty;
            string partnerRoot;
            if (string.Equals(source, selectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                partnerRoot = target;
            }
            else if (string.Equals(target, selectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                partnerRoot = source;
            }
            else
            {
                continue;
            }

            var partnerForest = _draft.Forests.FirstOrDefault(forest =>
                string.Equals(forest.RootDomainId?.Trim() ?? string.Empty, partnerRoot, StringComparison.OrdinalIgnoreCase));
            var partnerName = string.IsNullOrWhiteSpace(partnerForest.ForestId)
                ? partnerRoot
                : TemplatesBuilderTopologyAuthoring.ResolveForestName(_draft, partnerForest);
            rows.Add(new BuilderForestTrustRowViewModel(
                trust.TrustId,
                partnerName,
                new RelayCommand(() => RemoveForestTrustById(trust.TrustId))));
        }

        ForestTrustRows = new ObservableCollection<BuilderForestTrustRowViewModel>(rows);
    }

    private static bool TrustJoinsRoots(TemplatesBuilderTrustDraft trust, string rootA, string rootB)
    {
        var source = trust.SourceDomainId?.Trim() ?? string.Empty;
        var target = trust.TargetDomainId?.Trim() ?? string.Empty;
        return (string.Equals(source, rootA, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(target, rootB, StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(source, rootB, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(target, rootA, StringComparison.OrdinalIgnoreCase));
    }

    private void RenderVmOverview()
    {
        var projection = TemplatesBuilderSectionProjections.ProjectVmOverview(_draft);
        VmTotalCountText = projection.TotalVmCount.ToString();
        VmMembershipCountsText = $"Standalone {projection.StandaloneVmCount} / Domain {projection.DomainMemberVmCount}";
        VmAdDcCountText = projection.ActiveDirectoryDomainControllerCount.ToString();

        VmOverviewRows = new ObservableCollection<string>(projection.SummaryRows);
        HasVmOverviewRows = projection.TotalVmCount > 0;
    }

    private void RenderVmDetail()
    {
        var projection = _navigation.Project(_draft, _canNavigate);
        var detail = TemplatesBuilderSectionProjections.ProjectSelectedVmDetail(_draft, projection);
        ClearVmDetail();
        if (detail is null)
        {
            _vmDetailFields = Array.Empty<BuilderFieldViewModel>();
            VmDetailInfoText = "No VM selected.";
            HasVmDetailInfoText = true;
            return;
        }

        var value = detail.Value;
        var vm = value.Vm;
        VmDetailTitle = value.Title;
        HasVmDetailTitle = true;
        VmDetailCategoryLabel = value.CategoryLabel;
        HasVmDetailCategoryLabel = true;

        switch (value.Category)
        {
            case BuilderVmDetailCategory.Basics:
                SetVmDetailFields(
                    Text(TemplatesBuilderFieldKeys.VmId, "VM ID", vm.VmId),
                    Text(TemplatesBuilderFieldKeys.VmName, "Name", vm.Name));
                break;
            case BuilderVmDetailCategory.Resources:
                SetVmDetailFields(
                    Text(TemplatesBuilderFieldKeys.VmMemoryMb, "Memory MB", vm.MemoryMb),
                    Text(TemplatesBuilderFieldKeys.VmCpuCount, "CPU Count", vm.CpuCount),
                    Text(TemplatesBuilderFieldKeys.VmVhdxId, "Base Disk / VHDX ID", vm.VhdxId));
                break;
            case BuilderVmDetailCategory.Membership:
                SetVmDetailFields(
                    Combo(TemplatesBuilderFieldKeys.VmMembershipMode, "Membership", vm.MembershipMode, selectFirstWhenMissing: true, V2MembershipModeCatalog.DomainMember, V2MembershipModeCatalog.Standalone),
                    Text(TemplatesBuilderFieldKeys.VmDomainId, "Domain ID", vm.DomainId));
                break;
            case BuilderVmDetailCategory.Roles:
                SetVmDetailFields(value.Roles
                    .Where(role => role.IsAuthorable)
                    .Select(role => Check(TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController, role.DisplayName, role.IsAssigned))
                    .ToArray());
                break;
            case BuilderVmDetailCategory.Networking:
                if (value.IsNicDetailSelected)
                {
                    RenderNicDetail(value);
                }
                else
                {
                    RenderNicOverview(value);
                }

                break;
            case BuilderVmDetailCategory.Credentials:
                SetVmDetailFields(
                    Text(TemplatesBuilderFieldKeys.VmLocalBootstrap, "Local Bootstrap Slot", vm.CredentialSlots.LocalBootstrap),
                    Text(TemplatesBuilderFieldKeys.VmDomainAdmin, "Domain Admin Slot", vm.CredentialSlots.DomainAdmin),
                    Text(TemplatesBuilderFieldKeys.VmDomainJoin, "Domain Join Slot", vm.CredentialSlots.DomainJoin),
                    Text(TemplatesBuilderFieldKeys.VmDsrm, "DSRM Slot", vm.CredentialSlots.Dsrm),
                    Text(TemplatesBuilderFieldKeys.VmParentDomainAdmin, "Parent Domain Admin Slot", vm.CredentialSlots.ParentDomainAdmin));
                break;
        }
    }

    private void RenderNicOverview(TemplatesBuilderVmDetailProjection detail)
    {
        _vmDetailFields = Array.Empty<BuilderFieldViewModel>();
        VmDetailInfoText = detail.Nics.Count == 0
            ? "No NICs in this VM."
            : $"{detail.Nics.Count} NICs in this VM.";
        HasVmDetailInfoText = true;
    }

    private void RenderNicDetail(TemplatesBuilderVmDetailProjection detail)
    {
        if (detail.SelectedNicIndex < 0 || detail.SelectedNicIndex >= detail.Nics.Count)
        {
            _vmDetailFields = Array.Empty<BuilderFieldViewModel>();
            VmDetailInfoText = "No NIC selected.";
            HasVmDetailInfoText = true;
            return;
        }

        var nic = detail.Nics[detail.SelectedNicIndex].Draft;
        VmDetailNicSubhead = $"NIC: {FormatResourceName(nic.Name, nic.NicId)}";
        HasVmDetailNicSubhead = true;
        SetVmDetailFields(
            Text(TemplatesBuilderFieldKeys.NicId, "NIC ID", nic.NicId),
            Text(TemplatesBuilderFieldKeys.NicName, "Name", nic.Name),
            Text(TemplatesBuilderFieldKeys.NicNetworkId, "Network ID", nic.NetworkId),
            Text(TemplatesBuilderFieldKeys.NicSwitchName, "Switch Override", nic.SwitchName),
            Text(TemplatesBuilderFieldKeys.NicIpAddress, "IP Address", nic.IpAddress),
            Text(TemplatesBuilderFieldKeys.NicPrefixLength, "Prefix", nic.PrefixLength),
            Text(TemplatesBuilderFieldKeys.NicDefaultGateway, "Gateway", nic.DefaultGateway),
            Text(TemplatesBuilderFieldKeys.NicDnsServers, "DNS Servers", string.Join(", ", nic.DnsServers)));
    }

    private void SetVmDetailFields(params BuilderFieldViewModel[] fields)
    {
        _vmDetailFields = fields;
        VmDetailFieldRows = PairRows(fields);
        HasVmDetailFieldRows = fields.Length > 0;
    }

    private void ClearVmDetail()
    {
        VmDetailTitle = string.Empty;
        HasVmDetailTitle = false;
        VmDetailCategoryLabel = string.Empty;
        HasVmDetailCategoryLabel = false;
        VmDetailFieldRows = [];
        HasVmDetailFieldRows = false;
        VmDetailNicSubhead = string.Empty;
        HasVmDetailNicSubhead = false;
        VmDetailInfoText = string.Empty;
        HasVmDetailInfoText = false;
    }

    private void RenderReview()
    {
        ReviewSummaryText = $"{BuildReviewSummary(_draft)} {_validationState.BuildReviewSummary()}";
        ReviewBlockerText = _validationState.HasBlockers
            ? string.Join(Environment.NewLine, _validationState.Blockers.Select(issue => issue.Message))
            : _validationState.Warnings.Count > 0
                ? string.Join(Environment.NewLine, _validationState.Warnings.Select(issue => issue.Message))
                : "Builder validation has no current blockers or warnings.";
        IsReviewBlockerVisible = _validationState.HasBlockers || _validationState.Warnings.Count > 0;
        RefreshTopbarState();
    }

    private void RefreshTopbarState()
    {
        ValidationChipIsOk = !_validationState.HasBlockers;
        ValidationChipText = _validationState.HasBlockers
            ? $"{_validationState.Blockers.Count} blocker(s)"
            : _validationState.Warnings.Count > 0
                ? $"{_validationState.Warnings.Count} warning(s)"
                : "Valid";

        ValidationIssues.Clear();
        foreach (var blocker in _validationState.Blockers)
        {
            ValidationIssues.Add(new BuilderValidationIssueRow(blocker.Message, isBlocker: true));
        }

        foreach (var warning in _validationState.Warnings)
        {
            ValidationIssues.Add(new BuilderValidationIssueRow(warning.Message, isBlocker: false));
        }

        HasValidationIssues = ValidationIssues.Count > 0;

        var containerDisplayName = string.IsNullOrWhiteSpace(MachineLevelTitle) ? "Topology" : MachineLevelTitle;
        BuilderBreadcrumbText = IsMachineLevelVisible
            ? $"{containerDisplayName} . machines"
            : "Topology";
    }

    private void RefreshActionState()
    {
        _canNavigate = _hasActiveDraft && !IsLoading;
        var canSave = _hasActiveDraft && !IsLoading && !_validationState.HasBlockers;
        AddNetworkEnabled = _canNavigate;
        AddCredentialEnabled = _canNavigate;
        AddForestEnabled = _canNavigate;
        AddTreeEnabled = _canNavigate;
        AddStandaloneMachineEnabled = _canNavigate;
        AddVmEnabled = _canNavigate;
        _machinePlaceholder.IsEnabled = _canNavigate;
        BackToLibraryEnabled = !IsLoading;

        var projection = _navigation.Project(_draft, _canNavigate);
        NavigatorBackEnabled = _canNavigate && projection.CanNavigateBack;
        UpdateFooter(projection, canSave);
        RenderNavigator(projection);
    }

    private void UpdateFooter(BuilderWorkflowProjection projection)
        => UpdateFooter(projection, _hasActiveDraft && !IsLoading && !_validationState.HasBlockers);

    private void UpdateFooter(BuilderWorkflowProjection projection, bool canSave)
    {
        var footer = _navigation.ProjectFooter(_draft, _canNavigate, canSave, canSave);
        PreviousStepEnabled = footer.CanGoPrevious;
        NextStepEnabled = footer.CanGoNext;
        NextStepVisible = !footer.IsReview;
        // Phase 4 retired the stepper/Review route: the canvas is the permanent Builder surface and Save / Save As
        // live in the topbar, so they are always visible. Enablement is driven purely by draft + validation state
        // (canSave = active draft, not loading, no blockers) rather than by the now-unreachable Review route, which
        // would otherwise leave both buttons permanently hidden and disabled.
        SaveEnabled = canSave;
        SaveAsEnabled = canSave;
        SaveVisible = true;
        SaveAsVisible = true;
        PreviousStepTooltip = FormatCommandLabel("Previous", footer.PreviousTargetLabel);
        NextStepTooltip = FormatCommandLabel("Next", footer.NextTargetLabel);
    }

    #endregion

    #region Field factories + helpers

    private BuilderFieldViewModel Text(BuilderDraftFieldKey key, string header, string value)
    {
        var field = new BuilderFieldViewModel(key, header, BuilderFieldKind.Text);
        field.Mutate(f => f.Value = value ?? string.Empty);
        field.Changed = OnFieldEdited;
        return field;
    }

    private static BuilderFieldViewModel ReadOnly(string header, string value)
    {
        var field = new BuilderFieldViewModel(DisplayOnlyFieldKey, header, BuilderFieldKind.ReadOnlyText);
        field.Mutate(f => f.Value = value ?? string.Empty);
        return field;
    }

    private BuilderFieldViewModel Combo(BuilderDraftFieldKey key, string header, string value, bool selectFirstWhenMissing, params string[] options)
    {
        var resolved = ResolveComboValue(options, value, selectFirstWhenMissing);
        var field = new BuilderFieldViewModel(key, header, BuilderFieldKind.Combo) { Options = options };
        field.Mutate(f => f.Value = resolved);
        field.Changed = OnFieldEdited;
        return field;
    }

    private BuilderFieldViewModel SwitchPicker(TemplatesBuilderNetworkSwitchIntentProjection switchIntent)
    {
        var options = switchIntent.Options.Select(option => new BuilderSwitchOptionViewModel(option)).ToList();
        var selected = options.FirstOrDefault(option => string.Equals(option.OptionKey, switchIntent.SelectedOption.OptionKey, StringComparison.Ordinal))
            ?? options.FirstOrDefault();
        var field = new BuilderFieldViewModel(TemplatesBuilderFieldKeys.NetworkSwitchSelection, "Switch", BuilderFieldKind.SwitchPicker)
        {
            SwitchOptions = options
        };
        field.Mutate(f => f.SelectedSwitchOption = selected);
        field.Changed = OnFieldEdited;
        return field;
    }

    private BuilderFieldViewModel Check(BuilderDraftFieldKey key, string header, bool isChecked)
    {
        var field = new BuilderFieldViewModel(key, header, BuilderFieldKind.CheckBox);
        field.Mutate(f => f.IsChecked = isChecked);
        field.Changed = OnFieldEdited;
        return field;
    }

    private bool TryGetSelectedSwitchOption(out TemplatesBuilderSwitchPickerOption selectedOption)
    {
        var switchField = _networkFields.FirstOrDefault(field => Equals(field.FieldKey, TemplatesBuilderFieldKeys.NetworkSwitchSelection));
        if (switchField?.SelectedSwitchOption is { } option)
        {
            selectedOption = option.Option;
            return true;
        }

        selectedOption = default;
        return false;
    }

    private void RebuildDeploymentProfileRows()
    {
        DeploymentProfileRows =
        [
            BuildProfileRow(ConservativeDeploymentProfile),
            BuildProfileRow(BalancedDeploymentProfile),
            BuildProfileRow(AggressiveDeploymentProfile)
        ];
    }

    private BuilderNavRowViewModel BuildProfileRow(string profile)
    {
        var isSelected = string.Equals(_selectedDeploymentProfile, profile, StringComparison.OrdinalIgnoreCase);
        return BuilderNavRowViewModel.Button(profile, isSelected, isEnabled: true, new RelayCommand(() => SetDeploymentProfile(profile)));
    }

    private void EnsureSelectedResourcesInBounds()
    {
        _selectedNetworkIndex = ClampIndex(_selectedNetworkIndex, _draft.LabNetworks.Count);
        _selectedCredentialSlotIndex = ClampIndex(_selectedCredentialSlotIndex, _draft.CredentialSlots.Count);
        _navigation.EnsureCurrentRouteInBounds(_draft);

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

        // Preserve the "nothing selected" sentinel when there is nothing to select. ClampIndex would fold a
        // negative index back to 0, silently re-pointing at a nonexistent forest/domain; keeping -1 here matches
        // the authoring layer's post-delete contract and every consumer's "index < 0 means no selection" guard.
        _selectedForestDomainIndex = forestDomainCount == 0
            ? TemplatesBuilderTopologyAuthoring.NoSelectionIndex
            : ClampIndex(_selectedForestDomainIndex, forestDomainCount);
    }

    private void OnSelfPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            RefreshActionState();
        }
    }

    private static ObservableCollection<BuilderFieldRowViewModel> PairRows(IReadOnlyList<BuilderFieldViewModel> fields)
    {
        var rows = new ObservableCollection<BuilderFieldRowViewModel>();
        for (var i = 0; i < fields.Count; i += 2)
        {
            rows.Add(new BuilderFieldRowViewModel(fields[i], i + 1 < fields.Count ? fields[i + 1] : null));
        }

        return rows;
    }

    private static string FormatCommandLabel(string commandLabel, string targetLabel)
        => string.IsNullOrWhiteSpace(targetLabel) ? commandLabel : $"{commandLabel}: {targetLabel}";

    private static string ResolveComboValue(IReadOnlyList<string> options, string value, bool selectFirstWhenMissing)
    {
        var normalized = (value ?? string.Empty).Trim();
        var match = options.FirstOrDefault(option => string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        return selectFirstWhenMissing && options.Count > 0 ? options[0] : string.Empty;
    }

    private static string FieldText(IReadOnlyList<BuilderFieldViewModel> fields, BuilderDraftFieldKey key)
        => fields.FirstOrDefault(field => Equals(field.FieldKey, key))?.Value ?? string.Empty;

    private static bool FieldHas(IReadOnlyList<BuilderFieldViewModel> fields, BuilderDraftFieldKey key)
        => fields.Any(field => Equals(field.FieldKey, key));

    private static bool FieldChecked(IReadOnlyList<BuilderFieldViewModel> fields, BuilderDraftFieldKey key)
        => fields.FirstOrDefault(field => Equals(field.FieldKey, key))?.IsChecked == true;

    private static string FormatResourceName(string primary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "(unnamed)" : fallback.Trim();
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

    private static IReadOnlyList<T> CopyList<T>(IEnumerable<T>? values)
        => values is null ? Array.Empty<T>() : values.ToList();

    private static TemplatesBuilderDraftSnapshot CreateEmptyDraft()
        => new(
            string.Empty,
            string.Empty,
            BalancedDeploymentProfile,
            [],
            [],
            [],
            [],
            [],
            false);

    #endregion

    partial void OnTemplateNameChanged(string value) => HandleGeneralEdit();

    partial void OnTemplateDescriptionChanged(string value) => HandleGeneralEdit();

    partial void OnRoleSearchTextChanged(string value) => RebuildSelectedMachineInspector();
}
