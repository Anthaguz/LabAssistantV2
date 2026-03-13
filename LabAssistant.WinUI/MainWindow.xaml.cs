using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Business.Assets;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Assets;
using LabAssistant.WinUI.ViewModels.Machines;
using LabAssistant.WinUI.Views.Assets;
using LabAssistant.WinUI.Views.Deploy;
using LabAssistant.WinUI.Views.Diagnostics;
using LabAssistant.WinUI.Views.Machines;
using LabAssistant.WinUI.Views.Templates;
using LabAssistant.WinUI.Interop;
using Microsoft.UI.Dispatching;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shellViewModel = new();
    private readonly Dictionary<string, NavigationViewItem> _routeToNavigationItem = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NavigationViewItem> _routeToCapabilityNavigationItem = new(StringComparer.Ordinal);
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly IDeploymentPreflightService _deploymentPreflightService;
    private readonly IDeploymentCoordinator _deploymentCoordinator;
    private readonly IDeploymentOutcomeSummaryBuilder _deploymentOutcomeSummaryBuilder;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IVhdxCatalogStore _vhdxCatalogStore;
    private readonly IStructuredLogViewerService _structuredLogViewerService;
    private readonly IAssetsBaseDisksCapabilityService _assetsBaseDisksCapabilityService;
    private readonly IAssetsSwitchesCapabilityService _assetsSwitchesCapabilityService;
    private readonly MachinesWorkspaceComposition _machinesWorkspaceComposition;
    private readonly AssetsWorkspaceComposition _assetsWorkspaceComposition;
    private readonly ObservableCollection<StructuredLogViewerEntry> _structuredLogEntries = [];
    private readonly ObservableCollection<TemplateLibraryItem> _templateLibraryItems = [];
    private readonly ObservableCollection<VmTemplate> _templateVmEntries = [];
    private readonly ObservableCollection<AssetsBaseDiskListRow> _assetsBaseDiskRows = [];
    private readonly ObservableCollection<AssetsSwitchListRow> _assetsSwitchRows = [];
    private readonly ObservableCollection<string> _assetsSwitchAttachedVmNames = [];
    private readonly ObservableCollection<VmTemplate> _deployOnTheFlyVmEntries = [];
    private readonly ObservableCollection<DeployOnTheFlyVmEntryRow> _deployOnTheFlyVmEntryRows = [];
    private readonly ObservableCollection<DeployVmResultRow> _deployVmResultRows = [];
    private readonly ObservableCollection<DeployIssueRow> _deployIssueRows = [];
    private readonly ObservableCollection<string> _deploySharedIssueSummaries = [];
    private readonly ObservableCollection<DeployVmResultRow> _deployOnTheFlyVmResultRows = [];
    private readonly ObservableCollection<DeployIssueRow> _deployOnTheFlyIssueRows = [];
    private readonly Dictionary<string, DeployVmProgressState> _deployProgressByVm = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DeployVmProgressState> _deployOnTheFlyProgressByVm = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TemplateVhdxCatalogOption> _templateVhdxCatalogOptions = [];
    private readonly List<ComboBox> _templateVmSwitchRowCombos = [];
    private readonly List<DeployCompatibilityIssue> _deployCompatibilityIssues = [];
    private readonly List<DeployCompatibilityIssue> _deployOnTheFlyCompatibilityIssues = [];
    private IReadOnlyList<string> _templateAvailableSwitches = Array.Empty<string>();
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private string _activeRouteKey = string.Empty;
    private StructuredLogViewerEntry? _selectedStructuredLogEntry;
    private TemplateLibraryItem? _selectedTemplateLibraryItem;
    private TemplateEditorDocument? _activeTemplateEditorDocument;
    private TemplateLibraryItem? _selectedDeployTemplateLibraryItem;
    private TemplateEditorDocument? _activeDeployTemplateDocument;
    private VmTemplate? _selectedDeployOnTheFlyVmEntry;
    private AssetsBaseDiskListRow? _selectedAssetsBaseDiskRow;
    private AssetsBaseDiskDraft? _pendingAssetsBaseDiskDraft;
    private AssetsSwitchListRow? _selectedAssetsSwitchRow;
    private AssetsSwitchDraft? _pendingAssetsSwitchDraft;
    private DeploymentReadinessReport? _deployReadinessReport;
    private DeploymentReadinessReport? _deployOnTheFlyReadinessReport;
    private VmTemplate? _selectedTemplateVmEntry;
    private bool _isSavingDeletionPolicy;
    private bool _isStructuredLogsLoading;
    private bool _isTemplatesLoading;
    private bool _isAssetsBaseDisksLoading;
    private bool _isAssetsBaseDisksSaving;
    private bool _isAssetsBaseDisksRemoving;
    private bool _isUpdatingAssetsBaseDisksEditor;
    private bool _hasAssetsBaseDisksErrorState;
    private bool _isAssetsSwitchesLoading;
    private bool _isAssetsSwitchesSaving;
    private bool _isAssetsSwitchesDeleting;
    private bool _isUpdatingAssetsSwitchesEditor;
    private bool _hasAssetsSwitchesErrorState;
    private int _assetsSwitchesValidationRequestVersion;
    private int _assetsSwitchesAssessmentRequestVersion;
    private bool _isUpdatingNavigationSelection;
    private bool _isUpdatingDeploySubviewSelection;
    private bool _isUpdatingDiagnosticsSubviewSelection;
    private bool _isUpdatingTemplateVmEditorControls;
    private bool _isUpdatingTemplateVmSwitchRows;
    private bool _isUpdatingTemplateVhdxSelector;
    private bool _isDeployLoadingTemplates;
    private bool _isDeployEvaluatingReadiness;
    private bool _isDeployStarting;
    private bool _showDeployAllVmRows;
    private bool _isDeployOnTheFlyEvaluatingReadiness;
    private bool _isDeployOnTheFlyStarting;
    private bool _isUpdatingDeployOnTheFlyEditor;
    private int _deployOnTheFlyAutoEvaluateNonce;
    private bool _showDeployOnTheFlyAllVmRows;
    private string _deployLifecycleState = "Idle";
    private int _deployProgressPercent;
    private string _deployProgressSummary = "No deployment started.";
    private string _deployOnTheFlyLifecycleState = "Idle";
    private int _deployOnTheFlyProgressPercent;
    private string _deployOnTheFlyProgressSummary = "No deployment started.";
    private string _deployOnTheFlyReadinessSummary = "Readiness has not been evaluated.";
    private bool _isShellRightPanelOpen;
    private bool _isShellRightPanelInCompactFallback;
    private string _shellRightPanelOwnerCapabilityKey = string.Empty;
    private const double ShellRightPanelCompactThreshold = 1200;
    private const double ShellRightPanelExpandedWidth = 380;
    private const double ShellNavigationDrawerThreshold = 1100;
    private ElementTheme _theme = ElementTheme.Light;
    private DispatcherQueueTimer? _rdpReadinessTimer;

    private DeployOverviewView DeployOverviewView => DeployOverviewViewHost;
    private DeployFromTemplateView DeployFromTemplateView => DeployFromTemplateViewHost;
    private DeployOnTheFlyView DeployOnTheFlyView => DeployOnTheFlyViewHost;
    private DiagnosticsOverviewView DiagnosticsOverviewView => DiagnosticsOverviewViewHost;
    private DiagnosticsLogsView DiagnosticsLogsView => DiagnosticsLogsViewHost;
    private AssetsOverviewView AssetsOverviewView => AssetsOverviewViewHost;
    private AssetsBaseDisksView AssetsBaseDisksView => AssetsBaseDisksViewHost;
    private AssetsSwitchesView AssetsSwitchesView => AssetsSwitchesViewHost;
    private DeployFromTemplateRightPanelView DeployFromTemplateRightPanelView => DeployFromTemplateRightPanelViewHost;
    private DeployOnTheFlyRightPanelView DeployOnTheFlyRightPanelView => DeployOnTheFlyRightPanelViewHost;
    private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;
    private FrameworkElement DeployOverviewPanel => DeployOverviewViewHost;
    private FrameworkElement DeployFromTemplatePanel => DeployFromTemplateViewHost;
    private FrameworkElement DeployOnTheFlyPanel => DeployOnTheFlyViewHost;
    private FrameworkElement DiagnosticsOverviewPanel => DiagnosticsOverviewViewHost;
    private FrameworkElement AssetsOverviewPanel => AssetsOverviewViewHost;
    private FrameworkElement AssetsBaseDisksPanel => AssetsBaseDisksViewHost;
    private FrameworkElement AssetsSwitchesPanel => AssetsSwitchesViewHost;
    private FrameworkElement TemplatesWorkspaceHost => TemplatesWorkspacePanel;
    private FrameworkElement DeployLocalNavPanel => DeployLocalNavigationPanel;
    private FrameworkElement AssetsLocalNavPanel => AssetsLocalNavigationPanel;
    private FrameworkElement DiagnosticsLocalNavPanel => DiagnosticsLocalNavigationPanel;
    private FrameworkElement DeployFromTemplateRightPanel => DeployFromTemplateRightPanelViewHost;
    private FrameworkElement DeployOnTheFlyRightPanel => DeployOnTheFlyRightPanelViewHost;
    private FrameworkElement DiagnosticsLogsPanel => DiagnosticsLogsViewHost;
    private TextBox LogFilterOperationIdTextBox => DiagnosticsLogsView.LogFilterOperationIdTextBox;
    private TextBox LogFilterLevelTextBox => DiagnosticsLogsView.LogFilterLevelTextBox;
    private TextBox LogFilterEventTextBox => DiagnosticsLogsView.LogFilterEventTextBox;
    private TextBox LogFilterTextSearchTextBox => DiagnosticsLogsView.LogFilterTextSearchTextBox;
    private CheckBox LogFilterUseStartDateCheckBox => DiagnosticsLogsView.LogFilterUseStartDateCheckBox;
    private DatePicker LogFilterStartDatePicker => DiagnosticsLogsView.LogFilterStartDatePicker;
    private CheckBox LogFilterUseEndDateCheckBox => DiagnosticsLogsView.LogFilterUseEndDateCheckBox;
    private DatePicker LogFilterEndDatePicker => DiagnosticsLogsView.LogFilterEndDatePicker;
    private Button ApplyLogFiltersButton => DiagnosticsLogsView.ApplyLogFiltersButton;
    private Button ClearLogFiltersButton => DiagnosticsLogsView.ClearLogFiltersButton;
    private Button ReloadLogsButton => DiagnosticsLogsView.ReloadLogsButton;
    private Button OpenRawJsonlButton => DiagnosticsLogsView.OpenRawJsonlButton;
    private TextBlock LogsStatusTextBlock => DiagnosticsLogsView.LogsStatusTextBlock;
    private ListView StructuredLogsListView => DiagnosticsLogsView.StructuredLogsListView;
    private TextBlock SelectedLogEnvelopeTextBlock => DiagnosticsLogsView.SelectedLogEnvelopeTextBlock;
    private TextBox SelectedLogContextTextBox => DiagnosticsLogsView.SelectedLogContextTextBox;
    private Button DiagnosticsOverviewOpenLogsButton => DiagnosticsOverviewView.DiagnosticsOverviewOpenLogsButtonControl;
    private TextBlock DiagnosticsOverviewLogsSummaryTextBlock => DiagnosticsOverviewView.DiagnosticsOverviewLogsSummaryTextBlockControl;
    private Button DiagnosticsOverviewOpenSupportExportButton => DiagnosticsOverviewView.DiagnosticsOverviewOpenSupportExportButtonControl;
    private TextBlock DiagnosticsOverviewSupportSummaryTextBlock => DiagnosticsOverviewView.DiagnosticsOverviewSupportSummaryTextBlockControl;
    private ListView AssetsBaseDisksListView => AssetsBaseDisksView.AssetsBaseDisksListViewControl;
    private Button AssetsBaseDisksRefreshButton => AssetsBaseDisksView.AssetsBaseDisksRefreshButtonControl;
    private Button AssetsBaseDisksImportButton => AssetsBaseDisksView.AssetsBaseDisksImportButtonControl;
    private Button AssetsBaseDisksValidateButton => AssetsBaseDisksView.AssetsBaseDisksValidateButtonControl;
    private Button AssetsBaseDisksRemoveButton => AssetsBaseDisksView.AssetsBaseDisksRemoveButtonControl;
    private TextBlock AssetsBaseDisksStatusTextBlock => AssetsBaseDisksView.AssetsBaseDisksStatusTextBlockControl;
    private TextBlock AssetsBaseDisksSelectedDiskSummaryTextBlock => AssetsBaseDisksView.AssetsBaseDisksSelectedDiskSummaryTextBlockControl;
    private TextBlock AssetsBaseDisksSelectedDiskValidationTextBlock => AssetsBaseDisksView.AssetsBaseDisksSelectedDiskValidationTextBlockControl;
    private TextBlock AssetsBaseDisksReferenceWarningTextBlock => AssetsBaseDisksView.AssetsBaseDisksReferenceWarningTextBlockControl;
    private TextBox AssetsBaseDisksOsNameTextBox => AssetsBaseDisksView.AssetsBaseDisksOsNameTextBoxControl;
    private TextBox AssetsBaseDisksOsVersionTextBox => AssetsBaseDisksView.AssetsBaseDisksOsVersionTextBoxControl;
    private TextBox AssetsBaseDisksPathTextBox => AssetsBaseDisksView.AssetsBaseDisksPathTextBoxControl;
    private Button AssetsBaseDisksBrowsePathButton => AssetsBaseDisksView.AssetsBaseDisksBrowsePathButtonControl;
    private TextBox AssetsBaseDisksGenerationTextBox => AssetsBaseDisksView.AssetsBaseDisksGenerationTextBoxControl;
    private TextBox AssetsBaseDisksNotesTextBox => AssetsBaseDisksView.AssetsBaseDisksNotesTextBoxControl;
    private Button AssetsBaseDisksSaveMetadataButton => AssetsBaseDisksView.AssetsBaseDisksSaveMetadataButtonControl;
    private Border AssetsBaseDisksLoadingStatePanel => AssetsBaseDisksView.AssetsBaseDisksLoadingStatePanelControl;
    private Border AssetsBaseDisksEmptyStatePanel => AssetsBaseDisksView.AssetsBaseDisksEmptyStatePanelControl;
    private Border AssetsBaseDisksErrorStatePanel => AssetsBaseDisksView.AssetsBaseDisksErrorStatePanelControl;
    private TextBlock AssetsBaseDisksLoadingStateTextBlock => AssetsBaseDisksView.AssetsBaseDisksLoadingStateTextBlockControl;
    private TextBlock AssetsBaseDisksEmptyStateTextBlock => AssetsBaseDisksView.AssetsBaseDisksEmptyStateTextBlockControl;
    private TextBlock AssetsBaseDisksErrorStateTextBlock => AssetsBaseDisksView.AssetsBaseDisksErrorStateTextBlockControl;
    private ListView AssetsSwitchesListView => AssetsSwitchesView.AssetsSwitchesListViewControl;
    private Button AssetsSwitchesRefreshButton => AssetsSwitchesView.AssetsSwitchesRefreshButtonControl;
    private Button AssetsSwitchesCreateButton => AssetsSwitchesView.AssetsSwitchesCreateButtonControl;
    private Button AssetsSwitchesApplyButton => AssetsSwitchesView.AssetsSwitchesApplyButtonControl;
    private Button AssetsSwitchesDeleteButton => AssetsSwitchesView.AssetsSwitchesDeleteButtonControl;
    private TextBlock AssetsSwitchesStatusTextBlock => AssetsSwitchesView.AssetsSwitchesStatusTextBlockControl;
    private TextBlock AssetsSwitchesSelectedSwitchValidationTextBlock => AssetsSwitchesView.AssetsSwitchesSelectedSwitchValidationTextBlockControl;
    private TextBlock AssetsSwitchesDeleteConstraintTextBlock => AssetsSwitchesView.AssetsSwitchesDeleteConstraintTextBlockControl;
    private TextBox AssetsSwitchesNameTextBox => AssetsSwitchesView.AssetsSwitchesNameTextBoxControl;
    private ComboBox AssetsSwitchesTypeComboBox => AssetsSwitchesView.AssetsSwitchesTypeComboBoxControl;
    private TextBox AssetsSwitchesAdapterTextBox => AssetsSwitchesView.AssetsSwitchesAdapterTextBoxControl;
    private TextBlock AssetsSwitchesAttachedVmsHintTextBlock => AssetsSwitchesView.AssetsSwitchesAttachedVmsHintTextBlockControl;
    private ListView AssetsSwitchesAttachedVmsListView => AssetsSwitchesView.AssetsSwitchesAttachedVmsListViewControl;
    private Border AssetsSwitchesLoadingStatePanel => AssetsSwitchesView.AssetsSwitchesLoadingStatePanelControl;
    private Border AssetsSwitchesEmptyStatePanel => AssetsSwitchesView.AssetsSwitchesEmptyStatePanelControl;
    private Border AssetsSwitchesErrorStatePanel => AssetsSwitchesView.AssetsSwitchesErrorStatePanelControl;
    private TextBlock AssetsSwitchesLoadingStateTextBlock => AssetsSwitchesView.AssetsSwitchesLoadingStateTextBlockControl;
    private TextBlock AssetsSwitchesEmptyStateTextBlock => AssetsSwitchesView.AssetsSwitchesEmptyStateTextBlockControl;
    private TextBox AssetsSwitchesErrorStateTextBox => AssetsSwitchesView.AssetsSwitchesErrorStateTextBoxControl;
    private TemplatesLibraryView TemplatesLibraryView => TemplatesLibraryViewHost;
    private TemplatesEditorView TemplatesEditorView => TemplatesEditorViewHost;
    private Button DeployOverviewOpenQuickDeployButton => DeployOverviewView.DeployOverviewOpenQuickDeployButtonControl;
    private Button DeployOverviewOpenFromTemplateButton => DeployOverviewView.DeployOverviewOpenFromTemplateButtonControl;
    private TextBlock DeployOverviewQuickDeploySummaryTextBlock => DeployOverviewView.DeployOverviewQuickDeploySummaryTextBlockControl;
    private TextBlock DeployOverviewFromTemplateSummaryTextBlock => DeployOverviewView.DeployOverviewFromTemplateSummaryTextBlockControl;
    private ComboBox DeployTemplateSelectorComboBox => DeployFromTemplateView.DeployTemplateSelectorComboBoxControl;
    private Button DeployReloadTemplatesButton => DeployFromTemplateView.DeployReloadTemplatesButtonControl;
    private Button DeployEvaluateReadinessButton => DeployFromTemplateView.DeployEvaluateReadinessButtonControl;
    private TextBlock DeployOverallStateTextBlock => DeployFromTemplateView.DeployOverallStateTextBlockControl;
    private ProgressBar DeployProgressBar => DeployFromTemplateView.DeployProgressBarControl;
    private TextBlock DeployProgressSummaryTextBlock => DeployFromTemplateView.DeployProgressSummaryTextBlockControl;
    private Button DeployOpenResultsPanelButton => DeployFromTemplateView.DeployOpenResultsPanelButtonControl;
    private TextBlock DeployResultsPanelSummaryTextBlock => DeployFromTemplateView.DeployResultsPanelSummaryTextBlockControl;
    private TextBlock DeployGlobalIssuesBadgeTextBlock => DeployFromTemplateView.DeployGlobalIssuesBadgeTextBlockControl;
    private TextBlock DeployReadinessSummaryTextBlock => DeployFromTemplateView.DeployReadinessSummaryTextBlockControl;
    private TextBlock DeployTemplateSummaryTextBlock => DeployFromTemplateView.DeployTemplateSummaryTextBlockControl;
    private TextBlock DeployTemplateRemediationTextBlock => DeployFromTemplateView.DeployTemplateRemediationTextBlockControl;
    private TextBlock DeploySharedIssuesSummaryTextBlock => DeployFromTemplateView.DeploySharedIssuesSummaryTextBlockControl;
    private ListView DeploySharedIssuesListView => DeployFromTemplateView.DeploySharedIssuesListViewControl;
    private Expander DeployGlobalIssuesExpander => DeployFromTemplateRightPanelView.DeployGlobalIssuesExpanderControl;
    private ListView DeployGlobalIssuesListView => DeployFromTemplateRightPanelView.DeployGlobalIssuesListViewControl;
    private ListView DeployVmResultsListView => DeployFromTemplateRightPanelView.DeployVmResultsListViewControl;
    private Button DeployResolveSuggestionsButton => DeployFromTemplateView.DeployResolveSuggestionsButtonControl;
    private Button DeployOpenTemplateEditorButton => DeployFromTemplateView.DeployOpenTemplateEditorButtonControl;
    private Button DeployStartButton => DeployFromTemplateView.DeployStartButtonControl;
    private TextBlock DeployActionStatusTextBlock => DeployFromTemplateView.DeployActionStatusTextBlockControl;
    private ListView DeployOnTheFlyVmEntriesListView => DeployOnTheFlyView.DeployOnTheFlyVmEntriesListViewControl;
    private Button DeployOnTheFlyAddVmButton => DeployOnTheFlyView.DeployOnTheFlyAddVmButtonControl;
    private Button DeployOnTheFlyRemoveVmButton => DeployOnTheFlyView.DeployOnTheFlyRemoveVmButtonControl;
    private TextBox DeployOnTheFlyVmNameTextBox => DeployOnTheFlyView.DeployOnTheFlyVmNameTextBoxControl;
    private TextBox DeployOnTheFlyVmMemoryTextBox => DeployOnTheFlyView.DeployOnTheFlyVmMemoryTextBoxControl;
    private TextBox DeployOnTheFlyVmCpuTextBox => DeployOnTheFlyView.DeployOnTheFlyVmCpuTextBoxControl;
    private ComboBox DeployOnTheFlyVmVhdxCatalogComboBox => DeployOnTheFlyView.DeployOnTheFlyVmVhdxCatalogComboBoxControl;
    private ComboBox DeployOnTheFlyVmSwitchComboBox => DeployOnTheFlyView.DeployOnTheFlyVmSwitchComboBoxControl;
    private TextBlock DeployOnTheFlyVmSwitchGuidanceTextBlock => DeployOnTheFlyView.DeployOnTheFlyVmSwitchGuidanceTextBlockControl;
    private TextBlock DeployOnTheFlyVmVhdxGuidanceTextBlock => DeployOnTheFlyView.DeployOnTheFlyVmVhdxGuidanceTextBlockControl;
    private TextBlock DeployOnTheFlyEditorIssueSummaryTextBlock => DeployOnTheFlyView.DeployOnTheFlyEditorIssueSummaryTextBlockControl;
    private Button DeployOnTheFlyApplyVmChangesButton => DeployOnTheFlyView.DeployOnTheFlyApplyVmChangesButtonControl;
    private TextBlock DeployOnTheFlyOverallStateTextBlock => DeployOnTheFlyView.DeployOnTheFlyOverallStateTextBlockControl;
    private ProgressBar DeployOnTheFlyProgressBar => DeployOnTheFlyView.DeployOnTheFlyProgressBarControl;
    private TextBlock DeployOnTheFlyProgressSummaryTextBlock => DeployOnTheFlyView.DeployOnTheFlyProgressSummaryTextBlockControl;
    private Button DeployOnTheFlyOpenResultsPanelButton => DeployOnTheFlyView.DeployOnTheFlyOpenResultsPanelButtonControl;
    private TextBlock DeployOnTheFlyResultsPanelSummaryTextBlock => DeployOnTheFlyView.DeployOnTheFlyResultsPanelSummaryTextBlockControl;
    private TextBlock DeployOnTheFlyGlobalIssuesBadgeTextBlock => DeployOnTheFlyView.DeployOnTheFlyGlobalIssuesBadgeTextBlockControl;
    private TextBlock DeployOnTheFlyReadinessSummaryTextBlock => DeployOnTheFlyView.DeployOnTheFlyReadinessSummaryTextBlockControl;
    private ListView DeployOnTheFlyVmResultsListView => DeployOnTheFlyRightPanelView.DeployOnTheFlyVmResultsListViewControl;
    private Button DeployOnTheFlyEvaluateButton => DeployOnTheFlyView.DeployOnTheFlyEvaluateButtonControl;
    private Button DeployOnTheFlyResolveSuggestionsButton => DeployOnTheFlyView.DeployOnTheFlyResolveSuggestionsButtonControl;
    private Button DeployOnTheFlyOpenTemplateEditorButton => DeployOnTheFlyView.DeployOnTheFlyOpenTemplateEditorButtonControl;
    private Button DeployOnTheFlyStartButton => DeployOnTheFlyView.DeployOnTheFlyStartButtonControl;
    private TextBlock DeployOnTheFlyStatusTextBlock => DeployOnTheFlyView.DeployOnTheFlyStatusTextBlockControl;
    private ListView TemplateLibraryListView => TemplatesLibraryView.TemplateLibraryListViewControl;
    private TextBox TemplateSearchTextBox => TemplatesLibraryView.TemplateSearchTextBoxControl;
    private Button ApplyTemplateSearchButton => TemplatesLibraryView.ApplyTemplateSearchButtonControl;
    private Button ClearTemplateSearchButton => TemplatesLibraryView.ClearTemplateSearchButtonControl;
    private Button ReloadTemplatesButton => TemplatesLibraryView.ReloadTemplatesButtonControl;
    private Button OpenTemplateInEditorButton => TemplatesLibraryView.OpenTemplateInEditorButtonControl;
    private Button CreateTemplateButton => TemplatesLibraryView.CreateTemplateButtonControl;
    private Button DeleteTemplateButton => TemplatesLibraryView.DeleteTemplateButtonControl;
    private Button ImportTemplateButton => TemplatesLibraryView.ImportTemplateButtonControl;
    private Button ExportTemplateButton => TemplatesLibraryView.ExportTemplateButtonControl;
    private TextBlock TemplatesLibraryStatusTextBlock => TemplatesLibraryView.TemplatesLibraryStatusTextBlockControl;
    private TextBox TemplateNameTextBox => TemplatesEditorView.TemplateNameTextBoxControl;
    private TextBox TemplateDescriptionTextBox => TemplatesEditorView.TemplateDescriptionTextBoxControl;
    private TextBlock TemplateEditorContextTextBlock => TemplatesEditorView.TemplateEditorContextTextBlockControl;
    private TextBlock TemplateIdTextBlock => TemplatesEditorView.TemplateIdTextBlockControl;
    private TextBlock TemplateFilePathTextBlock => TemplatesEditorView.TemplateFilePathTextBlockControl;
    private TextBlock TemplateVmCountTextBlock => TemplatesEditorView.TemplateVmCountTextBlockControl;
    private ListView TemplateVmListView => TemplatesEditorView.TemplateVmListViewControl;
    private Button AddTemplateVmButton => TemplatesEditorView.AddTemplateVmButtonControl;
    private Button RemoveTemplateVmButton => TemplatesEditorView.RemoveTemplateVmButtonControl;
    private TextBlock TemplateVmIdTextBlock => TemplatesEditorView.TemplateVmIdTextBlockControl;
    private TextBox TemplateVmNameTextBox => TemplatesEditorView.TemplateVmNameTextBoxControl;
    private TextBox TemplateVmMemoryTextBox => TemplatesEditorView.TemplateVmMemoryTextBoxControl;
    private TextBox TemplateVmCpuTextBox => TemplatesEditorView.TemplateVmCpuTextBoxControl;
    private StackPanel TemplateVmSwitchRowsPanel => TemplatesEditorView.TemplateVmSwitchRowsPanelControl;
    private Button AddTemplateVmSwitchRowButton => TemplatesEditorView.AddTemplateVmSwitchRowButtonControl;
    private TextBlock TemplateVmSwitchGuidanceTextBlock => TemplatesEditorView.TemplateVmSwitchGuidanceTextBlockControl;
    private ComboBox TemplateVmVhdxCatalogComboBox => TemplatesEditorView.TemplateVmVhdxCatalogComboBoxControl;
    private TextBlock TemplateVmVhdxGuidanceTextBlock => TemplatesEditorView.TemplateVmVhdxGuidanceTextBlockControl;
    private TextBox TemplateVmVhdxIdTextBox => TemplatesEditorView.TemplateVmVhdxIdTextBoxControl;
    private TextBox TemplateVmVhdPathTextBox => TemplatesEditorView.TemplateVmVhdPathTextBoxControl;
    private TextBox TemplateVmVhdxSignatureTextBox => TemplatesEditorView.TemplateVmVhdxSignatureTextBoxControl;
    private Button ApplyTemplateVmChangesButton => TemplatesEditorView.ApplyTemplateVmChangesButtonControl;
    private TextBlock TemplateEditorStatusTextBlock => TemplatesEditorView.TemplateEditorStatusTextBlockControl;
    private Button SaveTemplateButton => TemplatesEditorView.SaveTemplateButtonControl;
    private Button SaveTemplateAsButton => TemplatesEditorView.SaveTemplateAsButtonControl;
    private Button ValidateTemplateButton => TemplatesEditorView.ValidateTemplateButtonControl;
    private Button BackToLibraryButton => TemplatesEditorView.BackToLibraryButtonControl;
    private const string TemplateSwitchPlaceholder = "(Select switch)";
    private const string TemplateVhdxPlaceholder = "(Keep current / unresolved)";
    private const string DeployOnTheFlySwitchPlaceholder = "(No switch)";
    private const string DeployOnTheFlyVhdxPlaceholder = "(Select base disk)";
    private const string DeployCapabilityKey = "deploy";

    public MainWindow()
    {
        InitializeComponent();
        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _templatesCapabilityService = App.Services.GetRequiredService<ITemplatesCapabilityService>();
        _deploymentPreflightService = App.Services.GetRequiredService<IDeploymentPreflightService>();
        _deploymentCoordinator = App.Services.GetRequiredService<IDeploymentCoordinator>();
        _deploymentOutcomeSummaryBuilder = App.Services.GetRequiredService<IDeploymentOutcomeSummaryBuilder>();
        _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        _vhdxCatalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();
        _structuredLogViewerService = App.Services.GetRequiredService<IStructuredLogViewerService>();
        _assetsBaseDisksCapabilityService = App.Services.GetRequiredService<IAssetsBaseDisksCapabilityService>();
        _assetsSwitchesCapabilityService = App.Services.GetRequiredService<IAssetsSwitchesCapabilityService>();
        _machinesWorkspaceComposition = new MachinesWorkspaceComposition(
            _machinesCapabilityService,
            MachinesOverviewViewHost,
            new MachinesWorkspaceShellBridge(
                () => IsMachinesOverviewActive,
                UpdateReadinessPollingState,
                () => RootLayout.XamlRoot));
        _assetsWorkspaceComposition = new AssetsWorkspaceComposition(
            AssetsOverviewViewHost,
            AssetsBaseDisksViewHost,
            AssetsSwitchesViewHost,
            AssetsSubviewTabView,
            AssetsOverviewTabViewItem,
            AssetsBaseDisksTabViewItem,
            AssetsSwitchesTabViewItem,
            _assetsBaseDiskRows,
            _assetsSwitchRows,
            _assetsSwitchAttachedVmNames,
            new AssetsWorkspaceHost(
                () => _isAssetsBaseDisksLoading,
                () => _isAssetsSwitchesLoading,
                () => _assetsBaseDiskRows.Count,
                () => _assetsSwitchRows.Count,
                EnsureAssetsBaseDisksAsync,
                EnsureAssetsSwitchesAsync,
                UpdateAssetsBaseDisksUi,
                UpdateAssetsSwitchesUi),
            new AssetsWorkspaceShellBridge(
                () => IsAssetsCapabilityActive,
                () => IsAssetsOverviewActive,
                () => IsAssetsBaseDisksActive,
                () => IsAssetsSwitchesActive,
                NavigateToRoute));
        _activeRouteKey = _shellViewModel.StartupRoute;
        _shellViewModel.TryResolveRoute(_activeRouteKey, out _activeCapability, out _activeSubview);
        StructuredLogsListView.ItemsSource = _structuredLogEntries;
        TemplateLibraryListView.ItemsSource = _templateLibraryItems;
        TemplateVmListView.ItemsSource = _templateVmEntries;
        WireAssetsBaseDisksHandlers();
        WireAssetsSwitchesHandlers();
        WireDiagnosticsLogsHandlers();
        WireTemplatesHandlers();
        WireDeployHandlers();
        WireOverviewHandlers();
        ConfigureShellIcons();
        ConfigureNavigationView();
        ApplyShellNavigationMode(1280);
        Title = "LabAssistant.WinUI";
        SetInitialSize(1280, 800);
        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.SizeChanged += RootLayout_SizeChanged;
        InitializeRdpReadinessTimer();
        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            await _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);
            await EnsureTemplateSwitchesAsync(forceRefresh: true);
            await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh: true);
            await EnsureTemplatesLibraryAsync(forceRefresh: true);
            await EnsureAssetsBaseDisksAsync(forceRefresh: true);
            await LoadMachinesDeletionPolicyAsync();
        };
        ApplyState();
    }

    private void SetInitialSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void WireDiagnosticsLogsHandlers()
    {
        ApplyLogFiltersButton.Click += ApplyLogFiltersButton_Click;
        ClearLogFiltersButton.Click += ClearLogFiltersButton_Click;
        ReloadLogsButton.Click += ReloadLogsButton_Click;
        OpenRawJsonlButton.Click += OpenRawJsonlButton_Click;
        StructuredLogsListView.SelectionChanged += StructuredLogsListView_SelectionChanged;
    }

    private void WireAssetsBaseDisksHandlers()
    {
        AssetsBaseDisksListView.SelectionChanged += AssetsBaseDisksListView_SelectionChanged;
        AssetsBaseDisksRefreshButton.Click += AssetsBaseDisksRefreshButton_Click;
        AssetsBaseDisksImportButton.Click += AssetsBaseDisksImportButton_Click;
        AssetsBaseDisksValidateButton.Click += AssetsBaseDisksValidateButton_Click;
        AssetsBaseDisksRemoveButton.Click += AssetsBaseDisksRemoveButton_Click;
        AssetsBaseDisksBrowsePathButton.Click += AssetsBaseDisksBrowsePathButton_Click;
        AssetsBaseDisksSaveMetadataButton.Click += AssetsBaseDisksSaveMetadataButton_Click;
        AssetsBaseDisksOsNameTextBox.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
        AssetsBaseDisksOsVersionTextBox.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
        AssetsBaseDisksGenerationTextBox.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
        AssetsBaseDisksNotesTextBox.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
    }

    private void WireAssetsSwitchesHandlers()
    {
        AssetsSwitchesListView.SelectionChanged += AssetsSwitchesListView_SelectionChanged;
        AssetsSwitchesRefreshButton.Click += AssetsSwitchesRefreshButton_Click;
        AssetsSwitchesCreateButton.Click += AssetsSwitchesCreateButton_Click;
        AssetsSwitchesApplyButton.Click += AssetsSwitchesApplyButton_Click;
        AssetsSwitchesDeleteButton.Click += AssetsSwitchesDeleteButton_Click;
        AssetsSwitchesNameTextBox.TextChanged += AssetsSwitchesEditorControl_Changed;
        AssetsSwitchesTypeComboBox.SelectionChanged += AssetsSwitchesEditorControl_Changed;
        AssetsSwitchesAdapterTextBox.TextChanged += AssetsSwitchesEditorControl_Changed;
    }

    private void WireTemplatesHandlers()
    {
        TemplateLibraryListView.SelectionChanged += TemplateLibraryListView_SelectionChanged;
        TemplateVmListView.SelectionChanged += TemplateVmListView_SelectionChanged;
        ApplyTemplateSearchButton.Click += ApplyTemplateSearchButton_Click;
        ClearTemplateSearchButton.Click += ClearTemplateSearchButton_Click;
        ReloadTemplatesButton.Click += ReloadTemplatesButton_Click;
        OpenTemplateInEditorButton.Click += OpenTemplateInEditorButton_Click;
        CreateTemplateButton.Click += CreateTemplateButton_Click;
        DeleteTemplateButton.Click += DeleteTemplateButton_Click;
        ImportTemplateButton.Click += ImportTemplateButton_Click;
        ExportTemplateButton.Click += ExportTemplateButton_Click;
        SaveTemplateButton.Click += SaveTemplateButton_Click;
        SaveTemplateAsButton.Click += SaveTemplateAsButton_Click;
        ValidateTemplateButton.Click += ValidateTemplateButton_Click;
        BackToLibraryButton.Click += BackToLibraryButton_Click;
        AddTemplateVmButton.Click += AddTemplateVmButton_Click;
        RemoveTemplateVmButton.Click += RemoveTemplateVmButton_Click;
        AddTemplateVmSwitchRowButton.Click += AddTemplateVmSwitchRowButton_Click;
        TemplateVmVhdxCatalogComboBox.SelectionChanged += TemplateVmVhdxCatalogComboBox_SelectionChanged;
        ApplyTemplateVmChangesButton.Click += ApplyTemplateVmChangesButton_Click;
    }

    private void WireDeployHandlers()
    {
        DeployReloadTemplatesButton.Click += DeployReloadTemplatesButton_Click;
        DeployEvaluateReadinessButton.Click += DeployEvaluateReadinessButton_Click;
        DeployResolveSuggestionsButton.Click += DeployResolveSuggestionsButton_Click;
        DeployOpenTemplateEditorButton.Click += DeployOpenTemplateEditorButton_Click;
        DeployStartButton.Click += DeployStartButton_Click;
        DeployTemplateSelectorComboBox.SelectionChanged += DeployTemplateSelectorComboBox_SelectionChanged;
        DeployTemplateSelectorComboBox.DisplayMemberPath = nameof(TemplateLibraryItem.Name);
        DeployTemplateSelectorComboBox.ItemsSource = _templateLibraryItems;
        DeployOpenResultsPanelButton.Click += DeployOpenResultsPanelButton_Click;
        DeployVmResultsListView.ItemsSource = _deployVmResultRows;
        DeployGlobalIssuesListView.ItemsSource = _deployIssueRows;
        DeploySharedIssuesListView.ItemsSource = _deploySharedIssueSummaries;
        DeployGlobalIssuesExpander.IsExpanded = false;
        UpdateDeployResultRows();
        UpdateDeployIssueRows();

        DeployOnTheFlyVmEntriesListView.ItemsSource = _deployOnTheFlyVmEntryRows;
        DeployOnTheFlyVmEntriesListView.SelectionChanged += DeployOnTheFlyVmEntriesListView_SelectionChanged;
        DeployOnTheFlyView.VmRemoveRequested += DeployOnTheFlyView_VmRemoveRequested;
        DeployOnTheFlyAddVmButton.Click += DeployOnTheFlyAddVmButton_Click;
        DeployOnTheFlyRemoveVmButton.Click += DeployOnTheFlyRemoveVmButton_Click;
        DeployOnTheFlyApplyVmChangesButton.Click += DeployOnTheFlyApplyVmChangesButton_Click;
        DeployOnTheFlyVmNameTextBox.TextChanged += DeployOnTheFlyVmNameTextBox_TextChanged;
        DeployOnTheFlyVmMemoryTextBox.TextChanged += DeployOnTheFlyVmMemoryTextBox_TextChanged;
        DeployOnTheFlyVmCpuTextBox.TextChanged += DeployOnTheFlyVmCpuTextBox_TextChanged;
        DeployOnTheFlyVmSwitchComboBox.SelectionChanged += DeployOnTheFlyVmSwitchComboBox_SelectionChanged;
        DeployOnTheFlyVmVhdxCatalogComboBox.SelectionChanged += DeployOnTheFlyVmVhdxCatalogComboBox_SelectionChanged;
        DeployOnTheFlyEvaluateButton.Click += DeployOnTheFlyEvaluateButton_Click;
        DeployOnTheFlyResolveSuggestionsButton.Click += DeployOnTheFlyResolveSuggestionsButton_Click;
        DeployOnTheFlyOpenTemplateEditorButton.Click += DeployOnTheFlyOpenTemplateEditorButton_Click;
        DeployOnTheFlyStartButton.Click += DeployOnTheFlyStartButton_Click;
        DeployOnTheFlyOpenResultsPanelButton.Click += DeployOnTheFlyOpenResultsPanelButton_Click;
        DeployOnTheFlyVmResultsListView.ItemsSource = _deployOnTheFlyVmResultRows;
        EnsureDeployOnTheFlySeeded();
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private void WireOverviewHandlers()
    {
        DeployOverviewOpenQuickDeployButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DeployOnTheFly);
        DeployOverviewOpenFromTemplateButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DeployFromTemplate);
        DiagnosticsOverviewOpenLogsButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);
        DiagnosticsOverviewOpenSupportExportButton.Click += (_, _) => OpenStructuredLogLocation();
    }

    private void ConfigureShellIcons()
    {
        HamburgerButton.Content = CreateIconGlyph(ShellIconToken.Menu);
        InsightsToggleButton.Content = CreateIconGlyph(ShellIconToken.Insights);
    }

    private void ConfigureNavigationView()
    {
        _routeToNavigationItem.Clear();
        _routeToCapabilityNavigationItem.Clear();
        GlobalNavigationView.MenuItems.Clear();
        GlobalNavigationView.FooterMenuItems.Clear();

        foreach (var capability in _shellViewModel.Capabilities)
        {
            var parentItem = new NavigationViewItem
            {
                Content = capability.DisplayName,
                Tag = capability.Key,
                Icon = new FontIcon { Glyph = capability.Glyph }
            };

            if (!capability.IsFooter)
            {
                foreach (var subview in capability.Subviews)
                {
                    if (!capability.ShowChildRoutesInShell)
                    {
                        _routeToNavigationItem[subview.RouteKey] = parentItem;
                        _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                        continue;
                    }

                    if (capability.HasOverview && string.Equals(subview.RouteKey, capability.DefaultSubview.RouteKey, StringComparison.Ordinal))
                    {
                        _routeToNavigationItem[subview.RouteKey] = parentItem;
                        _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                        continue;
                    }

                    var childItem = new NavigationViewItem
                    {
                        Content = subview.DisplayName,
                        Tag = subview.RouteKey
                    };
                    parentItem.MenuItems.Add(childItem);
                    _routeToNavigationItem[subview.RouteKey] = childItem;
                    _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                }
            }

            if (capability.IsFooter)
            {
                GlobalNavigationView.FooterMenuItems.Add(parentItem);
                _routeToNavigationItem[capability.DefaultSubview.RouteKey] = parentItem;
                _routeToCapabilityNavigationItem[capability.DefaultSubview.RouteKey] = parentItem;
            }
            else
            {
                GlobalNavigationView.MenuItems.Add(parentItem);
            }
        }
    }

    private TextBlock CreateIconGlyph(string token)
    {
        return new TextBlock
        {
            Text = ShellIconCatalog.GetGlyph(token),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        };
    }

    private void ApplyState()
    {
        CurrentRouteTextBlock.Text = $"{_activeCapability.DisplayName} / {_activeSubview.DisplayName}";
        ContentTitleTextBlock.Text = _activeCapability.DisplayName;
        ContentDescriptionTextBlock.Text = IsMachinesOverviewActive
            ? "Manage host Hyper-V VMs. Start/stop/restart, open console, or delete with explicit scope."
            : IsDeployCapabilityActive
                ? "Configure and run deployment workflows from one capability surface with readiness, remediation, and results context."
            : IsAssetsCapabilityActive
                ? "Manage shared Hyper-V assets, inventory, and compatibility state from one capability surface."
            : IsTemplatesCapabilityActive
                ? "Browse templates and enter the editor through explicit create or edit workflows."
            : IsSettingsMachinesActive
                ? "Configure Machines policy defaults."
                : IsDiagnosticsCapabilityActive
                    ? "Inspect support-oriented diagnostics and structured log context from one capability surface."
                : $"Subview: {_activeSubview.DisplayName}. Placeholder content until capability migration lands.";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        ApplyRightPanelState();
        MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        DeployLocalNavPanel.Visibility = IsDeployCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        DeployOverviewPanel.Visibility = IsDeployOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        DeployFromTemplatePanel.Visibility = IsDeployFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;
        DeployOnTheFlyPanel.Visibility = IsDeployOnTheFlyActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsLocalNavPanel.Visibility = IsAssetsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsOverviewPanel.Visibility = IsAssetsOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksPanel.Visibility = IsAssetsBaseDisksActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesPanel.Visibility = IsAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;
        TemplatesWorkspaceHost.Visibility = IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        TemplatesLibraryViewHost.Visibility = IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;
        TemplatesEditorViewHost.Visibility = IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsLocalNavPanel.Visibility = IsDiagnosticsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsOverviewPanel.Visibility = IsDiagnosticsOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        SyncDeploySubviewSelection();
        SyncDiagnosticsSubviewSelection();
        UpdateTemplatesUi();
        SettingsMachinesPanel.Visibility = IsSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsLogsPanel.Visibility = IsDiagnosticsLogsActive ? Visibility.Visible : Visibility.Collapsed;
        NonMachinesPlaceholderTextBlock.Visibility = (IsMachinesOverviewActive || IsDeployCapabilityActive || IsAssetsCapabilityActive || IsTemplatesCapabilityActive || IsSettingsMachinesActive || IsDiagnosticsCapabilityActive) ? Visibility.Collapsed : Visibility.Visible;

        QueueNavigationSelectionUpdate();

        _machinesWorkspaceComposition.ApplyShellState();
        _assetsWorkspaceComposition.ApplyShellState();
        if (IsSettingsMachinesActive)
        {
            _ = LoadMachinesDeletionPolicyAsync();
        }

        if (IsDiagnosticsOverviewActive)
        {
            UpdateDiagnosticsOverviewUi();
        }

        if (IsDiagnosticsLogsActive)
        {
            _ = EnsureStructuredLogsLoadedAsync(forceReload: false);
        }

        if (IsTemplatesLibraryActive)
        {
            _ = EnsureTemplatesLibraryAsync(forceRefresh: false);
        }

        if (IsDeployOverviewActive)
        {
            UpdateDeployOverviewUi();
        }

        if (IsDeployFromTemplateActive)
        {
            _ = EnsureDeployTemplatesLoadedAsync(forceRefresh: false);
            UpdateDeployUi();
        }

        if (IsDeployOnTheFlyActive)
        {
            EnsureDeployOnTheFlySeeded();
            _ = EnsureDeployOnTheFlyReferenceDataAsync(forceRefresh: false);
            UpdateDeployOnTheFlyUi();

            if (_deployOnTheFlyVmEntries.Count > 0 &&
                _deployOnTheFlyReadinessReport is null &&
                !_isDeployOnTheFlyEvaluatingReadiness &&
                !_isDeployOnTheFlyStarting)
            {
                ScheduleDeployOnTheFlyAutoEvaluate();
            }
        }
    }

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyShellNavigationMode(e.NewSize.Width);

        var isCompact = e.NewSize.Width < ShellRightPanelCompactThreshold;
        if (_isShellRightPanelInCompactFallback == isCompact)
        {
            return;
        }

        _isShellRightPanelInCompactFallback = isCompact;
        if (_isShellRightPanelInCompactFallback)
        {
            _isShellRightPanelOpen = false;
        }

        ApplyRightPanelState();
    }

    private void ApplyShellNavigationMode(double width)
    {
        var useDrawerMode = width < ShellNavigationDrawerThreshold;
        GlobalNavigationView.PaneDisplayMode = useDrawerMode
            ? NavigationViewPaneDisplayMode.LeftMinimal
            : NavigationViewPaneDisplayMode.LeftCompact;
        GlobalNavigationView.CompactPaneLength = useDrawerMode ? 0 : 56;

        if (useDrawerMode)
        {
            GlobalNavigationView.IsPaneOpen = false;
        }
    }

    private void ResetRightPanelForCapabilitySwitch(string incomingCapabilityKey)
    {
        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(incomingCapabilityKey);
        _isShellRightPanelOpen = false;
        DeployGlobalIssuesExpander.IsExpanded = false;
    }

    private string ResolveRightPanelOwnerCapabilityKey(string capabilityKey)
    {
        return string.Equals(capabilityKey, DeployCapabilityKey, StringComparison.Ordinal)
            ? DeployCapabilityKey
            : string.Empty;
    }

    private bool CanActiveCapabilityOwnRightPanel()
    {
        return string.Equals(_shellRightPanelOwnerCapabilityKey, DeployCapabilityKey, StringComparison.Ordinal);
    }

    private bool ShouldOwnerAutoOpenRightPanel()
    {
        if (!CanActiveCapabilityOwnRightPanel())
        {
            return false;
        }

        return _isDeployStarting ||
            _isDeployOnTheFlyStarting ||
            string.Equals(_deployLifecycleState, "Running", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_deployOnTheFlyLifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyRightPanelState()
    {
        if (RootLayout.ActualWidth > 0)
        {
            _isShellRightPanelInCompactFallback = RootLayout.ActualWidth < ShellRightPanelCompactThreshold;
        }

        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(_activeCapability.Key);
        if (_isShellRightPanelInCompactFallback && _isShellRightPanelOpen)
        {
            _isShellRightPanelOpen = false;
        }

        if (ShouldOwnerAutoOpenRightPanel())
        {
            _isShellRightPanelOpen = true;
        }

        var hasOwner = CanActiveCapabilityOwnRightPanel();
        var showPanel = hasOwner && _isShellRightPanelOpen && !_isShellRightPanelInCompactFallback;
        InsightsPanel.Visibility = showPanel ? Visibility.Visible : Visibility.Collapsed;
        ShellRightPanelColumn.Width = showPanel ? new GridLength(ShellRightPanelExpandedWidth) : new GridLength(0);
        InsightsToggleButton.IsEnabled = hasOwner && !_isShellRightPanelInCompactFallback;
        InsightsToggleButton.Opacity = InsightsToggleButton.IsEnabled ? 1.0 : 0.45;
        ToolTipService.SetToolTip(InsightsToggleButton, "Toggle progress and results panel");
        RightPanelTitleTextBlock.Text = IsDeployFromTemplateActive
            ? "From Template Progress / Results"
            : IsDeployOnTheFlyActive
                ? "Quick Deploy Progress / Results"
                : "Details";
        DeployFromTemplateRightPanel.Visibility = IsDeployFromTemplateActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        DeployOnTheFlyRightPanel.Visibility = IsDeployOnTheFlyActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        RightPanelEmptyStateBorder.Visibility = (!IsDeployFromTemplateActive && !IsDeployOnTheFlyActive && showPanel)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateDeployRightPanelLaunchers(showPanel);
        IssueBadge.Visibility = Visibility.Collapsed;
        IssueBadgeTextBlock.Text = string.Empty;
    }

    private void UpdateDeployRightPanelLaunchers(bool showPanel)
    {
        var panelUnavailable = _isShellRightPanelInCompactFallback;
        var fromTemplateIsRunning = _isDeployStarting || string.Equals(_deployLifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
        var quickDeployIsRunning = _isDeployOnTheFlyStarting || string.Equals(_deployOnTheFlyLifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

        DeployOpenResultsPanelButton.Content = showPanel && IsDeployFromTemplateActive ? "Hide Progress / Results" : "Open Progress / Results";
        DeployOnTheFlyOpenResultsPanelButton.Content = showPanel && IsDeployOnTheFlyActive ? "Hide Progress / Results" : "Open Progress / Results";

        DeployOpenResultsPanelButton.IsEnabled = IsDeployFromTemplateActive && !panelUnavailable;
        DeployOnTheFlyOpenResultsPanelButton.IsEnabled = IsDeployOnTheFlyActive && !panelUnavailable;

        DeployResultsPanelSummaryTextBlock.Text = panelUnavailable
            ? "Expand the window to review the progress and results panel."
            : fromTemplateIsRunning
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : _deployVmResultRows.Count > 0
                    ? $"{_deployVmResultRows.Count} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.";

        DeployOnTheFlyResultsPanelSummaryTextBlock.Text = panelUnavailable
            ? "Expand the window to review the progress and results panel."
            : quickDeployIsRunning
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : _deployOnTheFlyVmResultRows.Count > 0
                    ? $"{_deployOnTheFlyVmResultRows.Count} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.";
    }

    private void NavigateToRoute(string routeKey)
    {
        if (!_shellViewModel.TryResolveRoute(routeKey, out var capability, out var subview))
        {
            return;
        }

        var changedCapability = !string.Equals(_activeCapability.Key, capability.Key, StringComparison.Ordinal);
        var changedSubview = !string.Equals(_activeSubview.RouteKey, subview.RouteKey, StringComparison.Ordinal);
        if (!changedCapability && !changedSubview)
        {
            return;
        }

        if (changedCapability || changedSubview)
        {
            _machinesWorkspaceComposition.DiscardEditDraft();
        }

        if (changedCapability)
        {
            ResetRightPanelForCapabilitySwitch(capability.Key);
        }

        _activeCapability = capability;
        _activeSubview = subview;
        _activeRouteKey = subview.RouteKey;
        ApplyState();

        if (IsMachinesOverviewActive)
        {
            _ = _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: false);
        }
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        GlobalNavigationView.IsPaneOpen = !GlobalNavigationView.IsPaneOpen;
    }

    private void GlobalNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (_isUpdatingNavigationSelection)
        {
            return;
        }

        if (args.InvokedItemContainer is not NavigationViewItem invokedItem)
        {
            return;
        }

        if (invokedItem.Tag is not string key)
        {
            return;
        }

        if (_shellViewModel.TryResolveCapability(key, out var capability))
        {
            NavigateToRoute(capability.DefaultSubview.RouteKey);
            return;
        }

        NavigateToRoute(key);
    }

    private void QueueNavigationSelectionUpdate()
    {
        if (!_routeToCapabilityNavigationItem.TryGetValue(_activeRouteKey, out var selectedNavigationItem))
        {
            return;
        }

        if (ReferenceEquals(GlobalNavigationView.SelectedItem, selectedNavigationItem))
        {
            return;
        }

        _isUpdatingNavigationSelection = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                GlobalNavigationView.SelectedItem = selectedNavigationItem;
            }
            finally
            {
                _isUpdatingNavigationSelection = false;
            }
        });
    }

    private void DeploySubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDeploySubviewSelection || DeploySubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, DeployOverviewTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.DeployOverview);
        }
        else if (ReferenceEquals(selectedTab, DeployQuickDeployTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.DeployOnTheFly);
        }
        else if (ReferenceEquals(selectedTab, DeployFromTemplateTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.DeployFromTemplate);
        }
    }

    private void DiagnosticsSubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDiagnosticsSubviewSelection || DiagnosticsSubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, DiagnosticsOverviewTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.DiagnosticsOverview);
        }
        else if (ReferenceEquals(selectedTab, DiagnosticsLogsTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);
        }
    }

    private void SyncDeploySubviewSelection()
    {
        if (!IsDeployCapabilityActive)
        {
            return;
        }

        var selectedTab = IsDeployOverviewActive
            ? DeployOverviewTabViewItem
            : IsDeployOnTheFlyActive
                ? DeployQuickDeployTabViewItem
                : DeployFromTemplateTabViewItem;

        if (ReferenceEquals(DeploySubviewTabView.SelectedItem, selectedTab))
        {
            return;
        }

        _isUpdatingDeploySubviewSelection = true;
        try
        {
            DeploySubviewTabView.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingDeploySubviewSelection = false;
        }
    }

    private void SyncDiagnosticsSubviewSelection()
    {
        if (!IsDiagnosticsCapabilityActive)
        {
            return;
        }

        var selectedTab = IsDiagnosticsOverviewActive
            ? DiagnosticsOverviewTabViewItem
            : DiagnosticsLogsTabViewItem;

        if (ReferenceEquals(DiagnosticsSubviewTabView.SelectedItem, selectedTab))
        {
            return;
        }

        _isUpdatingDiagnosticsSubviewSelection = true;
        try
        {
            DiagnosticsSubviewTabView.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingDiagnosticsSubviewSelection = false;
        }
    }

    private void UpdateDeployOverviewUi()
    {
        DeployOverviewQuickDeploySummaryTextBlock.Text = _deployOnTheFlyVmEntries.Count > 0
            ? $"{_deployOnTheFlyVmEntries.Count} VM entries currently staged in the Quick Deploy draft."
            : "Open Quick Deploy to configure VM entries and run deployment.";
        DeployOverviewFromTemplateSummaryTextBlock.Text = _isDeployLoadingTemplates
            ? "Template inventory is loading."
            : _templateLibraryItems.Count > 0
                ? $"{_templateLibraryItems.Count} templates currently available for From Template."
                : "Open From Template to load template inventory and review readiness.";
    }

    private void UpdateDiagnosticsOverviewUi()
    {
        DiagnosticsOverviewLogsSummaryTextBlock.Text = _isStructuredLogsLoading
            ? "Structured logs are loading."
            : _structuredLogEntries.Count > 0
                ? $"{_structuredLogEntries.Count} structured log entries are currently loaded."
                : "Open Logs to inspect structured events and current support context.";
        DiagnosticsOverviewSupportSummaryTextBlock.Text = "Open the current structured log location for support export or manual diagnostics collection.";
    }

    private void InsightsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanActiveCapabilityOwnRightPanel())
        {
            return;
        }

        _isShellRightPanelOpen = !_isShellRightPanelOpen;
        ApplyRightPanelState();
    }

    private void DeployOpenResultsPanelButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleDeployRightPanelFromWorkflow();
    }

    private void DeployOnTheFlyOpenResultsPanelButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleDeployRightPanelFromWorkflow();
    }

    private void ToggleDeployRightPanelFromWorkflow()
    {
        if (!CanActiveCapabilityOwnRightPanel() || _isShellRightPanelInCompactFallback)
        {
            return;
        }

        _isShellRightPanelOpen = !_isShellRightPanelOpen;
        ApplyRightPanelState();
    }

    private void CloseRightPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isShellRightPanelOpen)
        {
            return;
        }

        _isShellRightPanelOpen = false;
        ApplyRightPanelState();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _theme = _theme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
        ApplyState();
    }

    private async void SaveMachinesDeletionPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (MachinesDeletionPolicyComboBox.SelectedItem is not ComboBoxItem selectedItem ||
            selectedItem.Tag is not string modeRaw ||
            !Enum.TryParse<MachineDeletionPolicyMode>(modeRaw, ignoreCase: true, out var mode))
        {
            MachinesDeletionPolicyStatusTextBlock.Text = "Select a deletion policy mode first.";
            return;
        }

        _isSavingDeletionPolicy = true;
        SaveMachinesDeletionPolicyButton.IsEnabled = false;
        try
        {
            await _machinesCapabilityService.SetDeletionPolicyAsync(mode);
            MachinesDeletionPolicyStatusTextBlock.Text = $"Saved: {selectedItem.Content}";
        }
        catch (Exception ex)
        {
            MachinesDeletionPolicyStatusTextBlock.Text = $"Failed to save policy. {ex.Message}";
        }
        finally
        {
            _isSavingDeletionPolicy = false;
            SaveMachinesDeletionPolicyButton.IsEnabled = true;
        }
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && GlobalNavigationView.IsPaneOpen)
        {
            GlobalNavigationView.IsPaneOpen = false;
            e.Handled = true;
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (GlobalNavigationView.IsPaneOpen)
        {
            GlobalNavigationView.IsPaneOpen = false;
            args.Handled = true;
        }
    }

    private bool IsMachinesOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.MachinesOverview, StringComparison.Ordinal);

    private bool IsDeployOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployOverview, StringComparison.Ordinal);

    private bool IsDeployFromTemplateActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal);

    private bool IsDeployOnTheFlyActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployOnTheFly, StringComparison.Ordinal);

    private bool IsDeployCapabilityActive =>
        IsDeployOverviewActive || IsDeployFromTemplateActive || IsDeployOnTheFlyActive;

    private void EnsureDeployOnTheFlySeeded()
    {
        if (_deployOnTheFlyVmEntries.Count > 0)
        {
            SyncDeployOnTheFlyVmEntryRows();
            if (_selectedDeployOnTheFlyVmEntry is null)
            {
                _selectedDeployOnTheFlyVmEntry = _deployOnTheFlyVmEntries[0];
                SelectDeployOnTheFlyVmEntry(_selectedDeployOnTheFlyVmEntry);
            }

            return;
        }

        var entry = CreateDefaultDeployOnTheFlyVmEntry(1);
        _deployOnTheFlyVmEntries.Add(entry);
        SyncDeployOnTheFlyVmEntryRows();
        _selectedDeployOnTheFlyVmEntry = entry;
        SelectDeployOnTheFlyVmEntry(entry);
    }

    private static VmTemplate CreateDefaultDeployOnTheFlyVmEntry(int sequence)
    {
        return new VmTemplate
        {
            Name = $"Quick VM {sequence}",
            MemoryMb = 2048,
            CpuCount = 2
        };
    }

    private static VmTemplate CloneVmTemplate(VmTemplate source)
    {
        return new VmTemplate
        {
            VmId = source.VmId,
            Name = source.Name,
            MemoryMb = source.MemoryMb,
            CpuCount = source.CpuCount,
            VhdxId = source.VhdxId,
            VhdPath = source.VhdPath,
            VhdxSignature = source.VhdxSignature,
            SwitchName = source.SwitchName,
            SwitchNames = source.SwitchNames?.ToList()
        };
    }

    private LabTemplate BuildOnTheFlyTemplate()
    {
        return new LabTemplate
        {
            Name = "Quick Deploy Draft",
            Description = "Generated quick deploy input.",
            VmTemplates = _deployOnTheFlyVmEntries.Select(CloneVmTemplate).ToList()
        };
    }

    private void ReplaceDeployOnTheFlyEntriesFromTemplate(LabTemplate template)
    {
        var previousSelectionId = _selectedDeployOnTheFlyVmEntry?.VmId;
        _deployOnTheFlyVmEntries.Clear();
        foreach (var vmTemplate in template.VmTemplates)
        {
            _deployOnTheFlyVmEntries.Add(CloneVmTemplate(vmTemplate));
        }

        SyncDeployOnTheFlyVmEntryRows();

        _selectedDeployOnTheFlyVmEntry = !string.IsNullOrWhiteSpace(previousSelectionId)
            ? _deployOnTheFlyVmEntries.FirstOrDefault(item => string.Equals(item.VmId, previousSelectionId, StringComparison.OrdinalIgnoreCase))
            : null;

        _selectedDeployOnTheFlyVmEntry ??= _deployOnTheFlyVmEntries.FirstOrDefault();
        SelectDeployOnTheFlyVmEntry(_selectedDeployOnTheFlyVmEntry);
        UpdateDeployOnTheFlyEditorPanel();
    }

    private void SyncDeployOnTheFlyVmEntryRows()
    {
        var existingByVm = _deployOnTheFlyVmEntryRows.ToDictionary(row => row.VmEntry);
        var staleRows = _deployOnTheFlyVmEntryRows.Where(row => !_deployOnTheFlyVmEntries.Contains(row.VmEntry)).ToList();
        foreach (var staleRow in staleRows)
        {
            _deployOnTheFlyVmEntryRows.Remove(staleRow);
        }

        for (var index = 0; index < _deployOnTheFlyVmEntries.Count; index++)
        {
            var vmEntry = _deployOnTheFlyVmEntries[index];
            if (!existingByVm.TryGetValue(vmEntry, out var row))
            {
                row = new DeployOnTheFlyVmEntryRow(vmEntry);
                _deployOnTheFlyVmEntryRows.Insert(index, row);
                existingByVm[vmEntry] = row;
            }
            else
            {
                var currentIndex = _deployOnTheFlyVmEntryRows.IndexOf(row);
                if (currentIndex != index)
                {
                    _deployOnTheFlyVmEntryRows.Move(currentIndex, index);
                }
            }
        }
    }

    private void SelectDeployOnTheFlyVmEntry(VmTemplate? vmEntry)
    {
        if (vmEntry is null)
        {
            DeployOnTheFlyVmEntriesListView.SelectedItem = null;
            return;
        }

        var selectedRow = _deployOnTheFlyVmEntryRows.FirstOrDefault(row => ReferenceEquals(row.VmEntry, vmEntry));
        if (selectedRow is not null)
        {
            DeployOnTheFlyVmEntriesListView.SelectedItem = selectedRow;
        }
    }

    private void UpdateDeployOnTheFlyEditorPanel()
    {
        _isUpdatingDeployOnTheFlyEditor = true;
        try
        {
            if (_selectedDeployOnTheFlyVmEntry is null)
            {
                DeployOnTheFlyVmNameTextBox.Text = string.Empty;
                DeployOnTheFlyVmMemoryTextBox.Text = string.Empty;
                DeployOnTheFlyVmCpuTextBox.Text = string.Empty;
                UpdateDeployOnTheFlySelectorsFromVm();
                return;
            }

            DeployOnTheFlyVmNameTextBox.Text = _selectedDeployOnTheFlyVmEntry.Name;
            DeployOnTheFlyVmMemoryTextBox.Text = _selectedDeployOnTheFlyVmEntry.MemoryMb.ToString();
            DeployOnTheFlyVmCpuTextBox.Text = _selectedDeployOnTheFlyVmEntry.CpuCount.ToString();
            UpdateDeployOnTheFlySelectorsFromVm();
        }
        finally
        {
            _isUpdatingDeployOnTheFlyEditor = false;
        }
    }

    private void UpdateDeployOnTheFlySelectorsFromVm()
    {
        var switchItems = new List<object> { DeployOnTheFlySwitchPlaceholder };
        switchItems.AddRange(_templateAvailableSwitches);
        DeployOnTheFlyVmSwitchComboBox.ItemsSource = switchItems;

        var vhdItems = new List<object> { DeployOnTheFlyVhdxPlaceholder };
        vhdItems.AddRange(_templateVhdxCatalogOptions);
        DeployOnTheFlyVmVhdxCatalogComboBox.ItemsSource = vhdItems;

        if (_selectedDeployOnTheFlyVmEntry is null)
        {
            DeployOnTheFlyVmSwitchComboBox.SelectedItem = DeployOnTheFlySwitchPlaceholder;
            DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem = DeployOnTheFlyVhdxPlaceholder;
            DeployOnTheFlyVmSwitchGuidanceTextBlock.Text = "Select a VM entry first.";
            DeployOnTheFlyVmVhdxGuidanceTextBlock.Text = "Select a VM entry first.";
            return;
        }

        var selectedSwitch = _selectedDeployOnTheFlyVmEntry.SwitchNames?.FirstOrDefault()
                             ?? _selectedDeployOnTheFlyVmEntry.SwitchName;
        if (!string.IsNullOrWhiteSpace(selectedSwitch) &&
            _templateAvailableSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
        {
            DeployOnTheFlyVmSwitchComboBox.SelectedItem = _templateAvailableSwitches.First(name =>
                string.Equals(name, selectedSwitch, StringComparison.OrdinalIgnoreCase));
            DeployOnTheFlyVmSwitchGuidanceTextBlock.Text = "Switch selected from host inventory.";
        }
        else
        {
            DeployOnTheFlyVmSwitchComboBox.SelectedItem = DeployOnTheFlySwitchPlaceholder;
            DeployOnTheFlyVmSwitchGuidanceTextBlock.Text = _templateAvailableSwitches.Count == 0
                ? "No host switches available. Add a switch in Assets first."
                : "Switch selection is optional.";
        }

        var vhdSelection = string.IsNullOrWhiteSpace(_selectedDeployOnTheFlyVmEntry.VhdxId)
            ? null
            : _templateVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, _selectedDeployOnTheFlyVmEntry.VhdxId, StringComparison.OrdinalIgnoreCase));
        if (vhdSelection is null && !string.IsNullOrWhiteSpace(_selectedDeployOnTheFlyVmEntry.VhdPath))
        {
            vhdSelection = _templateVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, _selectedDeployOnTheFlyVmEntry.VhdPath, StringComparison.OrdinalIgnoreCase));
        }

        if (vhdSelection is not null)
        {
            DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem = vhdSelection;
            DeployOnTheFlyVmVhdxGuidanceTextBlock.Text = $"Selected: {vhdSelection.DisplayLabel} ({vhdSelection.Id}).";
        }
        else
        {
            DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem = DeployOnTheFlyVhdxPlaceholder;
            DeployOnTheFlyVmVhdxGuidanceTextBlock.Text = _templateVhdxCatalogOptions.Count == 0
                ? "No VHDX catalog entries available. Import base disks in Assets first."
                : "Select a base disk from catalog.";
        }
    }

    private bool TryApplyDeployOnTheFlyVmFields(bool showSuccessStatus, bool showValidationErrors = true)
    {
        if (_selectedDeployOnTheFlyVmEntry is null || _isUpdatingDeployOnTheFlyEditor)
        {
            return false;
        }

        var vmName = DeployOnTheFlyVmNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vmName))
        {
            if (showValidationErrors)
            {
                DeployOnTheFlyStatusTextBlock.Text = "VM name is required.";
            }
            return false;
        }

        if (!int.TryParse(DeployOnTheFlyVmMemoryTextBox.Text, out var memoryMb) || memoryMb <= 0)
        {
            if (showValidationErrors)
            {
                DeployOnTheFlyStatusTextBlock.Text = "Memory must be a positive integer.";
            }
            return false;
        }

        if (!int.TryParse(DeployOnTheFlyVmCpuTextBox.Text, out var cpuCount) || cpuCount <= 0)
        {
            if (showValidationErrors)
            {
                DeployOnTheFlyStatusTextBlock.Text = "CPU count must be a positive integer.";
            }
            return false;
        }

        var previousName = _selectedDeployOnTheFlyVmEntry.Name;
        _selectedDeployOnTheFlyVmEntry.Name = vmName;
        _selectedDeployOnTheFlyVmEntry.MemoryMb = memoryMb;
        _selectedDeployOnTheFlyVmEntry.CpuCount = cpuCount;

        if (DeployOnTheFlyVmSwitchComboBox.SelectedItem is string selectedSwitch &&
            !string.Equals(selectedSwitch, DeployOnTheFlySwitchPlaceholder, StringComparison.Ordinal))
        {
            _selectedDeployOnTheFlyVmEntry.SwitchNames = [selectedSwitch];
            _selectedDeployOnTheFlyVmEntry.SwitchName = selectedSwitch;
        }
        else
        {
            _selectedDeployOnTheFlyVmEntry.SwitchNames = null;
            _selectedDeployOnTheFlyVmEntry.SwitchName = null;
        }

        if (DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem is TemplateVhdxCatalogOption selectedCatalogOption)
        {
            _selectedDeployOnTheFlyVmEntry.VhdxId = selectedCatalogOption.Id;
            _selectedDeployOnTheFlyVmEntry.VhdPath = selectedCatalogOption.Path;
            _selectedDeployOnTheFlyVmEntry.VhdxSignature = selectedCatalogOption.Signature;
        }
        else
        {
            _selectedDeployOnTheFlyVmEntry.VhdxId = null;
            _selectedDeployOnTheFlyVmEntry.VhdPath = null;
            _selectedDeployOnTheFlyVmEntry.VhdxSignature = null;
        }

        if (showSuccessStatus)
        {
            DeployOnTheFlyStatusTextBlock.Text = $"Updated '{vmName}'.";
        }

        if (!string.Equals(previousName, vmName, StringComparison.Ordinal))
        {
            RefreshDeployOnTheFlyVmEntriesList();
        }

        return true;
    }

    private void RefreshDeployOnTheFlyVmEntriesList()
    {
        SyncDeployOnTheFlyVmEntryRows();
        SelectDeployOnTheFlyVmEntry(_selectedDeployOnTheFlyVmEntry);
    }

    private async Task EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode mode)
    {
        if (!_isDeployOnTheFlyStarting)
        {
            _showDeployOnTheFlyAllVmRows = false;
        }
        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false) && _selectedDeployOnTheFlyVmEntry is not null)
        {
            return;
        }

        if (_deployOnTheFlyVmEntries.Count == 0)
        {
            DeployOnTheFlyStatusTextBlock.Text = "Add at least one VM entry first.";
            return;
        }

        _isDeployOnTheFlyEvaluatingReadiness = true;
        _deployOnTheFlyLifecycleState = "Evaluating";
        _deployOnTheFlyProgressPercent = mode == DeploymentPreflightMode.Full ? 18 : 12;
        _deployOnTheFlyProgressSummary = mode == DeploymentPreflightMode.Full
            ? "Running full readiness checks..."
            : "Running quick readiness checks...";
        DeployOnTheFlyStatusTextBlock.Text = mode == DeploymentPreflightMode.Full
            ? "Running full quick deploy readiness evaluation..."
            : "Running quick deploy readiness evaluation...";

        try
        {
            await EnsureTemplateSwitchesAsync(forceRefresh: false);
            var template = BuildOnTheFlyTemplate();
            var deployContext = BuildDeployContext(template);
            _deployOnTheFlyCompatibilityIssues.Clear();
            _deployOnTheFlyCompatibilityIssues.AddRange(deployContext.CompatibilityIssues);
            _deployOnTheFlyReadinessReport = await _deploymentPreflightService.RunAsync(deployContext.MultiVmContext, mode);

            var blockingCount = _deployOnTheFlyCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                _deployOnTheFlyReadinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = _deployOnTheFlyCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               _deployOnTheFlyReadinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn);

            _deployOnTheFlyLifecycleState = blockingCount > 0 ? "Blocked" : warningCount > 0 ? "Warning" : "Ready";
            _deployOnTheFlyProgressPercent = blockingCount > 0 ? 35 : warningCount > 0 ? 45 : 55;
            _deployOnTheFlyProgressSummary = blockingCount > 0
                ? "Readiness blocked."
                : warningCount > 0
                    ? "Readiness passed with warnings."
                    : "Readiness passed.";
            _deployOnTheFlyReadinessSummary = blockingCount > 0
                ? $"Readiness blocked ({blockingCount} fail, {warningCount} warn)."
                : warningCount > 0
                    ? $"Readiness passed with warnings ({warningCount})."
                    : "Readiness passed.";
            DeployOnTheFlyStatusTextBlock.Text = blockingCount > 0
                ? "Deploy blocked by readiness failures. Resolve blocking items first."
                : warningCount > 0
                    ? $"Readiness passed with {warningCount} warning(s)."
                    : "Readiness passed with no issues.";
        }
        catch (Exception ex)
        {
            _deployOnTheFlyReadinessReport = null;
            _deployOnTheFlyCompatibilityIssues.Clear();
            _deployOnTheFlyLifecycleState = "Error";
            _deployOnTheFlyProgressPercent = 0;
            _deployOnTheFlyProgressSummary = "Readiness evaluation failed.";
            _deployOnTheFlyReadinessSummary = "Readiness evaluation failed.";
            DeployOnTheFlyStatusTextBlock.Text = $"Readiness evaluation failed. {ex.Message}";
        }
        finally
        {
            _isDeployOnTheFlyEvaluatingReadiness = false;
            UpdateDeployOnTheFlyUi();
        }
    }

    private void ScheduleDeployOnTheFlyAutoEvaluate()
    {
        if (_isUpdatingDeployOnTheFlyEditor || _isDeployOnTheFlyStarting)
        {
            return;
        }

        var nonce = Interlocked.Increment(ref _deployOnTheFlyAutoEvaluateNonce);
        _ = DebouncedDeployOnTheFlyAutoEvaluateAsync(nonce);
    }

    private async Task DebouncedDeployOnTheFlyAutoEvaluateAsync(int nonce)
    {
        await Task.Delay(350);
        if (nonce != _deployOnTheFlyAutoEvaluateNonce || _isDeployOnTheFlyStarting || _isDeployOnTheFlyEvaluatingReadiness)
        {
            return;
        }

        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false, showValidationErrors: false))
        {
            return;
        }

        _deployOnTheFlyReadinessReport = null;
        _deployOnTheFlyCompatibilityIssues.Clear();
        _deployOnTheFlyLifecycleState = "Idle";
        _deployOnTheFlyProgressPercent = 0;
        _deployOnTheFlyProgressSummary = "No deployment started.";
        _deployOnTheFlyReadinessSummary = "Readiness has not been evaluated.";
        UpdateDeployOnTheFlyUi();

        await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private void UpdateDeployOnTheFlyVmEntryRows()
    {
        SyncDeployOnTheFlyVmEntryRows();

        var compatibilityByVm = _deployOnTheFlyCompatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (_deployOnTheFlyReadinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in _deployOnTheFlyVmEntryRows)
        {
            row.DisplayName = string.IsNullOrWhiteSpace(row.VmEntry.Name) ? "Unnamed VM" : row.VmEntry.Name.Trim();
            row.SecondaryText = BuildDeployOnTheFlyVmEntrySecondaryText(row.VmEntry);

            compatibilityByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var compatibilityIssues);
            readinessByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var readinessIssues);
            compatibilityIssues ??= [];
            readinessIssues ??= [];

            var draftIssues = ReferenceEquals(row.VmEntry, _selectedDeployOnTheFlyVmEntry)
                ? GetDeployOnTheFlyDraftIssues()
                : GetDeployOnTheFlyVmEntryIssues(row.VmEntry);

            var blockingMessages = new List<string>();
            var warningMessages = new List<string>();

            blockingMessages.AddRange(draftIssues.Where(issue => issue.IsBlocking).Select(issue => issue.Message));
            warningMessages.AddRange(draftIssues.Where(issue => !issue.IsBlocking).Select(issue => issue.Message));

            blockingMessages.AddRange(compatibilityIssues.Where(issue => issue.IsBlocking).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.Guidance)));
            warningMessages.AddRange(compatibilityIssues.Where(issue => !issue.IsBlocking).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.Guidance)));

            blockingMessages.AddRange(readinessIssues.Where(issue => issue.Status == DeploymentReadinessStatus.Fail).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.ActionableGuidance)));
            warningMessages.AddRange(readinessIssues.Where(issue => issue.Status == DeploymentReadinessStatus.Warn).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.ActionableGuidance)));

            if (blockingMessages.Count > 0)
            {
                row.IssueBadgeText = "Blocked";
                row.IssueSummary = blockingMessages[0];
                row.IssueBrush = Application.Current.Resources["ShellCriticalBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Visible;
                row.IssueSummaryVisibility = Visibility.Visible;
            }
            else if (warningMessages.Count > 0)
            {
                row.IssueBadgeText = "Warning";
                row.IssueSummary = warningMessages[0];
                row.IssueBrush = Application.Current.Resources["ShellWarnBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Visible;
                row.IssueSummaryVisibility = Visibility.Visible;
            }
            else
            {
                row.IssueBadgeText = string.Empty;
                row.IssueSummary = string.Empty;
                row.IssueBrush = Application.Current.Resources["ShellTextSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Collapsed;
                row.IssueSummaryVisibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateDeployOnTheFlyEditorIssueSummary()
    {
        if (_selectedDeployOnTheFlyVmEntry is null)
        {
            DeployOnTheFlyEditorIssueSummaryTextBlock.Text = "Select a VM entry to review its properties and resolve any issues inline.";
            return;
        }

        var draftIssues = GetDeployOnTheFlyDraftIssues();
        if (draftIssues.Count > 0)
        {
            var blockingCount = draftIssues.Count(issue => issue.IsBlocking);
            var warningCount = draftIssues.Count - blockingCount;
            DeployOnTheFlyEditorIssueSummaryTextBlock.Text = blockingCount > 0
                ? $"Blocking issues in this VM: {string.Join(" ", draftIssues.Where(issue => issue.IsBlocking).Select(issue => issue.Message))}"
                : $"Warnings in this VM: {string.Join(" ", draftIssues.Select(issue => issue.Message))}";
            return;
        }

        var selectedRow = _deployOnTheFlyVmEntryRows.FirstOrDefault(row => ReferenceEquals(row.VmEntry, _selectedDeployOnTheFlyVmEntry));
        if (selectedRow is not null && selectedRow.IssueSummaryVisibility == Visibility.Visible)
        {
            DeployOnTheFlyEditorIssueSummaryTextBlock.Text = $"{selectedRow.IssueBadgeText}: {selectedRow.IssueSummary}";
            return;
        }

        DeployOnTheFlyEditorIssueSummaryTextBlock.Text = "Ready. Changes validate while you edit. Row signals show which VM needs attention.";
    }

    private List<(bool IsBlocking, string Message)> GetDeployOnTheFlyDraftIssues()
    {
        if (_selectedDeployOnTheFlyVmEntry is null)
        {
            return [];
        }

        return GetDeployOnTheFlyDraftIssues(
            DeployOnTheFlyVmNameTextBox.Text,
            DeployOnTheFlyVmMemoryTextBox.Text,
            DeployOnTheFlyVmCpuTextBox.Text,
            DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem,
            _templateVhdxCatalogOptions.Count);
    }

    private static List<(bool IsBlocking, string Message)> GetDeployOnTheFlyVmEntryIssues(VmTemplate vmEntry)
    {
        var memoryText = vmEntry.MemoryMb.ToString(CultureInfo.InvariantCulture);
        var cpuText = vmEntry.CpuCount.ToString(CultureInfo.InvariantCulture);
        var selectedCatalog = string.IsNullOrWhiteSpace(vmEntry.VhdxId) && string.IsNullOrWhiteSpace(vmEntry.VhdPath)
            ? null
            : new object();

        return GetDeployOnTheFlyDraftIssues(vmEntry.Name, memoryText, cpuText, selectedCatalog, availableCatalogCount: 1);
    }

    private static List<(bool IsBlocking, string Message)> GetDeployOnTheFlyDraftIssues(
        string? vmName,
        string? memoryText,
        string? cpuText,
        object? selectedCatalogItem,
        int availableCatalogCount)
    {
        var issues = new List<(bool IsBlocking, string Message)>();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            issues.Add((true, "VM name is required."));
        }

        if (!int.TryParse(memoryText, out var memoryMb) || memoryMb <= 0)
        {
            issues.Add((true, "Memory must be a positive integer."));
        }

        if (!int.TryParse(cpuText, out var cpuCount) || cpuCount <= 0)
        {
            issues.Add((true, "CPU count must be a positive integer."));
        }

        if (selectedCatalogItem is null)
        {
            issues.Add((true, availableCatalogCount == 0
                ? "Import a base disk in Assets before deploy."
                : "Select a base disk in VM Properties."));
        }

        return issues;
    }

    private static string BuildDeployOnTheFlyVmEntrySecondaryText(VmTemplate vmEntry)
    {
        var diskText = string.IsNullOrWhiteSpace(vmEntry.VhdxId) && string.IsNullOrWhiteSpace(vmEntry.VhdPath)
            ? "No base disk"
            : string.IsNullOrWhiteSpace(vmEntry.VhdxId)
                ? "Catalog disk selected"
                : $"Disk: {vmEntry.VhdxId}";
        var switchText = vmEntry.SwitchNames?.FirstOrDefault()
                         ?? vmEntry.SwitchName
                         ?? "No switch";
        return $"{vmEntry.MemoryMb} MB | {vmEntry.CpuCount} vCPU | {diskText} | Switch: {switchText}";
    }

    private static string FormatDeployOnTheFlyIssueMessage(string message, string? guidance)
    {
        return string.IsNullOrWhiteSpace(guidance) ? message.Trim() : $"{message} {guidance}".Trim();
    }

    private void UpdateDeployOnTheFlyUi()
    {
        var hasEntries = _deployOnTheFlyVmEntries.Count > 0;
        var hasBlockingFailures = _deployOnTheFlyCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                                  (_deployOnTheFlyReadinessReport?.HasBlockingFailures ?? false);

        DeployOnTheFlyAddVmButton.IsEnabled = !_isDeployOnTheFlyEvaluatingReadiness && !_isDeployOnTheFlyStarting;
        DeployOnTheFlyRemoveVmButton.IsEnabled = _selectedDeployOnTheFlyVmEntry is not null &&
                                                 !_isDeployOnTheFlyEvaluatingReadiness &&
                                                 !_isDeployOnTheFlyStarting;
        DeployOnTheFlyApplyVmChangesButton.IsEnabled = _selectedDeployOnTheFlyVmEntry is not null &&
                                                       !_isDeployOnTheFlyEvaluatingReadiness &&
                                                       !_isDeployOnTheFlyStarting;
        DeployOnTheFlyEvaluateButton.IsEnabled = false;
        DeployOnTheFlyResolveSuggestionsButton.IsEnabled = hasEntries && !_isDeployOnTheFlyEvaluatingReadiness && !_isDeployOnTheFlyStarting;
        DeployOnTheFlyOpenTemplateEditorButton.IsEnabled = hasEntries && !_isDeployOnTheFlyStarting;
        DeployOnTheFlyStartButton.IsEnabled = hasEntries && !hasBlockingFailures && !_isDeployOnTheFlyEvaluatingReadiness && !_isDeployOnTheFlyStarting;

        if (!hasEntries)
        {
            _deployOnTheFlyLifecycleState = "Idle";
            _deployOnTheFlyReadinessSummary = "Add at least one VM entry to evaluate readiness.";
            _deployOnTheFlyProgressPercent = 0;
            _deployOnTheFlyProgressSummary = "No deployment started.";
        }

        UpdateDeployOnTheFlyResultRows();
        UpdateDeployOnTheFlyIssueRows();
        UpdateDeployOnTheFlyVmEntryRows();
        UpdateDeployOnTheFlyEditorIssueSummary();

        DeployOnTheFlyOverallStateTextBlock.Text = _deployOnTheFlyLifecycleState;
        DeployOnTheFlyProgressBar.Value = _deployOnTheFlyProgressPercent;
        DeployOnTheFlyProgressSummaryTextBlock.Text = _deployOnTheFlyProgressSummary;
        var blockingIssueCount = _deployOnTheFlyIssueRows.Count(issue => string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        var warningIssueCount = _deployOnTheFlyIssueRows.Count(issue => !string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        DeployOnTheFlyGlobalIssuesBadgeTextBlock.Text = $"Blocking: {blockingIssueCount} | Warnings: {warningIssueCount}";
        var shouldShowInlineGuidance = hasEntries &&
                                       !_isDeployOnTheFlyStarting &&
                                       _deployOnTheFlyProgressByVm.Count == 0;
        DeployOnTheFlyReadinessSummaryTextBlock.Text = shouldShowInlineGuidance
            ? $"{_deployOnTheFlyReadinessSummary} Review VM row badges and the selected VM details to fix blockers here before deploy."
            : _deployOnTheFlyReadinessSummary;
        ApplyRightPanelState();
    }

    private async Task EnsureAssetsBaseDisksAsync(bool forceRefresh)
    {
        if (_isAssetsBaseDisksLoading)
        {
            return;
        }

        if (!forceRefresh && (_assetsBaseDiskRows.Count > 0 || _pendingAssetsBaseDiskDraft is not null))
        {
            UpdateAssetsBaseDisksUi();
            return;
        }

        _isAssetsBaseDisksLoading = true;
        AssetsBaseDisksStatusTextBlock.Text = forceRefresh
            ? "Refreshing base disks..."
            : "Loading base disks...";
        UpdateAssetsBaseDisksUi();

        try
        {
            var result = await _assetsBaseDisksCapabilityService.LoadAsync(isRefresh: forceRefresh);
            var previouslySelectedId = _selectedAssetsBaseDiskRow?.Id;
            _assetsBaseDiskRows.Clear();
            foreach (var item in result.Items)
            {
                _assetsBaseDiskRows.Add(new AssetsBaseDiskListRow(item));
            }

            if (_pendingAssetsBaseDiskDraft is null)
            {
                _selectedAssetsBaseDiskRow = _assetsBaseDiskRows.FirstOrDefault(row => string.Equals(row.Id, previouslySelectedId, StringComparison.OrdinalIgnoreCase))
                    ?? _assetsBaseDiskRows.FirstOrDefault();
                AssetsBaseDisksListView.SelectedItem = _selectedAssetsBaseDiskRow;
            }
            else
            {
                _selectedAssetsBaseDiskRow = null;
                AssetsBaseDisksListView.SelectedItem = null;
            }

            AssetsBaseDisksStatusTextBlock.Text = result.Errors.Count > 0
                ? $"Loaded {_assetsBaseDiskRows.Count} base disk(s) with {result.Errors.Count} issue(s). Review the error panel and use Refresh after correcting the catalog."
                : $"Loaded {_assetsBaseDiskRows.Count} base disk(s).";
            _hasAssetsBaseDisksErrorState = result.Errors.Count > 0;
            AssetsBaseDisksErrorStateTextBlock.Text = result.Errors.Count > 0
                ? $"Catalog load completed with issues:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors)}"
                : "No catalog load errors.";

            if (_selectedAssetsBaseDiskRow is not null)
            {
                LoadAssetsBaseDisksEditorFromRow(_selectedAssetsBaseDiskRow);
            }
            else if (_pendingAssetsBaseDiskDraft is not null)
            {
                LoadAssetsBaseDisksEditorFromDraft(_pendingAssetsBaseDiskDraft);
                AssetsBaseDisksSelectedDiskSummaryTextBlock.Text = "New base disk draft. Review metadata, validate, then save to register it.";
                AssetsBaseDisksSelectedDiskValidationTextBlock.Text = "Validation has not been evaluated for this draft yet.";
                AssetsBaseDisksReferenceWarningTextBlock.Text = "Removal assessment is only available for registered base disks.";
            }
            else
            {
                ClearAssetsBaseDisksEditor();
            }
        }
        finally
        {
            _isAssetsBaseDisksLoading = false;
            UpdateAssetsBaseDisksUi();
        }
    }

    private void UpdateAssetsBaseDisksUi()
    {
        AssetsBaseDisksRefreshButton.IsEnabled = !_isAssetsBaseDisksLoading && !_isAssetsBaseDisksSaving && !_isAssetsBaseDisksRemoving;
        AssetsBaseDisksImportButton.IsEnabled = !_isAssetsBaseDisksLoading && !_isAssetsBaseDisksSaving && !_isAssetsBaseDisksRemoving;
        AssetsBaseDisksValidateButton.IsEnabled = !_isAssetsBaseDisksLoading && _selectedAssetsBaseDiskRow is not null;
        AssetsBaseDisksRemoveButton.IsEnabled = !_isAssetsBaseDisksLoading && !_isAssetsBaseDisksRemoving && _selectedAssetsBaseDiskRow is not null;
        AssetsBaseDisksBrowsePathButton.IsEnabled = !_isAssetsBaseDisksLoading && !_isAssetsBaseDisksSaving && !_isAssetsBaseDisksRemoving;
        AssetsBaseDisksSaveMetadataButton.IsEnabled = !_isAssetsBaseDisksLoading && !_isAssetsBaseDisksSaving && !_isAssetsBaseDisksRemoving && CanSaveAssetsBaseDiskDraft();
        AssetsBaseDisksLoadingStatePanel.Visibility = _isAssetsBaseDisksLoading ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksEmptyStatePanel.Visibility = !_isAssetsBaseDisksLoading && _assetsBaseDiskRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksErrorStatePanel.Visibility = _hasAssetsBaseDisksErrorState ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksLoadingStateTextBlock.Text = _isAssetsBaseDisksLoading
            ? "Loading base disk catalog. Current details remain visible until refresh completes."
            : "Base disk catalog is idle.";
        AssetsBaseDisksEmptyStateTextBlock.Text = "No base disks are registered. Use Import / Register to choose a VHDX and then save its metadata.";
        var showDetailsMessages = _selectedAssetsBaseDiskRow is not null || _pendingAssetsBaseDiskDraft is not null;
        AssetsBaseDisksSelectedDiskValidationTextBlock.Visibility = showDetailsMessages ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksReferenceWarningTextBlock.Visibility = showDetailsMessages ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AssetsBaseDisksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedAssetsBaseDiskRow = AssetsBaseDisksListView.SelectedItem as AssetsBaseDiskListRow;
        if (_selectedAssetsBaseDiskRow is not null)
        {
            LoadAssetsBaseDisksEditorFromRow(_selectedAssetsBaseDiskRow);
        }
        else
        {
            ClearAssetsBaseDisksEditor();
        }

        UpdateAssetsBaseDisksUi();
    }

    private async void AssetsBaseDisksRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureAssetsBaseDisksAsync(forceRefresh: true);
    }

    private void AssetsBaseDisksImportButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = PickBaseDiskFilePath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        _selectedAssetsBaseDiskRow = null;
        _pendingAssetsBaseDiskDraft = null;
        AssetsBaseDisksListView.SelectedItem = null;
        LoadAssetsBaseDisksEditorFromDraft(new AssetsBaseDiskDraft
        {
            IsNew = true,
            Path = selectedPath,
            OsName = Path.GetFileNameWithoutExtension(selectedPath),
            OsVersion = string.Empty,
            Generation = 1,
            Notes = null
        });
        _pendingAssetsBaseDiskDraft = TryBuildAssetsBaseDiskDraft(isNewOverride: true);
        AssetsBaseDisksStatusTextBlock.Text = "Selected VHDX path. Review metadata, validate, and click Save Metadata to register the base disk.";
        AssetsBaseDisksSelectedDiskValidationTextBlock.Text = "Validation has not been evaluated for this new draft yet.";
        AssetsBaseDisksReferenceWarningTextBlock.Text = "New base disk draft. Removal assessment is not applicable.";
        UpdateAssetsBaseDisksUi();
    }

    private async void AssetsBaseDisksValidateButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = TryBuildAssetsBaseDiskDraft(isNewOverride: _selectedAssetsBaseDiskRow is null);
        if (draft is null)
        {
            AssetsBaseDisksStatusTextBlock.Text = "Select or prepare a base disk draft before validating.";
            return;
        }

        var validation = await _assetsBaseDisksCapabilityService.ValidateAsync(draft);
        ApplyAssetsBaseDiskValidationResult(validation);
        AssetsBaseDisksStatusTextBlock.Text = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? "Validation passed. The base disk is ready to use."
            : $"Validation blocked. Review the details and correct the metadata or path before saving.";
    }

    private async void AssetsBaseDisksRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAssetsBaseDiskRow is null)
        {
            AssetsBaseDisksStatusTextBlock.Text = "Select a base disk to remove.";
            return;
        }

        _isAssetsBaseDisksRemoving = true;
        UpdateAssetsBaseDisksUi();

        try
        {
            var assessment = await _assetsBaseDisksCapabilityService.AssessRemoveAsync(_selectedAssetsBaseDiskRow.Id);
            _selectedAssetsBaseDiskRow.ReferenceSummary = assessment.ReferenceSignalSummary;
            AssetsBaseDisksReferenceWarningTextBlock.Text = string.Join(Environment.NewLine, assessment.WarningReasons.DefaultIfEmpty(assessment.ReferenceSignalSummary));
            if (!assessment.Exists || !assessment.CanRemove)
            {
                AssetsBaseDisksStatusTextBlock.Text = assessment.BlockingReasons.FirstOrDefault() ?? "Base disk removal is blocked. Resolve the issue and try again.";
                _hasAssetsBaseDisksErrorState = true;
                AssetsBaseDisksErrorStateTextBlock.Text = string.Join(Environment.NewLine, assessment.BlockingReasons.DefaultIfEmpty("Base disk removal is blocked."));
                return;
            }

            if (!await ShowAssetsBaseDiskRemoveConfirmationDialogAsync(_selectedAssetsBaseDiskRow, assessment))
            {
                AssetsBaseDisksStatusTextBlock.Text = "Base disk removal canceled.";
                return;
            }

            var result = await _assetsBaseDisksCapabilityService.RemoveAsync(_selectedAssetsBaseDiskRow.Id);
            AssetsBaseDisksStatusTextBlock.Text = result.UserMessage;
            if (!result.Success)
            {
                _hasAssetsBaseDisksErrorState = true;
                AssetsBaseDisksErrorStateTextBlock.Text = $"Remove failed. {string.Join(Environment.NewLine, result.Errors.DefaultIfEmpty(result.UserMessage))}";
            }
            else
            {
                _hasAssetsBaseDisksErrorState = false;
                AssetsBaseDisksErrorStateTextBlock.Text = "No catalog load errors.";
                _pendingAssetsBaseDiskDraft = null;
            }

            await EnsureAssetsBaseDisksAsync(forceRefresh: true);
        }
        finally
        {
            _isAssetsBaseDisksRemoving = false;
            UpdateAssetsBaseDisksUi();
        }
    }

    private void AssetsBaseDisksBrowsePathButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = PickBaseDiskFilePath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        AssetsBaseDisksPathTextBox.Text = selectedPath;
        if (_selectedAssetsBaseDiskRow is null)
        {
            _pendingAssetsBaseDiskDraft = TryBuildAssetsBaseDiskDraft(isNewOverride: true);
        }

        AssetsBaseDisksStatusTextBlock.Text = "Updated base disk path. Validate and save metadata to persist the change.";
        UpdateAssetsBaseDisksUi();
    }

    private async void AssetsBaseDisksSaveMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = TryBuildAssetsBaseDiskDraft(isNewOverride: _selectedAssetsBaseDiskRow is null);
        if (draft is null)
        {
            AssetsBaseDisksStatusTextBlock.Text = "Complete required metadata before saving.";
            return;
        }

        _isAssetsBaseDisksSaving = true;
        AssetsBaseDisksStatusTextBlock.Text = draft.IsNew
            ? "Registering base disk..."
            : "Saving base disk metadata...";
        UpdateAssetsBaseDisksUi();

        try
        {
            var result = await _assetsBaseDisksCapabilityService.SaveAsync(draft);
            AssetsBaseDisksStatusTextBlock.Text = result.UserMessage;
            if (!result.Success)
            {
                _hasAssetsBaseDisksErrorState = true;
                AssetsBaseDisksErrorStateTextBlock.Text = $"Save failed. {string.Join(Environment.NewLine, result.Errors.DefaultIfEmpty(result.UserMessage))}";
                UpdateAssetsBaseDisksUi();
                return;
            }

            _hasAssetsBaseDisksErrorState = false;
            AssetsBaseDisksErrorStateTextBlock.Text = "No catalog load errors.";
            _pendingAssetsBaseDiskDraft = null;
            await EnsureAssetsBaseDisksAsync(forceRefresh: true);
            if (result.Item is not null)
            {
                _selectedAssetsBaseDiskRow = _assetsBaseDiskRows.FirstOrDefault(row => string.Equals(row.Id, result.Item.Id, StringComparison.OrdinalIgnoreCase));
                AssetsBaseDisksListView.SelectedItem = _selectedAssetsBaseDiskRow;
            }

            var validationDraft = new AssetsBaseDiskDraft
            {
                Id = result.Item?.Id ?? draft.Id,
                Path = draft.Path,
                OsName = draft.OsName,
                OsVersion = draft.OsVersion,
                Generation = draft.Generation,
                Notes = draft.Notes,
                IsNew = false
            };
            var validation = await _assetsBaseDisksCapabilityService.ValidateAsync(validationDraft);
            ApplyAssetsBaseDiskValidationResult(validation);
        }
        finally
        {
            _isAssetsBaseDisksSaving = false;
            UpdateAssetsBaseDisksUi();
        }
    }

    private void AssetsBaseDisksMetadataTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingAssetsBaseDisksEditor)
        {
            return;
        }

        AssetsBaseDisksStatusTextBlock.Text = _selectedAssetsBaseDiskRow is null
            ? "Base disk draft changed. Validate and Save Metadata to register it."
            : "Base disk metadata changed. Validate and Save Metadata to persist changes.";
        if (_selectedAssetsBaseDiskRow is null)
        {
            _pendingAssetsBaseDiskDraft = TryBuildAssetsBaseDiskDraft(isNewOverride: true);
        }

        UpdateAssetsBaseDisksUi();
    }

    private void LoadAssetsBaseDisksEditorFromRow(AssetsBaseDiskListRow row)
    {
        LoadAssetsBaseDisksEditorFromDraft(new AssetsBaseDiskDraft
        {
            Id = row.Id,
            Path = row.Path,
            OsName = row.OsName,
            OsVersion = row.OsVersion,
            Generation = row.Generation,
            Notes = row.Notes,
            IsNew = false
        });
        _pendingAssetsBaseDiskDraft = null;
        AssetsBaseDisksSelectedDiskSummaryTextBlock.Text = $"Catalog id: {row.Id}{Environment.NewLine}{row.Path}";
        AssetsBaseDisksSelectedDiskValidationTextBlock.Text = row.ValidationSummary;
        AssetsBaseDisksReferenceWarningTextBlock.Text = row.ReferenceSummary;
    }

    private void LoadAssetsBaseDisksEditorFromDraft(AssetsBaseDiskDraft draft)
    {
        _isUpdatingAssetsBaseDisksEditor = true;
        try
        {
            AssetsBaseDisksOsNameTextBox.Text = draft.OsName;
            AssetsBaseDisksOsVersionTextBox.Text = draft.OsVersion;
            AssetsBaseDisksPathTextBox.Text = draft.Path;
            AssetsBaseDisksGenerationTextBox.Text = draft.Generation > 0 ? draft.Generation.ToString(CultureInfo.InvariantCulture) : string.Empty;
            AssetsBaseDisksNotesTextBox.Text = draft.Notes ?? string.Empty;
        }
        finally
        {
            _isUpdatingAssetsBaseDisksEditor = false;
        }
    }

    private void ClearAssetsBaseDisksEditor()
    {
        _selectedAssetsBaseDiskRow = null;
        _pendingAssetsBaseDiskDraft = null;
        _isUpdatingAssetsBaseDisksEditor = true;
        try
        {
            AssetsBaseDisksOsNameTextBox.Text = string.Empty;
            AssetsBaseDisksOsVersionTextBox.Text = string.Empty;
            AssetsBaseDisksPathTextBox.Text = string.Empty;
            AssetsBaseDisksGenerationTextBox.Text = string.Empty;
            AssetsBaseDisksNotesTextBox.Text = string.Empty;
        }
        finally
        {
            _isUpdatingAssetsBaseDisksEditor = false;
        }

        AssetsBaseDisksSelectedDiskSummaryTextBlock.Text = "Select a base disk or import a VHDX to begin.";
        AssetsBaseDisksSelectedDiskValidationTextBlock.Text = "Validation has not been evaluated.";
        AssetsBaseDisksReferenceWarningTextBlock.Text = "No removal assessment has been performed.";
    }

    private AssetsBaseDiskDraft? TryBuildAssetsBaseDiskDraft(bool isNewOverride)
    {
        if (!int.TryParse(AssetsBaseDisksGenerationTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var generation) || generation <= 0)
        {
            return null;
        }

        var path = AssetsBaseDisksPathTextBox.Text.Trim();
        var osName = AssetsBaseDisksOsNameTextBox.Text.Trim();
        var osVersion = AssetsBaseDisksOsVersionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(osName) || string.IsNullOrWhiteSpace(osVersion))
        {
            return null;
        }

        return new AssetsBaseDiskDraft
        {
            Id = isNewOverride ? null : _selectedAssetsBaseDiskRow?.Id,
            Path = path,
            OsName = osName,
            OsVersion = osVersion,
            Generation = generation,
            Notes = AssetsBaseDisksNotesTextBox.Text,
            IsNew = isNewOverride
        };
    }

    private bool CanSaveAssetsBaseDiskDraft()
    {
        return TryBuildAssetsBaseDiskDraft(isNewOverride: _selectedAssetsBaseDiskRow is null) is not null;
    }

    private void ApplyAssetsBaseDiskValidationResult(AssetsBaseDiskValidationResult validation)
    {
        var validationText = FormatAssetsBaseDiskValidationText(validation);
        AssetsBaseDisksSelectedDiskValidationTextBlock.Text = validationText;
        if (_selectedAssetsBaseDiskRow is not null)
        {
            _selectedAssetsBaseDiskRow.ValidationSummary = validationText;
        }
    }

    private static string FormatAssetsBaseDiskValidationText(AssetsBaseDiskValidationResult validation)
    {
        var label = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? "Ready"
            : string.Equals(validation.Severity, "Warn", StringComparison.OrdinalIgnoreCase)
                ? "Warning"
                : "Blocking";
        if (validation.Details.Count == 0)
        {
            return $"{label}: {validation.Summary}";
        }

        return $"{label}: {validation.Summary}{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", validation.Details)}";
    }

    private string? PickBaseDiskFilePath()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var selectedPath = NativeFileDialogs.ShowOpenVhdxDialog(hwnd);
        return string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
    }

    private async Task<bool> ShowAssetsBaseDiskRemoveConfirmationDialogAsync(
        AssetsBaseDiskListRow row,
        AssetsBaseDiskRemovalAssessment assessment)
    {
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to remove '{row.DisplayName}' from the registry."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "This removes the base disk from the LabAssistant registry only. It does not delete the underlying VHDX file.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = assessment.ReferenceSignalSummary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var warning in assessment.WarningReasons)
        {
            content.Children.Add(new TextBlock
            {
                Text = "- " + warning,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellWarnBrush"]
            });
        }

        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Remove Base Disk",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task EnsureAssetsSwitchesAsync(bool forceRefresh)
    {
        if (_isAssetsSwitchesLoading)
        {
            return;
        }

        if (!forceRefresh && (_assetsSwitchRows.Count > 0 || _pendingAssetsSwitchDraft is not null))
        {
            UpdateAssetsSwitchesUi();
            return;
        }

        _isAssetsSwitchesLoading = true;
        AssetsSwitchesStatusTextBlock.Text = forceRefresh ? "Refreshing virtual switches..." : "Loading virtual switches...";
        UpdateAssetsSwitchesUi();

        try
        {
            var previousSelectionName = _selectedAssetsSwitchRow?.Name;
            var result = await _assetsSwitchesCapabilityService.LoadAsync(isRefresh: forceRefresh);

            _assetsSwitchRows.Clear();
            foreach (var item in result.Items)
            {
                _assetsSwitchRows.Add(new AssetsSwitchListRow(item));
            }

            if (result.Errors.Count > 0)
            {
                _hasAssetsSwitchesErrorState = true;
                AssetsSwitchesErrorStateTextBox.Text = $"Switch inventory load completed with issues:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors)}";
                AssetsSwitchesStatusTextBlock.Text = $"Loaded {_assetsSwitchRows.Count} switch(es) with {result.Errors.Count} issue(s). Review the error panel and refresh after correcting the host state.";
            }
            else
            {
                ClearAssetsSwitchesErrorState();
            }

            if (_pendingAssetsSwitchDraft is null)
            {
                var matchedSelection = !string.IsNullOrWhiteSpace(previousSelectionName)
                    ? _assetsSwitchRows.FirstOrDefault(row => string.Equals(row.Name, previousSelectionName, StringComparison.OrdinalIgnoreCase))
                    : null;
                _selectedAssetsSwitchRow = matchedSelection ?? _assetsSwitchRows.FirstOrDefault();

                AssetsSwitchesListView.SelectedItem = _selectedAssetsSwitchRow;
                if (_selectedAssetsSwitchRow is not null)
                {
                    LoadAssetsSwitchesEditorFromRow(_selectedAssetsSwitchRow);
                    AssetsSwitchesStatusTextBlock.Text = forceRefresh
                        ? "Virtual switch inventory refreshed."
                        : $"Loaded {_assetsSwitchRows.Count} switch(es).";
                    if (!string.IsNullOrWhiteSpace(previousSelectionName) && matchedSelection is null)
                    {
                        AssetsSwitchesStatusTextBlock.Text = $"Previously selected switch '{previousSelectionName}' is no longer available. Review the refreshed inventory.";
                    }
                }
                else
                {
                    ClearAssetsSwitchesEditor();
                    AssetsSwitchesStatusTextBlock.Text = _assetsSwitchRows.Count == 0
                        ? "No virtual switches were found on this host. Click Create to prepare a new switch."
                        : "Select a virtual switch or click New to prepare a new switch draft.";
                }
            }
            else
            {
                AssetsSwitchesListView.SelectedItem = null;
                LoadAssetsSwitchesEditorFromDraft(_pendingAssetsSwitchDraft);
                AssetsSwitchesSelectedSwitchValidationTextBlock.Text = "Enter a switch name, choose a type, and provide an adapter for External switches.";
                AssetsSwitchesDeleteConstraintTextBlock.Text = string.Empty;
                SetAssetsSwitchAttachedVmState(Array.Empty<string>(), "Attached VMs are shown for existing switches.");
                AssetsSwitchesStatusTextBlock.Text = forceRefresh
                    ? "Virtual switch inventory refreshed. The current new-switch draft was preserved."
                    : "Virtual switch inventory loaded. The current new-switch draft was preserved.";
            }
        }
        finally
        {
            _isAssetsSwitchesLoading = false;
            UpdateAssetsSwitchesUi();
        }
    }

    private void UpdateAssetsSwitchesUi()
    {
        var canValidateOrApply = TryBuildAssetsSwitchDraft() is not null;

        AssetsSwitchesRefreshButton.IsEnabled = !_isAssetsSwitchesLoading && !_isAssetsSwitchesSaving && !_isAssetsSwitchesDeleting;
        AssetsSwitchesCreateButton.IsEnabled = !_isAssetsSwitchesLoading && !_isAssetsSwitchesSaving && !_isAssetsSwitchesDeleting;
        AssetsSwitchesApplyButton.IsEnabled = !_isAssetsSwitchesLoading && !_isAssetsSwitchesSaving && !_isAssetsSwitchesDeleting && canValidateOrApply;
        AssetsSwitchesDeleteButton.IsEnabled = !_isAssetsSwitchesLoading && !_isAssetsSwitchesSaving && !_isAssetsSwitchesDeleting && _selectedAssetsSwitchRow is not null;

        var isEditingExisting = _selectedAssetsSwitchRow is not null && _pendingAssetsSwitchDraft is null;
        AssetsSwitchesTypeComboBox.IsEnabled = !isEditingExisting && !_isAssetsSwitchesSaving && !_isAssetsSwitchesDeleting;
        AssetsSwitchesAdapterTextBox.IsEnabled =
            !isEditingExisting &&
            !_isAssetsSwitchesSaving &&
            !_isAssetsSwitchesDeleting &&
            string.Equals(GetSelectedAssetsSwitchType(), "External", StringComparison.OrdinalIgnoreCase);

        AssetsSwitchesLoadingStatePanel.Visibility = _isAssetsSwitchesLoading ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesEmptyStatePanel.Visibility = !_isAssetsSwitchesLoading && _assetsSwitchRows.Count == 0 && !_hasAssetsSwitchesErrorState ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesErrorStatePanel.Visibility = _hasAssetsSwitchesErrorState ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesLoadingStateTextBlock.Text = _isAssetsSwitchesLoading
            ? "Loading current Hyper-V virtual switches. Current details remain visible until refresh completes."
            : "Virtual switch inventory is idle.";
        AssetsSwitchesEmptyStateTextBlock.Text = "No virtual switches were found on this host. Click Create to prepare a new switch.";
    }

    private void AssetsSwitchesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AssetsSwitchesListView.SelectedItem is AssetsSwitchListRow row)
        {
            _selectedAssetsSwitchRow = row;
            _pendingAssetsSwitchDraft = null;
            ClearAssetsSwitchesErrorState();
            LoadAssetsSwitchesEditorFromRow(row);
            _ = LoadAssetsSwitchAttachedVmNamesAsync(row.Name);
            _ = RefreshAssetsSwitchValidationAsync();
        }
        else
        {
            _selectedAssetsSwitchRow = null;
            if (_pendingAssetsSwitchDraft is null)
            {
                ClearAssetsSwitchesEditor();
            }
        }

        UpdateAssetsSwitchesUi();
    }

    private async void AssetsSwitchesRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureAssetsSwitchesAsync(forceRefresh: true);
    }

    private void AssetsSwitchesCreateButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedAssetsSwitchRow = null;
        AssetsSwitchesListView.SelectedItem = null;
        ClearAssetsSwitchesErrorState();
        _pendingAssetsSwitchDraft = new AssetsSwitchDraft
        {
            IsNew = true,
            SwitchType = "External"
        };
        LoadAssetsSwitchesEditorFromDraft(_pendingAssetsSwitchDraft);
        AssetsSwitchesStatusTextBlock.Text = "Preparing a new virtual switch draft. Complete the fields and apply when the inline validation state is ready.";
        AssetsSwitchesSelectedSwitchValidationTextBlock.Text = "Enter a switch name, choose a type, and provide an adapter for External switches.";
        AssetsSwitchesDeleteConstraintTextBlock.Text = string.Empty;
        SetAssetsSwitchAttachedVmState(Array.Empty<string>(), "Attached VMs are shown for existing switches.");
        _ = RefreshAssetsSwitchValidationAsync();
        UpdateAssetsSwitchesUi();
    }

    private async void AssetsSwitchesApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = TryBuildAssetsSwitchDraft();
        if (draft is null)
        {
            AssetsSwitchesStatusTextBlock.Text = "Complete required switch fields before applying.";
            return;
        }

        _isAssetsSwitchesSaving = true;
        AssetsSwitchesStatusTextBlock.Text = draft.IsNew ? "Creating virtual switch..." : "Updating virtual switch...";
        UpdateAssetsSwitchesUi();

        try
        {
            var result = await _assetsSwitchesCapabilityService.SaveAsync(draft);
            AssetsSwitchesStatusTextBlock.Text = result.UserMessage;
            if (!result.Success)
            {
                _hasAssetsSwitchesErrorState = true;
                AssetsSwitchesErrorStateTextBox.Text = $"Switch save failed. Review the message below, correct the configuration, and try Apply again.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors.DefaultIfEmpty(result.UserMessage))}";
                UpdateAssetsSwitchesUi();
                return;
            }

            ClearAssetsSwitchesErrorState();
            _pendingAssetsSwitchDraft = null;
            await EnsureAssetsSwitchesAsync(forceRefresh: true);
            if (result.Item is not null)
            {
                _selectedAssetsSwitchRow = _assetsSwitchRows.FirstOrDefault(row => string.Equals(row.Name, result.Item.Name, StringComparison.OrdinalIgnoreCase));
                AssetsSwitchesListView.SelectedItem = _selectedAssetsSwitchRow;
                if (_selectedAssetsSwitchRow is not null)
                {
                    LoadAssetsSwitchesEditorFromRow(_selectedAssetsSwitchRow);
                }
            }
        }
        finally
        {
            _isAssetsSwitchesSaving = false;
            UpdateAssetsSwitchesUi();
        }
    }

    private async void AssetsSwitchesDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAssetsSwitchRow is null)
        {
            AssetsSwitchesStatusTextBlock.Text = "Select a virtual switch to delete.";
            return;
        }

        _isAssetsSwitchesDeleting = true;
        UpdateAssetsSwitchesUi();

        try
        {
            var assessment = await _assetsSwitchesCapabilityService.AssessDeleteAsync(_selectedAssetsSwitchRow.Name);
            ApplyAssetsSwitchDeleteAssessment(assessment);

            if (!assessment.CanDelete)
            {
                AssetsSwitchesStatusTextBlock.Text = "Delete blocked. Disconnect the attached VMs from this switch and refresh before trying again.";
                _hasAssetsSwitchesErrorState = true;
                AssetsSwitchesErrorStateTextBox.Text = $"Delete is blocked for '{_selectedAssetsSwitchRow.Name}'.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", assessment.BlockingReasons.DefaultIfEmpty("At least one VM is attached to this switch."))}";
                UpdateAssetsSwitchesUi();
                return;
            }

            if (!await ShowAssetsSwitchDeleteConfirmationDialogAsync(_selectedAssetsSwitchRow, assessment))
            {
                AssetsSwitchesStatusTextBlock.Text = "Virtual switch delete canceled.";
                return;
            }

            var result = await _assetsSwitchesCapabilityService.DeleteAsync(_selectedAssetsSwitchRow.Name);
            AssetsSwitchesStatusTextBlock.Text = result.UserMessage;
            if (!result.Success)
            {
                _hasAssetsSwitchesErrorState = true;
                AssetsSwitchesErrorStateTextBox.Text = $"Delete failed. Review the details below before trying again.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors.DefaultIfEmpty(result.UserMessage))}";
            }
            else
            {
                ClearAssetsSwitchesErrorState();
                _selectedAssetsSwitchRow = null;
                _pendingAssetsSwitchDraft = null;
            }

            await EnsureAssetsSwitchesAsync(forceRefresh: true);
        }
        finally
        {
            _isAssetsSwitchesDeleting = false;
            UpdateAssetsSwitchesUi();
        }
    }

    private void AssetsSwitchesEditorControl_Changed(object sender, object e)
    {
        if (_isUpdatingAssetsSwitchesEditor)
        {
            return;
        }

        var draft = TryBuildAssetsSwitchDraft();
        if (_selectedAssetsSwitchRow is null)
        {
            _pendingAssetsSwitchDraft = CaptureAssetsSwitchDraftFromEditor(isNewOverride: true);
        }

        _ = RefreshAssetsSwitchValidationAsync();
        UpdateAssetsSwitchesUi();
    }

    private void LoadAssetsSwitchesEditorFromRow(AssetsSwitchListRow row)
    {
        LoadAssetsSwitchesEditorFromDraft(new AssetsSwitchDraft
        {
            IsNew = false,
            OriginalName = row.Name,
            Name = row.Name,
            SwitchType = row.SwitchType,
            AdapterName = row.AdapterName
        });
        AssetsSwitchesSelectedSwitchValidationTextBlock.Text = row.ValidationSummary;
        AssetsSwitchesDeleteConstraintTextBlock.Text = string.Empty;
        SetAssetsSwitchAttachedVmState(Array.Empty<string>(), "Loading attached VMs...");
    }

    private void LoadAssetsSwitchesEditorFromDraft(AssetsSwitchDraft draft)
    {
        _isUpdatingAssetsSwitchesEditor = true;
        try
        {
            AssetsSwitchesNameTextBox.Text = draft.Name;
            SetAssetsSwitchTypeSelection(draft.SwitchType);
            AssetsSwitchesAdapterTextBox.Text = draft.AdapterName ?? string.Empty;
        }
        finally
        {
            _isUpdatingAssetsSwitchesEditor = false;
        }
    }

    private void ClearAssetsSwitchesEditor()
    {
        _isUpdatingAssetsSwitchesEditor = true;
        try
        {
            AssetsSwitchesNameTextBox.Text = string.Empty;
            SetAssetsSwitchTypeSelection(string.Empty);
            AssetsSwitchesAdapterTextBox.Text = string.Empty;
            SetAssetsSwitchAttachedVmState(Array.Empty<string>(), "No attached VMs.");
        }
        finally
        {
            _isUpdatingAssetsSwitchesEditor = false;
        }

        AssetsSwitchesSelectedSwitchValidationTextBlock.Text = "Select a switch or click New to begin.";
        AssetsSwitchesDeleteConstraintTextBlock.Text = string.Empty;
    }

    private AssetsSwitchDraft? TryBuildAssetsSwitchDraft()
    {
        var draft = CaptureAssetsSwitchDraftFromEditor(isNewOverride: _selectedAssetsSwitchRow is null);
        var name = draft.Name;
        var type = draft.SwitchType;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        return draft;
    }

    private AssetsSwitchDraft CaptureAssetsSwitchDraftFromEditor(bool isNewOverride)
    {
        return new AssetsSwitchDraft
        {
            IsNew = isNewOverride,
            OriginalName = isNewOverride ? null : _selectedAssetsSwitchRow?.Name,
            Name = AssetsSwitchesNameTextBox.Text.Trim(),
            SwitchType = GetSelectedAssetsSwitchType(),
            AdapterName = AssetsSwitchesAdapterTextBox.Text
        };
    }

    private void SetAssetsSwitchTypeSelection(string switchType)
    {
        var match = AssetsSwitchesTypeComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), switchType, StringComparison.OrdinalIgnoreCase));
        AssetsSwitchesTypeComboBox.SelectedItem = match;
    }

    private string GetSelectedAssetsSwitchType()
    {
        return AssetsSwitchesTypeComboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static string FormatAssetsSwitchValidationText(AssetsSwitchValidationResult validation)
    {
        var heading = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? $"Ready: {validation.Summary}"
            : string.Equals(validation.Severity, "Warn", StringComparison.OrdinalIgnoreCase)
                ? $"Warning: {validation.Summary}"
                : $"Blocking: {validation.Summary}";
        if (validation.Details.Count == 0 || string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase))
        {
            return heading;
        }

        return string.Join(Environment.NewLine, new[] { heading }.Concat(validation.Details.Select(detail => "- " + detail)));
    }

    private void ApplyAssetsSwitchValidationResult(AssetsSwitchValidationResult validation)
    {
        var validationText = FormatAssetsSwitchValidationText(validation);
        AssetsSwitchesSelectedSwitchValidationTextBlock.Text = validationText;
        if (_selectedAssetsSwitchRow is not null)
        {
            _selectedAssetsSwitchRow.ValidationSummary = validationText;
        }
    }

    private void ApplyAssetsSwitchDeleteAssessment(AssetsSwitchDeleteAssessment assessment)
    {
        SetAssetsSwitchAttachedVmState(
            assessment.AttachedVmNames,
            assessment.AttachedVmNames.Count > 0
                ? "Attached VMs currently using this switch."
                : "No attached VMs.");
        AssetsSwitchesDeleteConstraintTextBlock.Text = assessment.CanDelete
            ? string.Empty
            : $"Delete blocked. {assessment.Summary}";
        if (_selectedAssetsSwitchRow is not null)
        {
            _selectedAssetsSwitchRow.DeleteSummary = AssetsSwitchesDeleteConstraintTextBlock.Text;
        }
    }

    private async Task RefreshAssetsSwitchValidationAsync()
    {
        var requestVersion = ++_assetsSwitchesValidationRequestVersion;
        var draft = TryBuildAssetsSwitchDraft();
        if (draft is null)
        {
            if (requestVersion != _assetsSwitchesValidationRequestVersion)
            {
                return;
            }

            AssetsSwitchesSelectedSwitchValidationTextBlock.Text = _selectedAssetsSwitchRow is null
                ? "Complete the switch name, choose a type, and provide an adapter for External switches."
                : "Edit the switch name to validate changes. Type and adapter changes require creating a new switch.";

            return;
        }

        var validation = await _assetsSwitchesCapabilityService.ValidateAsync(draft);
        if (requestVersion != _assetsSwitchesValidationRequestVersion)
        {
            return;
        }

        ApplyAssetsSwitchValidationResult(validation);
    }

    private async Task LoadAssetsSwitchAttachedVmNamesAsync(string switchName)
    {
        var requestVersion = ++_assetsSwitchesAssessmentRequestVersion;
        var vmNames = await _assetsSwitchesCapabilityService.GetAttachedVmNamesAsync(switchName);
        if (requestVersion != _assetsSwitchesAssessmentRequestVersion)
        {
            return;
        }

        if (_selectedAssetsSwitchRow is null || !string.Equals(_selectedAssetsSwitchRow.Name, switchName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetAssetsSwitchAttachedVmState(
            vmNames,
            vmNames.Count > 0
                ? "Attached VMs currently using this switch."
                : "No attached VMs.");
    }

    private void SetAssetsSwitchAttachedVmState(IEnumerable<string> vmNames, string hintText)
    {
        _assetsSwitchAttachedVmNames.Clear();
        foreach (var vmName in vmNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _assetsSwitchAttachedVmNames.Add(vmName);
        }

        AssetsSwitchesAttachedVmsHintTextBlock.Text = hintText;
    }

    private void ClearAssetsSwitchesErrorState()
    {
        _hasAssetsSwitchesErrorState = false;
        AssetsSwitchesErrorStateTextBox.Text = "No switch load or action errors.";
    }

    private async Task<bool> ShowAssetsSwitchDeleteConfirmationDialogAsync(
        AssetsSwitchListRow row,
        AssetsSwitchDeleteAssessment assessment)
    {
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete virtual switch '{row.Name}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Delete is allowed only when no Hyper-V VM is attached to the switch. VM power state does not make delete safe.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = assessment.Summary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete Virtual Switch",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private bool IsTemplatesLibraryActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesLibrary, StringComparison.Ordinal);

    private bool IsTemplatesEditorActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesEditor, StringComparison.Ordinal);

    private bool IsAssetsOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.AssetsOverview, StringComparison.Ordinal);

    private bool IsAssetsBaseDisksActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.AssetsBaseDisks, StringComparison.Ordinal);

    private bool IsAssetsSwitchesActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.AssetsSwitches, StringComparison.Ordinal);

    private bool IsAssetsCapabilityActive =>
        IsAssetsOverviewActive || IsAssetsBaseDisksActive || IsAssetsSwitchesActive;

    private bool IsTemplatesCapabilityActive =>
        IsTemplatesLibraryActive || IsTemplatesEditorActive;

    private bool IsSettingsMachinesActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);

    private bool IsDiagnosticsOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsOverview, StringComparison.Ordinal);

    private bool IsDiagnosticsLogsActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsLogs, StringComparison.Ordinal);

    private bool IsDiagnosticsCapabilityActive =>
        IsDiagnosticsOverviewActive || IsDiagnosticsLogsActive;

    private async void DeployReloadTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureDeployTemplatesLoadedAsync(forceRefresh: true);
    }

    private async void DeployEvaluateReadinessButton_Click(object sender, RoutedEventArgs e)
    {
        await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async void DeployResolveSuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDeployTemplateDocument is null)
        {
            DeployActionStatusTextBlock.Text = "Select a template first.";
            return;
        }

        var applied = await ApplyDeployResolveSuggestionsAsync(_activeDeployTemplateDocument.Template);
        DeployActionStatusTextBlock.Text = applied == 0
            ? "No auto-resolve suggestions available for the current template state."
            : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...";
        await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async void DeployOpenTemplateEditorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDeployTemplateLibraryItem is null)
        {
            DeployActionStatusTextBlock.Text = "Select a template first.";
            return;
        }

        await OpenTemplateInEditorAsync(_selectedDeployTemplateLibraryItem, fromDeploy: true);
    }

    private async void DeployTemplateSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isDeployLoadingTemplates || _isTemplatesLoading)
        {
            return;
        }

        _selectedDeployTemplateLibraryItem = DeployTemplateSelectorComboBox.SelectedItem as TemplateLibraryItem;
        if (_selectedDeployTemplateLibraryItem is null)
        {
            _activeDeployTemplateDocument = null;
            _deployReadinessReport = null;
            _deployCompatibilityIssues.Clear();
            _deployLifecycleState = "Idle";
            _deployProgressPercent = 0;
            _deployProgressSummary = "No template selected.";
            UpdateDeployUi();
            return;
        }

        try
        {
            _activeDeployTemplateDocument = await _templatesCapabilityService.LoadForEditorAsync(_selectedDeployTemplateLibraryItem.FilePath);
            _deployLifecycleState = "Ready";
            _deployProgressPercent = 0;
            _deployProgressSummary = $"Template '{_selectedDeployTemplateLibraryItem.Name}' loaded.";
            DeployActionStatusTextBlock.Text = $"Loaded '{_selectedDeployTemplateLibraryItem.Name}' for deploy readiness.";
            await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);
        }
        catch (Exception ex)
        {
            _activeDeployTemplateDocument = null;
            _deployReadinessReport = null;
            _deployCompatibilityIssues.Clear();
            _deployLifecycleState = "Error";
            _deployProgressPercent = 0;
            _deployProgressSummary = "Template load failed.";
            DeployActionStatusTextBlock.Text = $"Failed to load selected template. {ex.Message}";
            UpdateDeployUi();
        }
    }

    private void DeployOnTheFlyVmEntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedDeployOnTheFlyVmEntry = (DeployOnTheFlyVmEntriesListView.SelectedItem as DeployOnTheFlyVmEntryRow)?.VmEntry;
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private async void DeployOnTheFlyView_VmRemoveRequested(VmTemplate vmEntry)
    {
        await RemoveDeployOnTheFlyVmEntryAsync(vmEntry);
    }

    private void DeployOnTheFlyAddVmButton_Click(object sender, RoutedEventArgs e)
    {
        _showDeployOnTheFlyAllVmRows = false;
        var nextSequence = _deployOnTheFlyVmEntries.Count + 1;
        var entry = CreateDefaultDeployOnTheFlyVmEntry(nextSequence);
        _deployOnTheFlyVmEntries.Add(entry);
        SyncDeployOnTheFlyVmEntryRows();
        _selectedDeployOnTheFlyVmEntry = entry;
        SelectDeployOnTheFlyVmEntry(entry);
        _deployOnTheFlyReadinessReport = null;
        _deployOnTheFlyCompatibilityIssues.Clear();
        _deployOnTheFlyLifecycleState = "Idle";
        _deployOnTheFlyProgressPercent = 0;
        _deployOnTheFlyProgressSummary = "No deployment started.";
        _deployOnTheFlyReadinessSummary = "Readiness has not been evaluated.";
        DeployOnTheFlyStatusTextBlock.Text = $"Added VM entry '{entry.Name}'.";
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private async void DeployOnTheFlyRemoveVmButton_Click(object sender, RoutedEventArgs e)
    {
        await RemoveDeployOnTheFlyVmEntryAsync(_selectedDeployOnTheFlyVmEntry);
    }

    private async Task RemoveDeployOnTheFlyVmEntryAsync(VmTemplate? vmEntry)
    {
        _showDeployOnTheFlyAllVmRows = false;
        if (vmEntry is null)
        {
            DeployOnTheFlyStatusTextBlock.Text = "Select a VM entry first.";
            return;
        }

        var vmName = vmEntry.Name;
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove '{vmName}' from quick deploy configuration?",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _deployOnTheFlyVmEntries.Remove(vmEntry);
        SyncDeployOnTheFlyVmEntryRows();
        _selectedDeployOnTheFlyVmEntry = _deployOnTheFlyVmEntries.FirstOrDefault();
        SelectDeployOnTheFlyVmEntry(_selectedDeployOnTheFlyVmEntry);
        _deployOnTheFlyReadinessReport = null;
        _deployOnTheFlyCompatibilityIssues.Clear();
        _deployOnTheFlyLifecycleState = "Idle";
        _deployOnTheFlyProgressPercent = 0;
        _deployOnTheFlyProgressSummary = "No deployment started.";
        _deployOnTheFlyReadinessSummary = _deployOnTheFlyVmEntries.Count == 0
            ? "Add at least one VM entry to evaluate readiness."
            : "Readiness has not been evaluated.";
        DeployOnTheFlyStatusTextBlock.Text = $"Removed VM entry '{vmName}'.";
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private void DeployOnTheFlyApplyVmChangesButton_Click(object sender, RoutedEventArgs e)
    {
        _showDeployOnTheFlyAllVmRows = false;
        if (_selectedDeployOnTheFlyVmEntry is null)
        {
            DeployOnTheFlyStatusTextBlock.Text = "Select a VM entry first.";
            return;
        }

        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: true))
        {
            return;
        }

        _deployOnTheFlyReadinessReport = null;
        _deployOnTheFlyCompatibilityIssues.Clear();
        _deployOnTheFlyLifecycleState = "Idle";
        _deployOnTheFlyProgressPercent = 0;
        _deployOnTheFlyProgressSummary = "No deployment started.";
        _deployOnTheFlyReadinessSummary = "Readiness has not been evaluated.";
        UpdateDeployOnTheFlyUi();
        _ = EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private void DeployOnTheFlyVmNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingDeployOnTheFlyEditor)
        {
            return;
        }

        UpdateDeployOnTheFlyUi();
        ScheduleDeployOnTheFlyAutoEvaluate();
    }

    private void DeployOnTheFlyVmMemoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingDeployOnTheFlyEditor)
        {
            return;
        }

        UpdateDeployOnTheFlyUi();
        ScheduleDeployOnTheFlyAutoEvaluate();
    }

    private void DeployOnTheFlyVmCpuTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingDeployOnTheFlyEditor)
        {
            return;
        }

        UpdateDeployOnTheFlyUi();
        ScheduleDeployOnTheFlyAutoEvaluate();
    }

    private void DeployOnTheFlyVmSwitchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDeployOnTheFlyEditor)
        {
            return;
        }

        DeployOnTheFlyVmSwitchGuidanceTextBlock.Text = DeployOnTheFlyVmSwitchComboBox.SelectedItem is string selected &&
                                                        !string.Equals(selected, DeployOnTheFlySwitchPlaceholder, StringComparison.Ordinal)
            ? $"Selected switch: {selected}"
            : _templateAvailableSwitches.Count == 0
                ? "No host switches available. Add a switch in Assets first."
                : "Switch selection is optional.";

        UpdateDeployOnTheFlyUi();
        ScheduleDeployOnTheFlyAutoEvaluate();
    }

    private void DeployOnTheFlyVmVhdxCatalogComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDeployOnTheFlyEditor)
        {
            return;
        }

        DeployOnTheFlyVmVhdxGuidanceTextBlock.Text = DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem is TemplateVhdxCatalogOption selectedOption
            ? $"Selected: {selectedOption.DisplayLabel} ({selectedOption.Id})."
            : _templateVhdxCatalogOptions.Count == 0
                ? "No VHDX catalog entries available. Import base disks in Assets first."
                : "Select a base disk from catalog.";

        UpdateDeployOnTheFlyUi();
        ScheduleDeployOnTheFlyAutoEvaluate();
    }

    private async void DeployOnTheFlyEvaluateButton_Click(object sender, RoutedEventArgs e)
    {
        await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private async void DeployOnTheFlyResolveSuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_deployOnTheFlyVmEntries.Count == 0)
        {
            DeployOnTheFlyStatusTextBlock.Text = "Add at least one VM entry first.";
            return;
        }

        var template = BuildOnTheFlyTemplate();
        var applied = await ApplyDeployResolveSuggestionsAsync(template);
        if (applied > 0)
        {
            ReplaceDeployOnTheFlyEntriesFromTemplate(template);
        }

        DeployOnTheFlyStatusTextBlock.Text = applied == 0
            ? "No auto-resolve suggestions available for the current quick deploy configuration."
            : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...";
        await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private void DeployOnTheFlyOpenTemplateEditorButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false) && _selectedDeployOnTheFlyVmEntry is not null)
        {
            return;
        }

        if (_deployOnTheFlyVmEntries.Count == 0)
        {
            DeployOnTheFlyStatusTextBlock.Text = "Add at least one VM entry first.";
            return;
        }

        _activeTemplateEditorDocument = new TemplateEditorDocument
        {
            Template = BuildOnTheFlyTemplate(),
            SourceFilePath = null
        };
        BindTemplateEditorDocument();
        TemplateEditorStatusTextBlock.Text = "Opened quick deploy configuration in Templates editor.";
        NavigateToRoute(ShellRouteKeys.TemplatesEditor);
        DeployOnTheFlyStatusTextBlock.Text = "Opened quick deploy configuration in Templates editor.";
    }

    private async void DeployOnTheFlyStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false) && _selectedDeployOnTheFlyVmEntry is not null)
        {
            return;
        }

        if (_deployOnTheFlyVmEntries.Count == 0)
        {
            DeployOnTheFlyStatusTextBlock.Text = "Add at least one VM entry first.";
            return;
        }

        _isDeployOnTheFlyStarting = true;
        _showDeployOnTheFlyAllVmRows = true;
        _deployOnTheFlyProgressByVm.Clear();
        _deployOnTheFlyLifecycleState = "Running";
        _deployOnTheFlyProgressPercent = 15;
        _deployOnTheFlyProgressSummary = "Preparing deployment...";
        _deployOnTheFlyReadinessSummary = "Preparing deployment...";
        UpdateDeployOnTheFlyUi();
        try
        {
            await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
            var hasBlockingFailures = _deployOnTheFlyCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                                      (_deployOnTheFlyReadinessReport?.HasBlockingFailures ?? false);
            if (hasBlockingFailures)
            {
                _deployOnTheFlyLifecycleState = "Blocked";
                _deployOnTheFlyProgressPercent = 35;
                _deployOnTheFlyProgressSummary = "Deployment blocked by readiness failures.";
                _deployOnTheFlyReadinessSummary = "Deployment blocked by readiness failures.";
                DeployOnTheFlyStatusTextBlock.Text = "Deploy blocked by readiness failures. Resolve blocking items first.";
                return;
            }

            var template = BuildOnTheFlyTemplate();
            var deployContext = BuildDeployContext(template);
            InitializeDeployOnTheFlyProgressRows(deployContext.MultiVmContext);
            AttachDeployOnTheFlyProgressCallbacks(deployContext.MultiVmContext);
            _deployOnTheFlyProgressPercent = 40;
            _deployOnTheFlyProgressSummary = $"Deploying {deployContext.MultiVmContext.VmContexts.Count} VM(s)...";
            DeployOnTheFlyStatusTextBlock.Text = "Starting quick deploy...";
            UpdateDeployOnTheFlyResultRows();
            UpdateDeployOnTheFlyUi();
            await _deploymentCoordinator.DeployAllAsync(deployContext.MultiVmContext);
            var summary = _deploymentOutcomeSummaryBuilder.Build(deployContext.MultiVmContext);
            UpdateDeployOnTheFlyRowsFromSummary(summary);

            _deployOnTheFlyLifecycleState = summary.OperationState switch
            {
                DeploymentOperationState.Completed => "Completed",
                DeploymentOperationState.Cancelled or DeploymentOperationState.CancelledWithResiduals => "Cancelled",
                DeploymentOperationState.Failed or DeploymentOperationState.FailedWithResiduals => "Failed",
                _ => "Completed"
            };
            _deployOnTheFlyProgressPercent = 100;
            _deployOnTheFlyProgressSummary = $"Completed. Success={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Cancelled={summary.CancelledVmCount}.";
            _deployOnTheFlyReadinessSummary =
                $"Completed. Success={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Cancelled={summary.CancelledVmCount}.";
            DeployOnTheFlyStatusTextBlock.Text =
                $"Deployment finished: {summary.OperationState}. Total={summary.TotalVmCount}, Succeeded={summary.SucceededVmCount}, Failed={summary.FailedVmCount}.";
        }
        catch (Exception ex)
        {
            _deployOnTheFlyLifecycleState = "Failed";
            _deployOnTheFlyProgressPercent = 100;
            _deployOnTheFlyProgressSummary = "Deployment failed.";
            _deployOnTheFlyReadinessSummary = "Deployment failed.";
            DeployOnTheFlyStatusTextBlock.Text = $"Deploy failed. {ex.Message}";
        }
        finally
        {
            _showDeployOnTheFlyAllVmRows = true;
            _isDeployOnTheFlyStarting = false;
            UpdateDeployOnTheFlyUi();
        }
    }

    private void InitializeDeployOnTheFlyProgressRows(MultiVmDeploymentContext context)
    {
        InitializeDeployProgressRows(context, _deployOnTheFlyProgressByVm);
    }

    private void AttachDeployOnTheFlyProgressCallbacks(MultiVmDeploymentContext context)
    {
        AttachDeployProgressCallbacks(context, _deployOnTheFlyProgressByVm, isOnTheFly: true);
    }

    private void InitializeDeployProgressRows(
        MultiVmDeploymentContext context,
        Dictionary<string, DeployVmProgressState> stateByVm)
    {
        stateByVm.Clear();

        foreach (var vmContext in context.VmContexts)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            stateByVm[vmName] = new DeployVmProgressState(
                vmName,
                BuildExpectedDeploySteps(vmContext));
        }
    }

    private void AttachDeployProgressCallbacks(
        MultiVmDeploymentContext context,
        Dictionary<string, DeployVmProgressState> stateByVm,
        bool isOnTheFly)
    {
        foreach (var vmContext in context.VmContexts)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            vmContext.LogCallback = message => DispatcherQueue.TryEnqueue(() =>
            {
                if (!stateByVm.TryGetValue(vmName, out var state))
                {
                    return;
                }

                state.UpdateSummaryMessage(message);
                if (isOnTheFly)
                {
                    UpdateDeployOnTheFlyResultRows();
                    UpdateDeployOnTheFlyUi();
                }
                else
                {
                    UpdateDeployResultRows();
                    UpdateDeployUi();
                }
            });

            vmContext.StepStateEmitter = update => DispatcherQueue.TryEnqueue(() =>
            {
                if (!stateByVm.TryGetValue(vmName, out var state))
                {
                    return;
                }

                state.ApplyStepStateUpdate(update);
                if (isOnTheFly)
                {
                    UpdateDeployOnTheFlyResultRows();
                    UpdateDeployOnTheFlyUi();
                }
                else
                {
                    UpdateDeployResultRows();
                    UpdateDeployUi();
                }
            });
        }
    }

    private static IReadOnlyList<DeployTimelineStepDefinition> BuildExpectedDeploySteps(VmDeploymentContext context)
    {
        var steps = new List<DeployTimelineStepDefinition>
        {
            new(DeploymentStepKeys.CheckHyperV, "Check Hyper-V"),
            new(DeploymentStepKeys.CreateVmFolder, "Create VM folder"),
            new(DeploymentStepKeys.CreateVhd, "Create differencing disk"),
            new(DeploymentStepKeys.CreateVm, "Create VM"),
            new(DeploymentStepKeys.AddNicToVm, "Add network adapter"),
            new(DeploymentStepKeys.ConfigureVm, "Configure VM"),
            new(DeploymentStepKeys.EnableGuestServices, "Enable guest services"),
            new(DeploymentStepKeys.DisableVmCheckpoints, "Disable VM checkpoints"),
            new(DeploymentStepKeys.StartVm, "Start VM")
        };

        if (context.ConfigureTimeZone)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.SetTimeZone, "Set Time Zone"));
        }

        if (context.InstallSoftware)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.InstallSoftware, "Install Software"));
        }

        if (context.InstallRole)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.InstallRole, "Install Role"));
        }

        if (context.ConfigureNetworkInformation)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.ConfigureNetworkInformation, "Configure Network Information"));
        }

        return steps;
    }

    private static IReadOnlyList<DeployTimelineStepRow> CreateReadinessTimelineSteps(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        IReadOnlyList<DeploymentReadinessCheckResult> readinessResults,
        bool hasBlocking)
    {
        var state = hasBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded;
        var rows = new List<DeployTimelineStepRow>
        {
            new("Readiness evaluation", state)
        };

        foreach (var issue in compatibilityIssues)
        {
            var issueState = issue.IsBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending;
            rows.Add(new DeployTimelineStepRow($"{issue.Message} {issue.Guidance}".Trim(), issueState));
        }

        foreach (var result in readinessResults.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
        {
            var issueState = result.Status == DeploymentReadinessStatus.Fail ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending;
            rows.Add(new DeployTimelineStepRow($"{result.Message} {result.ActionableGuidance}".Trim(), issueState));
        }

        return rows;
    }

    private static IReadOnlyList<DeployTimelineStepRow> CreateOutcomeTimelineSteps(VmDeploymentOutcomeSummary vmOutcome)
    {
        var outcomeState = vmOutcome.Status switch
        {
            VmDeploymentOutcomeStatus.Succeeded => DeployTimelineStepState.Succeeded,
            VmDeploymentOutcomeStatus.Failed => DeployTimelineStepState.Failed,
            VmDeploymentOutcomeStatus.Cancelled => DeployTimelineStepState.Skipped,
            _ => DeployTimelineStepState.Pending
        };

        var rows = new List<DeployTimelineStepRow>
        {
            new("Deploy VM", outcomeState)
        };

        if (vmOutcome.Cleanup.CleanupRan)
        {
            var cleanupState = vmOutcome.Cleanup.Status switch
            {
                VmCleanupOutcomeStatus.Succeeded => DeployTimelineStepState.Succeeded,
                VmCleanupOutcomeStatus.Residuals => DeployTimelineStepState.Failed,
                _ => DeployTimelineStepState.Skipped
            };

            if (cleanupState != DeployTimelineStepState.Skipped)
            {
                rows.Add(new("Cleanup", cleanupState));
            }
        }

        return rows;
    }

    private void UpdateTemplatesUi()
    {
        OpenTemplateInEditorButton.IsEnabled = _selectedTemplateLibraryItem is not null && !_isTemplatesLoading;
        DeleteTemplateButton.IsEnabled = _selectedTemplateLibraryItem is not null && !_isTemplatesLoading;
        ExportTemplateButton.IsEnabled = _selectedTemplateLibraryItem is not null && !_isTemplatesLoading;
        ApplyTemplateSearchButton.IsEnabled = !_isTemplatesLoading;
        ClearTemplateSearchButton.IsEnabled = !_isTemplatesLoading;
        ReloadTemplatesButton.IsEnabled = !_isTemplatesLoading;
        ImportTemplateButton.IsEnabled = !_isTemplatesLoading;
        CreateTemplateButton.IsEnabled = !_isTemplatesLoading;
        SaveTemplateButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        SaveTemplateAsButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        ValidateTemplateButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        BackToLibraryButton.IsEnabled = !_isTemplatesLoading;
        AddTemplateVmButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        RemoveTemplateVmButton.IsEnabled = _selectedTemplateVmEntry is not null && !_isTemplatesLoading;
        AddTemplateVmSwitchRowButton.IsEnabled = _selectedTemplateVmEntry is not null && !_isTemplatesLoading;
        TemplateVmVhdxCatalogComboBox.IsEnabled = _selectedTemplateVmEntry is not null && !_isTemplatesLoading;
        ApplyTemplateVmChangesButton.IsEnabled = _selectedTemplateVmEntry is not null && !_isTemplatesLoading;

        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorContextTextBlock.Text = "No template selected.";
            TemplateIdTextBlock.Text = "Template ID: -";
            TemplateFilePathTextBlock.Text = "File path: new template (not saved)";
            TemplateVmCountTextBlock.Text = "VMs: 0";
            TemplateNameTextBox.Text = string.Empty;
            TemplateDescriptionTextBox.Text = string.Empty;
            _templateVmEntries.Clear();
            _selectedTemplateVmEntry = null;
            UpdateTemplateVmEditorPanel();
            UpdateTemplateSwitchGuidanceText();
            return;
        }

        TemplateEditorContextTextBlock.Text = string.IsNullOrWhiteSpace(_activeTemplateEditorDocument.SourceFilePath)
            ? "Editing new template draft."
            : "Editing existing template.";
        TemplateIdTextBlock.Text = $"Template ID: {_activeTemplateEditorDocument.Template.Id}";
        TemplateFilePathTextBlock.Text = $"File path: {_activeTemplateEditorDocument.SourceFilePath ?? "new template (not saved)"}";
        TemplateVmCountTextBlock.Text = $"VMs: {_activeTemplateEditorDocument.Template.VmTemplates.Count}";
        UpdateTemplateVmEditorPanel();
        UpdateTemplateSwitchGuidanceText();
    }

    private void UpdateDeployUi()
    {
        var hasTemplate = _activeDeployTemplateDocument is not null;
        var hasBlockingFailures = _deployCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                                  (_deployReadinessReport?.HasBlockingFailures ?? false);

        DeployTemplateSelectorComboBox.IsEnabled = !_isDeployLoadingTemplates && !_isDeployStarting;
        DeployReloadTemplatesButton.IsEnabled = !_isDeployLoadingTemplates && !_isDeployStarting;
        DeployEvaluateReadinessButton.IsEnabled = hasTemplate && !_isDeployEvaluatingReadiness && !_isDeployStarting;
        DeployResolveSuggestionsButton.IsEnabled = hasTemplate && !_isDeployEvaluatingReadiness && !_isDeployStarting;
        DeployOpenTemplateEditorButton.IsEnabled = _selectedDeployTemplateLibraryItem is not null && !_isDeployStarting;
        DeployStartButton.IsEnabled = hasTemplate && !hasBlockingFailures && !_isDeployEvaluatingReadiness && !_isDeployStarting;

        if (_activeDeployTemplateDocument is null)
        {
            DeployReadinessSummaryTextBlock.Text = "Select a template to evaluate readiness and run deploy.";
            DeployTemplateSummaryTextBlock.Text = "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";
            DeployTemplateRemediationTextBlock.Text = "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";
            _deploySharedIssueSummaries.Clear();
            DeploySharedIssuesSummaryTextBlock.Text = "Shared review items appear here when multiple VMs need the same remediation.";
            DeployOverallStateTextBlock.Text = _deployLifecycleState;
            DeployProgressBar.Value = _deployProgressPercent;
            DeployProgressSummaryTextBlock.Text = _deployProgressSummary;
            DeployGlobalIssuesBadgeTextBlock.Text = $"Issues: {_deployIssueRows.Count}";
            UpdateDeployResultRows();
            UpdateDeployIssueRows();
            UpdateDeploySharedIssueSummaries();
            ApplyRightPanelState();
            return;
        }

        var failCount = _deployCompatibilityIssues.Count(issue => issue.IsBlocking) +
                        (_deployReadinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail) ?? 0);
        var warnCount = _deployCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                        (_deployReadinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn) ?? 0);
        var passCount = _deployReadinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Pass) ?? 0;
        var deployState = hasBlockingFailures ? "Blocked" : "Ready";
        DeployReadinessSummaryTextBlock.Text =
            $"{deployState}. Pass={passCount}, Warn={warnCount}, Fail={failCount}. " +
            $"Template: {_activeDeployTemplateDocument.Template.Name} ({_activeDeployTemplateDocument.Template.VmTemplates.Count} VMs).";
        DeployTemplateSummaryTextBlock.Text =
            $"Template '{_activeDeployTemplateDocument.Template.Name}' will deploy {_activeDeployTemplateDocument.Template.VmTemplates.Count} VM(s). Review shared environment blockers here before deciding whether to remediate or open the template editor.";
        DeployTemplateRemediationTextBlock.Text = hasBlockingFailures
            ? "Blocking issues are grouped below when possible. Use Resolve Suggestions for safe shared remaps, or Open in Templates Editor for structural fixes."
            : "This surface is for template review and remediation. Use Open in Templates Editor only when the template itself needs structural changes.";

        DeployOverallStateTextBlock.Text = _deployLifecycleState;
        DeployProgressBar.Value = _deployProgressPercent;
        DeployProgressSummaryTextBlock.Text = _deployProgressSummary;
        DeployGlobalIssuesBadgeTextBlock.Text = $"Issues: {_deployIssueRows.Count}";

        UpdateDeployResultRows();
        UpdateDeployIssueRows();
        UpdateDeploySharedIssueSummaries();
        ApplyRightPanelState();
    }

    private void UpdateDeploySharedIssueSummaries()
    {
        _deploySharedIssueSummaries.Clear();

        var groupedIssues = _deployIssueRows
            .Where(issue => !string.Equals(issue.Scope, "Global", StringComparison.OrdinalIgnoreCase))
            .GroupBy(issue => $"{issue.Severity}|{issue.Message}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(issue => issue.Scope).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .OrderByDescending(group => group.Key.StartsWith("Block|", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(group => group.Count())
            .ToList();

        foreach (var group in groupedIssues)
        {
            var scopes = group
                .Select(issue => issue.Scope)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var message = group.First().Message;
            var severity = group.First().Severity;
            _deploySharedIssueSummaries.Add($"{severity}: {message} Shared across {scopes.Count} VM(s): {string.Join(", ", scopes)}");
        }

        DeploySharedIssuesSummaryTextBlock.Text = _deploySharedIssueSummaries.Count > 0
            ? "Shared environment and compatibility issues detected across multiple VMs. Fix them here when safe, or open Templates Editor for structural changes."
            : "No shared review items are currently grouped. Review the readiness summary, then use the main actions below.";
    }

    private void UpdateDeployResultRows()
    {
        _deployVmResultRows.Clear();

        if (_showDeployAllVmRows && _deployProgressByVm.Count > 0)
        {
            foreach (var state in _deployProgressByVm.Values.OrderBy(value => value.VmName, StringComparer.OrdinalIgnoreCase))
            {
                _deployVmResultRows.Add(state.ToRow());
            }

            return;
        }

        if (_activeDeployTemplateDocument is null)
        {
            return;
        }

        var vmNames = _activeDeployTemplateDocument.Template.VmTemplates
            .Select(vm => string.IsNullOrWhiteSpace(vm.Name) ? "Unnamed-VM" : vm.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compatibilityByVm = _deployCompatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (_deployReadinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var vmName in vmNames)
        {
            compatibilityByVm.TryGetValue(vmName, out var vmCompatibilityIssues);
            readinessByVm.TryGetValue(vmName, out var vmReadinessResults);

            vmCompatibilityIssues ??= [];
            vmReadinessResults ??= [];

            var hasBlocking = vmCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Fail);
            var hasWarnings = vmCompatibilityIssues.Any(issue => !issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Warn);
            if (!hasBlocking && !hasWarnings)
            {
                continue;
            }

            var status = hasBlocking ? "Blocked" : hasWarnings ? "Warning" : "Ready";
            var blockingCount = vmCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = vmCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            var summary = $"Blocking: {blockingCount} | Warnings: {warningCount}";

            _deployVmResultRows.Add(new DeployVmResultRow(
                VmName: vmName,
                Status: status,
                Summary: summary,
                ProgressPercent: hasBlocking ? 100 : 80,
                TimelineSteps: CreateReadinessTimelineSteps(vmCompatibilityIssues, vmReadinessResults, hasBlocking)));
        }
    }

    private void UpdateDeployIssueRows()
    {
        _deployIssueRows.Clear();

        foreach (var issue in _deployCompatibilityIssues)
        {
            var scope = string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName;
            _deployIssueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: $"{issue.Message} {issue.Guidance}".Trim()));
        }

        if (_deployReadinessReport is not null)
        {
            foreach (var result in _deployReadinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
            {
                var scope = result.AffectedVmNames.Count == 0 ? "Global" : string.Join(", ", result.AffectedVmNames);
                _deployIssueRows.Add(new DeployIssueRow(
                    Scope: scope,
                    Severity: result.Status == DeploymentReadinessStatus.Fail ? "Block" : "Warn",
                    Message: $"{result.Message} {result.ActionableGuidance}".Trim()));
            }
        }
    }

    private void UpdateDeployOnTheFlyResultRows()
    {
        _deployOnTheFlyVmResultRows.Clear();

        if (_showDeployOnTheFlyAllVmRows && _deployOnTheFlyProgressByVm.Count > 0)
        {
            foreach (var state in _deployOnTheFlyProgressByVm.Values.OrderBy(value => value.VmName, StringComparer.OrdinalIgnoreCase))
            {
                _deployOnTheFlyVmResultRows.Add(state.ToRow());
            }

            return;
        }

        var vmNames = _deployOnTheFlyVmEntries
            .Select(vm => string.IsNullOrWhiteSpace(vm.Name) ? "Unnamed-VM" : vm.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compatibilityByVm = _deployOnTheFlyCompatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (_deployOnTheFlyReadinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var vmName in vmNames)
        {
            compatibilityByVm.TryGetValue(vmName, out var vmCompatibilityIssues);
            readinessByVm.TryGetValue(vmName, out var vmReadinessResults);

            vmCompatibilityIssues ??= [];
            vmReadinessResults ??= [];

            var hasBlocking = vmCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Fail);
            var hasWarnings = vmCompatibilityIssues.Any(issue => !issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Warn);
            if (!hasBlocking && !hasWarnings)
            {
                continue;
            }

            var status = hasBlocking ? "Blocked" : hasWarnings ? "Warning" : "Ready";
            var blockingCount = vmCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = vmCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            var summary = $"Blocking: {blockingCount} | Warnings: {warningCount}";

            _deployOnTheFlyVmResultRows.Add(new DeployVmResultRow(
                VmName: vmName,
                Status: status,
                Summary: summary,
                ProgressPercent: hasBlocking ? 100 : 80,
                TimelineSteps: CreateReadinessTimelineSteps(vmCompatibilityIssues, vmReadinessResults, hasBlocking)));
        }

        var hasReadinessData = _deployOnTheFlyReadinessReport is not null || _deployOnTheFlyCompatibilityIssues.Count > 0;
        if (_deployOnTheFlyVmResultRows.Count == 0 && vmNames.Count > 0 && hasReadinessData)
        {
            _deployOnTheFlyVmResultRows.Add(new DeployVmResultRow(
                VmName: "Ready to deploy",
                Status: "Ready",
                Summary: "No blocking issues or warnings detected.",
                ProgressPercent: 100,
                TimelineSteps: [new DeployTimelineStepRow("Readiness evaluation", DeployTimelineStepState.Succeeded)],
                IsExpandable: false));
        }
    }

    private void UpdateDeployOnTheFlyIssueRows()
    {
        _deployOnTheFlyIssueRows.Clear();

        foreach (var issue in _deployOnTheFlyCompatibilityIssues)
        {
            var scope = string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName;
            _deployOnTheFlyIssueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: $"{issue.Message} {issue.Guidance}".Trim()));
        }

        if (_deployOnTheFlyReadinessReport is not null)
        {
            foreach (var result in _deployOnTheFlyReadinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
            {
                var scope = result.AffectedVmNames.Count == 0 ? "Global" : string.Join(", ", result.AffectedVmNames);
                _deployOnTheFlyIssueRows.Add(new DeployIssueRow(
                    Scope: scope,
                    Severity: result.Status == DeploymentReadinessStatus.Fail ? "Block" : "Warn",
                    Message: $"{result.Message} {result.ActionableGuidance}".Trim()));
            }
        }
    }

    private static string BuildCleanupSummary(VmDeploymentOutcomeSummary vmOutcome)
    {
        if (!vmOutcome.Cleanup.CleanupRan)
        {
            return "Completed";
        }

        return vmOutcome.Cleanup.Status switch
        {
            VmCleanupOutcomeStatus.Succeeded => "Cleanup completed",
            VmCleanupOutcomeStatus.Residuals => $"Cleanup completed with residuals ({vmOutcome.Cleanup.ResidualCount}). Manual cleanup may be required.",
            _ => "Cleanup not needed"
        };
    }

    private void UpdateDeployRowsFromSummary(DeploymentOutcomeSummary summary)
    {
        _deployVmResultRows.Clear();

        foreach (var vmOutcome in summary.VmOutcomes)
        {
            if (_deployProgressByVm.TryGetValue(vmOutcome.VmName, out var liveState))
            {
                liveState.MarkCompleted(vmOutcome.Status.ToString(), BuildCleanupSummary(vmOutcome));
                _deployVmResultRows.Add(liveState.ToRow());
            }
            else
            {
                _deployVmResultRows.Add(new DeployVmResultRow(
                    VmName: vmOutcome.VmName,
                    Status: vmOutcome.Status.ToString(),
                    Summary: BuildCleanupSummary(vmOutcome),
                    ProgressPercent: 100,
                    TimelineSteps: CreateOutcomeTimelineSteps(vmOutcome)));
            }
        }

        _deployIssueRows.Clear();
        foreach (var residual in summary.Residuals)
        {
            _deployIssueRows.Add(new DeployIssueRow(
                Scope: residual.VmName,
                Severity: "Warn",
                Message: $"{residual.ResourceType} '{residual.Identifier}' residual. Suggested action: {residual.SuggestedAction}"));
        }
    }

    private void UpdateDeployOnTheFlyRowsFromSummary(DeploymentOutcomeSummary summary)
    {
        _deployOnTheFlyVmResultRows.Clear();

        foreach (var vmOutcome in summary.VmOutcomes)
        {
            if (_deployOnTheFlyProgressByVm.TryGetValue(vmOutcome.VmName, out var liveState))
            {
                liveState.MarkCompleted(vmOutcome.Status.ToString(), BuildCleanupSummary(vmOutcome));
                _deployOnTheFlyVmResultRows.Add(liveState.ToRow());
            }
            else
            {
                _deployOnTheFlyVmResultRows.Add(new DeployVmResultRow(
                    VmName: vmOutcome.VmName,
                    Status: vmOutcome.Status.ToString(),
                    Summary: BuildCleanupSummary(vmOutcome),
                    ProgressPercent: 100,
                    TimelineSteps: CreateOutcomeTimelineSteps(vmOutcome)));
            }
        }

        _deployOnTheFlyIssueRows.Clear();
        foreach (var residual in summary.Residuals)
        {
            _deployOnTheFlyIssueRows.Add(new DeployIssueRow(
                Scope: residual.VmName,
                Severity: "Warn",
                Message: $"{residual.ResourceType} '{residual.Identifier}' residual. Suggested action: {residual.SuggestedAction}"));
        }
    }

    private async Task EnsureDeployTemplatesLoadedAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateLibraryItems.Count > 0)
        {
            DeployTemplateSelectorComboBox.SelectedItem = _selectedDeployTemplateLibraryItem;
            UpdateDeployUi();
            return;
        }

        _isDeployLoadingTemplates = true;
        _deployLifecycleState = "Loading";
        _deployProgressPercent = 0;
        _deployProgressSummary = "Loading templates...";
        UpdateDeployUi();
        DeployActionStatusTextBlock.Text = "Loading templates for deploy...";

        try
        {
            await EnsureTemplatesLibraryAsync(forceRefresh: forceRefresh);

            if (_templateLibraryItems.Count == 0)
            {
                _selectedDeployTemplateLibraryItem = null;
                _activeDeployTemplateDocument = null;
                _deployReadinessReport = null;
                _deployCompatibilityIssues.Clear();
                _deployLifecycleState = "Idle";
                _deployProgressPercent = 0;
                _deployProgressSummary = "No templates available.";
                DeployActionStatusTextBlock.Text = "No templates available for deploy.";
            }
            else
            {
                _selectedDeployTemplateLibraryItem ??= _templateLibraryItems[0];
                DeployTemplateSelectorComboBox.SelectedItem = _selectedDeployTemplateLibraryItem;
                _deployLifecycleState = "Idle";
                _deployProgressPercent = 0;
                _deployProgressSummary = "Template list loaded.";
                DeployActionStatusTextBlock.Text = $"Loaded {_templateLibraryItems.Count} template(s) for deploy.";
            }
        }
        catch (Exception ex)
        {
            _deployLifecycleState = "Error";
            _deployProgressPercent = 0;
            _deployProgressSummary = "Template load failed.";
            DeployActionStatusTextBlock.Text = $"Failed to load deploy templates. {ex.Message}";
        }
        finally
        {
            _isDeployLoadingTemplates = false;
            UpdateDeployUi();
        }
    }

    private async Task EvaluateDeployReadinessAsync(DeploymentPreflightMode mode)
    {
        _showDeployAllVmRows = false;
        if (_activeDeployTemplateDocument is null)
        {
            DeployActionStatusTextBlock.Text = "Select a template first.";
            UpdateDeployUi();
            return;
        }

        _isDeployEvaluatingReadiness = true;
        _deployLifecycleState = "Evaluating";
        _deployProgressPercent = 10;
        _deployProgressSummary = mode == DeploymentPreflightMode.Full
            ? "Running full readiness checks..."
            : "Running quick readiness checks...";
        DeployActionStatusTextBlock.Text = mode == DeploymentPreflightMode.Full
            ? "Running full deploy readiness evaluation..."
            : "Running quick deploy readiness evaluation...";

        try
        {
            await EnsureTemplateSwitchesAsync(forceRefresh: false);
            var deployContext = BuildDeployContext(_activeDeployTemplateDocument.Template);
            _deployCompatibilityIssues.Clear();
            _deployCompatibilityIssues.AddRange(deployContext.CompatibilityIssues);

            _deployReadinessReport = await _deploymentPreflightService.RunAsync(deployContext.MultiVmContext, mode);

            var blockingCount = _deployCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                _deployReadinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = _deployCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               _deployReadinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            _deployProgressPercent = 35;
            _deployLifecycleState = blockingCount > 0 ? "Blocked" : warningCount > 0 ? "Warning" : "Ready";
            _deployProgressSummary = blockingCount > 0
                ? $"Readiness blocked ({blockingCount} fail, {warningCount} warn)."
                : warningCount > 0
                    ? $"Readiness passed with warnings ({warningCount})."
                    : "Readiness passed.";
            DeployActionStatusTextBlock.Text = blockingCount > 0
                ? $"Readiness found {blockingCount} blocking issue(s) and {warningCount} warning(s)."
                : warningCount > 0
                    ? $"Readiness passed with {warningCount} warning(s)."
                    : "Readiness passed with no issues.";
        }
        catch (Exception ex)
        {
            _deployReadinessReport = null;
            _deployCompatibilityIssues.Clear();
            _deployLifecycleState = "Error";
            _deployProgressPercent = 0;
            _deployProgressSummary = "Readiness evaluation failed.";
            DeployActionStatusTextBlock.Text = $"Readiness evaluation failed. {ex.Message}";
        }
        finally
        {
            _isDeployEvaluatingReadiness = false;
            UpdateDeployUi();
        }
    }

    private async void DeployStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDeployTemplateDocument is null)
        {
            DeployActionStatusTextBlock.Text = "Select a template first.";
            return;
        }

        _isDeployStarting = true;
        _showDeployAllVmRows = true;
        _deployProgressByVm.Clear();
        _deployLifecycleState = "Running";
        _deployProgressPercent = 45;
        _deployProgressSummary = "Preparing deployment...";
        UpdateDeployUi();
        try
        {
            await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Full);
            var hasBlockingFailures = _deployCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                                      (_deployReadinessReport?.HasBlockingFailures ?? false);
            if (hasBlockingFailures)
            {
                _deployLifecycleState = "Blocked";
                _deployProgressPercent = 35;
                _deployProgressSummary = "Deployment blocked by readiness failures.";
                DeployActionStatusTextBlock.Text = "Deploy blocked by readiness failures. Resolve blocking items first.";
                return;
            }

            var deployContext = BuildDeployContext(_activeDeployTemplateDocument.Template);
            InitializeDeployProgressRows(deployContext.MultiVmContext, _deployProgressByVm);
            AttachDeployProgressCallbacks(deployContext.MultiVmContext, _deployProgressByVm, isOnTheFly: false);
            _deployProgressPercent = 60;
            _deployProgressSummary = $"Deploying {deployContext.MultiVmContext.VmContexts.Count} VM(s)...";
            DeployActionStatusTextBlock.Text = "Starting deployment...";
            UpdateDeployResultRows();
            UpdateDeployUi();
            await _deploymentCoordinator.DeployAllAsync(deployContext.MultiVmContext);

            var summary = _deploymentOutcomeSummaryBuilder.Build(deployContext.MultiVmContext);
            UpdateDeployRowsFromSummary(summary);
            _deployLifecycleState = summary.OperationState switch
            {
                DeploymentOperationState.Completed => "Completed",
                DeploymentOperationState.Cancelled or DeploymentOperationState.CancelledWithResiduals => "Cancelled",
                DeploymentOperationState.Failed or DeploymentOperationState.FailedWithResiduals => "Failed",
                _ => "Completed"
            };
            _deployProgressPercent = 100;
            _deployProgressSummary =
                $"Completed. Success={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Cancelled={summary.CancelledVmCount}.";
            DeployActionStatusTextBlock.Text =
                $"Deployment finished: {summary.OperationState}. Total={summary.TotalVmCount}, " +
                $"Succeeded={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Residuals={summary.ResidualVmCount}.";
        }
        catch (Exception ex)
        {
            _deployLifecycleState = "Failed";
            _deployProgressPercent = 100;
            _deployProgressSummary = "Deployment failed.";
            DeployActionStatusTextBlock.Text = $"Deploy failed. {ex.Message}";
        }
        finally
        {
            _showDeployAllVmRows = true;
            _isDeployStarting = false;
            UpdateDeployUi();
        }
    }

    private async Task<int> ApplyDeployResolveSuggestionsAsync(LabTemplate template)
    {
        var catalogResult = _vhdxCatalogStore.Load(_settingsStore.Settings.CatalogPath);
        var catalogItems = catalogResult.Items;
        var templateSwitches = _templateAvailableSwitches.Count > 0
            ? _templateAvailableSwitches
            : await _machinesCapabilityService.LoadVirtualSwitchesAsync();
        var availableSwitches = templateSwitches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var applied = 0;
        foreach (var vm in template.VmTemplates)
        {
            var switchNames = vm.SwitchNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [];
            if (switchNames.Count == 0 && !string.IsNullOrWhiteSpace(vm.SwitchName))
            {
                switchNames.Add(vm.SwitchName);
            }

            if (switchNames.Count > 0)
            {
                var normalized = switchNames
                    .Where(name => availableSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (normalized.Count != switchNames.Count)
                {
                    applied++;
                }

                vm.SwitchNames = normalized.Count > 0 ? normalized : null;
                vm.SwitchName = normalized.Count > 0 ? normalized[0] : null;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxSignature))
            {
                var signatureMatches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems);
                if (signatureMatches.Count == 1)
                {
                    var match = signatureMatches[0];
                    vm.VhdxId = match.Id;
                    vm.VhdPath = match.Path;
                    vm.VhdxSignature = match.Signature;
                    applied++;
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdPath))
            {
                var pathMatch = catalogItems.FirstOrDefault(item =>
                    string.Equals(item.Path, vm.VhdPath, StringComparison.OrdinalIgnoreCase));
                if (pathMatch is not null)
                {
                    vm.VhdxId = pathMatch.Id;
                    vm.VhdxSignature = pathMatch.Signature;
                    vm.VhdPath = pathMatch.Path;
                    applied++;
                }
            }
        }

        return applied;
    }

    private DeployContextBuildResult BuildDeployContext(LabTemplate template)
    {
        var compatibilityIssues = new List<DeployCompatibilityIssue>();
        var settings = _settingsStore.Settings;
        var catalogResult = _vhdxCatalogStore.Load(settings.CatalogPath);
        var catalogItems = catalogResult.Items.ToList();
        var availableSwitches = _templateAvailableSwitches.ToList();

        var contexts = new List<VmDeploymentContext>();
        foreach (var vmTemplate in template.VmTemplates)
        {
            var vmName = string.IsNullOrWhiteSpace(vmTemplate.Name) ? "Unnamed-VM" : vmTemplate.Name.Trim();
            var vmId = Guid.TryParse(vmTemplate.VmId, out var parsedVmId) ? parsedVmId : Guid.NewGuid();

            var diskResolution = ResolveDeployDiskIdentity(vmTemplate, catalogItems);
            compatibilityIssues.AddRange(diskResolution.Issues.Select(issue => issue with { VmName = vmName }));

            var switchResolution = ResolveDeploySwitches(vmTemplate, availableSwitches);
            compatibilityIssues.AddRange(switchResolution.Issues.Select(issue => issue with { VmName = vmName }));

            var vmPath = Path.Combine(settings.VmBasePath, vmName);
            var vhdPath = Path.Combine(vmPath, $"{vmName}.vhdx");

            var context = new VmDeploymentContext
            {
                VmId = vmId,
                VmName = vmName,
                MemoryMb = vmTemplate.MemoryMb > 0 ? vmTemplate.MemoryMb : settings.DefaultVmMemoryMb,
                CpuCount = vmTemplate.CpuCount > 0 ? vmTemplate.CpuCount : settings.DefaultCpuCount,
                VmPath = vmPath,
                VhdPath = vhdPath,
                BaseVhdPath = diskResolution.EffectiveBasePath,
                VhdxId = diskResolution.EffectiveId,
                VhdxSignature = diskResolution.EffectiveSignature,
                VirtualSwitchName = switchResolution.EffectiveSwitch,
                PerVmFailFast = settings.PerVmFailFast,
                NonBlockingOptionalSteps = new List<string>(settings.NonBlockingOptionalSteps ?? []),
                ConfigureTimeZone = vmTemplate.TimeZoneConfig?.Enabled == true,
                InstallSoftware = vmTemplate.SoftwareConfig?.Enabled == true,
                InstallRole = vmTemplate.RoleConfig?.Enabled == true,
                ConfigureNetworkInformation = vmTemplate.GuestNetworkConfig?.Enabled == true,
                TimeZoneConfig = vmTemplate.TimeZoneConfig,
                SoftwareConfig = vmTemplate.SoftwareConfig,
                RoleConfig = vmTemplate.RoleConfig,
                GuestNetworkConfig = vmTemplate.GuestNetworkConfig
            };
            contexts.Add(context);
        }

        var multiVmContext = new MultiVmDeploymentContext
        {
            VmContexts = contexts,
            StopAllOnAnyVmFailure = settings.StopAllOnAnyVmFailure
        };

        return new DeployContextBuildResult(multiVmContext, compatibilityIssues);
    }

    private static DeployDiskResolution ResolveDeployDiskIdentity(VmTemplate vmTemplate, IReadOnlyList<VhdxCatalogItem> catalogItems)
    {
        var issues = new List<DeployCompatibilityIssue>();
        var idMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdxId)
            ? null
            : catalogItems.FirstOrDefault(item => string.Equals(item.Id, vmTemplate.VhdxId, StringComparison.OrdinalIgnoreCase));

        var signatureMatches = string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature)
            ? []
            : VhdxSignature.FindMatches(vmTemplate.VhdxSignature, catalogItems).ToList();

        var pathMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdPath)
            ? null
            : catalogItems.FirstOrDefault(item => string.Equals(item.Path, vmTemplate.VhdPath, StringComparison.OrdinalIgnoreCase));

        if (idMatch is not null)
        {
            var pathConflict = !string.IsNullOrWhiteSpace(vmTemplate.VhdPath) &&
                               !string.Equals(vmTemplate.VhdPath, idMatch.Path, StringComparison.OrdinalIgnoreCase);
            var signatureConflict = !string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature) &&
                                    !string.IsNullOrWhiteSpace(idMatch.Signature) &&
                                    !string.Equals(vmTemplate.VhdxSignature, idMatch.Signature, StringComparison.OrdinalIgnoreCase);
            if (pathConflict || signatureConflict)
            {
                issues.Add(new DeployCompatibilityIssue(
                    VmName: string.Empty,
                    IsBlocking: true,
                    Message: "Disk identity conflict detected. Resolve in Templates editor.",
                    Guidance: "Select one catalog-backed identity and save template."));
                return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
            }

            return new DeployDiskResolution(idMatch.Path, idMatch.Id, idMatch.Signature, issues);
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdxId))
        {
            issues.Add(new DeployCompatibilityIssue(
                VmName: string.Empty,
                IsBlocking: true,
                Message: $"Catalog entry '{vmTemplate.VhdxId}' is missing.",
                Guidance: "Open in Templates editor and select a valid catalog disk."));
            return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
        }

        if (signatureMatches.Count > 1)
        {
            issues.Add(new DeployCompatibilityIssue(
                VmName: string.Empty,
                IsBlocking: true,
                Message: "Disk signature maps to multiple catalog entries.",
                Guidance: "Resolve ambiguous disk selection in Templates editor."));
            return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
        }

        if (signatureMatches.Count == 1)
        {
            var match = signatureMatches[0];
            return new DeployDiskResolution(match.Path, match.Id, match.Signature, issues);
        }

        if (pathMatch is not null)
        {
            issues.Add(new DeployCompatibilityIssue(
                VmName: string.Empty,
                IsBlocking: false,
                Message: "Using legacy path-based disk match.",
                Guidance: "Use resolve suggestions or Templates editor to normalize to catalog id."));
            return new DeployDiskResolution(pathMatch.Path, pathMatch.Id, pathMatch.Signature, issues);
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdPath))
        {
            issues.Add(new DeployCompatibilityIssue(
                VmName: string.Empty,
                IsBlocking: true,
                Message: "Disk path does not match catalog entries.",
                Guidance: "Open in Templates editor and choose a valid catalog base disk."));
            return new DeployDiskResolution(vmTemplate.VhdPath, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
        }

        issues.Add(new DeployCompatibilityIssue(
            VmName: string.Empty,
            IsBlocking: true,
            Message: "No disk identity configured.",
            Guidance: "Open in Templates editor and select a base disk."));
        return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
    }

    private static DeploySwitchResolution ResolveDeploySwitches(VmTemplate vmTemplate, IReadOnlyList<string> availableSwitches)
    {
        var issues = new List<DeployCompatibilityIssue>();
        var switches = vmTemplate.SwitchNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (switches.Count == 0 && !string.IsNullOrWhiteSpace(vmTemplate.SwitchName))
        {
            switches.Add(vmTemplate.SwitchName);
        }

        if (switches.Count == 0)
        {
            issues.Add(new DeployCompatibilityIssue(
                VmName: string.Empty,
                IsBlocking: false,
                Message: "No switch assigned.",
                Guidance: "Assign a switch in Quick Deploy VM properties (or Templates editor) if networking is required."));
            return new DeploySwitchResolution(string.Empty, issues);
        }

        var available = switches
            .Where(name => availableSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var missing = switches.Where(name => !available.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            issues.Add(new DeployCompatibilityIssue(
                VmName: string.Empty,
                IsBlocking: false,
                Message: $"Switch mapping partial/missing ({string.Join(", ", missing)}).",
                Guidance: "Update switch mapping in Quick Deploy VM properties (or Templates editor)."));
        }

        return new DeploySwitchResolution(available.FirstOrDefault() ?? string.Empty, issues);
    }

    private async Task OpenTemplateInEditorAsync(TemplateLibraryItem templateItem, bool fromDeploy)
    {
        _selectedTemplateLibraryItem = templateItem;
        TemplateLibraryListView.SelectedItem = templateItem;

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            _activeTemplateEditorDocument = await _templatesCapabilityService.LoadForEditorAsync(templateItem.FilePath);
            await EnsureTemplateSwitchesAsync(forceRefresh: false);
            await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh: false);
            BindTemplateEditorDocument();
            TemplateEditorStatusTextBlock.Text = "Template loaded.";
            NavigateToRoute(ShellRouteKeys.TemplatesEditor);
            if (fromDeploy)
            {
                DeployActionStatusTextBlock.Text = $"Opened '{templateItem.Name}' in Templates editor.";
            }
        }
        catch (Exception ex)
        {
            TemplateEditorStatusTextBlock.Text = $"Failed to open template. {ex.Message}";
            if (fromDeploy)
            {
                DeployActionStatusTextBlock.Text = $"Failed to open template in editor. {ex.Message}";
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
            UpdateDeployUi();
        }
    }

    private async Task EnsureTemplatesLibraryAsync(bool forceRefresh)
    {
        if (_isTemplatesLoading && !forceRefresh)
        {
            return;
        }

        if (!forceRefresh && _templateLibraryItems.Count > 0)
        {
            return;
        }

        var ownsLoadingState = !_isTemplatesLoading;
        if (ownsLoadingState)
        {
            _isTemplatesLoading = true;
            UpdateTemplatesUi();
        }
        TemplatesLibraryStatusTextBlock.Text = "Loading templates...";

        try
        {
            var result = await _templatesCapabilityService.LoadLibraryAsync(TemplateSearchTextBox.Text);
            _templateLibraryItems.Clear();
            foreach (var item in result.Items)
            {
                _templateLibraryItems.Add(item);
            }

            if (_templateLibraryItems.Count == 0)
            {
                TemplatesLibraryStatusTextBlock.Text = result.Errors.Count == 0
                    ? "No templates found in configured template folder."
                    : $"No templates loaded. {result.Errors[0]}";
            }
            else
            {
                TemplatesLibraryStatusTextBlock.Text = result.Errors.Count == 0
                    ? $"Loaded {_templateLibraryItems.Count} template(s)."
                    : $"Loaded {_templateLibraryItems.Count} template(s) with warnings.";
            }

            if (_selectedTemplateLibraryItem is not null)
            {
                _selectedTemplateLibraryItem = _templateLibraryItems
                    .FirstOrDefault(item => string.Equals(item.FilePath, _selectedTemplateLibraryItem.FilePath, StringComparison.OrdinalIgnoreCase));
                TemplateLibraryListView.SelectedItem = _selectedTemplateLibraryItem;
            }

            if (_selectedDeployTemplateLibraryItem is not null)
            {
                _selectedDeployTemplateLibraryItem = _templateLibraryItems
                    .FirstOrDefault(item => string.Equals(item.FilePath, _selectedDeployTemplateLibraryItem.FilePath, StringComparison.OrdinalIgnoreCase));
                DeployTemplateSelectorComboBox.SelectedItem = _selectedDeployTemplateLibraryItem;
            }
        }
        catch (Exception ex)
        {
            TemplatesLibraryStatusTextBlock.Text = $"Failed to load templates. {ex.Message}";
        }
        finally
        {
            if (ownsLoadingState)
            {
                _isTemplatesLoading = false;
                UpdateTemplatesUi();
            }
        }
    }

    private async Task OpenSelectedTemplateInEditorAsync()
    {
        if (_selectedTemplateLibraryItem is null)
        {
            TemplatesLibraryStatusTextBlock.Text = "Select a template first.";
            return;
        }

        await OpenTemplateInEditorAsync(_selectedTemplateLibraryItem, fromDeploy: false);
    }

    private void BindTemplateEditorDocument()
    {
        if (_activeTemplateEditorDocument is null)
        {
            UpdateTemplatesUi();
            return;
        }

        RefreshTemplateVmEntriesFromDocument();
        TemplateNameTextBox.Text = _activeTemplateEditorDocument.Template.Name;
        TemplateDescriptionTextBox.Text = _activeTemplateEditorDocument.Template.Description ?? string.Empty;
        UpdateTemplatesUi();
    }

    private async Task EnsureTemplateSwitchesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateAvailableSwitches.Count > 0)
        {
            return;
        }

        try
        {
            var switches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
            _templateAvailableSwitches = switches
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            _templateAvailableSwitches = Array.Empty<string>();
        }

        RenderTemplateSwitchRowsFromVm();
    }

    private async Task EnsureTemplateVhdxCatalogOptionsAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateVhdxCatalogOptions.Count > 0)
        {
            return;
        }

        _templateVhdxCatalogOptions.Clear();
        var result = await _templatesCapabilityService.LoadVhdxCatalogOptionsAsync();
        foreach (var item in result.Items)
        {
            _templateVhdxCatalogOptions.Add(new TemplateVhdxCatalogOption(
                item.Id,
                item.Path,
                item.OsName,
                item.OsVersion,
                item.Generation,
                item.Signature));
        }

        if (result.Errors.Count > 0)
        {
            TemplateVmVhdxGuidanceTextBlock.Text = $"Catalog warning: {result.Errors[0]}";
        }

        UpdateTemplateVhdxSelectorFromVm();
    }

    private async Task EnsureDeployOnTheFlyReferenceDataAsync(bool forceRefresh)
    {
        await EnsureTemplateSwitchesAsync(forceRefresh);
        await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh);
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private bool PullEditorFieldsIntoDocument()
    {
        if (_activeTemplateEditorDocument is null)
        {
            return false;
        }

        if (!TryApplySelectedTemplateVmFields(showSuccessStatus: false))
        {
            return false;
        }

        var template = _activeTemplateEditorDocument.Template;
        template.Name = TemplateNameTextBox.Text?.Trim() ?? string.Empty;
        template.Description = TemplateDescriptionTextBox.Text?.Trim();
        SyncTemplateVmEntriesToDocument();
        return true;
    }

    private void RefreshTemplateVmEntriesFromDocument()
    {
        _templateVmEntries.Clear();
        _selectedTemplateVmEntry = null;
        if (_activeTemplateEditorDocument is null)
        {
            return;
        }

        foreach (var vmTemplate in _activeTemplateEditorDocument.Template.VmTemplates)
        {
            _templateVmEntries.Add(vmTemplate);
        }

        if (_templateVmEntries.Count > 0)
        {
            _selectedTemplateVmEntry = _templateVmEntries[0];
            TemplateVmListView.SelectedItem = _selectedTemplateVmEntry;
        }
        else
        {
            TemplateVmListView.SelectedItem = null;
        }
    }

    private void SyncTemplateVmEntriesToDocument()
    {
        if (_activeTemplateEditorDocument is null)
        {
            return;
        }

        _activeTemplateEditorDocument.Template.VmTemplates = _templateVmEntries.ToList();
        TemplateVmCountTextBlock.Text = $"VMs: {_activeTemplateEditorDocument.Template.VmTemplates.Count}";
    }

    private void RefreshTemplateVmListView()
    {
        var selectedVm = _selectedTemplateVmEntry;
        TemplateVmListView.ItemsSource = null;
        TemplateVmListView.ItemsSource = _templateVmEntries;
        TemplateVmListView.SelectedItem = selectedVm;
    }

    private void UpdateTemplateVmEditorPanel()
    {
        _isUpdatingTemplateVmEditorControls = true;
        try
        {
            if (_selectedTemplateVmEntry is null)
            {
                TemplateVmIdTextBlock.Text = "VM ID: -";
                TemplateVmNameTextBox.Text = string.Empty;
                TemplateVmMemoryTextBox.Text = string.Empty;
                TemplateVmCpuTextBox.Text = string.Empty;
                TemplateVmVhdxIdTextBox.Text = string.Empty;
                TemplateVmVhdPathTextBox.Text = string.Empty;
                TemplateVmVhdxSignatureTextBox.Text = string.Empty;
                RenderTemplateSwitchRows(Array.Empty<string>());
                UpdateTemplateVhdxSelectorFromVm();
                return;
            }

            TemplateVmIdTextBlock.Text = $"VM ID: {_selectedTemplateVmEntry.VmId}";
            TemplateVmNameTextBox.Text = _selectedTemplateVmEntry.Name;
            TemplateVmMemoryTextBox.Text = _selectedTemplateVmEntry.MemoryMb.ToString();
            TemplateVmCpuTextBox.Text = _selectedTemplateVmEntry.CpuCount.ToString();
            TemplateVmVhdxIdTextBox.Text = _selectedTemplateVmEntry.VhdxId ?? string.Empty;
            TemplateVmVhdPathTextBox.Text = _selectedTemplateVmEntry.VhdPath ?? string.Empty;
            TemplateVmVhdxSignatureTextBox.Text = _selectedTemplateVmEntry.VhdxSignature ?? string.Empty;
            RenderTemplateSwitchRowsFromVm();
            UpdateTemplateVhdxSelectorFromVm();
        }
        finally
        {
            _isUpdatingTemplateVmEditorControls = false;
        }
    }

    private bool TryApplySelectedTemplateVmFields(bool showSuccessStatus)
    {
        if (_activeTemplateEditorDocument is null || _selectedTemplateVmEntry is null || _isUpdatingTemplateVmEditorControls)
        {
            return true;
        }

        var vmName = TemplateVmNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vmName))
        {
            TemplateEditorStatusTextBlock.Text = "VM name is required.";
            return false;
        }

        if (!int.TryParse(TemplateVmMemoryTextBox.Text, out var memoryMb) || memoryMb <= 0)
        {
            TemplateEditorStatusTextBlock.Text = "Memory must be a positive integer.";
            return false;
        }

        if (!int.TryParse(TemplateVmCpuTextBox.Text, out var cpuCount) || cpuCount <= 0)
        {
            TemplateEditorStatusTextBlock.Text = "CPU count must be a positive integer.";
            return false;
        }

        _selectedTemplateVmEntry.Name = vmName;
        _selectedTemplateVmEntry.MemoryMb = memoryMb;
        _selectedTemplateVmEntry.CpuCount = cpuCount;
        if (!TryGetTemplateSelectedSwitches(out var selectedSwitches, out var switchValidationError))
        {
            TemplateEditorStatusTextBlock.Text = switchValidationError;
            return false;
        }

        _selectedTemplateVmEntry.SwitchNames = selectedSwitches.Count > 0 ? selectedSwitches : null;
        _selectedTemplateVmEntry.SwitchName = selectedSwitches.Count > 0 ? selectedSwitches[0] : null;

        if (TemplateVmVhdxCatalogComboBox.SelectedItem is TemplateVhdxCatalogOption selectedCatalogOption)
        {
            _selectedTemplateVmEntry.VhdxId = selectedCatalogOption.Id;
            _selectedTemplateVmEntry.VhdPath = selectedCatalogOption.Path;
            _selectedTemplateVmEntry.VhdxSignature = selectedCatalogOption.Signature;
        }
        else
        {
            var normalization = EvaluateTemplateVhdxNormalization(_selectedTemplateVmEntry);
            if (normalization.RequiresUserResolution)
            {
                TemplateEditorStatusTextBlock.Text = normalization.Message;
                return false;
            }

            if (normalization.EffectiveOption is not null)
            {
                _selectedTemplateVmEntry.VhdxId = normalization.EffectiveOption.Id;
                _selectedTemplateVmEntry.VhdPath = normalization.EffectiveOption.Path;
                _selectedTemplateVmEntry.VhdxSignature = normalization.EffectiveOption.Signature;
            }
        }

        RefreshTemplateVmListView();
        SyncTemplateVmEntriesToDocument();
        if (showSuccessStatus)
        {
            TemplateEditorStatusTextBlock.Text = $"Updated VM entry '{vmName}'.";
        }

        return true;
    }

    private void RenderTemplateSwitchRowsFromVm()
    {
        var switches = _selectedTemplateVmEntry?.SwitchNames?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList() ?? [];

        if (switches.Count == 0 && !string.IsNullOrWhiteSpace(_selectedTemplateVmEntry?.SwitchName))
        {
            switches.Add(_selectedTemplateVmEntry.SwitchName!.Trim());
        }

        RenderTemplateSwitchRows(switches);
    }

    private void RenderTemplateSwitchRows(IReadOnlyList<string> selectedSwitches)
    {
        _isUpdatingTemplateVmSwitchRows = true;
        try
        {
            TemplateVmSwitchRowsPanel.Children.Clear();
            _templateVmSwitchRowCombos.Clear();

            foreach (var switchName in selectedSwitches)
            {
                AddTemplateSwitchRow(switchName);
            }
        }
        finally
        {
            _isUpdatingTemplateVmSwitchRows = false;
        }

        UpdateTemplateSwitchGuidanceText();
    }

    private void AddTemplateSwitchRow(string? selectedSwitch)
    {
        var row = new Grid
        {
            ColumnSpacing = 8
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var combo = new ComboBox
        {
            MinWidth = 220
        };
        combo.Items.Add(TemplateSwitchPlaceholder);
        foreach (var switchName in _templateAvailableSwitches)
        {
            combo.Items.Add(switchName);
        }

        var validSelection = !string.IsNullOrWhiteSpace(selectedSwitch) &&
                             _templateAvailableSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase);
        combo.SelectedItem = validSelection ? selectedSwitch : TemplateSwitchPlaceholder;
        combo.SelectionChanged += TemplateVmSwitchRowCombo_SelectionChanged;
        _templateVmSwitchRowCombos.Add(combo);
        Grid.SetColumn(combo, 0);
        row.Children.Add(combo);

        var removeButton = new Button
        {
            Content = "-",
            Tag = combo
        };
        ToolTipService.SetToolTip(removeButton, "Remove switch");
        removeButton.Click += RemoveTemplateVmSwitchRowButton_Click;
        Grid.SetColumn(removeButton, 1);
        row.Children.Add(removeButton);

        TemplateVmSwitchRowsPanel.Children.Add(row);
    }

    private void AddTemplateVmSwitchRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateVmEntry is null)
        {
            TemplateEditorStatusTextBlock.Text = "Select a VM entry first.";
            return;
        }

        AddTemplateSwitchRow(null);
        UpdateTemplateSwitchGuidanceText();
    }

    private void RemoveTemplateVmSwitchRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ComboBox combo)
        {
            return;
        }

        _templateVmSwitchRowCombos.Remove(combo);

        var rowToRemove = TemplateVmSwitchRowsPanel.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => grid.Children.OfType<ComboBox>().Any(c => ReferenceEquals(c, combo)));
        if (rowToRemove is not null)
        {
            TemplateVmSwitchRowsPanel.Children.Remove(rowToRemove);
        }

        UpdateTemplateSwitchGuidanceText();
    }

    private void TemplateVmSwitchRowCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTemplateVmSwitchRows)
        {
            return;
        }

        UpdateTemplateSwitchGuidanceText();
    }

    private void TemplateVmVhdxCatalogComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTemplateVhdxSelector || _selectedTemplateVmEntry is null)
        {
            return;
        }

        if (TemplateVmVhdxCatalogComboBox.SelectedItem is TemplateVhdxCatalogOption selectedCatalogOption)
        {
            TemplateVmVhdxIdTextBox.Text = selectedCatalogOption.Id;
            TemplateVmVhdPathTextBox.Text = selectedCatalogOption.Path;
            TemplateVmVhdxSignatureTextBox.Text = selectedCatalogOption.Signature ?? string.Empty;
            var normalization = EvaluateTemplateVhdxNormalization(_selectedTemplateVmEntry, selectedCatalogOption);
            TemplateVmVhdxGuidanceTextBlock.Text = $"{normalization.EffectiveSourceLabel} Catalog entry selected. Save to persist.";
            return;
        }

        UpdateTemplateVhdxSelectorGuidance();
    }

    private bool TryGetTemplateSelectedSwitches(out List<string> selectedSwitches, out string? validationError)
    {
        selectedSwitches = [];
        validationError = null;

        if (_templateVmSwitchRowCombos.Count == 0)
        {
            return true;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var combo in _templateVmSwitchRowCombos)
        {
            var selected = combo.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(selected) || string.Equals(selected, TemplateSwitchPlaceholder, StringComparison.Ordinal))
            {
                validationError = "Each switch row must have a selected host switch or be removed.";
                return false;
            }

            if (!_templateAvailableSwitches.Contains(selected, StringComparer.OrdinalIgnoreCase))
            {
                validationError = $"Switch '{selected}' is not available on this host.";
                return false;
            }

            if (!seen.Add(selected))
            {
                validationError = $"Duplicate switch '{selected}' is not allowed.";
                return false;
            }

            selectedSwitches.Add(selected);
        }

        return true;
    }

    private void UpdateTemplateSwitchGuidanceText()
    {
        if (_selectedTemplateVmEntry is null)
        {
            TemplateVmSwitchGuidanceTextBlock.Text = "Select a VM entry to configure switch assignments.";
            return;
        }

        if (_templateAvailableSwitches.Count == 0)
        {
            TemplateVmSwitchGuidanceTextBlock.Text = "No host switches available. Add a host switch before assigning VM switch rows.";
            return;
        }

        if (_templateVmSwitchRowCombos.Count == 0)
        {
            TemplateVmSwitchGuidanceTextBlock.Text = "No switch rows. Optional for template VM.";
            return;
        }

        if (!TryGetTemplateSelectedSwitches(out _, out var error))
        {
            TemplateVmSwitchGuidanceTextBlock.Text = error ?? "Switch selection requires attention.";
            return;
        }

        TemplateVmSwitchGuidanceTextBlock.Text = "Switch rows configured.";
    }

    private void UpdateTemplateVhdxSelectorFromVm()
    {
        _isUpdatingTemplateVhdxSelector = true;
        try
        {
            TemplateVmVhdxCatalogComboBox.ItemsSource = null;
            var options = new List<object> { TemplateVhdxPlaceholder };
            options.AddRange(_templateVhdxCatalogOptions);
            TemplateVmVhdxCatalogComboBox.ItemsSource = options;

            if (_selectedTemplateVmEntry is null)
            {
                TemplateVmVhdxCatalogComboBox.SelectedItem = TemplateVhdxPlaceholder;
                TemplateVmVhdxGuidanceTextBlock.Text = "Select a VM entry to configure base disk.";
                return;
            }

            var normalization = EvaluateTemplateVhdxNormalization(_selectedTemplateVmEntry);
            object selectedItem = normalization.RequiresUserResolution || normalization.EffectiveOption is null
                ? TemplateVhdxPlaceholder
                : normalization.EffectiveOption;
            TemplateVmVhdxCatalogComboBox.SelectedItem = selectedItem;
            UpdateTemplateVhdxSelectorGuidance(normalization);
        }
        finally
        {
            _isUpdatingTemplateVhdxSelector = false;
        }
    }

    private void UpdateTemplateVhdxSelectorGuidance()
    {
        if (_selectedTemplateVmEntry is null)
        {
            TemplateVmVhdxGuidanceTextBlock.Text = "Select a VM entry to configure base disk.";
            return;
        }

        var normalization = EvaluateTemplateVhdxNormalization(_selectedTemplateVmEntry);
        UpdateTemplateVhdxSelectorGuidance(normalization);
    }

    private void UpdateTemplateVhdxSelectorGuidance(TemplateVhdxNormalizationResult normalization)
    {
        if (_selectedTemplateVmEntry is null)
        {
            TemplateVmVhdxGuidanceTextBlock.Text = "Select a VM entry to configure base disk.";
            return;
        }

        if (_templateVhdxCatalogOptions.Count == 0)
        {
            TemplateVmVhdxGuidanceTextBlock.Text = "No catalog entries available. Import base disks in Assets > Base Disks.";
            return;
        }

        if (normalization.RequiresUserResolution)
        {
            TemplateVmVhdxGuidanceTextBlock.Text = normalization.Message;
            return;
        }

        if (normalization.EffectiveOption is not null)
        {
            TemplateVmVhdxGuidanceTextBlock.Text = $"{normalization.EffectiveSourceLabel} Effective disk: {normalization.EffectiveOption.DisplayLabel} ({normalization.EffectiveOption.Id}).";
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedTemplateVmEntry.VhdPath))
        {
            TemplateVmVhdxGuidanceTextBlock.Text = "Legacy path-based reference loaded. Select a catalog entry to normalize.";
            return;
        }

        TemplateVmVhdxGuidanceTextBlock.Text = "Catalog-backed selection is preferred.";
    }

    private TemplateVhdxNormalizationResult EvaluateTemplateVhdxNormalization(
        VmTemplate vmTemplate,
        TemplateVhdxCatalogOption? userSelection = null)
    {
        var idMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdxId)
            ? null
            : _templateVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, vmTemplate.VhdxId, StringComparison.OrdinalIgnoreCase));

        var signatureMatches = string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature)
            ? []
            : _templateVhdxCatalogOptions
                .Where(option => !string.IsNullOrWhiteSpace(option.Signature) &&
                                 string.Equals(option.Signature, vmTemplate.VhdxSignature, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var pathMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdPath)
            ? null
            : _templateVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, vmTemplate.VhdPath, StringComparison.OrdinalIgnoreCase));

        if (userSelection is not null)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: userSelection,
                Message: "User selected replacement catalog entry.",
                EffectiveSourceLabel: "Effective source: selected catalog.");
        }

        if (idMatch is not null)
        {
            var pathConflict = !string.IsNullOrWhiteSpace(vmTemplate.VhdPath)
                               && !string.Equals(vmTemplate.VhdPath, idMatch.Path, StringComparison.OrdinalIgnoreCase);
            var signatureConflict = !string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature)
                                    && !string.IsNullOrWhiteSpace(idMatch.Signature)
                                    && !string.Equals(vmTemplate.VhdxSignature, idMatch.Signature, StringComparison.OrdinalIgnoreCase);
            if (pathConflict || signatureConflict)
            {
                return new TemplateVhdxNormalizationResult(
                    RequiresUserResolution: true,
                    EffectiveOption: null,
                    Message: "VHD identity conflict detected. Select a catalog entry to resolve before saving.",
                    EffectiveSourceLabel: "Effective source: unresolved.");
            }

            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: idMatch,
                Message: "Resolved from vhdxId.",
                EffectiveSourceLabel: "Effective source: vhdxId.");
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdxId))
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: true,
                EffectiveOption: null,
                Message: $"Catalog entry '{vmTemplate.VhdxId}' is missing. Select a replacement before saving.",
                EffectiveSourceLabel: "Effective source: unresolved.");
        }

        if (signatureMatches.Count > 1)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: true,
                EffectiveOption: null,
                Message: "Multiple catalog entries match vhdxSignature. Select one entry before saving.",
                EffectiveSourceLabel: "Effective source: unresolved.");
        }

        if (signatureMatches.Count == 1)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: signatureMatches[0],
                Message: "Resolved from vhdxSignature.",
                EffectiveSourceLabel: "Effective source: vhdxSignature.");
        }

        if (pathMatch is not null)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: pathMatch,
                Message: "Resolved from vhdPath.",
                EffectiveSourceLabel: "Effective source: vhdPath.");
        }

        return new TemplateVhdxNormalizationResult(
            RequiresUserResolution: false,
            EffectiveOption: null,
            Message: "No matching catalog entry found; keeping path-first reference.",
            EffectiveSourceLabel: "Effective source: legacy path.");
    }

    private Task<string?> PickTemplateFileForOpenAsync()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var selectedPath = NativeFileDialogs.ShowOpenJsonDialog(hwnd);
        return Task.FromResult(selectedPath);
    }

    private Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var selectedPath = NativeFileDialogs.ShowSaveJsonDialog(hwnd, suggestedFileName);
        return Task.FromResult(selectedPath);
    }

    private void TemplateLibraryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTemplateLibraryItem = TemplateLibraryListView.SelectedItem as TemplateLibraryItem;
        UpdateTemplatesUi();
    }

    private void TemplateVmListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTemplateVmEntry = TemplateVmListView.SelectedItem as VmTemplate;
        UpdateTemplateVmEditorPanel();
        UpdateTemplatesUi();
    }

    private void AddTemplateVmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "Load or create a template first.";
            return;
        }

        var nextVmNumber = _templateVmEntries.Count + 1;
        var vmEntry = new VmTemplate
        {
            Name = $"VM-{nextVmNumber}",
            MemoryMb = 2048,
            CpuCount = 2
        };
        _templateVmEntries.Add(vmEntry);
        _selectedTemplateVmEntry = vmEntry;
        TemplateVmListView.SelectedItem = vmEntry;
        SyncTemplateVmEntriesToDocument();
        UpdateTemplateVmEditorPanel();
        TemplateEditorStatusTextBlock.Text = $"Added VM entry '{vmEntry.Name}'.";
        UpdateTemplatesUi();
    }

    private async void RemoveTemplateVmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateVmEntry is null)
        {
            TemplateEditorStatusTextBlock.Text = "Select a VM entry first.";
            return;
        }

        var vmName = _selectedTemplateVmEntry.Name;
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove VM entry '{vmName}' from this template draft?",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _templateVmEntries.Remove(_selectedTemplateVmEntry);
        _selectedTemplateVmEntry = _templateVmEntries.FirstOrDefault();
        TemplateVmListView.SelectedItem = _selectedTemplateVmEntry;
        SyncTemplateVmEntriesToDocument();
        UpdateTemplateVmEditorPanel();
        TemplateEditorStatusTextBlock.Text = $"Removed VM entry '{vmName}'.";
        UpdateTemplatesUi();
    }

    private void ApplyTemplateVmChangesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateVmEntry is null)
        {
            TemplateEditorStatusTextBlock.Text = "Select a VM entry first.";
            return;
        }

        TryApplySelectedTemplateVmFields(showSuccessStatus: true);
        UpdateTemplatesUi();
    }

    private async void ApplyTemplateSearchButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureTemplatesLibraryAsync(forceRefresh: true);
    }

    private async void ClearTemplateSearchButton_Click(object sender, RoutedEventArgs e)
    {
        TemplateSearchTextBox.Text = string.Empty;
        await EnsureTemplatesLibraryAsync(forceRefresh: true);
    }

    private async void ReloadTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureTemplatesLibraryAsync(forceRefresh: true);
    }

    private async void OpenTemplateInEditorButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenSelectedTemplateInEditorAsync();
    }

    private async void CreateTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            _activeTemplateEditorDocument = await _templatesCapabilityService.CreateDraftAsync();
            await EnsureTemplateSwitchesAsync(forceRefresh: false);
            await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh: false);
            BindTemplateEditorDocument();
            TemplateEditorStatusTextBlock.Text = "New template draft created.";
            NavigateToRoute(ShellRouteKeys.TemplatesEditor);
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateLibraryItem is null)
        {
            TemplatesLibraryStatusTextBlock.Text = "Select a template first.";
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Delete Template",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            Content = $"Delete '{_selectedTemplateLibraryItem.Name}'? This removes the template file.",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.DeleteAsync(_selectedTemplateLibraryItem.FilePath);
            TemplatesLibraryStatusTextBlock.Text = result.UserMessage;
            if (result.Success)
            {
                _selectedTemplateLibraryItem = null;
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void ImportTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var sourcePath = await PickTemplateFileForOpenAsync();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            TemplatesLibraryStatusTextBlock.Text = "Import cancelled.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.ImportAsync(sourcePath);
            TemplatesLibraryStatusTextBlock.Text = result.UserMessage;
            if (result.Success)
            {
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void ExportTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateLibraryItem is null)
        {
            TemplatesLibraryStatusTextBlock.Text = "Select a template first.";
            return;
        }

        var suggestedName = Path.GetFileName(_selectedTemplateLibraryItem.FilePath);
        var destinationPath = await PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            TemplatesLibraryStatusTextBlock.Text = "Export cancelled.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.ExportAsync(_selectedTemplateLibraryItem.FilePath, destinationPath);
            TemplatesLibraryStatusTextBlock.Text = result.UserMessage;
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void SaveTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "No template loaded.";
            return;
        }

        if (!PullEditorFieldsIntoDocument())
        {
            return;
        }
        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_activeTemplateEditorDocument);
            TemplateEditorStatusTextBlock.Text = result.UserMessage;
            if (result.Success)
            {
                _activeTemplateEditorDocument = new TemplateEditorDocument
                {
                    Template = _activeTemplateEditorDocument.Template,
                    SourceFilePath = result.FilePath
                };
                BindTemplateEditorDocument();
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void SaveTemplateAsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "No template loaded.";
            return;
        }

        if (!PullEditorFieldsIntoDocument())
        {
            return;
        }
        var suggestedName = string.IsNullOrWhiteSpace(_activeTemplateEditorDocument.Template.Name)
            ? "lab-template"
            : _activeTemplateEditorDocument.Template.Name;
        var destinationPath = await PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            TemplateEditorStatusTextBlock.Text = "Save As cancelled.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_activeTemplateEditorDocument, destinationPath, saveAs: true);
            TemplateEditorStatusTextBlock.Text = result.UserMessage;
            if (result.Success)
            {
                _activeTemplateEditorDocument = new TemplateEditorDocument
                {
                    Template = _activeTemplateEditorDocument.Template,
                    SourceFilePath = result.FilePath
                };
                BindTemplateEditorDocument();
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void ValidateTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "No template loaded.";
            return;
        }

        if (!PullEditorFieldsIntoDocument())
        {
            return;
        }
        var result = await _templatesCapabilityService.ValidateAsync(_activeTemplateEditorDocument);
        if (result.IsValid)
        {
            TemplateEditorStatusTextBlock.Text = "Template validation passed.";
            return;
        }

        TemplateEditorStatusTextBlock.Text = "Validation failed: " + string.Join(" ", result.Errors);
    }

    private void BackToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToRoute(ShellRouteKeys.TemplatesLibrary);
    }

    private void InitializeRdpReadinessTimer()
    {
        _rdpReadinessTimer = DispatcherQueue.CreateTimer();
        _rdpReadinessTimer.Interval = TimeSpan.FromMinutes(5);
        _rdpReadinessTimer.Tick += async (_, _) => await _machinesWorkspaceComposition.RefreshRdpReadinessAsync(selectedOnly: false);
    }

    private void UpdateReadinessPollingState()
    {
        if (_rdpReadinessTimer is null)
        {
            return;
        }

        if (IsMachinesOverviewActive && _machinesWorkspaceComposition.HasInventory)
        {
            if (!_rdpReadinessTimer.IsRunning)
            {
                _rdpReadinessTimer.Start();

                // Run one pass when Machines becomes active, then fall back to periodic checks.
                if (DateTimeOffset.UtcNow - _machinesWorkspaceComposition.LastRdpReadinessRefreshUtc >= _rdpReadinessTimer.Interval)
                {
                    _ = _machinesWorkspaceComposition.RefreshRdpReadinessAsync(selectedOnly: false);
                }
            }

            return;
        }

        if (_rdpReadinessTimer.IsRunning)
        {
            _rdpReadinessTimer.Stop();
        }

    }

    private async Task EnsureStructuredLogsLoadedAsync(bool forceReload)
    {
        if (!IsDiagnosticsLogsActive || _isStructuredLogsLoading)
        {
            return;
        }

        if (!forceReload && _structuredLogEntries.Count > 0)
        {
            return;
        }

        _isStructuredLogsLoading = true;
        ApplyLogFiltersButton.IsEnabled = false;
        ClearLogFiltersButton.IsEnabled = false;
        ReloadLogsButton.IsEnabled = false;
        OpenRawJsonlButton.IsEnabled = false;
        LogsStatusTextBlock.Text = "Loading structured logs...";

        try
        {
            var filter = BuildStructuredLogFilter();
            var result = await _structuredLogViewerService.LoadAsync(filter);

            _structuredLogEntries.Clear();
            foreach (var entry in result.Entries)
            {
                _structuredLogEntries.Add(entry);
            }

            StructuredLogsListView.SelectedItem = null;
            _selectedStructuredLogEntry = null;
            UpdateStructuredLogSelectionDetails();

            var filePath = _structuredLogViewerService.GetStructuredLogFilePath();
            var parseErrorSuffix = result.ParseErrorCount > 0
                ? $" Skipped malformed lines: {result.ParseErrorCount}."
                : string.Empty;
            LogsStatusTextBlock.Text = File.Exists(filePath)
                ? $"Loaded {_structuredLogEntries.Count} events from {result.TotalLineCount} lines.{parseErrorSuffix}"
                : $"Structured log file not found yet: {filePath}";
        }
        catch (Exception ex)
        {
            LogsStatusTextBlock.Text = $"Failed to load structured logs. {ex.Message}";
        }
        finally
        {
            _isStructuredLogsLoading = false;
            ApplyLogFiltersButton.IsEnabled = true;
            ClearLogFiltersButton.IsEnabled = true;
            ReloadLogsButton.IsEnabled = true;
            OpenRawJsonlButton.IsEnabled = true;
        }
    }

    private StructuredLogViewerFilter BuildStructuredLogFilter()
    {
        return new StructuredLogViewerFilter
        {
            OperationId = NormalizeFilterText(LogFilterOperationIdTextBox.Text),
            Level = NormalizeFilterText(LogFilterLevelTextBox.Text),
            Event = NormalizeFilterText(LogFilterEventTextBox.Text),
            TextSearch = NormalizeFilterText(LogFilterTextSearchTextBox.Text),
            StartUtc = LogFilterUseStartDateCheckBox.IsChecked == true
                ? ToDateBoundaryUtc(LogFilterStartDatePicker.Date, isEndBoundary: false)
                : null,
            EndUtc = LogFilterUseEndDateCheckBox.IsChecked == true
                ? ToDateBoundaryUtc(LogFilterEndDatePicker.Date, isEndBoundary: true)
                : null
        };
    }

    private void UpdateStructuredLogSelectionDetails()
    {
        if (_selectedStructuredLogEntry is null)
        {
            SelectedLogEnvelopeTextBlock.Text = "Select a log entry.";
            SelectedLogContextTextBox.Text = string.Empty;
            return;
        }

        SelectedLogEnvelopeTextBlock.Text =
            $"ts={_selectedStructuredLogEntry.TimestampText} | level={_selectedStructuredLogEntry.Level} | event={_selectedStructuredLogEntry.Event} | operationId={_selectedStructuredLogEntry.OperationId} | result={_selectedStructuredLogEntry.Result}";
        SelectedLogContextTextBox.Text = FormatJsonForDetails(_selectedStructuredLogEntry.ContextJson);
    }

    private static string NormalizeFilterText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private static DateTimeOffset ToDateBoundaryUtc(DateTimeOffset date, bool isEndBoundary)
    {
        var selectedDate = date.Date;
        var localBoundary = isEndBoundary
            ? selectedDate.AddDays(1).AddTicks(-1)
            : selectedDate;
        return localBoundary.ToUniversalTime();
    }

    private static string FormatJsonForDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return json;
        }
    }

    private async void ReloadLogsButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ApplyLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ClearLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        LogFilterOperationIdTextBox.Text = string.Empty;
        LogFilterLevelTextBox.Text = string.Empty;
        LogFilterEventTextBox.Text = string.Empty;
        LogFilterTextSearchTextBox.Text = string.Empty;
        LogFilterUseStartDateCheckBox.IsChecked = false;
        LogFilterUseEndDateCheckBox.IsChecked = false;
        LogFilterStartDatePicker.Date = DateTimeOffset.Now;
        LogFilterEndDatePicker.Date = DateTimeOffset.Now;
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private void StructuredLogsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedStructuredLogEntry = StructuredLogsListView.SelectedItem as StructuredLogViewerEntry;
        UpdateStructuredLogSelectionDetails();
    }

    private void OpenRawJsonlButton_Click(object sender, RoutedEventArgs e)
    {
        OpenStructuredLogLocation();
    }

    private void OpenStructuredLogLocation()
    {
        var filePath = _structuredLogViewerService.GetStructuredLogFilePath();
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            var folderPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
                {
                    UseShellExecute = true
                });
                LogsStatusTextBlock.Text = $"Active structured log file not found. Opened log folder: {folderPath}";
                return;
            }

            LogsStatusTextBlock.Text = $"Structured log path does not exist yet: {filePath}";
        }
        catch (Exception ex)
        {
            LogsStatusTextBlock.Text = $"Failed to open structured log location. {ex.Message}";
        }
    }

    private async Task LoadMachinesDeletionPolicyAsync()
    {
        if (!IsSettingsMachinesActive || _isSavingDeletionPolicy)
        {
            return;
        }

        try
        {
            var mode = await _machinesCapabilityService.GetDeletionPolicyAsync();
            var item = MachinesDeletionPolicyComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(candidate => string.Equals(candidate.Tag?.ToString(), mode.ToString(), StringComparison.Ordinal));
            MachinesDeletionPolicyComboBox.SelectedItem = item;
            MachinesDeletionPolicyStatusTextBlock.Text = $"Current: {item?.Content ?? mode.ToString()}";
        }
        catch (Exception ex)
        {
            MachinesDeletionPolicyStatusTextBlock.Text = $"Failed to load policy. {ex.Message}";
        }
    }

    /* obsolete_machines_delete_scope_dialog_signature
    {
        var vmOnlyRadio = new RadioButton
        {
            Content = "VM registration only",
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmRegistrationOnly
        };
        var vmAndStorageRadio = new RadioButton
        {
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmAndStorage,
            Content = "VM + associated disks/files"
        };
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Choose delete scope. This action is destructive.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} — {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var disk in preview.DiskClassifications)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Disk: {disk.DiskPath} | {disk.Classification} ({disk.Reason})",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
            });
        }

        content.Children.Add(vmOnlyRadio);
        content.Children.Add(vmAndStorageRadio);
        content.Children.Add(new TextBlock
        {
            Text = "If deleting with storage, associated disks/files will be removed where possible.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return vmAndStorageRadio.IsChecked == true
            ? MachineDeleteScope.VmAndStorage
            : MachineDeleteScope.VmRegistrationOnly;
    }

    obsolete_machines_delete_confirmation_dialog_signature(
        MachineInventoryItem vm,
        MachineDeletePreview preview,
        MachineDeleteScope effectiveScope)
    {
        var scopeText = effectiveScope == MachineDeleteScope.VmAndStorage
            ? "VM + associated disks/files"
            : "VM registration only";
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"Effective delete scope: {scopeText}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} — {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    */

    private sealed class TemplateVhdxCatalogOption
    {
        public TemplateVhdxCatalogOption(
            string id,
            string path,
            string osName,
            string osVersion,
            int generation,
            string? signature)
        {
            Id = id;
            Path = path;
            OsName = osName;
            OsVersion = osVersion;
            Generation = generation;
            Signature = signature;
        }

        public string Id { get; }

        public string Path { get; }

        public string OsName { get; }

        public string OsVersion { get; }

        public int Generation { get; }

        public string? Signature { get; }

        public string DisplayLabel => string.IsNullOrWhiteSpace(OsName) && string.IsNullOrWhiteSpace(OsVersion)
            ? $"{Id} (Gen{Generation})"
            : $"{OsName} {OsVersion} (Gen{Generation})";

        public override string ToString() => $"{DisplayLabel} - {Id}";
    }

    private sealed record TemplateVhdxNormalizationResult(
        bool RequiresUserResolution,
        TemplateVhdxCatalogOption? EffectiveOption,
        string Message,
        string EffectiveSourceLabel);

    private sealed record DeployCompatibilityIssue(
        string VmName,
        bool IsBlocking,
        string Message,
        string Guidance);

    private sealed record DeployDiskResolution(
        string EffectiveBasePath,
        string? EffectiveId,
        string? EffectiveSignature,
        IReadOnlyList<DeployCompatibilityIssue> Issues);

    private sealed record DeploySwitchResolution(
        string EffectiveSwitch,
        IReadOnlyList<DeployCompatibilityIssue> Issues);

    private sealed record DeployContextBuildResult(
        MultiVmDeploymentContext MultiVmContext,
        IReadOnlyList<DeployCompatibilityIssue> CompatibilityIssues);


    private sealed record DeployTimelineStepDefinition(
        string StepKey,
        string Label);

    private sealed record DeployTimelineStepRow(
        string Label,
        DeployTimelineStepState State)
    {
        public bool IsRunning => State == DeployTimelineStepState.Running;

        public Visibility RunningIndicatorVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

        public Visibility StaticIconVisibility => IsRunning ? Visibility.Collapsed : Visibility.Visible;

        public string IconGlyph => DeployTimelineIconCatalog.GetGlyph(State);
    }

    private sealed class DeployVmProgressState
    {
        private readonly Dictionary<string, DeployTimelineStepState> _stepStatesByKey;
        private readonly Dictionary<string, string> _stepLabelsByKey;
        private readonly List<string> _stepOrder;

        public DeployVmProgressState(string vmName, IReadOnlyList<DeployTimelineStepDefinition> expectedSteps)
        {
            VmName = vmName;
            _stepOrder = expectedSteps.Select(step => step.StepKey).ToList();
            _stepLabelsByKey = expectedSteps.ToDictionary(
                step => step.StepKey,
                step => step.Label,
                StringComparer.OrdinalIgnoreCase);
            _stepStatesByKey = expectedSteps.ToDictionary(
                step => step.StepKey,
                _ => DeployTimelineStepState.Pending,
                StringComparer.OrdinalIgnoreCase);
            Status = "Queued";
            Summary = "Queued for deployment.";
            ProgressPercent = 0;
        }

        public string VmName { get; }

        public string Status { get; private set; }

        public string Summary { get; private set; }

        public int ProgressPercent { get; private set; }

        public void ApplyStepStateUpdate(DeployStepStateUpdate update)
        {
            if (string.IsNullOrWhiteSpace(update.StepKey))
            {
                return;
            }

            if (!_stepStatesByKey.ContainsKey(update.StepKey))
            {
                _stepStatesByKey[update.StepKey] = DeployTimelineStepState.Pending;
                _stepOrder.Add(update.StepKey);
            }

            if (!string.IsNullOrWhiteSpace(update.StepLabel))
            {
                _stepLabelsByKey[update.StepKey] = update.StepLabel;
            }

            var mappedState = MapState(update.State);
            var currentState = _stepStatesByKey[update.StepKey];
            if (IsTerminal(currentState) && !IsTerminal(mappedState))
            {
                return;
            }

            _stepStatesByKey[update.StepKey] = mappedState;
            Status = mappedState switch
            {
                DeployTimelineStepState.Pending => "Queued",
                DeployTimelineStepState.Running => "Running",
                DeployTimelineStepState.Succeeded => "Succeeded",
                DeployTimelineStepState.Failed => "Failed",
                DeployTimelineStepState.Skipped => "Skipped",
                _ => Status
            };
            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                Summary = update.Message.Trim();
            }
            else if (string.IsNullOrWhiteSpace(Summary) || Status == "Queued")
            {
                Summary = $"{ResolveStepLabel(update.StepKey)}: {Status}";
            }

            RefreshProgress();
        }

        public void UpdateSummaryMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Summary = message.Trim();
        }

        public void MarkCompleted(string status, string summary)
        {
            var isFailed = status.Contains("failed", StringComparison.OrdinalIgnoreCase);
            var isCancelled = status.Contains("cancel", StringComparison.OrdinalIgnoreCase);
            var hasStepFailure = _stepStatesByKey.Values.Any(state => state == DeployTimelineStepState.Failed);
            var effectiveFailed = isFailed || hasStepFailure;

            foreach (var stepKey in _stepStatesByKey.Keys.ToList())
            {
                var state = _stepStatesByKey[stepKey];
                if (state == DeployTimelineStepState.Running)
                {
                    _stepStatesByKey[stepKey] = effectiveFailed ? DeployTimelineStepState.Failed : isCancelled ? DeployTimelineStepState.Skipped : DeployTimelineStepState.Succeeded;
                }
                else if (state == DeployTimelineStepState.Pending)
                {
                    _stepStatesByKey[stepKey] = isCancelled ? DeployTimelineStepState.Skipped : effectiveFailed ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded;
                }
            }

            Status = effectiveFailed && !status.Contains("failed", StringComparison.OrdinalIgnoreCase)
                ? "Failed"
                : status;
            Summary = summary;
            ProgressPercent = 100;
        }

        public DeployVmResultRow ToRow()
        {
            var timelineSteps = _stepOrder
                .Select(stepKey => new DeployTimelineStepRow(ResolveStepLabel(stepKey), _stepStatesByKey[stepKey]))
                .Where(step => step.State != DeployTimelineStepState.Skipped)
                .ToList();

            return new DeployVmResultRow(
                VmName: VmName,
                Status: Status,
                Summary: Summary,
                ProgressPercent: ProgressPercent,
                TimelineSteps: timelineSteps);
        }

        private void RefreshProgress()
        {
            if (Status == "Failed")
            {
                ProgressPercent = 100;
                return;
            }

            var visibleStates = _stepStatesByKey.Values.Where(state => state != DeployTimelineStepState.Skipped).ToList();
            if (visibleStates.Count == 0)
            {
                ProgressPercent = 50;
                return;
            }

            var terminalSteps = visibleStates.Count(state =>
                state == DeployTimelineStepState.Succeeded || state == DeployTimelineStepState.Failed);
            var rawProgress = (int)Math.Round((double)terminalSteps / visibleStates.Count * 100d, MidpointRounding.AwayFromZero);
            ProgressPercent = Math.Clamp(rawProgress, 5, 99);
        }

        private string ResolveStepLabel(string stepKey)
        {
            return _stepLabelsByKey.TryGetValue(stepKey, out var label) && !string.IsNullOrWhiteSpace(label)
                ? label
                : stepKey;
        }

        private static DeployTimelineStepState MapState(DeployStepState state)
        {
            return state switch
            {
                DeployStepState.Pending => DeployTimelineStepState.Pending,
                DeployStepState.Running => DeployTimelineStepState.Running,
                DeployStepState.Succeeded => DeployTimelineStepState.Succeeded,
                DeployStepState.Failed => DeployTimelineStepState.Failed,
                DeployStepState.Skipped => DeployTimelineStepState.Skipped,
                _ => DeployTimelineStepState.Pending
            };
        }

        private static bool IsTerminal(DeployTimelineStepState state)
        {
            return state is DeployTimelineStepState.Succeeded or DeployTimelineStepState.Failed or DeployTimelineStepState.Skipped;
        }
    }

    private sealed record DeployVmResultRow(
        string VmName,
        string Status,
        string Summary,
        int ProgressPercent,
        IReadOnlyList<DeployTimelineStepRow> TimelineSteps,
        bool IsExpandable = true)
    {
        public string DisplaySummary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Summary))
                {
                    return string.Empty;
                }

                var normalizedStatus = Status.Trim();
                var normalizedSummary = Summary.Trim();
                if (string.Equals(normalizedSummary, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if ((string.Equals(normalizedStatus, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(normalizedStatus, "Completed", StringComparison.OrdinalIgnoreCase)) &&
                    normalizedSummary.StartsWith("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                return normalizedSummary;
            }
        }

        public Visibility SummaryVisibility =>
            string.IsNullOrWhiteSpace(DisplaySummary) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ExpanderVisibility => IsExpandable ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FlatCardVisibility => IsExpandable ? Visibility.Collapsed : Visibility.Visible;
    }


    private sealed record DeployIssueRow(
        string Scope,
        string Severity,
        string Message)
    {
        public override string ToString() => $"{Severity} [{Scope}] {Message}";
    }
}
