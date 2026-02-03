using System;
using System.Collections.Generic;
using LabAssistant.Models.Catalog;
using Xunit;

namespace LabAssistant.Business.Tests;

public class VhdxSignatureTests
{
    [Fact]
    public void FindMatches_ReturnsSingleMatch_WhenUnique()
    {
        var item = BuildItem("Windows Server", "2022", 2, 123);
        var other = BuildItem("Windows 11", "23H2", 2, 123);

        var matches = VhdxSignature.FindMatches(item.Signature!, new[] { item, other });

        Assert.Single(matches);
        Assert.Equal(item.Id, matches[0].Id);
    }

    [Fact]
    public void FindMatches_ReturnsMultiple_WhenAmbiguous()
    {
        var item = BuildItem("Windows Server", "2022", 2, 123);
        var duplicate = BuildItem("Windows Server", "2022", 2, 123);

        var matches = VhdxSignature.FindMatches(item.Signature!, new[] { item, duplicate });

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void FindMatches_ReturnsEmpty_WhenNoMatch()
    {
        var item = BuildItem("Windows Server", "2022", 2, 123);
        var other = BuildItem("Windows 11", "23H2", 2, 123);

        var matches = VhdxSignature.FindMatches("os=linux|ver=ubuntu|gen=2|size=123", new[] { item, other });

        Assert.Empty(matches);
    }

    private static VhdxCatalogItem BuildItem(string osName, string osVersion, int generation, long sizeBytes)
    {
        var item = new VhdxCatalogItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Path = "C:/Lab/Base.vhdx",
            OsName = osName,
            OsVersion = osVersion,
            Generation = generation,
            SizeBytes = sizeBytes
        };
        item.Signature = VhdxSignature.Build(item);
        return item;
    }
}
