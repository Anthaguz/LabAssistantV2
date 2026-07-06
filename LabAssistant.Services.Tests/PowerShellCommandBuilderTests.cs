using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

public class PowerShellCommandBuilderTests
{
    [Fact]
    public void Quote_WrapsPlainValue_InSingleQuotes()
    {
        Assert.Equal("'MyVm'", PowerShellCommandBuilder.Quote("MyVm"));
    }

    [Fact]
    public void Quote_DoublesEmbeddedSingleQuote_SoItCannotBreakOut()
    {
        // A name of x'; Remove-VM *; ' must stay inside the literal: every ' becomes ''.
        var quoted = PowerShellCommandBuilder.Quote("x'; Remove-VM *; '");

        Assert.Equal("'x''; Remove-VM *; '''", quoted);
    }

    [Fact]
    public void Quote_EscapesTrailingSingleQuote()
    {
        Assert.Equal("'end'''", PowerShellCommandBuilder.Quote("end'"));
    }

    [Fact]
    public void Quote_NullValue_ProducesEmptyLiteral()
    {
        Assert.Equal("''", PowerShellCommandBuilder.Quote(null));
    }

    [Fact]
    public void Escape_DoublesSingleQuotes_WithoutWrapping()
    {
        Assert.Equal("a''b", PowerShellCommandBuilder.Escape("a'b"));
    }
}
