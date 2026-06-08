using System;
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

    [Fact]
    public void Validate_AllowsAbsentPlaceholderGuestPayloads()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchName = "Default Switch",
                    TimeZoneConfig = new TimeZoneStepConfig { Enabled = true },
                    SoftwareConfig = new SoftwareStepConfig { Enabled = false },
                    RoleConfig = null,
                    GuestNetworkConfig = null
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, []);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsMalformedGuestStepPayloadValues()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchName = "Default Switch",
                    GuestNetworkConfig = new GuestNetworkStepConfig
                    {
                        Enabled = true,
                        DnsServers = ["8.8.8.8", ""]
                    }
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("guestNetworkConfig.dnsServers must not contain empty values.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateSwitchNames()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchNames = [ "Default Switch", "default switch" ]
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("switchNames must not contain duplicates.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_V2DomainMemberRequiresDomainId()
    {
        var template = new LabTemplate
        {
            Id = "lab-v2",
            Name = "Lab V2",
            SchemaVersion = "2.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "member01",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdPath = "C:/base.vhdx",
                    MembershipMode = V2MembershipModeCatalog.DomainMember
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("domainId is required when membershipMode is 'DomainMember'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsEmptySwitchNameValuesInSwitchNames()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchNames = [ "Default Switch", " " ]
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, []);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("switchNames must not contain empty values.", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_V2Template_RejectsEmptyCredentialSlotReferences()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "2.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "dc1",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdxId = "win-2025",
                    TopologyRole = "RootDomainController",
                    DomainId = "domain-contoso",
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = " ",
                        DomainAdmin = "lab.contoso.domain-admin",
                        Dsrm = "lab.contoso.dsrm"
                    }
                }
            ],
            DirectoryTopology = CreateMinimalDirectoryTopology()
        };

        var catalog = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "win-2025",
                Path = "C:/base.vhdx",
                OsName = "Windows Server",
                OsVersion = "2025",
                Generation = 2
            }
        };

        var result = LabTemplateValidator.Validate(template, catalog);

        Assert.False(result.IsValid);
        Assert.Contains("VM 'dc1' credentialSlots.localBootstrap must not be empty.", result.Errors);
    }

    [Theory]
    [InlineData("Conservative")]
    [InlineData("Balanced")]
    [InlineData("Aggressive")]
    public void Validate_V2Template_AllowsSupportedDeploymentProfiles(string deploymentProfile)
    {
        var template = CreateMinimalV2Template();
        template.DeploymentProfile = deploymentProfile;

        var result = LabTemplateValidator.Validate(template, CreateMinimalCatalog());

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.Contains("deploymentProfile", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_V2Template_AllowsBlankDeploymentProfileForDeployTimeDefault(string deploymentProfile)
    {
        var template = CreateMinimalV2Template();
        template.DeploymentProfile = deploymentProfile;

        var result = LabTemplateValidator.Validate(template, CreateMinimalCatalog());

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.Contains("deploymentProfile", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_V2Template_RejectsUnknownDeploymentProfile()
    {
        var template = CreateMinimalV2Template();
        template.DeploymentProfile = "Turbo";

        var result = LabTemplateValidator.Validate(template, CreateMinimalCatalog());

        Assert.False(result.IsValid);
        Assert.Contains("V2 deploymentProfile must be one of: Conservative, Balanced, Aggressive.", result.Errors);
    }

    [Fact]
    public void Validate_V2Template_AllowsDirectoryTopologyShape()
    {
        var template = CreateMinimalV2Template();
        template.DirectoryTopology = new V2DirectoryTopologyTemplate
        {
            Forests =
            [
                new V2ForestTemplate
                {
                    ForestId = "forest-contoso",
                    RootDomainId = "domain-contoso"
                }
            ],
            Domains =
            [
                new V2DomainTemplate
                {
                    DomainId = "domain-contoso",
                    DnsName = "contoso.com",
                    NetBiosName = "CONTOSO",
                    ForestId = "forest-contoso",
                    RelationKind = V2DomainRelationKind.Root,
                    FirstDomainControllerVmId = "vm-1"
                },
                new V2DomainTemplate
                {
                    DomainId = "domain-child",
                    DnsName = "child.contoso.com",
                    NetBiosName = "CHILD",
                    ForestId = "forest-contoso",
                    RelationKind = V2DomainRelationKind.Child,
                    ParentDomainId = "domain-contoso",
                    FirstDomainControllerVmId = "vm-1"
                }
            ],
            Trusts =
            [
                new V2TrustTemplate
                {
                    TrustId = "trust-contoso-fabrikam",
                    SourceDomainId = "domain-contoso",
                    TargetDomainId = "domain-child",
                    TrustType = V2TrustType.Forest,
                    Direction = V2TrustDirection.Bidirectional
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, CreateMinimalCatalog());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_V2Template_RejectsInvalidDirectoryTopologyReferences()
    {
        var template = CreateMinimalV2Template();
        template.DirectoryTopology = new V2DirectoryTopologyTemplate
        {
            Forests =
            [
                new V2ForestTemplate
                {
                    ForestId = "forest-contoso",
                    RootDomainId = "missing-domain"
                }
            ],
            Domains =
            [
                new V2DomainTemplate
                {
                    DomainId = "domain-contoso",
                    DnsName = "",
                    NetBiosName = "",
                    ForestId = "forest-missing",
                    RelationKind = V2DomainRelationKind.Child,
                    ParentDomainId = "",
                    FirstDomainControllerVmId = "missing-vm"
                }
            ],
            Trusts =
            [
                new V2TrustTemplate
                {
                    TrustId = "",
                    SourceDomainId = "missing-source",
                    TargetDomainId = "missing-target"
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, CreateMinimalCatalog());

        Assert.False(result.IsValid);
        Assert.Contains("V2 forest 'forest-contoso' references unknown root domain id 'missing-domain'.", result.Errors);
        Assert.Contains("V2 domain 'domain-contoso' dnsName is required.", result.Errors);
        Assert.Contains("V2 domain 'domain-contoso' netBiosName is required.", result.Errors);
        Assert.Contains("V2 domain 'domain-contoso' references unknown VM id 'missing-vm'.", result.Errors);
        Assert.Contains("V2 trust '' references unknown source domain id 'missing-source'.", result.Errors);
    }

    [Fact]
    public void Validate_V1Template_DoesNotApplyV2DeploymentProfileValidation()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            DeploymentProfile = "Turbo",
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "vm1",
                    MemoryMb = 1024,
                    CpuCount = 1,
                    VhdPath = "C:/base.vhdx",
                    SwitchName = "Default Switch"
                }
            ]
        };

        var result = LabTemplateValidator.Validate(template, []);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.Contains("deploymentProfile", StringComparison.Ordinal));
    }

    private static LabTemplate CreateMinimalV2Template()
    {
        return new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            SchemaVersion = "2.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-1",
                    Name = "dc1",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdxId = "win-2025",
                    TopologyRole = "RootDomainController",
                    DomainId = "domain-contoso",
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        Dsrm = "lab.contoso.dsrm"
                    }
                }
            ],
            DirectoryTopology = CreateMinimalDirectoryTopology()
        };
    }

    private static V2DirectoryTopologyTemplate CreateMinimalDirectoryTopology()
    {
        return new V2DirectoryTopologyTemplate
        {
            Forests =
            [
                new V2ForestTemplate
                {
                    ForestId = "forest-contoso",
                    RootDomainId = "domain-contoso"
                }
            ],
            Domains =
            [
                new V2DomainTemplate
                {
                    DomainId = "domain-contoso",
                    DnsName = "contoso.com",
                    NetBiosName = "CONTOSO",
                    ForestId = "forest-contoso",
                    RelationKind = V2DomainRelationKind.Root,
                    FirstDomainControllerVmId = "vm-1"
                }
            ]
        };
    }

    private static List<VhdxCatalogItem> CreateMinimalCatalog()
    {
        return
        [
            new VhdxCatalogItem
            {
                Id = "win-2025",
                Path = "C:/base.vhdx",
                OsName = "Windows Server",
                OsVersion = "2025",
                Generation = 2
            }
        ];
    }
}
