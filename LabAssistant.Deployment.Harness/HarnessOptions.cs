namespace LabAssistant.Deployment.Harness;

/// <summary>
/// Immutable configuration for an <see cref="IsolatedLabEnvironment"/>: where the prepared base image lives,
/// the bootstrap credential to seed against it, and whether the V2 ready-set graph scheduler is enabled.
/// </summary>
/// <remarks>
/// The admin password is never defaulted in source so no secret is committed; it is supplied via the
/// <see cref="AdminPasswordEnvVar"/> environment variable when a real deploy is requested. Everything else
/// falls back to the known lab defaults for convenience.
/// </remarks>
public sealed class HarnessOptions
{
    /// <summary>Overrides the base image VHDX path. Default: <see cref="DefaultBaseImagePath"/>.</summary>
    public const string BaseImagePathEnvVar = "LABASSISTANT_SMOKE_BASE_VHDX";

    /// <summary>Overrides the catalog id assigned to the base image. Default: <see cref="DefaultBaseImageId"/>.</summary>
    public const string BaseImageIdEnvVar = "LABASSISTANT_SMOKE_BASE_VHDX_ID";

    /// <summary>Overrides the local admin username baked into the base image. Default: <see cref="DefaultAdminUser"/>.</summary>
    public const string AdminUserEnvVar = "LABASSISTANT_SMOKE_ADMIN_USER";

    /// <summary>Supplies the local admin password baked into the base image. Never defaulted (no committed secret).</summary>
    public const string AdminPasswordEnvVar = "LABASSISTANT_SMOKE_ADMIN_PASSWORD";

    /// <summary>Default prepared base image path on this host.</summary>
    public const string DefaultBaseImagePath = @"D:\BaseDisks\WinServer2022-Base.vhdx";

    /// <summary>Default catalog id for the prepared base image.</summary>
    public const string DefaultBaseImageId = "b0a5e0222022400080000000000000a1";

    /// <summary>Default local admin username baked into the prepared base image.</summary>
    public const string DefaultAdminUser = "Administrator";

    /// <summary>Credential slot key the harness seeds and the base image bootstrap profile references.</summary>
    public const string BootstrapSlotKey = "smoke-local-admin";

    /// <summary>Catalog id assigned to the base image so templates can reference it by id.</summary>
    public required string BaseImageId { get; init; }

    /// <summary>Absolute path to the prepared base VHDX.</summary>
    public required string BaseImagePath { get; init; }

    /// <summary>Local admin username baked into the base image.</summary>
    public required string AdminUser { get; init; }

    /// <summary>Local admin password baked into the base image (empty when unset).</summary>
    public required string AdminPassword { get; init; }

    /// <summary>When true, the isolated environment enables the V2 ready-set graph scheduler for the run.</summary>
    public bool UseGraphScheduler { get; init; } = true;

    /// <summary>Optional fixed config root. When null the environment allocates a throwaway temp root it owns.</summary>
    public string? AppRootOverride { get; init; }

    /// <summary>True when an admin password has been supplied, i.e. a real guest-authenticating deploy is possible.</summary>
    public bool HasPassword => !string.IsNullOrEmpty(AdminPassword);

    /// <summary>
    /// Builds options from environment variables, falling back to the known lab defaults for everything except
    /// the admin password, which is only taken from <see cref="AdminPasswordEnvVar"/> so no secret lives in source.
    /// </summary>
    public static HarnessOptions FromEnvironment(bool useGraphScheduler = true)
    {
        return new HarnessOptions
        {
            BaseImagePath = Env(BaseImagePathEnvVar) ?? DefaultBaseImagePath,
            BaseImageId = Env(BaseImageIdEnvVar) ?? DefaultBaseImageId,
            AdminUser = Env(AdminUserEnvVar) ?? DefaultAdminUser,
            AdminPassword = Env(AdminPasswordEnvVar) ?? string.Empty,
            UseGraphScheduler = useGraphScheduler
        };
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
