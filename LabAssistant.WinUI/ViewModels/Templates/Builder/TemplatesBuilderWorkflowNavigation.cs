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
    VmRole
}

internal readonly record struct BuilderWorkflowRoute(
    BuilderWorkflowRouteKind Kind,
    BuilderWorkflowStep Step,
    int VmIndex = -1,
    BuilderVmDetailCategory VmDetailCategory = BuilderVmDetailCategory.Basics,
    string RoleKey = "")
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

    public bool IsReview => Step == BuilderWorkflowStep.Review;

    public bool IsVmDetail => Kind is BuilderWorkflowRouteKind.VmCategory or BuilderWorkflowRouteKind.VmRole;
}

internal readonly record struct BuilderWorkflowNavigationRow(
    BuilderWorkflowRoute Route,
    string Label,
    bool IsSelected,
    bool IsEnabled);

internal readonly record struct BuilderWorkflowProjection(
    BuilderWorkflowRoute CurrentRoute,
    IReadOnlyList<BuilderWorkflowNavigationRow> StepRows,
    BuilderWorkflowNavigationRow VmOverviewRow,
    IReadOnlyList<BuilderWorkflowNavigationRow> VmRows,
    BuilderWorkflowStep ActiveStep,
    int SelectedVmIndex,
    BuilderVmDetailCategory SelectedVmDetailCategory,
    bool IsVmDetailSelected,
    bool IsVmOverviewSelected);

internal readonly record struct BuilderWorkflowFooterProjection(
    bool CanGoPrevious,
    bool CanGoNext,
    bool IsReview,
    bool CanSave,
    bool CanSaveAs);

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

    public BuilderWorkflowProjection Project(TemplatesBuilderDraftSnapshot draft, bool canNavigate)
    {
        EnsureCurrentRouteInBounds(draft.Vms.Count);
        var selectedVmIndex = CurrentRoute.IsVmDetail ? CurrentRoute.VmIndex : 0;
        var selectedVmDetailCategory = CurrentRoute.IsVmDetail
            ? CurrentRoute.VmDetailCategory
            : BuilderVmDetailCategory.Basics;

        return new BuilderWorkflowProjection(
            CurrentRoute,
            CreateStepRows(canNavigate),
            CreateVmOverviewRow(canNavigate),
            CreateVmRows(draft, canNavigate),
            CurrentRoute.Step,
            selectedVmIndex,
            selectedVmDetailCategory,
            CurrentRoute.IsVmDetail,
            CurrentRoute.Kind == BuilderWorkflowRouteKind.VmOverview);
    }

    public BuilderWorkflowFooterProjection ProjectFooter(
        TemplatesBuilderDraftSnapshot draft,
        bool canNavigate,
        bool canSave,
        bool canSaveAs)
    {
        EnsureCurrentRouteInBounds(draft.Vms.Count);
        var routes = BuildRoutes(draft);
        var currentIndex = FindRouteIndex(routes, CurrentRoute);
        var isReview = CurrentRoute.IsReview;

        return new BuilderWorkflowFooterProjection(
            canNavigate && currentIndex > 0,
            canNavigate && !isReview && currentIndex >= 0 && currentIndex < routes.Count - 1,
            isReview,
            isReview && canSave,
            isReview && canSaveAs);
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

    public bool SelectAdjacent(int offset, TemplatesBuilderDraftSnapshot draft)
    {
        EnsureCurrentRouteInBounds(draft.Vms.Count);
        var routes = BuildRoutes(draft);
        var currentIndex = FindRouteIndex(routes, CurrentRoute);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= routes.Count)
        {
            return false;
        }

        CurrentRoute = routes[targetIndex];
        return true;
    }

    public bool SelectRoute(BuilderWorkflowRoute route, TemplatesBuilderDraftSnapshot draft)
    {
        var selectedRoute = NormalizeRoute(route, draft.Vms.Count);
        if (selectedRoute == CurrentRoute)
        {
            return false;
        }

        CurrentRoute = selectedRoute;
        return true;
    }

    public void EnsureCurrentRouteInBounds(TemplatesBuilderDraftSnapshot draft)
        => EnsureCurrentRouteInBounds(draft.Vms.Count);

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
            }
        }

        routes.Add(BuilderWorkflowRoute.ForStep(BuilderWorkflowStep.Review));
        return routes;
    }

    private void EnsureCurrentRouteInBounds(int vmCount)
    {
        var normalizedRoute = NormalizeRoute(CurrentRoute, vmCount);
        if (normalizedRoute != CurrentRoute)
        {
            CurrentRoute = normalizedRoute;
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
        return route.Kind == BuilderWorkflowRouteKind.VmRole
            ? BuilderWorkflowRoute.ForVmRole(vmIndex, route.RoleKey)
            : BuilderWorkflowRoute.ForVmCategory(vmIndex, route.VmDetailCategory);
    }

    private IReadOnlyList<BuilderWorkflowNavigationRow> CreateStepRows(bool canNavigate)
        =>
        [
            CreateStepRow("General", BuilderWorkflowStep.General, canNavigate),
            CreateStepRow("Networks", BuilderWorkflowStep.Networks, canNavigate),
            CreateStepRow("Forests & Domains", BuilderWorkflowStep.ForestsDomains, canNavigate),
            CreateStepRow("Credentials", BuilderWorkflowStep.Credentials, canNavigate),
            CreateStepRow("Review", BuilderWorkflowStep.Review, canNavigate)
        ];

    private BuilderWorkflowNavigationRow CreateStepRow(string label, BuilderWorkflowStep step, bool canNavigate)
    {
        var route = BuilderWorkflowRoute.ForStep(step);
        return new BuilderWorkflowNavigationRow(route, label, CurrentRoute == route, canNavigate);
    }

    private BuilderWorkflowNavigationRow CreateVmOverviewRow(bool canNavigate)
    {
        var route = BuilderWorkflowRoute.VmOverview();
        return new BuilderWorkflowNavigationRow(route, "VMs", CurrentRoute == route, canNavigate);
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
}
