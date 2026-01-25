using System.Collections.Generic;
using System.Text.Json;
using LabAssistant.Models.Configuration;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class TemplateVhdxMappingPersistenceTests
{
    [Fact]
    public void SerializeAndDeserialize_PreservesTemplateSelections()
    {
        var settings = new AppSettings
        {
            TemplateSelections = new List<TemplateVhdxSelection>
            {
                new()
                {
                    TemplateId = "lab-1",
                    VmName = "dc01",
                    VhdxId = "win-2022-core",
                    VhdPath = "C:/Lab/Win2022.vhdx"
                },
                new()
                {
                    TemplateId = "lab-1",
                    VmName = "sql01",
                    VhdxId = "win-2022-full",
                    VhdPath = "C:/Lab/Win2022-Full.vhdx"
                }
            }
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.TemplateSelections.Count);
        Assert.Equal("lab-1", restored.TemplateSelections[0].TemplateId);
        Assert.Equal("dc01", restored.TemplateSelections[0].VmName);
        Assert.Equal("win-2022-core", restored.TemplateSelections[0].VhdxId);
        Assert.Equal("C:/Lab/Win2022.vhdx", restored.TemplateSelections[0].VhdPath);
    }
}
