using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Immutable presentation for one role or feature row in the Level 2 machine inspector.
/// The owning builder view model injects the toggle commands so the row stays runtime-independent.
/// </summary>
public sealed class BuilderRoleRowViewModel
{
    /// <summary>Initializes a role or feature row for the machine inspector.</summary>
    public BuilderRoleRowViewModel(
        string roleKey,
        string displayName,
        string description,
        TemplatesBuilderRoleCategory category,
        bool isAssigned,
        bool isInstallOnly,
        bool isLocked,
        bool hasConfiguration,
        string statusNote,
        bool isConfigExpanded,
        bool canToggle,
        IRelayCommand? toggleCommand,
        IRelayCommand? toggleConfigCommand)
    {
        RoleKey = roleKey;
        DisplayName = displayName;
        Description = description;
        Category = category;
        IsAssigned = isAssigned;
        IsInstallOnly = isInstallOnly;
        IsLocked = isLocked;
        HasConfiguration = hasConfiguration;
        StatusNote = statusNote;
        IsConfigExpanded = isConfigExpanded;
        CanToggle = canToggle;
        ToggleCommand = toggleCommand;
        ToggleConfigCommand = toggleConfigCommand;
    }

    /// <summary>Gets the stable catalog key for this role or feature.</summary>
    public string RoleKey { get; }

    /// <summary>Gets the display name shown as the row title.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the secondary description shown under the title.</summary>
    public string Description { get; }

    /// <summary>Gets whether the row belongs to the Roles or Features panel.</summary>
    public TemplatesBuilderRoleCategory Category { get; }

    /// <summary>Gets whether the selected machine currently carries this role or feature.</summary>
    public bool IsAssigned { get; }

    /// <summary>Gets whether the row represents an install-only role.</summary>
    public bool IsInstallOnly { get; }

    /// <summary>Gets whether the row is locked by another role assignment.</summary>
    public bool IsLocked { get; }

    /// <summary>Gets whether the role has an expandable configuration area.</summary>
    public bool HasConfiguration { get; }

    /// <summary>Gets the role-specific status note shown under the row.</summary>
    public string StatusNote { get; }

    /// <summary>Gets whether the configuration area is expanded.</summary>
    public bool IsConfigExpanded { get; }

    /// <summary>Gets whether the checkbox should allow user toggling.</summary>
    public bool CanToggle { get; }

    /// <summary>Gets the command that toggles the role assignment.</summary>
    public IRelayCommand? ToggleCommand { get; }

    /// <summary>Gets the command that expands or collapses the configuration area.</summary>
    public IRelayCommand? ToggleConfigCommand { get; }

    /// <summary>Gets whether the checkbox is enabled.</summary>
    public bool IsToggleEnabled => CanToggle && ToggleCommand is not null;

    /// <summary>Gets whether the secondary description should be visible.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>Gets whether an install-only badge should be visible.</summary>
    public bool ShowInstallOnlyBadge => IsInstallOnly;

    /// <summary>Gets whether a status note should be visible.</summary>
    public bool HasStatusNote => !string.IsNullOrWhiteSpace(StatusNote);

    /// <summary>Gets whether the Configure toggle should be visible.</summary>
    public bool ShowConfigureButton => HasConfiguration && ToggleConfigCommand is not null;

    /// <summary>Gets whether the configuration placeholder area should be visible.</summary>
    public bool ShowConfigurationArea => HasConfiguration && IsConfigExpanded;

    /// <summary>Gets the category label used by tests and automation.</summary>
    public string CategoryLabel => Category == TemplatesBuilderRoleCategory.Feature ? "Feature" : "Role";
}
