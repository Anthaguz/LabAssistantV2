using System;
using System.IO;
using LabAssistant.Data.Templates;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Data.Tests;

public class MilestoneWTemplatePersistenceMatrixTests
{
    [Fact]
    public void GuestStepTemplatePersistence_RoundTripsImplementedConfigs_AndOmitsPlaceholderOnlyGuestNetworkPayload()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "milestone-w-template.json");
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "w-lab-1",
            Name = "Milestone W",
            SchemaVersion = LabTemplate.CurrentSchemaVersion,
            TemplateRevision = 1,
            CreatedWithAppVersion = "1.0.0",
            TemplateType = LabTemplate.SupportedTemplateType,
            VmTemplates =
            {
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "VM1",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdPath = @"C:\base\vm1.vhdx",
                    SwitchName = "Default Switch",
                    TimeZoneConfig = new TimeZoneStepConfig { Enabled = true, TimeZoneId = "UTC" },
                    SoftwareConfig = new SoftwareStepConfig { Enabled = true, Packages = new() { "7zip", "git" } },
                    RoleConfig = new RoleStepConfig { Enabled = true, Roles = new() { "WebServer" } },
                    GuestNetworkConfig = new GuestNetworkStepConfig { Enabled = false } // placeholder-only, should be omitted
                }
            }
        };

        store.SaveToFile(filePath, template);
        var json = File.ReadAllText(filePath);

        Assert.Contains("\"timeZoneConfig\"", json);
        Assert.Contains("\"softwareConfig\"", json);
        Assert.Contains("\"roleConfig\"", json);
        Assert.DoesNotContain("\"guestNetworkConfig\"", json);

        var loaded = store.LoadFromFile(filePath);
        var vm = Assert.Single(loaded.VmTemplates);
        Assert.True(vm.TimeZoneConfig?.Enabled);
        Assert.Equal("UTC", vm.TimeZoneConfig?.TimeZoneId);
        Assert.True(vm.SoftwareConfig?.Enabled);
        Assert.Equal(["7zip", "git"], vm.SoftwareConfig?.Packages);
        Assert.True(vm.RoleConfig?.Enabled);
        Assert.Equal(["WebServer"], vm.RoleConfig?.Roles);
        Assert.Null(vm.GuestNetworkConfig);
    }

    private static string BuildTempRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
