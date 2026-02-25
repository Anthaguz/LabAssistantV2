using LabAssistant.Services.Diagnostics;
using Xunit;

namespace LabAssistant.Services.Tests;

public class RuntimeErrorMetadataNormalizerTests
{
    [Fact]
    public void FromException_ExtractsExceptionTypeAndHResult()
    {
        var ex = new TestException("nope", unchecked((int)0x80070005));

        var metadata = RuntimeErrorMetadataNormalizer.FromException(ex);

        Assert.Equal("TestException", metadata["exceptionType"]);
        Assert.Equal("0x80070005", metadata["hresult"]);
    }

    [Fact]
    public void FromPowerShellErrorText_ExtractsNormalizedFields_WhenPatternsArePresent()
    {
        var error = """
                    System.Management.Automation.RuntimeException: Something failed with HRESULT 0x80070570.
                    FullyQualifiedErrorId : OperationFailed,Microsoft.HyperV.PowerShell
                    """;

        var metadata = RuntimeErrorMetadataNormalizer.FromPowerShellErrorText(error);

        Assert.Equal("RuntimeException", metadata["exceptionType"]);
        Assert.Equal("0x80070570", metadata["hresult"]);
        Assert.Equal("OperationFailed", metadata["errorCode"]);
    }

    [Fact]
    public void FromPowerShellErrorText_ReturnsEmpty_WhenNoReliablePatternsExist()
    {
        var metadata = RuntimeErrorMetadataNormalizer.FromPowerShellErrorText("plain text error with no code");
        Assert.Empty(metadata);
    }

    private sealed class TestException : Exception
    {
        public TestException(string message, int hresult) : base(message)
        {
            HResult = hresult;
        }
    }
}
