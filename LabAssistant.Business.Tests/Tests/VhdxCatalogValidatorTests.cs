using System.Collections.Generic;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Validation;
using Xunit;

namespace LabAssistant.Business.Tests;

public class VhdxCatalogValidatorTests
{
    [Fact]
    public void Validate_ReturnsErrors_ForMissingRequiredFields()
    {
        var items = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "",
                Path = "",
                OsName = "",
                OsVersion = "",
                Generation = 0
            }
        };

        var result = VhdxCatalogValidator.Validate(items);

        Assert.False(result.IsValid);
        Assert.Contains("Catalog item id is required.", result.Errors);
        Assert.Contains("Catalog item '' path is required.", result.Errors);
        Assert.Contains("Catalog item '' OS name is required.", result.Errors);
        Assert.Contains("Catalog item '' OS version is required.", result.Errors);
        Assert.Contains("Catalog item '' generation must be positive.", result.Errors);
    }

    [Fact]
    public void Validate_FlagsDuplicateIds()
    {
        var items = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "dup",
                Path = "C:/one.vhdx",
                OsName = "Windows",
                OsVersion = "2022",
                Generation = 2
            },
            new()
            {
                Id = "dup",
                Path = "C:/two.vhdx",
                OsName = "Windows",
                OsVersion = "2022",
                Generation = 2
            }
        };

        var result = VhdxCatalogValidator.Validate(items);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate catalog id: dup.", result.Errors);
    }

    [Fact]
    public void Validate_AllowsBootstrapProfile_WithReferenceOnlyMetadata()
    {
        var items = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "win-2025",
                Path = "C:/base.vhdx",
                OsName = "Windows Server",
                OsVersion = "2025",
                Generation = 2,
                BootstrapProfile = new VhdxBootstrapProfile
                {
                    ExpectedLocalUser = "Administrator",
                    LocalCredentialSlotRef = "disk.win-2025.local-admin",
                    GuestOsFamily = "WindowsServer",
                    GuestTransport = "powershell-direct"
                }
            }
        };

        var result = VhdxCatalogValidator.Validate(items);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsUnsupportedBootstrapTransport()
    {
        var items = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "win-2025",
                Path = "C:/base.vhdx",
                OsName = "Windows Server",
                OsVersion = "2025",
                Generation = 2,
                BootstrapProfile = new VhdxBootstrapProfile
                {
                    LocalCredentialSlotRef = "disk.win-2025.local-admin",
                    GuestTransport = "winrm"
                }
            }
        };

        var result = VhdxCatalogValidator.Validate(items);

        Assert.False(result.IsValid);
        Assert.Contains("Catalog item 'win-2025' bootstrapProfile.guestTransport must be 'powershell-direct' in the current scope.", result.Errors);
    }
}
