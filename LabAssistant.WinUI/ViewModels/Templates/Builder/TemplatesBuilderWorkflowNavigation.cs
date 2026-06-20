namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal enum BuilderWorkflowStep
{
    General,
    Networks,
    ForestsDomains,
    Credentials,
    Vms,
    Review
}

internal enum BuilderVmDetailCategory
{
    Basics,
    Resources,
    Membership,
    Roles,
    Networking,
    Credentials
}

internal enum BuilderWorkflowRouteKind
{
    Step,
    VmOverview,
    VmCategory,
    VmRole,
    VmNic
}

internal enum BuilderNavigatorDepth
{
    Root,
    VmList,
    VmSections
}

internal readonly record struct BuilderWorkflowRoute(
    BuilderWorkflowRouteKind Kind,
    BuilderWorkflowStep Step,
    int VmIndex = -1,
    BuilderVmDetailCategory VmDetailCategory = BuilderVmDetailCategory.Basics,
    string RoleKey = "",
    int NicIndex = -1)
{
    public static BuilderWorkflowRoute ForStep(BuilderWorkflowStep step)
        => step == BuilderWorkflowStep.Vms
            ? VmOverview()
            : new BuilderWorkflowRoute(BuilderWorkflowRouteKind.Step, step);

    public static BuilderWorkflowRoute VmOverview()
        => new(BuilderWorkflowRouteKind.VmOverview, BuilderWorkflowStep.Vms);

    public static BuilderWorkflowRoute ForVmCategory(int vmIndex, BuilderVmDetailCategory category)
        => new(BuilderWorkflowRouteKind.VmCategory, BuilderWorkflowStep.Vms, vmIndex, category);

    public static BuilderWorkflowRoute ForVmRole(int vmIndex, string roleKey)
        => new(BuilderWorkflowRouteKind.VmRole, BuilderWorkflowStep.Vms, vmIndex, BuilderVmDetailCategory.Roles, roleKey);

    public static BuilderWorkflowRoute ForVmNic(int vmIndex, int nicIndex)
        => new(BuilderWorkflowRouteKind.VmNic, BuilderWorkflowStep.Vms, vmIndex, BuilderVmDetailCategory.Networking, NicIndex: nicIndex);

    public bool IsReview => Step == BuilderWorkflowStep.Review;

    public bool IsVmDetail => Kind is BuilderWorkflowRouteKind.VmCategory or BuilderWorkflowRouteKind.VmRole or BuilderWorkflowRouteKind.VmNic;

    public bool IsNicDetail => Kind == BuilderWorkflowRouteKind.VmNic;
}

internal readonly record struct BuilderWorkflowNavigationRow(
    BuilderWorkflowRoute Route,
    string Label,
    bool IsSelected,
    bool IsEnabled);

internal readonly record struct BuilderWorkflowProjection(
    BuilderWorkflowRoute CurrentRoute,
    IReadOnlyList<BuilderWorkflowNavigationRow> RootRows,
    IReadOnlyList<BuilderWorkflowNavigationRow> VmRows,
    IReadOnlyList<BuilderWorkflowNavigationRow> SelectedVmSectionRows,
    BuilderNavigatorDepth NavigatorDepth,
    string NavigatorTitle,
    string NavigatorBackTargetLabel,
    bool CanNavigateBack,
    BuilderWorkflowStep ActiveStep,
    int SelectedVmIndex,
    BuilderVmDetailCategory SelectedVmDetailCategory,
    int SelectedNicIndex,
    bool IsVmDetailSelected,
    bool IsNicDetailSelected,
    bool IsVmOverviewSelected);

internal readonly record struct BuilderWorkflowFooterProjection(
    bool CanGoPrevious,
    bool CanGoNext,
    bool IsReview,
    bool CanSave,
    bool CanSaveAs,
    string PreviousTargetLabel,
    string NextTargetLabel);

