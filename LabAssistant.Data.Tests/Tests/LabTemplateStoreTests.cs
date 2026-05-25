using System;
using System.IO;
using System.Linq;
using LabAssistant.Data.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Data.Tests;

public class LabTemplateStoreTests
{
    [Fact]
    public void SaveToFolder_And_LoadFromFile_RoundTripsTemplate()
    {
        var folder = BuildTempRoot();
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-1",
            Name = "Test Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            {
                new VmTemplate
                {
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    TimeZoneConfig = new TimeZoneStepConfig { Enabled = true, TimeZoneId = "UTC" },
                    SoftwareConfig = new SoftwareStepConfig { Enabled = true, Packages = new() { "7zip" } },
                    RoleConfig = new RoleStepConfig { Enabled = false },
                    GuestNetworkConfig = new GuestNetworkStepConfig
                    {
                        Enabled = false,
                        IpAddress = "192.168.1.10",
                        DefaultGateway = "192.168.1.1",
                        DnsServers = new() { "1.1.1.1", "8.8.8.8" }
                    }
                }
            }
        };

        var filePath = store.SaveToFolder(folder, template.Name, template);
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal(template.Id, loaded.Id);
        Assert.Equal(template.Name, loaded.Name);
        Assert.Single(loaded.VmTemplates);
        Assert.Equal("vm1", loaded.VmTemplates[0].Name);
        Assert.True(loaded.VmTemplates[0].TimeZoneConfig?.Enabled);
        Assert.Equal("UTC", loaded.VmTemplates[0].TimeZoneConfig?.TimeZoneId);
        Assert.Equal(["7zip"], loaded.VmTemplates[0].SoftwareConfig?.Packages);
        Assert.False(loaded.VmTemplates[0].RoleConfig?.Enabled);
        Assert.Equal("192.168.1.10", loaded.VmTemplates[0].GuestNetworkConfig?.IpAddress);
    }

    [Fact]
    public void SaveToFile_DoesNotPersistPlaceholderGuestPayloads_WhenAbsent()
    {
        var folder = BuildTempRoot();
        var path = Path.Combine(folder, "template.json");
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-1",
            Name = "No Placeholder Payloads",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            {
                new VmTemplate
                {
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchName = "Default Switch"
                }
            }
        };

        store.SaveToFile(path, template);
        var json = File.ReadAllText(path);

        Assert.DoesNotContain("\"guestNetworkConfig\"", json);
        Assert.DoesNotContain("\"roleConfig\"", json);
        Assert.DoesNotContain("\"softwareConfig\"", json);
        Assert.DoesNotContain("\"timeZoneConfig\"", json);
    }

    [Fact]
    public void SaveToFile_OmitsDisabledEmptyGuestNetworkPlaceholderPayload()
    {
        var folder = BuildTempRoot();
        var path = Path.Combine(folder, "template.json");
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-1",
            Name = "Placeholder Guest Network",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            {
                new VmTemplate
                {
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchName = "Default Switch",
                    GuestNetworkConfig = new GuestNetworkStepConfig { Enabled = false }
                }
            }
        };

        store.SaveToFile(path, template);
        var json = File.ReadAllText(path);

        Assert.DoesNotContain("\"guestNetworkConfig\"", json);
    }

    [Fact]
    public void LoadFromFolder_ReportsInvalidJson()
    {
        var folder = BuildTempRoot();
        File.WriteAllText(Path.Combine(folder, "bad.json"), "{ invalid json");

        var store = new LabTemplateStore();
        var result = store.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void LoadFromFolder_LoadsValidTemplates()
    {
        var folder = BuildTempRoot();
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-1",
            Name = "Test Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates = { new VmTemplate { Name = "vm1", MemoryMb = 1024, CpuCount = 1, VhdPath = "C:/base.vhdx" } }
        };

        var filePath = store.SaveToFolder(folder, template.Name, template);
        Assert.True(File.Exists(filePath));

        var result = store.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());

        Assert.Empty(result.Errors);
        Assert.Single(result.Templates);
        Assert.Equal("Test Lab", result.Templates.Single().Name);
    }

    [Fact]
    public void LoadFromFile_LegacyV0Template_MapsToCanonicalWithWarning()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "legacy.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "legacy-lab",
                                  "name": "Legacy Lab",
                                  "version": "v0",
                                  "vmTemplates": [
                                    {
                                      "name": "vm1",
                                      "memoryMb": 1024,
                                      "cpuCount": 1,
                                      "vhdPath": "C:/base.vhdx",
                                      "switchName": "Default Switch"
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal(LabTemplate.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(LabTemplate.SupportedTemplateType, loaded.TemplateType);
        Assert.NotEmpty(loaded.VmTemplates[0].VmId);
        Assert.Contains(store.LastLoadWarnings, warning => warning.Contains("migrated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveToFile_AfterLegacyLoad_WritesCanonicalFieldsOnly()
    {
        var folder = BuildTempRoot();
        var legacyPath = Path.Combine(folder, "legacy.json");
        var canonicalPath = Path.Combine(folder, "canonical.json");
        File.WriteAllText(legacyPath, """
                                  {
                                    "id": "legacy-lab",
                                    "name": "Legacy Lab",
                                    "version": "v0",
                                    "vmTemplates": [
                                      {
                                        "name": "vm1",
                                        "memoryMb": 1024,
                                        "cpuCount": 1,
                                        "vhdPath": "C:/base.vhdx",
                                        "switchName": "Default Switch"
                                      }
                                    ]
                                  }
                                  """);

        var store = new LabTemplateStore();
        var template = store.LoadFromFile(legacyPath);
        store.SaveToFile(canonicalPath, template);

        var json = File.ReadAllText(canonicalPath);
        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"templateRevision\"", json);
        Assert.Contains("\"createdWithAppVersion\"", json);
        Assert.Contains("\"templateType\"", json);
        Assert.Contains("\"vmId\"", json);
        Assert.DoesNotContain("\"version\"", json);
    }

    [Fact]
    public void LoadFromFile_UnsupportedMajorSchema_ThrowsActionableError()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "unsupported-major.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "lab-major2",
                                  "name": "Lab Major 2",
                                  "schemaVersion": "3.0.0",
                                  "templateRevision": 1,
                                  "createdWithAppVersion": "1.0.0",
                                  "templateType": "lab-template",
                                  "vmTemplates": [
                                    {
                                      "vmId": "vm-1",
                                      "name": "vm1",
                                      "memoryMb": 1024,
                                      "cpuCount": 1,
                                      "vhdPath": "C:/base.vhdx",
                                      "switchName": "Default Switch"
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var ex = Assert.Throws<InvalidOperationException>(() => store.LoadFromFile(filePath));
        Assert.Contains("Please update LabAssistant", ex.Message);
    }

    [Fact]
    public void LoadFromFile_V2Template_LoadsAndMarksExecutionEngine()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "v2-template.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "lab-v2",
                                  "name": "Lab V2",
                                  "schemaVersion": "2.0.0",
                                  "templateRevision": 1,
                                  "createdWithAppVersion": "1.0.0",
                                  "templateType": "lab-template",
                                  "deploymentProfile": "Balanced",
                                  "labNetworks": [
                                    {
                                      "networkId": "contoso-net",
                                      "name": "Contoso",
                                      "switchName": "Contoso"
                                    }
                                  ],
                                  "vmTemplates": [
                                    {
                                      "vmId": "vm-1",
                                      "name": "dc1",
                                      "memoryMb": 4096,
                                      "cpuCount": 2,
                                      "vhdxId": "win-server-2025-gen2-core",
                                      "topologyRole": "RootDomainController",
                                      "credentialSlots": {
                                        "localBootstrap": "disk.win.local-admin"
                                      },
                                      "nics": [
                                        {
                                          "nicId": "primary",
                                          "networkId": "contoso-net",
                                          "ipAddress": "10.0.0.2",
                                          "prefixLength": 24,
                                          "dnsServers": [ "10.0.0.2" ]
                                        }
                                      ]
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal("2.0.0", loaded.SchemaVersion);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, loaded.ExecutionEngine);
        Assert.Equal("Balanced", loaded.DeploymentProfile);
        Assert.Single(loaded.LabNetworks);
        Assert.Equal("RootDomainController", loaded.VmTemplates[0].TopologyRole);
        Assert.Equal("disk.win.local-admin", loaded.VmTemplates[0].CredentialSlots?.LocalBootstrap);
        Assert.Single(loaded.VmTemplates[0].Nics);
    }

    [Fact]
    public void SaveToFile_V2Template_PreservesV2SchemaAndPlanningFields()
    {
        var folder = BuildTempRoot();
        var path = Path.Combine(folder, "v2-template.json");
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-v2",
            Name = "Lab V2",
            SchemaVersion = "2.0.0",
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning,
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            DeploymentProfile = "Balanced",
            LabNetworks =
            [
                new LabNetworkTemplate
                {
                    NetworkId = "contoso-net",
                    Name = "Contoso",
                    SwitchName = "Contoso"
                }
            ],
            VmTemplates =
            {
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "dc1",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "win-server-2025-gen2-core",
                    TopologyRole = "RootDomainController",
                    CapabilityRoles = ["Pki"],
                    DependsOn = ["vm:router:ready"],
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = "disk.win.local-admin"
                    },
                    Nics =
                    [
                        new VmNetworkInterfaceTemplate
                        {
                            NicId = "primary",
                            NetworkId = "contoso-net",
                            IpAddress = "10.0.0.2",
                            PrefixLength = 24,
                            DnsServers = ["10.0.0.2"]
                        }
                    ]
                }
            }
        };

        store.SaveToFile(path, template);
        var loaded = store.LoadFromFile(path);
        var json = File.ReadAllText(path);

        Assert.Equal("2.0.0", loaded.SchemaVersion);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, loaded.ExecutionEngine);
        Assert.Equal("Balanced", loaded.DeploymentProfile);
        Assert.Equal(["Pki"], loaded.VmTemplates[0].CapabilityRoles);
        Assert.Equal(["vm:router:ready"], loaded.VmTemplates[0].DependsOn);
        Assert.Contains("\"deploymentProfile\": \"Balanced\"", json);
        Assert.Contains("\"labNetworks\"", json);
        Assert.Contains("\"nics\"", json);
    }

    [Fact]
    public void LoadFromFolder_NewerMinorSchema_WarnsAndContinues()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "minor-newer.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "lab-minor",
                                  "name": "Lab Minor",
                                  "schemaVersion": "1.1.0",
                                  "templateRevision": 2,
                                  "createdWithAppVersion": "1.0.0",
                                  "templateType": "lab-template",
                                  "vmTemplates": [
                                    {
                                      "vmId": "vm-1",
                                      "name": "vm1",
                                      "memoryMb": 1024,
                                      "cpuCount": 1,
                                      "vhdPath": "C:/base.vhdx",
                                      "switchName": "Default Switch"
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var result = store.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());

        Assert.Empty(result.Errors);
        Assert.Single(result.Templates);
        Assert.Contains(result.Warnings, warning => warning.Contains("newer minor/patch than supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadFromFile_SwitchNameOnly_PromotesToCanonicalSwitchNames()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "switchname-only.json");
        File.WriteAllText(filePath, """
                               {
                                 "id": "lab-switch",
                                 "name": "Switch Legacy",
                                 "schemaVersion": "1.0.0",
                                 "templateRevision": 1,
                                 "createdWithAppVersion": "1.0.0",
                                 "templateType": "lab-template",
                                 "vmTemplates": [
                                   {
                                     "vmId": "vm-1",
                                     "name": "vm1",
                                     "memoryMb": 1024,
                                     "cpuCount": 1,
                                     "vhdPath": "C:/base.vhdx",
                                     "switchName": "Default Switch"
                                   }
                                 ]
                               }
                               """);

        var store = new LabTemplateStore();
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal(["Default Switch"], loaded.VmTemplates[0].SwitchNames);
        Assert.Equal("Default Switch", loaded.VmTemplates[0].SwitchName);
    }

    [Fact]
    public void SaveToFile_PersistsSwitchNames_AndDualWritesLegacySwitchName()
    {
        var folder = BuildTempRoot();
        var path = Path.Combine(folder, "switches.json");
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-switch",
            Name = "Switch Canonical",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            {
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchNames = ["Default Switch", "External"]
                }
            }
        };

        store.SaveToFile(path, template);
        var loaded = store.LoadFromFile(path);

        Assert.Equal(["Default Switch", "External"], loaded.VmTemplates[0].SwitchNames);
        Assert.Equal("Default Switch", loaded.VmTemplates[0].SwitchName);

        var json = File.ReadAllText(path);
        Assert.Contains("\"switchNames\"", json);
        Assert.Contains("\"switchName\": \"Default Switch\"", json);
    }

    [Fact]
    public void LoadFromFile_MixedSwitchPayload_PrefersSwitchNames()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "switch-mixed.json");
        File.WriteAllText(filePath, """
                               {
                                 "id": "lab-switch",
                                 "name": "Switch Mixed",
                                 "schemaVersion": "1.0.0",
                                 "templateRevision": 1,
                                 "createdWithAppVersion": "1.0.0",
                                 "templateType": "lab-template",
                                 "vmTemplates": [
                                   {
                                     "vmId": "vm-1",
                                     "name": "vm1",
                                     "memoryMb": 1024,
                                     "cpuCount": 1,
                                     "vhdPath": "C:/base.vhdx",
                                     "switchName": "Legacy Switch",
                                     "switchNames": [ "Canonical-1", "Canonical-2" ]
                                   }
                                 ]
                               }
                               """);

        var store = new LabTemplateStore();
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal(["Canonical-1", "Canonical-2"], loaded.VmTemplates[0].SwitchNames);
        Assert.Equal("Canonical-1", loaded.VmTemplates[0].SwitchName);
    }

    private static string BuildTempRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
