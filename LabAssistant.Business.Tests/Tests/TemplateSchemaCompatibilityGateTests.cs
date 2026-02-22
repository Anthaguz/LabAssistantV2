using System;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests;

public class TemplateSchemaCompatibilityGateTests
{
    private static readonly Version Current = new(2, 0, 0);

    [Fact]
    public void Evaluate_Allows_CurrentMajorN()
    {
        var decision = TemplateSchemaCompatibilityGate.Evaluate("2.0.0", Current);

        Assert.Equal(TemplateSchemaCompatibilityStatus.Allow, decision.Status);
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluate_Allows_NMinus1_WithUpcast()
    {
        var decision = TemplateSchemaCompatibilityGate.Evaluate("1.4.2", Current);

        Assert.Equal(TemplateSchemaCompatibilityStatus.AllowWithUpcast, decision.Status);
        Assert.False(decision.IsBlocked);
        Assert.Contains("migrated", decision.Warning ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Blocks_NMinus2()
    {
        var decision = TemplateSchemaCompatibilityGate.Evaluate("0.9.0", Current);

        Assert.Equal(TemplateSchemaCompatibilityStatus.Block, decision.Status);
        Assert.True(decision.IsBlocked);
        Assert.Contains("older than the supported window", decision.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Blocks_NPlus1()
    {
        var decision = TemplateSchemaCompatibilityGate.Evaluate("3.0.0", Current);

        Assert.Equal(TemplateSchemaCompatibilityStatus.Block, decision.Status);
        Assert.True(decision.IsBlocked);
        Assert.Contains("Please update LabAssistant", decision.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
