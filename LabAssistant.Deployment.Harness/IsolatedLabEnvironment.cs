using LabAssistant.Business;
using LabAssistant.Data.Configuration;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Deployment.Harness;

/// <summary>
/// A fully isolated LabAssistant runtime rooted at a throwaway config directory. It composes the exact
/// production DI graph (infrastructure + business + persistence) but redirects <see cref="IAppPaths"/> to
/// the isolated root, so settings, catalog, credential slots and templates never touch the user's real
/// <c>%APPDATA%\LabAssistant</c>. It seeds the base-image catalog entry and bootstrap credential slot so a
/// V2 plan can be built and executed. Disposal deletes an owned temp root and restores the scheduler env var.
/// </summary>
public sealed class IsolatedLabEnvironment : IAsyncDisposable
{
    /// <summary>
    /// Environment variable read by the V2 runtime at construction to select the ready-set graph scheduler.
    /// Set before the runtime service is first resolved, which is why the environment owns it.
    /// </summary>
    public const string GraphSchedulerEnvVar = "LABASSISTANT_V2_USE_GRAPH_SCHEDULER";

    private readonly ServiceProvider _provider;
    private readonly bool _ownsRoot;
    private readonly string? _priorSchedulerFlag;
    private readonly bool _schedulerFlagTouched;

    /// <summary>Builds and seeds an isolated environment from the given options.</summary>
    public IsolatedLabEnvironment(HarnessOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));

        AppRoot = string.IsNullOrWhiteSpace(options.AppRootOverride)
            ? Path.Combine(Path.GetTempPath(), "LabAssistantHarness", Guid.NewGuid().ToString("N"))
            : options.AppRootOverride!;
        _ownsRoot = string.IsNullOrWhiteSpace(options.AppRootOverride);
        Directory.CreateDirectory(AppRoot);

        // The runtime reads the scheduler flag once at construction; set it before the provider resolves it,
        // capturing any prior value so disposal can restore the process environment unchanged.
        if (options.UseGraphScheduler)
        {
            _priorSchedulerFlag = Environment.GetEnvironmentVariable(GraphSchedulerEnvVar);
            _schedulerFlagTouched = true;
            Environment.SetEnvironmentVariable(GraphSchedulerEnvVar, "1");
        }

        var services = new ServiceCollection();
        services.AddInfrastructureServices();
        services.AddBusinessServices();
        services.AddPersistenceServices();
        // Last registration wins: redirect every persistence store to the isolated root.
        services.AddSingleton<IAppPaths>(new AppPaths(AppRoot));
        _provider = services.BuildServiceProvider();

        var settingsStore = _provider.GetRequiredService<IAppSettingsStore>();
        settingsStore.LoadOrCreate();
        Settings = settingsStore.Settings;

        SeedBootstrapCredential();
        SeedBaseImageCatalog();
    }

    /// <summary>The options this environment was built from.</summary>
    public HarnessOptions Options { get; }

    /// <summary>The composed service provider (production DI graph, isolated paths).</summary>
    public IServiceProvider Services => _provider;

    /// <summary>The settings snapshot derived from the isolated paths (VM base path, catalog, templates).</summary>
    public AppSettings Settings { get; }

    /// <summary>The isolated config root all stores point at.</summary>
    public string AppRoot { get; }

    /// <summary>Resolves a required service from the isolated provider.</summary>
    public T GetService<T>() where T : notnull => _provider.GetRequiredService<T>();

    private void SeedBootstrapCredential()
    {
        var credStore = _provider.GetRequiredService<ILocalCredentialSlotStore>();
        // Planning only needs the slot to exist (its key feeds ResolvedCredentialSlotKeys); the value is never
        // read during planning. When no real image password is supplied (plan-only use), seed a throwaway
        // placeholder so the slot is present. DeployAsync separately requires a real password before it runs,
        // so a real deploy can never authenticate with this placeholder.
        var password = Options.HasPassword ? Options.AdminPassword : Guid.NewGuid().ToString("N");
        credStore.Upsert(HarnessOptions.BootstrapSlotKey, Options.AdminUser, password);
    }

    private void SeedBaseImageCatalog()
    {
        var catalogStore = _provider.GetRequiredService<IVhdxCatalogStore>();
        var items = catalogStore.Load(Settings.CatalogPath).Items.ToList();
        if (items.Any(i => string.Equals(i.Id, Options.BaseImageId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        long? sizeBytes = null;
        try
        {
            if (File.Exists(Options.BaseImagePath))
            {
                sizeBytes = new FileInfo(Options.BaseImagePath).Length;
            }
        }
        catch
        {
            // Size is a best-effort matching hint only.
        }

        items.Add(new VhdxCatalogItem
        {
            Id = Options.BaseImageId,
            Path = Options.BaseImagePath,
            OsName = "Windows Server",
            OsVersion = "2022",
            Generation = 2,
            SizeBytes = sizeBytes,
            Notes = "Seeded by the deployment harness for isolated smoke testing.",
            BootstrapProfile = new VhdxBootstrapProfile
            {
                ExpectedLocalUser = Options.AdminUser,
                LocalCredentialSlotRef = HarnessOptions.BootstrapSlotKey,
                GuestOsFamily = "windows",
                GuestTransport = "powershell-direct"
            }
        });

        // The store computes the signature on save (VhdxSignature.Build), matching production behavior.
        catalogStore.Save(Settings.CatalogPath, items);
    }

    /// <summary>
    /// Disposes the provider, restores the scheduler env var, and deletes an owned temp root. This is async
    /// because the composed DI graph contains services (notably the host <c>PowerShellSessionPool</c>) that
    /// implement <see cref="IAsyncDisposable"/> only; a synchronous <c>ServiceProvider.Dispose()</c> throws on
    /// them, which would mask the real deploy result and leak the owned temp root.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync().ConfigureAwait(false);

        if (_schedulerFlagTouched)
        {
            Environment.SetEnvironmentVariable(GraphSchedulerEnvVar, _priorSchedulerFlag);
        }

        if (_ownsRoot)
        {
            try
            {
                if (Directory.Exists(AppRoot))
                {
                    Directory.Delete(AppRoot, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup of a throwaway config root; never fail disposal over it.
            }
        }
    }
}
