using LabAssistant.Business.Assets;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.HyperV;

namespace LabAssistant.Deployment.Harness;

/// <summary>
/// Drives a <see cref="LabScenario"/> through the V2 planner and (optionally) the V2 runtime against a given
/// <see cref="IsolatedLabEnvironment"/>. It mirrors exactly how the app composes and calls these seams, so the
/// smoke suite exercises the real production code paths. It also owns scenario teardown to honor the mandatory
/// no-orphans cleanup policy.
/// </summary>
public sealed class LabDeploymentHarness
{
    private readonly IsolatedLabEnvironment _env;
    private readonly Action<string> _log;

    /// <summary>Creates a harness bound to an isolated environment, optionally logging progress.</summary>
    public LabDeploymentHarness(IsolatedLabEnvironment env, Action<string>? log = null)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _log = log ?? (_ => { });
    }

    /// <summary>
    /// Builds the V2 plan for the scenario without any Hyper-V access (fast, safe, non-elevated). Switch
    /// availability is derived from the template so planning can validate switch references offline.
    /// </summary>
    public async Task<V2PlanBuildResult> BuildPlanAsync(LabScenario scenario, CancellationToken cancellationToken = default)
    {
        PrepareScenario(scenario);
        var switchNames = DeriveTemplateSwitchNames(scenario.Template);
        var switchInfo = switchNames
            .Select(n => new V2AvailableSwitchInfo { Name = n, SwitchType = "External" })
            .ToList();
        return await BuildPlanCoreAsync(scenario.Template, switchNames, switchInfo);
    }

    /// <summary>
    /// Full deploy: seed, plan against the real Hyper-V switch inventory, execute the runtime, and optionally
    /// probe the guest over PowerShell Direct. Requires elevation and Hyper-V.
    /// </summary>
    public async Task<LabDeploymentResult> DeployAsync(
        LabScenario scenario,
        bool probeGuest = false,
        CancellationToken cancellationToken = default)
    {
        // A real deploy authenticates into the guest, so it needs the real image password. Planning does not.
        if (!_env.Options.HasPassword)
        {
            throw new InvalidOperationException(
                $"Deploy requires the base image admin password; set env {HarnessOptions.AdminPasswordEnvVar}. " +
                "Use BuildPlanAsync for password-free plan validation.");
        }

        PrepareScenario(scenario);

        var switchService = _env.GetService<IAssetsSwitchesCapabilityService>();
        var inventory = await switchService.LoadAsync();
        var switchInfo = inventory.Items
            .Select(i => new V2AvailableSwitchInfo { Name = i.Name, SwitchType = i.SwitchType })
            .ToList();
        var switchNames = switchInfo.Select(i => i.Name).ToList();
        _log($"Available Hyper-V switches ({switchNames.Count}): {string.Join(", ", switchNames)}");

        var plan = await BuildPlanCoreAsync(scenario.Template, switchNames, switchInfo);
        _log(PlanTextFormatter.Format(plan));

        var blocking = plan.Issues.Where(i => i.Severity == V2PlanIssueSeverity.Blocking).ToList();
        var deployable = plan.Success && blocking.Count == 0 && plan.UnresolvedRequirements.Count == 0;
        if (!deployable)
        {
            _log("Plan is not deployable; skipping runtime execution.");
            return new LabDeploymentResult { PlanDeployable = false, Plan = plan };
        }

        var slotValues = ResolveCredentialSlots();
        var runtime = _env.GetService<IV2RuntimeCapabilityService>();
        var deploymentContext = new MultiVmDeploymentContext();
        var result = await runtime.ExecuteAsync(new V2RuntimeExecutionRequest
        {
            Template = scenario.Template,
            Plan = plan,
            Settings = _env.Settings,
            CredentialSlotValues = slotValues,
            BaseRemoteAccessOptions = new V2BaseRemoteAccessOptions(),
            DeploymentContext = deploymentContext
        });

        _log($"V2 runtime Success={result.Success}; executed [{string.Join(", ", result.ExecutedNodeIds)}]");
        foreach (var message in result.BlockingMessages)
        {
            _log($"  blocking: {message}");
        }

        var probeSucceeded = false;
        string? probeOutput = null;
        if (probeGuest && result.Success)
        {
            var started = result.DeploymentContext.VmContexts.FirstOrDefault(v => v.IsSuccess && v.VmStarted);
            if (started is not null)
            {
                (probeSucceeded, probeOutput) = await ProbeGuestAsync(started.VmName, cancellationToken);
            }
            else
            {
                _log("Guest probe skipped: no successfully started VM in the deployment context.");
            }
        }

        return new LabDeploymentResult
        {
            PlanDeployable = true,
            Plan = plan,
            Executed = true,
            RuntimeSuccess = result.Success,
            ExecutedNodeIds = result.ExecutedNodeIds.ToList(),
            BlockingMessages = result.BlockingMessages.ToList(),
            GuestProbeSucceeded = probeSucceeded,
            GuestProbeOutput = probeOutput
        };
    }

    /// <summary>
    /// Stops and deletes (including storage) every VM the scenario is expected to create. Best-effort and
    /// idempotent: VMs that are already gone or were never created are ignored. This honors the mandatory
    /// no-orphans cleanup policy after a smoke run.
    /// </summary>
    public async Task TeardownAsync(LabScenario scenario, CancellationToken cancellationToken = default)
    {
        var admin = _env.GetService<IHyperVMachineAdminService>();
        foreach (var vmName in scenario.ExpectedVmNames)
        {
            try
            {
                await admin.StopVmAsync(vmName);
            }
            catch (Exception ex)
            {
                _log($"teardown stop '{vmName}': {ex.Message}");
            }

            try
            {
                await admin.DeleteVmAsync(vmName, includeStorage: true);
            }
            catch (Exception ex)
            {
                _log($"teardown delete '{vmName}': {ex.Message}");
            }
        }
    }

    private void PrepareScenario(LabScenario scenario)
    {
        var credStore = _env.GetService<ILocalCredentialSlotStore>();
        foreach (var credential in scenario.ExtraCredentials)
        {
            credStore.Upsert(credential.SlotKey, credential.Username, credential.Password);
        }

        var templateStore = _env.GetService<ILabTemplateStore>();
        var templatePath = Path.Combine(_env.Settings.TemplateFolder, scenario.Template.Name + ".json");
        templateStore.SaveToFile(templatePath, scenario.Template);
    }

    private async Task<V2PlanBuildResult> BuildPlanCoreAsync(
        LabTemplate template,
        IReadOnlyList<string> switchNames,
        IReadOnlyList<V2AvailableSwitchInfo> switchInfo)
    {
        var slotValues = ResolveCredentialSlots();
        var planner = _env.GetService<IV2PlanningCapabilityService>();
        return await planner.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = LoadCatalogItems(),
            AvailableSwitchNames = switchNames,
            AvailableSwitches = switchInfo,
            ResolvedCredentialSlotKeys = slotValues.Keys.ToList(),
            ExternalSwitchAdapterMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            DefaultDeploymentProfile = "Balanced"
        });
    }

    private IReadOnlyList<VhdxCatalogItem> LoadCatalogItems()
    {
        var catalogStore = _env.GetService<IVhdxCatalogStore>();
        return catalogStore.Load(_env.Settings.CatalogPath).Items.ToList();
    }

    private Dictionary<string, V2RuntimeCredential> ResolveCredentialSlots()
    {
        var credStore = _env.GetService<ILocalCredentialSlotStore>();
        var slotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in credStore.LoadDefinitions())
        {
            if (credStore.TryGetCredential(definition.SlotKey, out var credential))
            {
                slotValues[definition.SlotKey] = credential;
            }
        }

        return slotValues;
    }

    private async Task<(bool ok, string? output)> ProbeGuestAsync(string vmName, CancellationToken cancellationToken)
    {
        var credStore = _env.GetService<ILocalCredentialSlotStore>();
        if (!credStore.TryGetCredential(HarnessOptions.BootstrapSlotKey, out var credential))
        {
            _log($"Probe skipped: credential slot '{HarnessOptions.BootstrapSlotKey}' not found.");
            return (false, null);
        }

        var guest = _env.GetService<IGuestCommandExecutor>();
        const int maxAttempts = 60;
        var delay = TimeSpan.FromSeconds(10);
        const string script = "$env:COMPUTERNAME; whoami; (Get-CimInstance Win32_OperatingSystem).Caption";

        _log($"Probing PowerShell Direct into '{vmName}' as {credential.Username} (up to {maxAttempts} x {delay.TotalSeconds:0}s)...");
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await guest.ExecutePowerShellDirectAsync(vmName, credential, script, cancellationToken);
                if (result.Success)
                {
                    _log($"Probe attempt {attempt}: SUCCESS.");
                    return (true, result.Output);
                }

                _log($"Probe {attempt}/{maxAttempts}: not ready - {Truncate(result.Error.Trim(), 160)}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log($"Probe {attempt}/{maxAttempts}: {Truncate(ex.Message, 160)}");
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return (false, null);
    }

    private static IReadOnlyList<string> DeriveTemplateSwitchNames(LabTemplate template) =>
        template.VmTemplates
            .SelectMany(v => (v.SwitchNames ?? new List<string>()).Append(v.SwitchName))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max) + "...";
}
