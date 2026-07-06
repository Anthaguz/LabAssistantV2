using System.Collections.Generic;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Covers the contractual Editor decision logic extracted into <see cref="TemplatesEditorLogic"/>:
/// the strictly ordered VHDX catalog normalization precedence (vhdxId -> missing id -> signature ->
/// path -> none, with path-conflict detection) and the VM switch validation rules. The message and
/// label strings are asserted verbatim because they are part of the observable editor behavior.
/// </summary>
public sealed class TemplatesEditorLogicTests
{
    private static TemplateVhdxCatalogOption Option(string id, string path, string? signature = null, int generation = 2)
        => new(id, path, osName: "Windows", osVersion: "2022", generation: generation, signature: signature);

    [Fact]
    public void Vhdx_IdMatch_ResolvesFromVhdxId()
    {
        var catalog = new List<TemplateVhdxCatalogOption> { Option("disk-a", @"C:\a.vhdx", signature: "sig-a") };
        var vm = new VmTemplate { VhdxId = "disk-a" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.False(result.RequiresUserResolution);
        Assert.Equal("disk-a", result.EffectiveOption!.Id);
        Assert.Equal("Resolved from vhdxId.", result.Message);
        Assert.Equal("Effective source: vhdxId.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_IdTakesPrecedenceOverSignatureAndPath()
    {
        var catalog = new List<TemplateVhdxCatalogOption>
        {
            Option("disk-a", @"C:\a.vhdx", signature: "shared"),
            Option("disk-b", @"C:\b.vhdx", signature: "shared")
        };
        // Signature would be ambiguous and path points at disk-b, but a valid vhdxId wins outright.
        var vm = new VmTemplate { VhdxId = "disk-a", VhdxSignature = "shared", VhdPath = @"C:\a.vhdx" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.False(result.RequiresUserResolution);
        Assert.Equal("disk-a", result.EffectiveOption!.Id);
        Assert.Equal("Effective source: vhdxId.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_IdMatchButPathPointsElsewhere_IsUnresolvedConflict()
    {
        var catalog = new List<TemplateVhdxCatalogOption>
        {
            Option("disk-a", @"C:\a.vhdx"),
            Option("disk-b", @"C:\b.vhdx")
        };
        var vm = new VmTemplate { VhdxId = "disk-a", VhdPath = @"C:\b.vhdx" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.True(result.RequiresUserResolution);
        Assert.Null(result.EffectiveOption);
        Assert.Equal("VHD identity conflict detected. Select a catalog entry to resolve before saving.", result.Message);
        Assert.Equal("Effective source: unresolved conflict.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_MissingIdBeatsSignatureAndPath()
    {
        var catalog = new List<TemplateVhdxCatalogOption> { Option("disk-a", @"C:\a.vhdx", signature: "sig-a") };
        // vhdxId is set but absent from catalog; even a matching signature/path must not silently resolve.
        var vm = new VmTemplate { VhdxId = "ghost", VhdxSignature = "sig-a", VhdPath = @"C:\a.vhdx" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.True(result.RequiresUserResolution);
        Assert.Null(result.EffectiveOption);
        Assert.Equal("Catalog entry 'ghost' is missing. Select a replacement before saving.", result.Message);
        Assert.Equal("Effective source: unresolved missing catalog.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_AmbiguousSignature_IsUnresolved()
    {
        var catalog = new List<TemplateVhdxCatalogOption>
        {
            Option("disk-a", @"C:\a.vhdx", signature: "shared"),
            Option("disk-b", @"C:\b.vhdx", signature: "shared")
        };
        var vm = new VmTemplate { VhdxSignature = "shared" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.True(result.RequiresUserResolution);
        Assert.Equal("Multiple catalog entries match vhdxSignature. Select one entry before saving.", result.Message);
        Assert.Equal("Effective source: unresolved signature.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_SingleSignatureMatch_ResolvesFromSignature()
    {
        var catalog = new List<TemplateVhdxCatalogOption> { Option("disk-a", @"C:\a.vhdx", signature: "sig-a") };
        var vm = new VmTemplate { VhdxSignature = "sig-a" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.False(result.RequiresUserResolution);
        Assert.Equal("disk-a", result.EffectiveOption!.Id);
        Assert.Equal("Resolved from vhdxSignature.", result.Message);
        Assert.Equal("Effective source: vhdxSignature.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_SingleSignatureMatchButPathConflicts_IsUnresolvedConflict()
    {
        var catalog = new List<TemplateVhdxCatalogOption>
        {
            Option("disk-a", @"C:\a.vhdx", signature: "sig-a"),
            Option("disk-b", @"C:\b.vhdx", signature: "sig-b")
        };
        var vm = new VmTemplate { VhdxSignature = "sig-a", VhdPath = @"C:\b.vhdx" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.True(result.RequiresUserResolution);
        Assert.Equal("VHD identity conflict detected. Select a catalog entry to resolve before saving.", result.Message);
        Assert.Equal("Effective source: unresolved conflict.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_PathMatchOnly_ResolvesFromVhdPath()
    {
        var catalog = new List<TemplateVhdxCatalogOption> { Option("disk-a", @"C:\a.vhdx") };
        var vm = new VmTemplate { VhdPath = @"C:\a.vhdx" };

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.False(result.RequiresUserResolution);
        Assert.Equal("disk-a", result.EffectiveOption!.Id);
        Assert.Equal("Catalog entry 'disk-a' resolves current vhdPath.", result.Message);
        Assert.Equal("Effective source: vhdPath.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Vhdx_NoReferences_PrefersCatalogSelection()
    {
        var catalog = new List<TemplateVhdxCatalogOption> { Option("disk-a", @"C:\a.vhdx") };
        var vm = new VmTemplate();

        var result = TemplatesEditorLogic.EvaluateVhdxNormalization(vm, catalog);

        Assert.False(result.RequiresUserResolution);
        Assert.Null(result.EffectiveOption);
        Assert.Equal("Catalog-backed selection is preferred.", result.Message);
        Assert.Equal("Effective source: none.", result.EffectiveSourceLabel);
    }

    [Fact]
    public void Switches_Empty_IsValid()
    {
        var valid = TemplatesEditorLogic.TryValidateSwitches(new List<string>(), new[] { "External" }, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void Switches_BlankRow_IsRejected()
    {
        var valid = TemplatesEditorLogic.TryValidateSwitches(new[] { " " }, new[] { "External" }, out var error);

        Assert.False(valid);
        Assert.Equal("Each switch row must have a selected host switch or be removed.", error);
    }

    [Fact]
    public void Switches_UnknownSwitch_IsRejected()
    {
        var valid = TemplatesEditorLogic.TryValidateSwitches(new[] { "Ghost" }, new[] { "External" }, out var error);

        Assert.False(valid);
        Assert.Equal("Switch 'Ghost' is not available on this host.", error);
    }

    [Fact]
    public void Switches_Duplicate_IsRejected()
    {
        var valid = TemplatesEditorLogic.TryValidateSwitches(new[] { "External", "external" }, new[] { "External" }, out var error);

        Assert.False(valid);
        Assert.Equal("Duplicate switch 'external' is not allowed.", error);
    }

    [Fact]
    public void Switches_DistinctKnownSwitches_AreValid()
    {
        var valid = TemplatesEditorLogic.TryValidateSwitches(
            new[] { "External", "Internal" },
            new[] { "External", "Internal", "Private" },
            out var error);

        Assert.True(valid);
        Assert.Null(error);
    }
}
