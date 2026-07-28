using LabAssistant.Business.Runtime;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed class RouterExternalAttachmentPolicyTests
{
    [Theory]
    [InlineData(V2SwitchTypeCatalog.External, "vSwitch-External", true)]
    [InlineData(V2SwitchTypeCatalog.External, "Default Switch", true)]
    [InlineData(V2SwitchTypeCatalog.Internal, "Default Switch", true)]
    [InlineData(V2SwitchTypeCatalog.Internal, "default switch", true)]
    [InlineData(V2SwitchTypeCatalog.Internal, "c08cb7b8-9b3c-408e-8e30-5e16a3aeb444", true)]
    [InlineData(V2SwitchTypeCatalog.Internal, "vSwitch-Core", false)]
    [InlineData(V2SwitchTypeCatalog.Private, "vSwitch-Isolated", false)]
    [InlineData(V2SwitchTypeCatalog.Internal, "", false)]
    [InlineData(null, null, false)]
    public void IsExternalAttachment_ClassifiesExternalTypeAndNatCapableDefaultSwitch(
        string? switchType,
        string? switchName,
        bool expected)
    {
        Assert.Equal(expected, RouterExternalAttachmentPolicy.IsExternalAttachment(switchType, switchName));
    }

    [Fact]
    public void IsExternalSwitchType_MatchesOnlyTheExternalType()
    {
        Assert.True(RouterExternalAttachmentPolicy.IsExternalSwitchType(V2SwitchTypeCatalog.External));
        Assert.True(RouterExternalAttachmentPolicy.IsExternalSwitchType("external"));
        Assert.False(RouterExternalAttachmentPolicy.IsExternalSwitchType(V2SwitchTypeCatalog.Internal));
        Assert.False(RouterExternalAttachmentPolicy.IsExternalSwitchType(null));
    }

    [Fact]
    public void IsNatCapableEgressSwitch_MatchesDefaultSwitchByNameOrGuidOnly()
    {
        Assert.True(RouterExternalAttachmentPolicy.IsNatCapableEgressSwitch("Default Switch"));
        Assert.True(RouterExternalAttachmentPolicy.IsNatCapableEgressSwitch("  Default Switch  "));
        Assert.True(RouterExternalAttachmentPolicy.IsNatCapableEgressSwitch("c08cb7b8-9b3c-408e-8e30-5e16a3aeb444"));
        Assert.False(RouterExternalAttachmentPolicy.IsNatCapableEgressSwitch("vSwitch-External"));
        Assert.False(RouterExternalAttachmentPolicy.IsNatCapableEgressSwitch(null));
    }
}
