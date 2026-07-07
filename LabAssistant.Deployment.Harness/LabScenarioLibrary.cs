using LabAssistant.Models.Templates;

namespace LabAssistant.Deployment.Harness;

/// <summary>
/// Builders for the canonical lab topologies used by the smoke suite. Standalone is implemented now; richer
/// topologies (domain controller, router, child domain, forest trust) are added here as each is promoted from
/// a fast plan-level golden test into a real Hyper-V smoke test.
/// </summary>
public static class LabScenarioLibrary
{
    /// <summary>
    /// One standalone VM off the base image on the Default Switch, driven through the V2 planner + runtime.
    /// The NIC is a bare DHCP attachment (switch only, no static addressing), so no guest configuration is
    /// required and the plan is <c>ProvisionVm -&gt; StartVm</c>. This is exactly the degenerate case the
    /// orphan-guest-network fix (PR #853) protects, which makes it a good end-to-end smoke.
    /// </summary>
    public static LabScenario Standalone(HarnessOptions options, string vmName = "harness-standalone-01")
    {
        var template = new LabTemplate
        {
            Name = "Harness Standalone Smoke",
            Description = "Single standalone VM through the V2 planner + runtime + ready-set graph scheduler.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    Name = vmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    SwitchName = "Default Switch",
                    SwitchNames = new List<string> { "Default Switch" },
                    MembershipMode = V2MembershipModeCatalog.Standalone
                }
            },
            NetworkConfig = new NetworkConfig { SwitchName = "Default Switch" }
        };

        return new LabScenario
        {
            Name = "Standalone",
            Template = template,
            ExpectedVmNames = new[] { vmName }
        };
    }
}
