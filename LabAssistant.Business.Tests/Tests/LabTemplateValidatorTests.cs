using System.Collections.Generic;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;
using Xunit;

namespace LabAssistant.Business.Tests;

public class LabTemplateValidatorTests
{
    [Fact]
    public void Validate_ReturnsErrors_WhenTemplateMissingRequiredFields()
    {
        var template = new LabTemplate
        {
            Id = "",
            Name = "",
            SchemaVersion = "",
            CreatedWithAppVersion = "",
            TemplateType = "",
            TemplateRevision = 0,
            VmTemplates = new List<VmTemplate>()
        };

        var result = LabTemplateValidator.Validate(template, new List<VhdxCatalogItem>());

        Assert.False(result.IsValid);
        Assert.Contains("Template schemaVersion is required.", result.Errors);
        Assert.Contains("Template id is required.", result.Errors);
        Assert.Contains("Template name is required.", result.Errors);
        Assert.Contains("Template templateRevision must be greater than zero.", result.Errors);
        Assert.Contains("Template createdWithAppVersion is required.", result.Errors);
        Assert.Contains("Template templateType must be 'lab-template'.", result.Errors);
        Assert.Contains("At least one VM template is required.", result.Errors);
    }

    [Fact]
    public void Validate_FlagsMissingVhdxReference()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdxId = "missing-id"
                }
            }
        };

        var catalog = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "known-id",
                Path = "C:/base.vhdx",
                OsName = "Windows",
                OsVersion = "2022",
                Generation = 2
            }
        };

        var result = LabTemplateValidator.Validate(template, catalog);

        Assert.Contains("missing-id", result.MissingVhdxIds);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RequiresVhdxIdOrPath()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1
                }
            }
        };

        var result = LabTemplateValidator.Validate(template, new List<VhdxCatalogItem>());

        Assert.Contains("VM 'vm1' must specify vhdxId or vhdPath.", result.Errors);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RequiresVmId_AndCanonicalTemplateType()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "vm-template",
            TemplateRevision = 1,
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = "",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchName = "Default Switch"
                }
            }
        };

        var result = LabTemplateValidator.Validate(template, new List<VhdxCatalogItem>());

        Assert.False(result.IsValid);
        Assert.Contains("VM 'vm1' vmId is required.", result.Errors);
        Assert.Contains("Template templateType must be 'lab-template'.", result.Errors);
    }
}