internal sealed class TemplatesBuilderWorkflowNavigation
{
    private static readonly BuilderVmDetailCategory[] VmDetailCategoryOrder =
    [
        BuilderVmDetailCategory.Basics,
        BuilderVmDetailCategory.Resources,
        BuilderVmDetailCategory.Membership,
        BuilderVmDetailCategory.Roles,
        BuilderVmDetailCategory.Networking,
        BuilderVmDetailCategory.Credentials
    ];

    public BuilderWorkflowRoute CurrentRoute { get; private set; } = BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.General);

    private BuilderNavigatorDepth _navigatorDepth = BuilderNavigatorDepth.Root;

    public BuilderWorkflowProjection Project(TemplatesBuilderDraftSnapshot draft, bool canNavigate)
    {
        EnsureCurrentRouteInBounds(draft);
        EnsureNavigatorDepthInBounds(draft.Vms.Count);
        var selectedVmIndex = CurrentRoute.IsVmDetail ? CurrentRoute.VmIndex : 0;
        var selectedVmDetailCategory = CurrentRoute.IsVmDetail
            ? CurrentRoute.VmDetailCategory
            : BuilderVmDetailCategory.Basics;
        var selectedNicIndex = CurrentRoute.IsNicDetail ? CurrentRoute.NicIndex : -1;

        return new BuilderWorkflowProjection(
            CurrentRoute,
            CreateRootRows(canNavigate),
            CreateVmRows(draft, canNavigate),
            _navigatorDepth == BuilderNavigatorDepth.VmSections
                ? CreateSelectedVmSectionRows(draft, selectedVmIndex, canNavigate)
                : [],
            _navigatorDepth,
            CreateNavigatorTitle(draft, selectedVmIndex),
            CreateNavigatorBackTargetLabel(),
            _navigatorDepth != BuilderNavigatorDepth.Root,
            CurrentRoute.Step,
            selectedVmIndex,
            selectedVmDetailCategory,
            selectedNicIndex,
            CurrentRoute.IsVmDetail,
            CurrentRoute.IsNicDetail,
            CurrentRoute.Kind == BuilderWorkflowRouteKind.VmOverview);
    }

    public BuilderWorkflowFooterProjection ProjectFooter(
        TemplatesBuilderDraftSnapshot draft,
        bool canNavigate,
        bool canSave,
        bool canSaveAs)
    {
        EnsureCurrentRouteInBounds(draft);
        var routes = BuildRoutes(draft);
        var currentIndex = FindRouteIndex(routes, CurrentRoute);
        var isReview = CurrentRoute.IsReview;
        var canGoPrevious = canNavigate && currentIndex > 0;
        var canGoNext = canNavigate && !isReview && currentIndex >= 0 && currentIndex < routes.Count - 1;

        return new BuilderWorkflowFooterProjection(
            canGoPrevious,
            canGoNext,
            isReview,
            isReview && canSave,
            isReview && canSaveAs,
            canGoPrevious ? FormatRouteLabel(routes[currentIndex - 1], draft) : string.Empty,
            canGoNext ? FormatRouteLabel(routes[currentIndex + 1], draft) : string.Empty);
    }

    public bool SelectStep(BuilderWorkflowStep step, TemplatesBuilderDraftSnapshot draft)
        => SelectRoute(BuilderWorkflowRoute.ForStep(step), draft);

    public bool SelectVmOverview(TemplatesBuilderDraftSnapshot draft)
        => SelectRoute(BuilderWorkflowRoute.VmOverview(), draft);

    public bool SelectVmChild(int index, TemplatesBuilderDraftSnapshot draft)
        => SelectRoute(BuilderWorkflowRoute.ForVmCategory(index, BuilderVmDetailCategory.Basics), draft);

    public bool SelectVmDetailCategory(BuilderVmDetailCategory category, TemplatesBuilderDraftSnapshot draft)
    {
        if (!CurrentRoute.IsVmDetail)
        {
            return false;
        }

        return SelectRoute(BuilderWorkflowRoute.ForVmCategory(CurrentRoute.VmIndex, category), draft);
    }

    public bool SelectVmNic(int nicIndex, TemplatesBuilderDraftSnapshot draft)
    {
        if (!CurrentRoute.IsVmDetail)
        {
            return false;
        }

        return SelectRoute(BuilderWorkflowRoute.ForVmNic(CurrentRoute.VmIndex, nicIndex), draft);
    }

    public bool SelectAdjacent(int offset, TemplatesBuilderDraftSnapshot draft)
    {
        EnsureCurrentRouteInBounds(draft);
        var routes = BuildRoutes(draft);
        var currentIndex = FindRouteIndex(routes, CurrentRoute);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= routes.Count)
        {
            return false;
        }

        CurrentRoute = routes[targetIndex];
        _navigatorDepth = GetDefaultNavigatorDepth(CurrentRoute);
        return true;
    }

    public bool SelectRoute(BuilderWorkflowRoute route, TemplatesBuilderDraftSnapshot draft)
    {
        var selectedRoute = NormalizeRoute(route, draft);
        var targetDepth = GetDefaultNavigatorDepth(selectedRoute);
        if (selectedRoute == CurrentRoute &&
            targetDepth == _navigatorDepth)
        {
            return false;
        }

        CurrentRoute = selectedRoute;
        _navigatorDepth = targetDepth;
        return true;
    }

    public bool MoveNavigatorBack()
    {
        if (_navigatorDepth == BuilderNavigatorDepth.VmSections)
        {
            _navigatorDepth = BuilderNavigatorDepth.VmList;
            return true;
        }

        if (_navigatorDepth == BuilderNavigatorDepth.VmList)
        {
            _navigatorDepth = BuilderNavigatorDepth.Root;
            return true;
        }

        return false;
    }

    public void EnsureCurrentRouteInBounds(TemplatesBuilderDraftSnapshot draft)
    {
        var normalizedRoute = NormalizeRoute(CurrentRoute, draft);
        if (normalizedRoute != CurrentRoute)
        {
            CurrentRoute = normalizedRoute;
        }
    }

    public static IReadOnlyList<BuilderWorkflowRoute> BuildRoutes(TemplatesBuilderDraftSnapshot draft)
    {
        var routes = new List<BuilderWorkflowRoute>
        {
            BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.General),
            BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Networks),
            BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.ForestsDomains),
            BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Credentials),
            BuilderWorkflowRoute.VmOverview()
        };

        for (var vmIndex = 0; vmIndex < draft.Vms.Count; vmIndex++)
        {
            foreach (var category in VmDetailCategoryOrder)
            {
                routes.Add(BuilderWorkflowRoute.ForVmCategory(vmIndex, category));
                if (category == BuilderVmDetailCategory.Networking)
                {
                    for (var nicIndex = 0; nicIndex < (draft.Vms[vmIndex].Nics?.Count ?? 0); nicIndex++)
                    {
                        routes.Add(BuilderWorkflowRoute.ForVmNic(vmIndex, nicIndex));
                    }
                }
            }
        }

        routes.Add(BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Review));
        return routes;
    }

    private void EnsureNavigatorDepthInBounds(int vmCount)
    {
        if (CurrentRoute.Step != BuilderWorkflowStep.Vms)
        {
            _navigatorDepth = BuilderNavigatorDepth.Root;
            return;
        }

        if (vmCount == 0 &&
            _navigatorDepth == BuilderNavigatorDepth.VmSections)
        {
            _navigatorDepth = BuilderNavigatorDepth.VmList;
        }
    }

    private static BuilderWorkflowRoute NormalizeRoute(BuilderWorkflowRoute route, int vmCount)
    {
        if (route.Step != BuilderWorkflowStep.Vms)
        {
            return BuilderWorkflowRoute.ForStep(route.Step);
        }

        if (!route.IsVmDetail || vmCount == 0)
        {
            return BuilderWorkflowRoute.VmOverview();
        }

        var vmIndex = ClampIndex(route.VmIndex, vmCount);
        return route.Kind switch
        {
            BuilderWorkflowRouteKind.VmRole => BuilderWorkflowRoute.ForVmRole(vmIndex, route.RoleKey),
            BuilderWorkflowRouteKind.VmNic => BuilderWorkflowRoute.ForVmNic(vmIndex, Math.Max(0, route.NicIndex)),
            _ => BuilderWorkflowRoute.ForVmCategory(vmIndex, route.VmDetailCategory)
        };
    }

    private IReadOnlyList<BuilderWorkflowNavigationRow> CreateRootRows(bool canNavigate)
        =>
        [
            CreateRootRow("General", BuilderWorkflowStep.General, canNavigate),
            CreateRootRow("Networks", BuilderWorkflowStep.Networks, canNavigate),
            CreateRootRow("Forests & Domains", BuilderWorkflowStep.ForestsDomains, canNavigate),
            CreateRootRow("Credentials", BuilderWorkflowStep.Credentials, canNavigate),
            CreateRootRow("VMs", BuilderWorkflowStep.Vms, canNavigate),
            CreateRootRow("Review", BuilderWorkflowStep.Review, canNavigate)
        ];

    private BuilderWorkflowNavigationRow CreateRootRow(string label, BuilderWorkflowStep step, bool canNavigate)
    {
        var route = BuilderWorkflowRoute.ForStep(step);
        var isSelected = step == BuilderWorkflowStep.Vms
            ? CurrentRoute.Step == BuilderWorkflowStep.Vms
            : CurrentRoute == route;
        return new BuilderWorkflowNavigationRow(route, label, isSelected, canNavigate);
    }

    private IReadOnlyList<BuilderWorkflowNavigationRow> CreateVmRows(TemplatesBuilderDraftSnapshot draft, bool canNavigate)
    {
        var rows = new List<BuilderWorkflowNavigationRow>(draft.Vms.Count);
        for (var index = 0; index < draft.Vms.Count; index++)
        {
            var vm = draft.Vms[index];
            rows.Add(new BuilderWorkflowNavigationRow(
                BuilderWorkflowRoute.ForVmCategory(index, BuilderVmDetailCategory.Basics),
                FormatResourceName(vm.Name, vm.VmId),
                CurrentRoute.IsVmDetail && CurrentRoute.VmIndex == index,
                canNavigate));
        }

        return rows;
    }

    private IReadOnlyList<BuilderWorkflowNavigationRow> CreateSelectedVmSectionRows(
        TemplatesBuilderDraftSnapshot draft,
        int selectedVmIndex,
        bool canNavigate)
    {
        if (draft.Vms.Count == 0 ||
            selectedVmIndex < 0 ||
            selectedVmIndex >= draft.Vms.Count)
        {
            return [];
        }

        var rows = new List<BuilderWorkflowNavigationRow>(VmDetailCategoryOrder.Length);
        foreach (var category in VmDetailCategoryOrder)
        {
            var route = BuilderWorkflowRoute.ForVmCategory(selectedVmIndex, category);
            var isSelected = CurrentRoute == route ||
                CurrentRoute.Kind == BuilderWorkflowRouteKind.VmRole &&
                CurrentRoute.VmIndex == selectedVmIndex &&
                category == BuilderVmDetailCategory.Roles ||
                CurrentRoute.Kind == BuilderWorkflowRouteKind.VmNic &&
                CurrentRoute.VmIndex == selectedVmIndex &&
                category == BuilderVmDetailCategory.Networking;
            rows.Add(new BuilderWorkflowNavigationRow(
                route,
                GetVmDetailCategoryLabel(category),
                isSelected,
                canNavigate));
        }

        return rows;
    }

    private string CreateNavigatorTitle(TemplatesBuilderDraftSnapshot draft, int selectedVmIndex)
        => _navigatorDepth switch
        {
            BuilderNavigatorDepth.VmList => "VMs",
            BuilderNavigatorDepth.VmSections when selectedVmIndex >= 0 && selectedVmIndex < draft.Vms.Count => FormatResourceName(draft.Vms[selectedVmIndex].Name, draft.Vms[selectedVmIndex].VmId),
            BuilderNavigatorDepth.VmSections => "VM Sections",
            _ => "Builder"
        };

    private string CreateNavigatorBackTargetLabel()
        => _navigatorDepth switch
        {
            BuilderNavigatorDepth.VmList => "Back to Builder",
            BuilderNavigatorDepth.VmSections => "Back to VMs",
            _ => string.Empty
        };

    private static BuilderNavigatorDepth GetDefaultNavigatorDepth(BuilderWorkflowRoute route)
    {
        if (route.Step != BuilderWorkflowStep.Vms)
        {
            return BuilderNavigatorDepth.Root;
        }

        return route.IsVmDetail ? BuilderNavigatorDepth.VmSections : BuilderNavigatorDepth.VmList;
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (index < 0)
        {
            return 0;
        }

        return index >= count ? count - 1 : index;
    }

    private static int FindRouteIndex(IReadOnlyList<BuilderWorkflowRoute> routes, BuilderWorkflowRoute route)
    {
        for (var index = 0; index < routes.Count; index++)
        {
            if (routes[index] == route)
            {
                return index;
            }
        }

        return -1;
    }

    private static string FormatResourceName(string primary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "(unnamed)" : fallback.Trim();
    }

    private static string FormatRouteLabel(BuilderWorkflowRoute route, TemplatesBuilderDraftSnapshot draft)
        => route.Step switch
        {
            BuilderWorkflowStep.General => "General",
            BuilderWorkflowStep.Networks => "Networks",
            BuilderWorkflowStep.ForestsDomains => "Forests & Domains",
            BuilderWorkflowStep.Credentials => "Credentials",
            BuilderWorkflowStep.Vms when route.Kind == BuilderWorkflowRouteKind.VmOverview => "VMs",
            BuilderWorkflowStep.Vms => FormatVmRouteLabel(route, draft),
            BuilderWorkflowStep.Review => "Review",
            _ => route.Step.ToString()
        };

    private static string FormatVmRouteLabel(BuilderWorkflowRoute route, TemplatesBuilderDraftSnapshot draft)
    {
        var vmName = route.VmIndex >= 0 && route.VmIndex < draft.Vms.Count
            ? FormatResourceName(draft.Vms[route.VmIndex].Name, draft.Vms[route.VmIndex].VmId)
            : "VM";
        if (route.Kind == BuilderWorkflowRouteKind.VmNic &&
            route.VmIndex >= 0 &&
            route.VmIndex < draft.Vms.Count)
        {
            return $"{vmName} {FormatNicRouteLabel(draft.Vms[route.VmIndex], route.NicIndex)}";
        }

        return $"{vmName} {GetVmDetailCategoryLabel(route.VmDetailCategory)}";
    }

    private static BuilderWorkflowRoute NormalizeRoute(BuilderWorkflowRoute route, TemplatesBuilderDraftSnapshot draft)
    {
        var normalizedRoute = NormalizeRoute(route, draft.Vms.Count);
        if (normalizedRoute.Kind != BuilderWorkflowRouteKind.VmNic ||
            normalizedRoute.VmIndex < 0 ||
            normalizedRoute.VmIndex >= draft.Vms.Count)
        {
            return normalizedRoute;
        }

        var nicCount = draft.Vms[normalizedRoute.VmIndex].Nics?.Count ?? 0;
        return nicCount == 0
            ? BuilderWorkflowRoute.ForVmCategory(normalizedRoute.VmIndex, BuilderVmDetailCategory.Networking)
            : BuilderWorkflowRoute.ForVmNic(normalizedRoute.VmIndex, ClampIndex(normalizedRoute.NicIndex, nicCount));
    }

    private static string FormatNicRouteLabel(TemplatesBuilderVmDraft vm, int nicIndex)
    {
        if (vm.Nics is null || nicIndex < 0 || nicIndex >= vm.Nics.Count)
        {
            return "Networking";
        }

        return $"Networking - {FormatResourceName(vm.Nics[nicIndex].Name, vm.Nics[nicIndex].NicId)}";
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
}
