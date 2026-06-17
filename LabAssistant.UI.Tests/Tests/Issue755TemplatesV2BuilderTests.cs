using System.Xml.Linq;
using System.Text.Json;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue755TemplatesV2BuilderTests
{
    [Fact]
    public void ShellViewModel_ResolvesTemplatesBuilder_AsDistinctWorkflowRoute()
    {
        var shell = new ShellViewModel();

        Assert.True(shell.TryResolveRoute(ShellRouteKeys.TemplatesBuilder, out var capability, out var subview));
        Assert.Equal("templates", capability.Key);
        Assert.Equal("Builder", subview.DisplayName);
        Assert.Equal(ShellRouteKeys.TemplatesLibrary, capability.DefaultSubview.RouteKey);
    }

    [Fact]
    public void TemplatesLibrary_ExposesBuilderEntryPoints_WhilePreservingEditorActions()
    {
        var library = LoadXaml(Path.Combine("Views", "Templates", "TemplatesLibraryView.xaml"));

        Assert.Equal("Open in Editor", FindByName(library, "OpenTemplateInEditorButton").Attribute("Content")?.Value);
        Assert.Equal("Create New", FindByName(library, "CreateTemplateButton").Attribute("Content")?.Value);
        Assert.Equal("Open in Builder", FindByName(library, "OpenTemplateInBuilderButton").Attribute("Content")?.Value);
        Assert.Equal("Create V2 Builder", FindByName(library, "CreateV2BuilderTemplateButton").Attribute("Content")?.Value);
    }

    [Fact]
    public void MainWindow_HostsBuilderRoute_WithoutOwningBuilderWorkflowState()
    {
        var mainWindowXaml = LoadXaml("MainWindow.xaml");
        var mainWindowSource = File.ReadAllText(WinUIPath("MainWindow.xaml.cs"));

        Assert.NotNull(FindByName(mainWindowXaml, "TemplatesBuilderViewHost"));
        Assert.Contains("IsTemplatesBuilderActive", mainWindowSource);
        Assert.DoesNotContain("TemplatesBuilderWorkspaceViewModel", mainWindowSource);
        Assert.DoesNotContain("TemplatesBuilderWorkspaceController", mainWindowSource);
    }

    [Fact]
    public void TemplatesBuilderView_UsesStepWorkflow_WithOneActivePanel()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var builderModels = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderDraftModels.cs")));

        Assert.NotNull(FindByName(builder, "BuilderLeftStepper"));
        Assert.NotNull(FindByName(builder, "BuilderWorkflowTreePanel"));
        Assert.NotNull(FindByName(builder, "BuilderActiveStepPanel"));
        Assert.NotNull(FindByName(builder, "BuilderGeneralSection"));
        Assert.NotNull(FindByName(builder, "BuilderNetworksSection"));
        Assert.NotNull(FindByName(builder, "BuilderForestsDomainsSection"));
        Assert.NotNull(FindByName(builder, "BuilderCredentialsSection"));
        Assert.NotNull(FindByName(builder, "BuilderVmsSection"));
        Assert.NotNull(FindByName(builder, "BuilderReviewSection"));
        Assert.DoesNotContain(builder.Descendants().Attributes().Select(attribute => attribute.Value), value => value == "BuilderProfileSection");
        Assert.Null(FindByNameOrDefault(builder, "BuilderGeneralStepButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderNetworksStepButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderForestsDomainsStepButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderCredentialsStepButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderVmsStepButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderReviewStepButton"));
        Assert.Contains("CreateResourceButton(\"General\"", builderSource);
        Assert.Contains("CreateResourceButton(\"Networks\"", builderSource);
        Assert.Contains("CreateResourceButton(\"Forests & Domains\"", builderSource);
        Assert.Contains("CreateResourceButton(\"Credentials\"", builderSource);
        Assert.Contains("CreateResourceButton(\"VMs\"", builderSource);
        Assert.Contains("CreateResourceButton(\"Review\"", builderSource);
        Assert.Contains("ShellAccentBrush", builderSource);
        Assert.Contains("ShellBackgroundBrush", builderSource);
        Assert.Contains("BuilderWorkflowStep.General", builderSource);
        Assert.Contains("BuilderWorkflowStep.Networks", builderSource);
        Assert.Contains("BuilderWorkflowStep.ForestsDomains", builderSource);
        Assert.Contains("BuilderWorkflowStep.Credentials", builderSource);
        Assert.Contains("BuilderWorkflowStep.Vms", builderSource);
        Assert.Contains("BuilderWorkflowStep.Review", builderSource);
        Assert.DoesNotContain("BuilderProfileSection", builderSource);
        Assert.DoesNotContain("BuilderNetworksPanel", builder.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("BuilderDomainsPanel", builder.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("BuilderVmsPanel", builder.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("BuilderVmsPanel", builderSource, StringComparison.Ordinal);
        Assert.NotNull(FindByName(builder, "BuilderNetworksListPanel"));
        Assert.NotNull(FindByName(builder, "BuilderSelectedNetworkDetailPanel"));
        Assert.NotNull(FindByName(builder, "BuilderForestDomainResourcesListPanel"));
        Assert.NotNull(FindByName(builder, "BuilderSelectedForestDomainDetailPanel"));
        Assert.NotNull(FindByName(builder, "BuilderCredentialSlotsListPanel"));
        Assert.NotNull(FindByName(builder, "BuilderSelectedCredentialSlotDetailPanel"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderVmNameListPanel"));
        Assert.NotNull(FindByName(builder, "BuilderVmNavChildrenPanel"));
        Assert.Contains("CreateVmWorkflowNavRow", builderSource);
        Assert.Contains("Content = \"+\"", builderSource);
        Assert.Contains("ToolTipService.SetToolTip(addButton, \"Add VM\")", builderSource);
        Assert.NotNull(FindByName(builder, "BuilderVmOverviewPanel"));
        Assert.NotNull(FindByName(builder, "BuilderVmTotalCountTextBlock"));
        Assert.NotNull(FindByName(builder, "BuilderVmMembershipCountsTextBlock"));
        Assert.NotNull(FindByName(builder, "BuilderVmAdDcCountTextBlock"));
        Assert.NotNull(FindByName(builder, "BuilderVmOverviewListPanel"));
        Assert.NotNull(FindByName(builder, "BuilderSelectedVmDetailHost"));
        Assert.NotNull(FindByName(builder, "BuilderVmDetailCategoryNavPanel"));
        Assert.NotNull(FindByName(builder, "BuilderSelectedVmDetailPanel"));
        Assert.Contains("Basics", builderSource);
        Assert.Contains("Resources", builderSource);
        Assert.Contains("Membership", builderSource);
        Assert.Contains("Networking", builderSource);
        Assert.Contains("Roles", builderSource);
        Assert.Contains("Credentials", builderSource);
        Assert.DoesNotContain("Compute", builderSource);
        Assert.Contains("Base Disk / VHDX ID", builderSource);
        Assert.Contains("Active Directory Domain Controller", builderSource);
        Assert.Contains("BuilderVmDetailCategory.Basics", builderSource);
        Assert.Contains("BuilderVmDetailCategory.Resources", builderSource);
        Assert.Contains("BuilderVmDetailCategory.Membership", builderSource);
        Assert.Contains("BuilderVmDetailCategory.Roles", builderSource);
        Assert.Contains("BuilderVmDetailCategory.Networking", builderSource);
        Assert.Contains("BuilderVmDetailCategory.Credentials", builderSource);
        Assert.Contains("BuilderVmNavChildrenPanel.Children.Add", builderSource);
        Assert.Contains("SelectVmChild(index)", builderSource);
        Assert.Contains("_selectedVmDetailCategory = BuilderVmDetailCategory.Basics", builderSource);
        Assert.Contains("FindNextVmNumber", builderSource);
        Assert.Contains("$\"vm-{nextVmNumber}\"", builderSource);
        Assert.Contains("$\"VM {nextVmNumber}\"", builderSource);
        Assert.Contains("_isVmOverviewSelected = false", builderSource);
        Assert.Contains("ReadNics(BuilderSelectedVmDetailPanel)", builderSource);
        Assert.DoesNotContain("BuilderVmNameListPanel", builderSource);
        Assert.DoesNotContain("BuilderLabNetworksTextBox", builderSource);
        Assert.DoesNotContain("BuilderDomainsTextBox", builderSource);
        Assert.DoesNotContain("BuilderVmsTextBox", builderSource);
        Assert.DoesNotContain("BuilderNicsTextBox", builderSource);
        Assert.DoesNotContain("LabNetworksText", builderModels);
        Assert.DoesNotContain("DomainsText", builderModels);
        Assert.DoesNotContain("VmsText", builderModels);
        Assert.DoesNotContain("NicsText", builderModels);
        Assert.DoesNotContain("topologyRole", builder.ToString(), StringComparison.OrdinalIgnoreCase);

        Assert.Null(FindByNameOrDefault(builder, "BuilderApplySuggestionsButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderValidateButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderConfirmSaveCheckBox"));
        Assert.NotNull(FindByName(builder, "BuilderReviewBlockerTextBlock"));
        Assert.Equal("Back", FindByName(builder, "BuilderBackToLibraryButton").Attribute("Content")?.Value);
        Assert.Equal("Previous", FindByName(builder, "BuilderPreviousStepButton").Attribute("Content")?.Value);
        Assert.Equal("Next", FindByName(builder, "BuilderNextStepButton").Attribute("Content")?.Value);
        Assert.Equal("Save As", FindByName(builder, "BuilderSaveAsButton").Attribute("Content")?.Value);
        Assert.Equal("Save", FindByName(builder, "BuilderSaveButton").Attribute("Content")?.Value);
        Assert.Equal("{StaticResource ShellAccentBrush}", FindByName(builder, "BuilderNextStepButton").Attribute("Background")?.Value);
        Assert.Equal("{StaticResource ShellAccentBrush}", FindByName(builder, "BuilderSaveButton").Attribute("Background")?.Value);
        Assert.Contains("SelectAdjacentStep(-1)", builderSource);
        Assert.Contains("SelectAdjacentStep(1)", builderSource);
        Assert.Contains("BuildWorkflowRoutes", builderSource);
        Assert.Contains("BuilderNextStepButton.Visibility = isReview ? Visibility.Collapsed : Visibility.Visible", builderSource);
        Assert.Contains("BuilderSaveAsButton.Visibility = isReview ? Visibility.Visible : Visibility.Collapsed", builderSource);
        Assert.Contains("BuilderSaveButton.Visibility = isReview ? Visibility.Visible : Visibility.Collapsed", builderSource);
        Assert.Contains("_isVmOverviewSelected = step == BuilderWorkflowStep.Vms", builderSource);
    }

    [Fact]
    public void TemplatesBuilderView_DraftEditsRefreshResourceLists_WithoutFullDetailRerender()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var notifyDraftChangedBody = ExtractMethodBody(builderSource, "private void NotifyDraftChanged(object sender)");

        Assert.Contains("RenderResourceLists();", notifyDraftChangedBody);
        Assert.DoesNotContain("RenderDraftResources();", notifyDraftChangedBody);
        Assert.Contains("RenderSelectedVmDetail();", ExtractMethodBody(builderSource, "private void RenderDraftResources()"));
    }

    [Fact]
    public void TemplatesBuilderView_FieldEditHandlers_UpdateDraftImmediately()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var builderModels = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderDraftModels.cs")));
        var textHandlerBody = ExtractMethodBody(builderSource, "private void BuilderDraftControl_Changed(object sender, TextChangedEventArgs e)");
        var selectionHandlerBody = ExtractMethodBody(builderSource, "private void BuilderSelectionControl_Changed(object sender, SelectionChangedEventArgs e)");
        var checkboxHandlerBody = ExtractMethodBody(builderSource, "private void BuilderCheckBox_Changed(object sender, RoutedEventArgs e)");
        var notifyDraftChangedBody = ExtractMethodBody(builderSource, "private void NotifyDraftChanged(object sender)");
        var changedControlBody = ExtractMethodBody(builderSource, "private void UpdateWorkingDraftFromChangedControl(object sender)");

        Assert.Contains("NotifyDraftChanged(sender);", textHandlerBody);
        Assert.Contains("NotifyDraftChanged(sender);", selectionHandlerBody);
        Assert.Contains("NotifyDraftChanged(sender);", checkboxHandlerBody);
        Assert.Contains("UpdateWorkingDraftFromChangedControl(sender);", notifyDraftChangedBody);
        Assert.DoesNotContain("UpdateWorkingDraftFromVisibleControls();", notifyDraftChangedBody);
        Assert.Contains("_draft = _draft with", changedControlBody);
        Assert.Contains("TemplateName = BuilderTemplateNameTextBox.Text", changedControlBody);
        Assert.Contains("UpdateSelectedNetwork(_draft)", changedControlBody);
        Assert.Contains("UpdateSelectedCredentialSlot(_draft)", changedControlBody);
        Assert.Contains("UpdateSelectedForestOrDomain(_draft)", changedControlBody);
        Assert.Contains("UpdateSelectedVm(_draft)", changedControlBody);
        Assert.Contains("string MemoryMb", builderModels);
        Assert.Contains("string CpuCount", builderModels);
        Assert.Contains("string PrefixLength", builderModels);
    }

    [Fact]
    public void TemplatesBuilderView_InvalidNumericDraftText_ReRendersExactlyAcrossVmNavigation()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var resourcesRenderBody = ExtractSwitchCaseBody(
            builderSource,
            "case BuilderVmDetailCategory.Resources:",
            "case BuilderVmDetailCategory.Membership:");
        var networkingRenderBody = ExtractSwitchCaseBody(
            builderSource,
            "case BuilderVmDetailCategory.Networking:",
            "case BuilderVmDetailCategory.Credentials:");
        var createNicRowBody = ExtractMethodBody(builderSource, "private StackPanel CreateNicRow(TemplatesBuilderNicDraft nic)");
        var updateVmBody = ExtractMethodBody(builderSource, "private TemplatesBuilderDraftSnapshot UpdateSelectedVm(TemplatesBuilderDraftSnapshot draft)");
        var selectVmChildBody = ExtractMethodBody(builderSource, "private void SelectVmChild(int index)");
        var selectVmDetailCategoryBody = ExtractMethodBody(builderSource, "private void SelectVmDetailCategory(BuilderVmDetailCategory category)");

        Assert.Contains("CreateTextBox(\"Memory MB\", \"vm.MemoryMb\", vm.MemoryMb)", resourcesRenderBody);
        Assert.Contains("CreateTextBox(\"CPU Count\", \"vm.CpuCount\", vm.CpuCount)", resourcesRenderBody);
        Assert.Contains("CreateNicRow(nic)", networkingRenderBody);
        Assert.Contains("CreateTextBox(\"Prefix\", \"nic.PrefixLength\", nic.PrefixLength)", createNicRowBody);
        Assert.Contains("MemoryMb = GetText(BuilderSelectedVmDetailPanel, \"vm.MemoryMb\")", updateVmBody);
        Assert.Contains("CpuCount = GetText(BuilderSelectedVmDetailPanel, \"vm.CpuCount\")", updateVmBody);
        Assert.Contains("GetText(row, \"nic.PrefixLength\")", builderSource);
        Assert.DoesNotContain("ParsePositiveInt", builderSource);
        Assert.DoesNotContain("ParseNullableInt", builderSource);
        Assert.DoesNotContain("RenderDraftResources();", selectVmChildBody);
        Assert.DoesNotContain("RenderDraftResources();", selectVmDetailCategoryBody);
    }

    [Fact]
    public void TemplatesBuilderView_VmNavigation_UsesNarrowRenderPaths()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var selectVmChildBody = ExtractMethodBody(builderSource, "private void SelectVmChild(int index)");
        var selectVmDetailCategoryBody = ExtractMethodBody(builderSource, "private void SelectVmDetailCategory(BuilderVmDetailCategory category)");

        Assert.DoesNotContain("UpdateWorkingDraftFromVisibleControls();", selectVmChildBody);
        Assert.DoesNotContain("RenderDraftResources();", selectVmChildBody);
        Assert.Contains("_selectedVmDetailCategory = BuilderVmDetailCategory.Basics;", selectVmChildBody);
        Assert.Contains("RenderVmNavChildren();", selectVmChildBody);
        Assert.Contains("RenderSelectedVmDetail();", selectVmChildBody);

        Assert.DoesNotContain("UpdateWorkingDraftFromVisibleControls();", selectVmDetailCategoryBody);
        Assert.DoesNotContain("RenderDraftResources();", selectVmDetailCategoryBody);
        Assert.Contains("RenderSelectedVmDetail();", selectVmDetailCategoryBody);
    }

    [Fact]
    public void TemplatesBuilderView_SelectedNavState_IsHoverSafe()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var renderWorkflowTreeBody = ExtractMethodBody(builderSource, "private void RenderWorkflowTreeState()");
        var createVmWorkflowNavRowBody = ExtractMethodBody(builderSource, "private Grid CreateVmWorkflowNavRow()");
        var createResourceButtonBody = ExtractMethodBody(builderSource, "private Button CreateResourceButton(string content, bool isSelected, Action select, bool isEnabled = true)");
        var createNavContentBody = ExtractMethodBody(builderSource, "private static Border CreateNavButtonContent(string content, bool isSelected)");
        var configureNavChromeBody = ExtractMethodBody(builderSource, "private static void ConfigureNavButtonChrome(Button button)");
        var renderVmNavChildrenBody = ExtractMethodBody(builderSource, "private void RenderVmNavChildren()");
        var renderVmDetailCategoryNavBody = ExtractMethodBody(builderSource, "private void RenderVmDetailCategoryNav()");

        Assert.Null(FindByNameOrDefault(builder, "BuilderGeneralStepButton"));
        Assert.DoesNotContain("RegisterWorkflowStepNavButton", builderSource);
        Assert.DoesNotContain("SelectNavButton", builderSource);
        Assert.DoesNotContain("ApplyNavButtonState", builderSource);
        Assert.DoesNotContain("ApplyNavHoverState", builderSource);
        Assert.DoesNotContain("BuilderSelectedNavStateTag", builderSource);
        Assert.Contains("CreateResourceButton(\"General\"", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(\"Networks\"", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(\"Forests & Domains\"", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(\"Credentials\"", renderWorkflowTreeBody);
        Assert.Contains("CreateVmWorkflowNavRow()", renderWorkflowTreeBody);
        Assert.Contains("BuilderWorkflowTreePanel.Children.Add(BuilderVmNavChildrenPanel);", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(\"Review\"", renderWorkflowTreeBody);
        Assert.Contains("RenderVmNavChildren();", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(\"VMs\"", createVmWorkflowNavRowBody);
        Assert.Contains("Content = CreateNavButtonContent(content, isSelected)", createResourceButtonBody);
        Assert.Contains("ConfigureNavButtonChrome(button);", createResourceButtonBody);
        Assert.Contains("button.Click += (_, _) => select();", createResourceButtonBody);
        Assert.Contains("BorderBrush = GetBrush(isSelected ? \"ShellAccentBrush\" : \"ShellBorderBrush\")", createNavContentBody);
        Assert.Contains("Foreground = GetBrush(isSelected ? \"ShellAccentBrush\" : \"ShellTextPrimaryBrush\")", createNavContentBody);
        Assert.Contains("CreateResourceButton(", renderVmNavChildrenBody);
        Assert.Contains("CreateResourceButton(", renderVmDetailCategoryNavBody);
        Assert.Contains("ButtonBackgroundPointerOverResource", configureNavChromeBody);
        Assert.Contains("ButtonBackgroundPressedResource", configureNavChromeBody);
        Assert.Contains("ButtonBorderBrushPointerOverResource", configureNavChromeBody);
        Assert.Contains("ButtonBorderBrushPressedResource", configureNavChromeBody);
        Assert.Contains("transparent", configureNavChromeBody);
    }

    [Fact]
    public void TemplatesBuilderView_AddVm_SelectsNewVmAndBasics()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var addVmBody = ExtractMethodBody(builderSource, "private void BuilderAddVmButton_Click(object sender, RoutedEventArgs e)");

        Assert.Contains("_selectedVmIndex = vms.Count - 1;", addVmBody);
        Assert.Contains("_selectedStep = BuilderWorkflowStep.Vms;", addVmBody);
        Assert.Contains("_isVmOverviewSelected = false;", addVmBody);
        Assert.Contains("_selectedVmDetailCategory = BuilderVmDetailCategory.Basics;", addVmBody);
    }

    [Fact]
    public void TemplatesBuilderView_PreviousNextRoute_IncludesEveryVmCategoryInDraftOrder()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var routeBody = ExtractMethodBody(builderSource, "private List<BuilderWorkflowRoute> BuildWorkflowRoutes()");
        var selectAdjacentBody = ExtractMethodBody(builderSource, "private void SelectAdjacentStep(int offset)");
        var selectRouteBody = ExtractMethodBody(builderSource, "private void SelectRoute(BuilderWorkflowRoute route)");
        var vmRowBody = ExtractMethodBody(builderSource, "private Grid CreateVmWorkflowNavRow()");

        Assert.Contains("new(BuilderWorkflowStep.General)", routeBody);
        Assert.Contains("new(BuilderWorkflowStep.Networks)", routeBody);
        Assert.Contains("new(BuilderWorkflowStep.ForestsDomains)", routeBody);
        Assert.Contains("new(BuilderWorkflowStep.Credentials)", routeBody);
        Assert.Contains("new(BuilderWorkflowStep.Vms)", routeBody);
        Assert.Contains("for (var vmIndex = 0; vmIndex < _draft.Vms.Count; vmIndex++)", routeBody);
        Assert.Contains("foreach (var category in VmDetailCategoryOrder)", routeBody);
        Assert.Contains("routes.Add(new BuilderWorkflowRoute(BuilderWorkflowStep.Vms, vmIndex, category));", routeBody);
        Assert.Contains("routes.Add(new BuilderWorkflowRoute(BuilderWorkflowStep.Review));", routeBody);
        Assert.Contains("UpdateWorkingDraftFromVisibleControls();", selectAdjacentBody);
        Assert.Contains("SelectRoute(routes[targetIndex]);", selectAdjacentBody);
        Assert.Contains("_isVmOverviewSelected = route.Step == BuilderWorkflowStep.Vms && route.VmIndex < 0;", selectRouteBody);
        Assert.Contains("_selectedVmDetailCategory = route.VmDetailCategory;", selectRouteBody);
        Assert.Contains("_selectedStep == BuilderWorkflowStep.Vms && _isVmOverviewSelected", vmRowBody);
        Assert.Equal("0", FindByName(builder, "BuilderSelectedVmDetailPanel").Attribute("Grid.Column")?.Value);
        Assert.Equal("1", FindByName(builder, "BuilderVmDetailCategoryNavPanel").Attribute("Grid.Column")?.Value);
    }

    [Fact]
    public void TemplatesBuilderView_FooterRemovesManualValidationAndShowsReviewOnlySaveActions()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var updateFooterBody = ExtractMethodBody(builderSource, "private void UpdateFooterCommandState(TemplatesBuilderActionState state)");
        var compositionSource = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderWorkspaceComposition.cs")));

        Assert.Null(FindByNameOrDefault(builder, "BuilderApplySuggestionsButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderValidateButton"));
        Assert.DoesNotContain("ApplySuggestionsRequested", builderSource);
        Assert.DoesNotContain("ValidateRequested", builderSource);
        Assert.DoesNotContain("ApplySuggestionsRequested", compositionSource);
        Assert.DoesNotContain("ValidateRequested", compositionSource);
        Assert.Contains("BuilderSaveAsButton.Visibility = isReview ? Visibility.Visible : Visibility.Collapsed", updateFooterBody);
        Assert.Contains("BuilderSaveButton.Visibility = isReview ? Visibility.Visible : Visibility.Collapsed", updateFooterBody);
        Assert.Contains("BuilderSaveAsButton.IsEnabled = isReview && state.CanSaveAs;", updateFooterBody);
        Assert.Contains("BuilderSaveButton.IsEnabled = isReview && state.CanSave;", updateFooterBody);
    }

    [Fact]
    public void BuilderDraftMapper_BlocksInvalidIntermediateNumericDraftValues()
    {
        var draft = CreateConfirmedBuilderDraft();
        var invalidResourcesVm = draft.Vms[0] with
        {
            MemoryMb = "abc MB",
            CpuCount = "two"
        };

        var invalidResourcesBuild = TemplatesBuilderDraftMapper.BuildDocument(
            draft with { Vms = [invalidResourcesVm] },
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Null(invalidResourcesBuild.Document);
        Assert.Contains(invalidResourcesBuild.Errors, error => error.Contains("memory must be a positive integer", StringComparison.Ordinal));
        Assert.Equal("abc MB", invalidResourcesVm.MemoryMb);
        Assert.Equal("two", invalidResourcesVm.CpuCount);

        var invalidNicVm = draft.Vms[0] with
        {
            Nics = [draft.Vms[0].Nics[0] with { PrefixLength = "prefix-ish" }]
        };

        var invalidNicBuild = TemplatesBuilderDraftMapper.BuildDocument(
            draft with { Vms = [invalidNicVm] },
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Null(invalidNicBuild.Document);
        Assert.Contains(invalidNicBuild.Errors, error => error.Contains("prefix length must be 0 through 128", StringComparison.Ordinal));
        Assert.Equal("prefix-ish", invalidNicVm.Nics[0].PrefixLength);
    }

    [Fact]
    public void BuilderDraftMapper_SavesAdDcRoleAssignments_ToBackendCompatibleFields()
    {
        var draft = CreateConfirmedBuilderDraft();

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft,
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Empty(build.Errors);
        var template = Assert.IsType<TemplateEditorDocument>(build.Document).Template;
        var domain = Assert.Single(template.DirectoryTopology!.Domains!);
        var dc = Assert.Single(template.VmTemplates, vm => vm.TopologyRole == "FirstDomainController");
        Assert.Equal("vm-dc01", dc.VmId);
        Assert.Equal("vm-dc01", domain.FirstDomainControllerVmId);
        Assert.Equal("DomainMember", dc.MembershipMode);
        Assert.DoesNotContain(template.VmTemplates, vm => vm.TopologyRole == "RootDomainController");
    }

    [Fact]
    public void BuilderDraftMapper_BlocksSave_WhenDomainHasNoAdDcRoleAssignment()
    {
        var draft = CreateConfirmedBuilderDraft();
        var vmsWithoutDcRole = draft.Vms
            .Select(vm => vm with { IsActiveDirectoryDomainController = false })
            .ToList();

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft with { Vms = vmsWithoutDcRole },
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Null(build.Document);
        Assert.Contains(build.Errors, error => error.Contains("Active Directory Domain Controller role", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuilderDraftMapper_AllowsZeroDomainStandaloneVmTemplate_ToPlan()
    {
        var draft = CreateConfirmedBuilderDraft();
        var standaloneVm = draft.Vms[0] with
        {
            MembershipMode = V2MembershipModeCatalog.Standalone,
            DomainId = string.Empty,
            IsActiveDirectoryDomainController = false,
            CredentialSlots = new TemplatesBuilderVmCredentialSlotDraft("slot-local", string.Empty, string.Empty, string.Empty, string.Empty)
        };

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft with
            {
                Forests = [],
                Domains = [],
                Vms = [standaloneVm]
            },
            templateId: "template-v2-standalone",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Empty(build.Errors);
        var document = Assert.IsType<TemplateEditorDocument>(build.Document);
        Assert.Empty(document.Template.DirectoryTopology!.Domains!);

        var planner = new V2PlanningCapabilityService();
        var plan = await planner.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = document.Template,
            CatalogItems = [CreateCatalogItem("disk-dc", @"C:\base\disk-dc.vhdx", "sig-dc")],
            AvailableSwitchNames = ["vSwitch-Core"],
            AvailableSwitches = [new V2AvailableSwitchInfo { Name = "vSwitch-Core", SwitchType = "Internal" }],
            ResolvedCredentialSlotKeys = ["slot-local"],
            DefaultDeploymentProfile = "Balanced"
        });

        Assert.True(plan.Success);
        Assert.False(plan.Context.DomainSemanticsRequired);
    }

    [Fact]
    public void BuilderDraftMapper_BlocksZeroVmDraftBeforeSave()
    {
        var draft = CreateConfirmedBuilderDraft();

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft with
            {
                Forests = [],
                Domains = [],
                Vms = []
            },
            templateId: "template-v2-empty",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Null(build.Document);
        Assert.Contains(build.Errors, error => error.Contains("At least one VM is required before Save.", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderDraftMapper_BlocksDomainMemberVm_WhenNoDomainIsDeclared()
    {
        var draft = CreateConfirmedBuilderDraft();
        var domainMemberVm = draft.Vms[1] with
        {
            MembershipMode = V2MembershipModeCatalog.DomainMember,
            DomainId = "domain-contoso",
            IsActiveDirectoryDomainController = false
        };

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft with
            {
                Forests = [],
                Domains = [],
                Vms = [domainMemberVm]
            },
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Null(build.Document);
        Assert.Contains(build.Errors, error => error.Contains("DomainMember membership without a declared domain", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderDraftMapper_BlocksAdDcRoleVm_WhenNoDomainIsDeclared()
    {
        var draft = CreateConfirmedBuilderDraft();
        var dcVm = draft.Vms[0] with
        {
            MembershipMode = V2MembershipModeCatalog.DomainMember,
            DomainId = "domain-contoso",
            IsActiveDirectoryDomainController = true
        };

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft with
            {
                Forests = [],
                Domains = [],
                Vms = [dcVm]
            },
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Null(build.Document);
        Assert.Contains(build.Errors, error => error.Contains("Active Directory Domain Controller role without a declared domain", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderDraftMapper_PersistsCredentialSlotReferencesOnly()
    {
        var draft = CreateConfirmedBuilderDraft();

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft,
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Empty(build.Errors);
        var json = JsonSerializer.Serialize(Assert.IsType<TemplateEditorDocument>(build.Document).Template);
        Assert.Contains("slot-local", json);
        Assert.Contains("slot-admin", json);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secureString", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuilderDraftMapper_ProducesPlannerCompatibleV2Template()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);
        var draft = TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with
        {
            IsSaveConfirmed = true
        };

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft,
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Empty(build.Errors);
        var document = Assert.IsType<TemplateEditorDocument>(build.Document);
        Assert.Equal(TemplateSchemaVersionCatalog.V2SchemaVersion, document.Template.SchemaVersion);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, document.Template.ExecutionEngine);
        Assert.NotNull(document.Template.DirectoryTopology);

        var planner = new V2PlanningCapabilityService();
        var plan = await planner.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = document.Template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", @"C:\base\disk-dc.vhdx", "sig-dc"),
                CreateCatalogItem("disk-member", @"C:\base\disk-member.vhdx", "sig-member")
            ],
            AvailableSwitchNames = ["vSwitch-Core"],
            AvailableSwitches = [new V2AvailableSwitchInfo { Name = "vSwitch-Core", SwitchType = "Internal" }],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-admin", "slot-join", "slot-dsrm"],
            DefaultDeploymentProfile = "Balanced"
        });

        Assert.True(plan.Success);
        Assert.True(plan.Context.DomainSemanticsRequired);
        var nodeKinds = plan.Nodes.Select(node => node.Kind).ToArray();
        Assert.Contains(V2PlanNodeKind.PromoteFirstDomainController, nodeKinds);
        Assert.Contains(V2PlanNodeKind.JoinDomain, nodeKinds);
    }

    [Fact]
    public async Task BuilderSave_UsesReviewAsConfirmationWithoutSeparateCheckboxGate()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var controller = new TemplatesBuilderWorkspaceController(service, workspace, host);
        var draft = CreateConfirmedBuilderDraft();

        workspace.LoadNewDraft(draft with { IsSaveConfirmed = false });
        workspace.ApplyDraft(workspace.CaptureDraft() with
        {
            TemplateDescription = "Changed after confirmation.",
            IsSaveConfirmed = false
        });

        await controller.SaveAsync();

        Assert.Equal(1, service.SaveCalls);
        Assert.Equal("Changed after confirmation.", service.LastSavedDocument!.Template.Description);
        Assert.DoesNotContain("Confirm the visible Builder draft before saving.", workspace.StatusText);
    }

    [Fact]
    public async Task BuilderSave_PreservesExistingTrustsDuringRoundTrip()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var controller = new TemplatesBuilderWorkspaceController(service, workspace, host);
        var sourceTemplate = CreateTemplateWithTrust();

        workspace.LoadDocument(new TemplateEditorDocument
        {
            Template = sourceTemplate,
            SourceFilePath = @"C:\templates\v2-with-trust.json"
        });
        workspace.ApplyDraft(workspace.CaptureDraft() with { TemplateDescription = "Updated visible fields." });
        workspace.ApplyDraft(workspace.CaptureDraft() with { IsSaveConfirmed = true });

        await controller.SaveAsync();

        Assert.Equal(1, service.SaveCalls);
        var savedTrust = Assert.Single(service.LastSavedDocument!.Template.DirectoryTopology!.Trusts!);
        var sourceTrust = Assert.Single(sourceTemplate.DirectoryTopology!.Trusts!);
        Assert.NotSame(sourceTrust, savedTrust);
        Assert.Equal("trust-contoso-fabrikam", savedTrust.TrustId);
        Assert.Equal("domain-contoso", savedTrust.SourceDomainId);
        Assert.Equal("domain-fabrikam", savedTrust.TargetDomainId);
        Assert.Equal(V2TrustType.Forest, savedTrust.TrustType);
        Assert.Equal(V2TrustDirection.Bidirectional, savedTrust.Direction);
    }

    private static TemplatesBuilderDraftSnapshot CreateConfirmedBuilderDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with { IsSaveConfirmed = true };
    }

    private static LabTemplate CreateTemplateWithTrust()
    {
        return new LabTemplate
        {
            Id = "template-with-trust",
            Name = "Template With Trust",
            Description = "Existing V2 template with trust intent.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateRevision = 1,
            CreatedWithAppVersion = "1.0.0",
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning,
            LabNetworks =
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-core",
                    Name = "Core",
                    SwitchName = "vSwitch-Core",
                    Subnet = "10.0.0.0/24"
                }
            ],
            DirectoryTopology = new V2DirectoryTopologyTemplate
            {
                Forests =
                [
                    new V2ForestTemplate
                    {
                        ForestId = "forest-contoso",
                        RootDomainId = "domain-contoso"
                    },
                    new V2ForestTemplate
                    {
                        ForestId = "forest-fabrikam",
                        RootDomainId = "domain-fabrikam"
                    }
                ],
                Domains =
                [
                    new V2DomainTemplate
                    {
                        DomainId = "domain-contoso",
                        DnsName = "contoso.com",
                        NetBiosName = "CONTOSO",
                        ForestId = "forest-contoso",
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = "vm-contoso-dc"
                    },
                    new V2DomainTemplate
                    {
                        DomainId = "domain-fabrikam",
                        DnsName = "fabrikam.com",
                        NetBiosName = "FABRIKAM",
                        ForestId = "forest-fabrikam",
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = "vm-fabrikam-dc"
                    }
                ],
                Trusts =
                [
                    new V2TrustTemplate
                    {
                        TrustId = "trust-contoso-fabrikam",
                        SourceDomainId = "domain-contoso",
                        TargetDomainId = "domain-fabrikam",
                        TrustType = V2TrustType.Forest,
                        Direction = V2TrustDirection.Bidirectional
                    }
                ]
            },
            VmTemplates =
            {
                new VmTemplate
                {
                    VmId = "vm-contoso-dc",
                    Name = "contoso-dc",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "disk-dc",
                    TopologyRole = "FirstDomainController",
                    DomainId = "domain-contoso"
                },
                new VmTemplate
                {
                    VmId = "vm-fabrikam-dc",
                    Name = "fabrikam-dc",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "disk-member",
                    TopologyRole = "FirstDomainController",
                    DomainId = "domain-fabrikam"
                }
            }
        };
    }

    private static VhdxCatalogItem CreateCatalogItem(string id, string path, string signature)
    {
        return new VhdxCatalogItem
        {
            Id = id,
            Path = path,
            OsName = "Windows Server",
            OsVersion = "2022",
            Generation = 2,
            Signature = signature,
            BootstrapProfile = new VhdxBootstrapProfile
            {
                ExpectedLocalUser = "Administrator",
                LocalCredentialSlotRef = "slot-local",
                GuestOsFamily = "windows",
                GuestTransport = "powershell-direct"
            }
        };
    }

    private static XDocument LoadXaml(string relativePath)
        => XDocument.Load(WinUIPath(relativePath));

    private static string WinUIPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "LabAssistant.WinUI",
            relativePath));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }

    private static XElement? FindByNameOrDefault(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .SingleOrDefault(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }

    private static bool HasName(XElement element, string name)
        => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name;

    private static string ExtractMethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method signature '{signature}'.");
        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"Could not find method body for '{signature}'.");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : 0;
            depth -= source[i] == '}' ? 1 : 0;
            if (depth == 0)
            {
                return source.Substring(braceStart, i - braceStart + 1);
            }
        }

        throw new InvalidOperationException($"Could not read method body for '{signature}'.");
    }

    private static string ExtractSwitchCaseBody(string source, string caseStart, string nextCaseStart)
    {
        var start = source.IndexOf(caseStart, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find switch case '{caseStart}'.");
        var end = source.IndexOf(nextCaseStart, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find switch case boundary '{nextCaseStart}'.");
        return source[start..end];
    }

    private sealed class RecordingTemplatesCapabilityService : ITemplatesCapabilityService
    {
        public int SaveCalls { get; private set; }

        public TemplateEditorDocument? LastSavedDocument { get; private set; }

        public Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateLibraryLoadResult());

        public Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateEditorDocument());

        public Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateEditorDocument());

        public Task<TemplatesVhdxCatalogLoadResult> LoadVhdxCatalogOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplatesVhdxCatalogLoadResult());

        public Task<TemplateOperationResult> SaveAsync(
            TemplateEditorDocument document,
            string? targetFilePath = null,
            bool saveAs = false,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            LastSavedDocument = document;

            return Task.FromResult(new TemplateOperationResult
            {
                Success = true,
                OperationId = "operation-save",
                UserMessage = "Saved.",
                FilePath = targetFilePath ?? document.SourceFilePath ?? @"C:\templates\saved.json"
            });
        }

        public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateValidationSummaryResult { IsValid = true });

        public Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateOperationResult> ExportAsync(
            string sourceFilePath,
            string destinationFilePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());
    }

    private sealed class RecordingBuilderHost : ITemplatesBuilderWorkspaceControllerHost
    {
        public bool IsTemplatesLoading { get; private set; }

        public void SetTemplatesLoading(bool isLoading) => IsTemplatesLoading = isLoading;

        public void ApplyWorkspaceState()
        {
        }

        public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()));

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
            => Task.FromResult<string?>(@"C:\templates\save-as.json");

        public void NavigateToBuilder()
        {
        }

        public void NavigateToLibrary()
        {
        }
    }
}
