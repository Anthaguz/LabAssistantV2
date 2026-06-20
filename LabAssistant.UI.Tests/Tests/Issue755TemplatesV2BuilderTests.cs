using System.Xml.Linq;
using System.Text.Json;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Deploy;
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
        var builderProjections = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderSectionProjections.cs")));
        var builderRoleCatalog = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderRoleProjectionCatalog.cs")));

        Assert.NotNull(FindByName(builder, "BuilderLeftStepper"));
        Assert.NotNull(FindByName(builder, "BuilderWorkflowTreeScrollViewer"));
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
        Assert.Contains("ShellAccentBrush", builderSource);
        Assert.Contains("ShellBackgroundBrush", builderSource);
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
        Assert.Contains("TemplatesBuilderSectionProjections.ProjectNetworkRows", builderSource);
        Assert.Contains("TemplatesBuilderSectionProjections.ProjectCredentialSlotRows", builderSource);
        Assert.Contains("TemplatesBuilderSectionProjections.ProjectForestDomainRows", builderSource);
        Assert.Contains("TemplatesBuilderSectionProjections.ProjectVmOverview", builderSource);
        Assert.Contains("TemplatesBuilderSectionProjections.ProjectSelectedVmDetail", builderSource);
        Assert.Contains("Content = \"+\"", builderSource);
        Assert.Contains("ToolTipService.SetToolTip(addButton, \"Add VM\")", builderSource);
        Assert.NotNull(FindByName(builder, "BuilderVmOverviewPanel"));
        Assert.NotNull(FindByName(builder, "BuilderVmTotalCountTextBlock"));
        Assert.NotNull(FindByName(builder, "BuilderVmMembershipCountsTextBlock"));
        Assert.NotNull(FindByName(builder, "BuilderVmAdDcCountTextBlock"));
        Assert.NotNull(FindByName(builder, "BuilderVmOverviewListPanel"));
        Assert.NotNull(FindByName(builder, "BuilderSelectedVmDetailHost"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderVmDetailCategoryNavPanel"));
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
        Assert.Contains("SelectVmChild(row.Route.VmIndex)", builderSource);
        Assert.Contains("group.CategoryRows", builderSource);
        Assert.Contains("SelectVmDetailCategory(categoryRow.Route.VmIndex, categoryRow.Route.VmDetailCategory)", builderSource);
        Assert.Contains("FindNextVmNumber", builderSource);
        Assert.Contains("$\"vm-{nextVmNumber}\"", builderSource);
        Assert.Contains("$\"VM {nextVmNumber}\"", builderSource);
        Assert.Contains("ReadNics(BuilderSelectedVmDetailPanel)", builderSource);
        Assert.DoesNotContain("RenderVmDetailCategoryNav", builderSource);
        Assert.DoesNotContain("BuilderVmDetailCategoryNavPanel", builderSource);
        Assert.DoesNotContain("BuilderVmNameListPanel", builderSource);
        Assert.DoesNotContain("BuilderLabNetworksTextBox", builderSource);
        Assert.DoesNotContain("BuilderDomainsTextBox", builderSource);
        Assert.DoesNotContain("BuilderVmsTextBox", builderSource);
        Assert.DoesNotContain("BuilderNicsTextBox", builderSource);
        Assert.DoesNotContain("LabNetworksText", builderModels);
        Assert.DoesNotContain("DomainsText", builderModels);
        Assert.DoesNotContain("VmsText", builderModels);
        Assert.DoesNotContain("NicsText", builderModels);
        Assert.Contains("TemplatesBuilderResourceRowProjection", builderProjections);
        Assert.Contains("TemplatesBuilderVmDetailProjection", builderProjections);
        Assert.Contains("TemplatesBuilderNicRowProjection", builderProjections);
        Assert.Contains("TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles", builderProjections);
        Assert.Contains("ActiveDirectoryDomainControllerRoleKey", builderRoleCatalog);
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
    }

    [Fact]
    public void TemplatesBuilderView_GeneralUsesVisibleDeploymentProfileSelector()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var builderXamlSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml")));

        Assert.Null(FindByNameOrDefault(builder, "BuilderDeploymentProfileComboBox"));
        Assert.Equal("Conservative", FindByName(builder, "BuilderDeploymentProfileConservativeButton").Attribute("Content")?.Value);
        Assert.Equal("Balanced", FindByName(builder, "BuilderDeploymentProfileBalancedButton").Attribute("Content")?.Value);
        Assert.Equal("Aggressive", FindByName(builder, "BuilderDeploymentProfileAggressiveButton").Attribute("Content")?.Value);

        var infoIcon = FindByName(builder, "BuilderDeploymentProfileInfoIcon");
        Assert.Equal("Segoe MDL2 Assets", infoIcon.Attribute("FontFamily")?.Value);
        Assert.Equal("\uE946", infoIcon.Attribute("Text")?.Value);
        Assert.Contains("Chooses how aggressively LabAssistant should deploy this template.", builderXamlSource);
        Assert.Contains("Conservative: takes smaller steps", builderXamlSource);
        Assert.Contains("Balanced: recommended default pacing", builderXamlSource);
        Assert.Contains("Aggressive: starts more work in parallel", builderXamlSource);
        Assert.Contains("AutomationProperties.Name=\"Deployment profile information\"", builderXamlSource);

        Assert.Contains("ConfigureDeploymentProfileButton", builderSource);
        Assert.Contains("BuilderDeploymentProfileButton_Click", builderSource);
        Assert.Contains("SetSelectedProfile(profile);", builderSource);
        Assert.Contains("NotifyDraftChanged((Button)sender);", builderSource);
        Assert.DoesNotContain("BuilderDeploymentProfileComboBox", builderSource);
    }

    [Fact]
    public void TemplatesBuilderView_DraftEditsRefreshResourceLists_WithoutFullTreeOrDetailRerender()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var notifyDraftChangedBody = ExtractMethodBody(builderSource, "private void NotifyDraftChanged(object sender)");
        var updateActionStateBody = ExtractMethodBody(builderSource, "internal void UpdateActionState(TemplatesBuilderActionState state)");
        var updateWorkflowTreeActionStateBody = ExtractMethodBody(builderSource, "private void UpdateWorkflowTreeActionState()");
        var renderResourceListsBody = ExtractMethodBody(builderSource, "private void RenderResourceLists(bool refreshWorkflowTreeChildren = true)");

        Assert.Contains("RenderResourceLists();", notifyDraftChangedBody);
        Assert.DoesNotContain("RenderDraftResources();", notifyDraftChangedBody);
        Assert.DoesNotContain("RenderWorkflowTreeState", notifyDraftChangedBody);
        Assert.DoesNotContain("RenderWorkflowTreeState();", updateActionStateBody);
        Assert.Contains("UpdateWorkflowTreeActionState();", updateActionStateBody);
        Assert.Contains("FindDescendants<Button>(BuilderWorkflowTreePanel)", updateWorkflowTreeActionStateBody);
        Assert.DoesNotContain("BuilderWorkflowTreePanel.Children.Clear();", updateActionStateBody);
        Assert.Contains("if (refreshWorkflowTreeChildren)", renderResourceListsBody);
        Assert.Contains("RenderSelectedVmDetail();", ExtractMethodBody(builderSource, "private void RenderDraftResources(bool refreshWorkflowTreeChildren = true)"));
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
        Assert.Contains("FrameworkElement { Tag: BuilderDraftFieldKey fieldKey }", changedControlBody);
        Assert.Contains("switch (fieldKey.Scope)", changedControlBody);
        Assert.Contains("case BuilderDraftFieldScope.Network:", changedControlBody);
        Assert.Contains("case BuilderDraftFieldScope.CredentialSlot:", changedControlBody);
        Assert.Contains("case BuilderDraftFieldScope.Forest:", changedControlBody);
        Assert.Contains("case BuilderDraftFieldScope.Domain:", changedControlBody);
        Assert.Contains("case BuilderDraftFieldScope.Vm:", changedControlBody);
        Assert.Contains("case BuilderDraftFieldScope.Nic:", changedControlBody);
        Assert.DoesNotContain("StartsWith(", changedControlBody);
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
        var createNicRowBody = ExtractMethodBody(builderSource, "private StackPanel CreateNicRow(TemplatesBuilderNicRowProjection projection)");
        var updateVmBody = ExtractMethodBody(builderSource, "private TemplatesBuilderDraftSnapshot UpdateSelectedVm(TemplatesBuilderDraftSnapshot draft)");
        var selectVmChildBody = ExtractMethodBody(builderSource, "private void SelectVmChild(int index)");
        var selectVmDetailCategoryBody = ExtractMethodBody(builderSource, "private void SelectVmDetailCategory(BuilderVmDetailCategory category)");

        Assert.Contains("CreateTextBox(\"Memory MB\", TemplatesBuilderFieldKeys.VmMemoryMb, vm.MemoryMb)", resourcesRenderBody);
        Assert.Contains("CreateTextBox(\"CPU Count\", TemplatesBuilderFieldKeys.VmCpuCount, vm.CpuCount)", resourcesRenderBody);
        Assert.Contains("CreateNicRow(nic)", networkingRenderBody);
        Assert.Contains("CreateTextBox(\"Prefix\", TemplatesBuilderFieldKeys.NicPrefixLength, nic.PrefixLength)", createNicRowBody);
        Assert.Contains("MemoryMb = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmMemoryMb)", updateVmBody);
        Assert.Contains("CpuCount = GetText(BuilderSelectedVmDetailPanel, TemplatesBuilderFieldKeys.VmCpuCount)", updateVmBody);
        Assert.Contains("GetText(row, TemplatesBuilderFieldKeys.NicPrefixLength)", builderSource);
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
        Assert.DoesNotContain("RenderVmNavChildren();", selectVmChildBody);
        Assert.Contains("_workflowNavigation.SelectVmChild(index, _draft);", selectVmChildBody);
        Assert.Contains("RenderSelectedStep();", selectVmChildBody);
        Assert.Contains("RenderSelectedVmDetail();", selectVmChildBody);

        Assert.DoesNotContain("UpdateWorkingDraftFromVisibleControls();", selectVmDetailCategoryBody);
        Assert.DoesNotContain("RenderDraftResources();", selectVmDetailCategoryBody);
        Assert.DoesNotContain("RenderVmNavChildren();", selectVmDetailCategoryBody);
        Assert.Contains("_workflowNavigation.SelectVmDetailCategory(category, _draft);", selectVmDetailCategoryBody);
        Assert.Contains("RenderSelectedStep();", selectVmDetailCategoryBody);
        Assert.Contains("RenderSelectedVmDetail();", selectVmDetailCategoryBody);
    }

    [Fact]
    public void TemplatesBuilderView_SelectedNavState_IsHoverSafe()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var renderWorkflowTreeBody = ExtractMethodBody(builderSource, "private void RenderWorkflowTreeState(BuilderWorkflowProjection projection)");
        var createVmWorkflowNavRowBody = ExtractMethodBody(builderSource, "private Grid CreateVmWorkflowNavRow(BuilderWorkflowNavigationRow vmOverviewRow)");
        var createResourceButtonBody = ExtractMethodBody(builderSource, "private Button CreateResourceButton(string content, bool isSelected, Action select, bool isEnabled = true, bool isNested = false)");
        var createNavContentBody = ExtractMethodBody(builderSource, "private static Border CreateNavButtonContent(string content, bool isSelected, bool isNested = false)");
        var configureNavChromeBody = ExtractMethodBody(builderSource, "private static void ConfigureNavButtonChrome(Button button)");
        var renderVmNavChildrenBody = ExtractMethodBody(builderSource, "private void RenderVmNavChildren(BuilderWorkflowProjection projection)");

        Assert.Null(FindByNameOrDefault(builder, "BuilderGeneralStepButton"));
        Assert.DoesNotContain("RegisterWorkflowStepNavButton", builderSource);
        Assert.DoesNotContain("SelectNavButton", builderSource);
        Assert.DoesNotContain("ApplyNavButtonState", builderSource);
        Assert.DoesNotContain("ApplyNavHoverState", builderSource);
        Assert.DoesNotContain("BuilderSelectedNavStateTag", builderSource);
        Assert.Contains("projection.StepRows.Take(4)", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(row.Label, row.IsSelected, () => SelectStep(row.Route.Step), row.IsEnabled)", renderWorkflowTreeBody);
        Assert.Contains("CreateVmWorkflowNavRow(projection.VmOverviewRow)", renderWorkflowTreeBody);
        Assert.Contains("BuilderWorkflowTreePanel.Children.Add(BuilderVmNavChildrenPanel);", renderWorkflowTreeBody);
        Assert.Contains("var reviewRow = projection.StepRows.Last();", renderWorkflowTreeBody);
        Assert.Contains("RenderVmNavChildren(projection);", renderWorkflowTreeBody);
        Assert.Contains("CreateResourceButton(vmOverviewRow.Label, vmOverviewRow.IsSelected, SelectVmOverview, vmOverviewRow.IsEnabled)", createVmWorkflowNavRowBody);
        Assert.Contains("Content = CreateNavButtonContent(content, isSelected, isNested)", createResourceButtonBody);
        Assert.Contains("ConfigureNavButtonChrome(button);", createResourceButtonBody);
        Assert.Contains("ToolTipService.SetToolTip(button, content);", createResourceButtonBody);
        Assert.Contains("button.Click += (_, _) => select();", createResourceButtonBody);
        Assert.Contains("BorderBrush = GetBrush(isSelected ? \"ShellAccentBrush\" : \"ShellBorderBrush\")", createNavContentBody);
        Assert.Contains("FontWeight = isSelected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal", createNavContentBody);
        Assert.Contains("Foreground = GetBrush(isSelected ? \"ShellAccentBrush\" : \"ShellTextPrimaryBrush\")", createNavContentBody);
        Assert.Contains("TextTrimming = TextTrimming.CharacterEllipsis", createNavContentBody);
        Assert.Contains("TextWrapping = TextWrapping.NoWrap", createNavContentBody);
        Assert.Contains("CreateResourceButton(", renderVmNavChildrenBody);
        Assert.Contains("group.CategoryRows", renderVmNavChildrenBody);
        Assert.Contains("categoryRow.Label", renderVmNavChildrenBody);
        Assert.Contains("isNested: true", renderVmNavChildrenBody);
        Assert.Contains("SelectVmDetailCategory(categoryRow.Route.VmIndex, categoryRow.Route.VmDetailCategory)", renderVmNavChildrenBody);
        Assert.DoesNotContain("RenderVmDetailCategoryNav", builderSource);
        Assert.Contains("ButtonBackgroundPointerOverResource", configureNavChromeBody);
        Assert.Contains("ButtonBackgroundPressedResource", configureNavChromeBody);
        Assert.Contains("ButtonBorderBrushPointerOverResource", configureNavChromeBody);
        Assert.Contains("ButtonBorderBrushPressedResource", configureNavChromeBody);
        Assert.Contains("transparent", configureNavChromeBody);
    }

    [Fact]
    public void TemplatesBuilderView_NestedWorkflowTreeAndFooter_HandleDenseOrNarrowLayouts()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderXamlSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml")));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var createNavContentBody = ExtractMethodBody(builderSource, "private static Border CreateNavButtonContent(string content, bool isSelected, bool isNested = false)");
        var createResourceButtonBody = ExtractMethodBody(builderSource, "private Button CreateResourceButton(string content, bool isSelected, Action select, bool isEnabled = true, bool isNested = false)");

        Assert.Equal("520", FindByName(builder, "BuilderWorkflowTreeScrollViewer").Attribute("MaxHeight")?.Value);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", builderXamlSource);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", builderXamlSource);
        Assert.NotNull(FindByName(builder, "BuilderFooterCommandGrid"));
        Assert.NotNull(FindByName(builder, "BuilderFooterCommandScrollViewer"));
        Assert.NotNull(FindByName(builder, "BuilderFooterCommandPanel"));
        Assert.Contains("x:Name=\"BuilderFooterCommandScrollViewer\"", builderXamlSource);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", builderXamlSource);
        Assert.Contains("TextTrimming = TextTrimming.CharacterEllipsis", createNavContentBody);
        Assert.Contains("MaxLines = 1", createNavContentBody);
        Assert.Contains("ToolTipService.SetToolTip(button, content);", createResourceButtonBody);
    }

    [Fact]
    public void TemplatesBuilderView_AddVm_SelectsNewVmAndBasics()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var addVmBody = ExtractMethodBody(builderSource, "private void BuilderAddVmButton_Click(object sender, RoutedEventArgs e)");
        var draft = CreateDraftWithVms("vm-1", "vm-2");
        var navigation = new TemplatesBuilderWorkflowNavigation();

        navigation.SelectVmChild(draft.Vms.Count - 1, draft);
        var projection = navigation.Project(draft, canNavigate: true);

        Assert.Contains("_workflowNavigation.SelectVmChild(vms.Count - 1, updatedDraft);", addVmBody);
        Assert.Equal(BuilderWorkflowStep.Vms, projection.ActiveStep);
        Assert.False(projection.IsVmOverviewSelected);
        Assert.Equal(1, projection.SelectedVmIndex);
        Assert.Equal(BuilderVmDetailCategory.Basics, projection.SelectedVmDetailCategory);
        Assert.Equal(BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Basics), projection.CurrentRoute);
    }

    [Fact]
    public void TemplatesBuilderView_PreviousNextRoute_IncludesEveryVmCategoryInDraftOrder()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var draft = CreateDraftWithVms("vm-alpha", "vm-beta");

        var routes = TemplatesBuilderWorkflowNavigation.BuildRoutes(draft);

        Assert.Equal(
            [
                BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.General),
                BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Networks),
                BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.ForestsDomains),
                BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Credentials),
                BuilderWorkflowRoute.VmOverview(),
                BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Basics),
                BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Resources),
                BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Membership),
                BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Roles),
                BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Networking),
                BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Credentials),
                BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Basics),
                BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Resources),
                BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Membership),
                BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Roles),
                BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Networking),
                BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Credentials),
                BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Review)
            ],
            routes);
        Assert.Null(FindByName(builder, "BuilderSelectedVmDetailPanel").Attribute("Grid.Column"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderVmDetailCategoryNavPanel"));
    }

    [Fact]
    public void TemplatesBuilderWorkflowNavigation_ProjectsSelectedStateAndNestedVmCategoryRows()
    {
        var draft = CreateDraftWithVms("vm-alpha", "vm-beta");
        var navigation = new TemplatesBuilderWorkflowNavigation();

        navigation.SelectRoute(BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Networking), draft);
        var projection = navigation.Project(draft, canNavigate: true);
        var selectedVmGroup = Assert.Single(projection.VmRows, row => row.VmRow.IsSelected);
        var selectedCategoryRow = Assert.Single(selectedVmGroup.CategoryRows, row => row.IsSelected);

        Assert.Equal(BuilderWorkflowStep.Vms, projection.ActiveStep);
        Assert.False(projection.IsVmOverviewSelected);
        Assert.Equal(1, projection.SelectedVmIndex);
        Assert.Equal(BuilderVmDetailCategory.Networking, projection.SelectedVmDetailCategory);
        Assert.True(projection.IsVmDetailSelected);
        Assert.Equal("vm-beta", selectedVmGroup.VmRow.Label);
        Assert.Equal(BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Basics), selectedVmGroup.VmRow.Route);
        Assert.Equal(BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Networking), selectedCategoryRow.Route);
        Assert.Equal(
            ["Basics", "Resources", "Membership", "Roles", "Networking", "Credentials"],
            selectedVmGroup.CategoryRows.Select(row => row.Label).ToArray());
    }

    [Fact]
    public void TemplatesBuilderWorkflowNavigation_NonVmRoutesDoNotCaptureHiddenVmDetail()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var updateVmBody = ExtractMethodBody(builderSource, "private TemplatesBuilderDraftSnapshot UpdateSelectedVm(TemplatesBuilderDraftSnapshot draft)");
        var draft = CreateDraftWithVms("vm-alpha", "vm-beta");
        var navigation = new TemplatesBuilderWorkflowNavigation();

        navigation.SelectRoute(BuilderWorkflowRoute.ForVmCategory(1, BuilderVmDetailCategory.Networking), draft);
        navigation.SelectStep(BuilderWorkflowStep.General, draft);
        var projection = navigation.Project(draft, canNavigate: true);

        Assert.Equal(BuilderWorkflowStep.General, projection.ActiveStep);
        Assert.False(projection.IsVmOverviewSelected);
        Assert.False(projection.IsVmDetailSelected);
        Assert.Contains("if (!projection.IsVmDetailSelected)", updateVmBody);
    }

    [Fact]
    public void TemplatesBuilderWorkflowNavigation_ModelsFutureRoleChildRouteWithoutVmRowDrift()
    {
        var draft = CreateDraftWithVms("vm-alpha");
        var navigation = new TemplatesBuilderWorkflowNavigation();

        navigation.SelectRoute(BuilderWorkflowRoute.ForVmRole(0, "ad-domain-controller"), draft);
        var projection = navigation.Project(draft, canNavigate: true);

        Assert.Equal(BuilderWorkflowRouteKind.VmRole, projection.CurrentRoute.Kind);
        Assert.Equal(BuilderVmDetailCategory.Roles, projection.SelectedVmDetailCategory);
        Assert.True(projection.IsVmDetailSelected);
        Assert.Equal("ad-domain-controller", projection.CurrentRoute.RoleKey);
        var vmGroup = Assert.Single(projection.VmRows);
        Assert.True(vmGroup.VmRow.IsSelected);
        Assert.True(Assert.Single(vmGroup.CategoryRows, row => row.Route.VmDetailCategory == BuilderVmDetailCategory.Roles).IsSelected);
    }

    [Fact]
    public void TemplatesBuilderSectionProjections_ModelCurrentBuilderRowsAndTypedEditKeys()
    {
        var draft = CreateConfirmedBuilderDraft();
        var navigation = new TemplatesBuilderWorkflowNavigation();

        var networkRow = Assert.Single(TemplatesBuilderSectionProjections.ProjectNetworkRows(draft, selectedIndex: 0));
        var credentialRows = TemplatesBuilderSectionProjections.ProjectCredentialSlotRows(draft, selectedIndex: 1);
        var forestDomainRows = TemplatesBuilderSectionProjections.ProjectForestDomainRows(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0);
        var overview = TemplatesBuilderSectionProjections.ProjectVmOverview(draft);

        navigation.SelectRoute(BuilderWorkflowRoute.ForVmCategory(0, BuilderVmDetailCategory.Networking), draft);
        var detail = TemplatesBuilderSectionProjections.ProjectSelectedVmDetail(draft, navigation.Project(draft, canNavigate: true));
        Assert.NotNull(detail);

        Assert.Equal(TemplatesBuilderResourceKind.Network, networkRow.Kind);
        Assert.Equal("Core", networkRow.Label);
        Assert.True(networkRow.IsSelected);
        Assert.Equal(4, credentialRows.Count);
        Assert.Equal(TemplatesBuilderResourceKind.CredentialSlot, credentialRows[1].Kind);
        Assert.Equal(TemplatesBuilderResourceKind.Forest, forestDomainRows[0].Kind);
        Assert.Equal(TemplatesBuilderResourceKind.Domain, forestDomainRows[1].Kind);
        Assert.True(forestDomainRows[1].IsSelected);
        Assert.Equal(2, overview.TotalVmCount);
        Assert.Equal(2, overview.DomainMemberVmCount);
        Assert.Equal(1, overview.ActiveDirectoryDomainControllerCount);
        Assert.Contains(overview.SummaryRows, row => row.Contains("AD DC", StringComparison.Ordinal));
        Assert.Equal(BuilderVmDetailCategory.Networking, detail.Value.Category);
        Assert.Single(detail.Value.Nics);
        Assert.Equal(BuilderDraftFieldScope.Network, TemplatesBuilderFieldKeys.NetworkId.Scope);
        Assert.Equal(BuilderDraftFieldScope.Vm, TemplatesBuilderFieldKeys.VmMemoryMb.Scope);
        Assert.Equal(BuilderDraftFieldScope.Nic, TemplatesBuilderFieldKeys.NicPrefixLength.Scope);
    }

    [Fact]
    public void TemplatesBuilderRoleProjectionCatalog_ExposesOnlyAdDcAsAuthorableRole()
    {
        var draft = CreateConfirmedBuilderDraft();

        var catalogRole = Assert.Single(TemplatesBuilderRoleProjectionCatalog.GetAuthorableRoles());
        var assignedRole = Assert.Single(TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles(draft.Vms[0]));
        var unassignedRole = Assert.Single(TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles(draft.Vms[1]));

        Assert.Equal(TemplatesBuilderRoleProjectionCatalog.ActiveDirectoryDomainControllerRoleKey, catalogRole.RoleKey);
        Assert.Equal("Active Directory Domain Controller", catalogRole.DisplayName);
        Assert.True(catalogRole.IsAuthorable);
        Assert.True(assignedRole.IsAssigned);
        Assert.False(unassignedRole.IsAssigned);
        Assert.True(TemplatesBuilderRoleProjectionCatalog.IsActiveDirectoryDomainControllerTopologyRole("FirstDomainController"));
        Assert.True(TemplatesBuilderRoleProjectionCatalog.IsActiveDirectoryDomainControllerTopologyRole("RootDomainController"));
        Assert.DoesNotContain(TemplatesBuilderRoleProjectionCatalog.GetAuthorableRoles(), role => role.DisplayName.Contains("Root CA", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(TemplatesBuilderRoleProjectionCatalog.GetAuthorableRoles(), role => role.DisplayName.Contains("SQL", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(TemplatesBuilderRoleProjectionCatalog.GetAuthorableRoles(), role => role.DisplayName.Contains("Web", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(TemplatesBuilderRoleProjectionCatalog.GetAuthorableRoles(), role => role.DisplayName.Contains("Operations", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TemplatesBuilderView_KeepsFutureRoleConfigUiOutOfFirstSlice()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var roleCatalogSource = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderRoleProjectionCatalog.cs")));
        var rolesRenderBody = ExtractSwitchCaseBody(
            builderSource,
            "case BuilderVmDetailCategory.Roles:",
            "case BuilderVmDetailCategory.Networking:");

        Assert.Contains("TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles", File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderSectionProjections.cs"))));
        Assert.Contains("role.DisplayName", rolesRenderBody);
        Assert.Contains("TemplatesBuilderFieldKeys.VmIsActiveDirectoryDomainController", rolesRenderBody);
        Assert.Contains("Active Directory Domain Controller", roleCatalogSource);
        Assert.DoesNotContain("Root CA", rolesRenderBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", rolesRenderBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Web", rolesRenderBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Operations", rolesRenderBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TemplatesBuilderView_FooterRemovesManualValidationAndShowsReviewOnlySaveActions()
    {
        var builder = LoadXaml(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml"));
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var updateFooterBody = ExtractMethodBody(builderSource, "private void UpdateFooterCommandState(TemplatesBuilderActionState state)");
        var compositionSource = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderWorkspaceComposition.cs")));
        var navigation = new TemplatesBuilderWorkflowNavigation();
        var draft = CreateDraftWithVms("vm-alpha");

        Assert.Null(FindByNameOrDefault(builder, "BuilderApplySuggestionsButton"));
        Assert.Null(FindByNameOrDefault(builder, "BuilderValidateButton"));
        Assert.DoesNotContain("ApplySuggestionsRequested", builderSource);
        Assert.DoesNotContain("ValidateRequested", builderSource);
        Assert.DoesNotContain("ApplySuggestionsRequested", compositionSource);
        Assert.DoesNotContain("ValidateRequested", compositionSource);
        Assert.Contains("ProjectFooter(_draft, _canNavigateWorkflow, state.CanSave, state.CanSaveAs)", updateFooterBody);

        var firstFooter = navigation.ProjectFooter(draft, canNavigate: true, canSave: true, canSaveAs: true);
        Assert.False(firstFooter.IsReview);
        Assert.False(firstFooter.CanGoPrevious);
        Assert.True(firstFooter.CanGoNext);
        Assert.False(firstFooter.CanSave);
        Assert.False(firstFooter.CanSaveAs);

        navigation.SelectStep(BuilderWorkflowStep.Review, draft);
        var reviewFooter = navigation.ProjectFooter(draft, canNavigate: true, canSave: true, canSaveAs: true);
        Assert.True(reviewFooter.IsReview);
        Assert.True(reviewFooter.CanGoPrevious);
        Assert.False(reviewFooter.CanGoNext);
        Assert.True(reviewFooter.CanSave);
        Assert.True(reviewFooter.CanSaveAs);
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

    [Theory]
    [InlineData("Conservative")]
    [InlineData("Balanced")]
    [InlineData("Aggressive")]
    public void BuilderDraftMapper_PreservesDeploymentProfileValue(string deploymentProfile)
    {
        var draft = CreateConfirmedBuilderDraft() with { DeploymentProfile = deploymentProfile };

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft,
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Empty(build.Errors);
        var template = Assert.IsType<TemplateEditorDocument>(build.Document).Template;
        Assert.Equal(deploymentProfile, template.DeploymentProfile);
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
    public async Task BuilderSavedTemplate_FeedsDeployV2ReviewProjection()
    {
        var savedDocument = await CreateSavedBuilderDocumentAsync();
        var template = savedDocument.Template;

        Assert.Equal(TemplateSchemaVersionCatalog.V2SchemaVersion, template.SchemaVersion);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, template.ExecutionEngine);
        Assert.NotNull(template.LabNetworks);
        Assert.Equal("vSwitch-Core", Assert.Single(template.LabNetworks!).SwitchName);
        Assert.Equal(2, template.VmTemplates.Count);
        var templateDomain = Assert.Single(template.DirectoryTopology!.Domains!);
        Assert.Equal("domain-contoso", templateDomain.DomainId);
        Assert.Equal("vm-dc01", templateDomain.FirstDomainControllerVmId);
        Assert.Contains(template.VmTemplates, vm => string.Equals(vm.CredentialSlots?.DomainJoin, "slot-join", StringComparison.Ordinal));

        var workspace = new DeployV2ReviewWorkspaceViewModel();
        var host = new RecordingV2ReviewHost(savedDocument);
        var controller = new DeployV2ReviewWorkspaceController(
            workspace,
            new DeployV2ReviewProjectionService(),
            host);

        await controller.RefreshPlanAsync(template);

        Assert.Equal(1, host.EnsureReferenceDataCalls);
        Assert.Equal(1, host.BuildPlanCalls);
        Assert.True(workspace.IsVisible);
        Assert.False(workspace.IsPlanning);
        Assert.True(workspace.CanStartDeploy);
        Assert.False(workspace.HasBlockingItems);
        Assert.Empty(workspace.BlockerRows);
        Assert.Empty(workspace.CredentialSlotRows);
        Assert.Equal(["slot-admin", "slot-dsrm", "slot-join", "slot-local"], host.LastResolvedCredentialSlotKeys);
        Assert.Equal(4, workspace.ResolvedCredentialSlotValues.Count);

        Assert.NotNull(workspace.PlanSummary);
        Assert.Equal("V2 Topology Template", workspace.PlanSummary!.TemplateName);
        Assert.Equal("V2 Unified Planning", workspace.PlanSummary.ExecutionEngine);
        Assert.Equal("Balanced", workspace.PlanSummary.DeploymentProfile);
        Assert.Equal(2, workspace.PlanSummary.VmCount);
        Assert.Equal("Domain-aware", workspace.PlanSummary.DomainSummary);
        Assert.Equal("Startable", workspace.PlanSummary.StartabilitySummary);
        Assert.Equal(0, workspace.PlanSummary.UnresolvedRequirementCount);
        Assert.NotEmpty(workspace.WaveRows);
        Assert.Contains(workspace.DiagnosticRows, row =>
            string.Equals(row.Title, "Nodes", StringComparison.Ordinal) &&
            string.Equals(row.Detail, $"{workspace.PlanSummary.NodeCount} node(s)", StringComparison.Ordinal));

        Assert.NotNull(workspace.CurrentPlan);
        var reviewPlan = workspace.CurrentPlan!;
        Assert.True(reviewPlan.Success);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, reviewPlan.Context.ExecutionEngine);
        Assert.True(reviewPlan.Context.DomainSemanticsRequired);
        Assert.Equal(2, reviewPlan.Context.Vms.Count);
        var reviewDomain = Assert.Single(reviewPlan.Context.Domains);
        Assert.Equal("domain-contoso", reviewDomain.DomainId);
        Assert.Equal("vm-dc01", reviewDomain.FirstDomainControllerVmId);

        var dcVm = Assert.Single(reviewPlan.Context.Vms, vm => string.Equals(vm.VmId, "vm-dc01", StringComparison.Ordinal));
        Assert.Equal("slot-local", dcVm.EffectiveBootstrapCredentialSlot);
        Assert.Equal("slot-admin", dcVm.EffectiveDomainAdminCredentialSlot);
        Assert.Equal("slot-dsrm", dcVm.EffectiveDsrmCredentialSlot);
        var dcNic = Assert.Single(dcVm.Nics);
        Assert.Equal("lab-core", dcNic.NetworkId);
        Assert.Equal("vSwitch-Core", dcNic.EffectiveSwitchName);
        Assert.Equal("10.0.0.10", dcNic.IpAddress);

        var memberVm = Assert.Single(reviewPlan.Context.Vms, vm => string.Equals(vm.VmId, "vm-member01", StringComparison.Ordinal));
        Assert.Equal("slot-local", memberVm.EffectiveBootstrapCredentialSlot);
        Assert.Equal("slot-admin", memberVm.EffectiveDomainAdminCredentialSlot);
        Assert.Equal("slot-join", memberVm.EffectiveDomainJoinCredentialSlot);
        Assert.True(memberVm.RequiresDomainJoin);

        var nodeKinds = reviewPlan.Nodes.Select(node => node.Kind).ToArray();
        Assert.Contains(V2PlanNodeKind.PromoteFirstDomainController, nodeKinds);
        Assert.Contains(V2PlanNodeKind.JoinDomain, nodeKinds);
        Assert.Contains(V2PlanNodeKind.ConfigureBaseRemoteAccess, nodeKinds);

        var runtimeResult = await controller.StartDeployAsync(template);
        Assert.True(runtimeResult.Success);
        Assert.Same(reviewPlan, host.LastDeployPlan);
        Assert.NotNull(host.LastBaseRemoteAccessOptions);
        Assert.True(host.LastBaseRemoteAccessOptions!.EnableRemoteDesktop);
        Assert.True(host.LastBaseRemoteAccessOptions.SetPrivateNetworkProfile);
        Assert.True(host.LastBaseRemoteAccessOptions.DisableFirewall);
        Assert.True(host.LastBaseRemoteAccessOptions.DisableRdpNla);
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

    private static async Task<TemplateEditorDocument> CreateSavedBuilderDocumentAsync()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var controller = new TemplatesBuilderWorkspaceController(service, workspace, host);

        workspace.LoadNewDraft(CreateConfirmedBuilderDraft());
        await controller.SaveAsync();

        Assert.Equal(1, service.SaveCalls);
        Assert.NotNull(service.LastSavedDocument);
        return service.LastSavedDocument!;
    }

    private static TemplatesBuilderDraftSnapshot CreateDraftWithVms(params string[] vmIds)
        => new(
            "Builder draft",
            "Draft for navigation tests.",
            "Balanced",
            [],
            [],
            [],
            [],
            vmIds
                .Select(vmId => new TemplatesBuilderVmDraft(
                    vmId,
                    vmId,
                    "4096",
                    "2",
                    string.Empty,
                    V2MembershipModeCatalog.Standalone,
                    string.Empty,
                    false,
                    new TemplatesBuilderVmCredentialSlotDraft(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                    []))
                .ToList(),
            false);

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

    private sealed class RecordingV2ReviewHost : IDeployFromTemplateV2ReviewHost
    {
        private readonly Dictionary<string, V2RuntimeCredential> _credentialValues = new(StringComparer.OrdinalIgnoreCase)
        {
            ["slot-local"] = new V2RuntimeCredential { Username = "local-admin", Password = "local-password" },
            ["slot-admin"] = new V2RuntimeCredential { Username = "CONTOSO\\Administrator", Password = "domain-password" },
            ["slot-join"] = new V2RuntimeCredential { Username = "CONTOSO\\Joiner", Password = "join-password" },
            ["slot-dsrm"] = new V2RuntimeCredential { Username = "DSRM", Password = "dsrm-password" }
        };

        public RecordingV2ReviewHost(TemplateEditorDocument activeTemplateDocument)
        {
            ActiveTemplateDocument = activeTemplateDocument;
        }

        public TemplateEditorDocument? ActiveTemplateDocument { get; }

        public int EnsureReferenceDataCalls { get; private set; }

        public int BuildPlanCalls { get; private set; }

        public IReadOnlyList<string> LastResolvedCredentialSlotKeys { get; private set; } = Array.Empty<string>();

        public V2PlanBuildResult? LastDeployPlan { get; private set; }

        public V2BaseRemoteAccessOptions? LastBaseRemoteAccessOptions { get; private set; }

        public Task EnsureReferenceDataAsync(bool forceRefresh)
        {
            EnsureReferenceDataCalls++;
            Assert.False(forceRefresh);
            return Task.CompletedTask;
        }

        public IReadOnlyList<LocalCredentialSlotDefinition> LoadLocalCredentialSlotDefinitions()
            => _credentialValues
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new LocalCredentialSlotDefinition
                {
                    SlotKey = pair.Key,
                    Username = pair.Value.Username
                })
                .ToList();

        public bool TryGetLocalCredentialSlotValue(string slotKey, out V2RuntimeCredential credential)
        {
            if (_credentialValues.TryGetValue(slotKey, out var value))
            {
                credential = value;
                return true;
            }

            credential = new V2RuntimeCredential();
            return false;
        }

        public void UpsertLocalCredentialSlot(string slotKey, string username, string password)
        {
            _credentialValues[slotKey] = new V2RuntimeCredential { Username = username, Password = password };
        }

        public async Task<V2PlanBuildResult> BuildV2PlanAsync(
            LabTemplate template,
            IReadOnlyCollection<string> resolvedCredentialSlotKeys)
        {
            BuildPlanCalls++;
            LastResolvedCredentialSlotKeys = resolvedCredentialSlotKeys
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var planner = new V2PlanningCapabilityService();
            return await planner.BuildPlanAsync(new V2PlanBuildRequest
            {
                Template = template,
                CatalogItems =
                [
                    CreateCatalogItem("disk-dc", @"C:\base\disk-dc.vhdx", "sig-dc"),
                    CreateCatalogItem("disk-member", @"C:\base\disk-member.vhdx", "sig-member")
                ],
                AvailableSwitchNames = ["vSwitch-Core"],
                AvailableSwitches = [new V2AvailableSwitchInfo { Name = "vSwitch-Core", SwitchType = "Internal" }],
                ResolvedCredentialSlotKeys = resolvedCredentialSlotKeys,
                DefaultDeploymentProfile = "Balanced"
            });
        }

        public Task<V2RuntimeExecutionResult> ExecuteV2DeployAsync(
            LabTemplate template,
            V2PlanBuildResult plan,
            IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
            V2BaseRemoteAccessOptions baseRemoteAccessOptions,
            MultiVmDeploymentContext deploymentContext)
        {
            LastDeployPlan = plan;
            LastBaseRemoteAccessOptions = baseRemoteAccessOptions;
            return Task.FromResult(new V2RuntimeExecutionResult
            {
                Success = true,
                DeploymentContext = deploymentContext,
                ExecutedNodeIds = plan.Nodes.Select(node => node.NodeId).ToList()
            });
        }

        public void ApplyWorkspaceState()
        {
        }
    }
}
