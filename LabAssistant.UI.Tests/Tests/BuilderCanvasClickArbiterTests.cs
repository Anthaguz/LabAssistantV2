using System;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Guards the select-vs-manage semantics of the directory-topology canvas: a single click selects a node and a
/// genuine double click (two clicks on the same node within the window) manages it. This is the pure decision
/// extracted out of the view so a pointer-capture re-entrancy regression - where a single physical click was
/// double-counted and wrongly zoomed into the machine list - stays covered without a XAML host.
/// </summary>
public sealed class BuilderCanvasClickArbiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(400);

    [Fact]
    public void SingleClick_Selects()
    {
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var node = new object();

        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(0)));
    }

    [Fact]
    public void TwoClicksOnSameNodeWithinWindow_SelectThenManage()
    {
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var node = new object();

        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(0)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Manage, arbiter.Register(node, At(200)));
    }

    [Fact]
    public void SecondClickAfterWindow_SelectsAgain()
    {
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var node = new object();

        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(0)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(401)));
    }

    [Fact]
    public void SecondClickOnDifferentNode_Selects()
    {
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var first = new object();
        var second = new object();

        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(first, At(0)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(second, At(100)));
    }

    [Fact]
    public void TwoClicksWithEqualButDistinctKeys_SelectThenManage()
    {
        // The view keys the arbiter on the node's stable id, not the node instance: selecting a node rebuilds
        // the canvas and replaces every node view model, so the second click carries a different string object
        // that is Equal-but-not-ReferenceEqual to the first. Value equality must still recognize the pair as a
        // double click, otherwise double-click-to-manage never fires after the first click rebuilds the canvas.
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var firstKey = new string("domain-1".ToCharArray());
        var secondKey = new string("domain-1".ToCharArray());

        Assert.False(ReferenceEquals(firstKey, secondKey));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(firstKey, At(0)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Manage, arbiter.Register(secondKey, At(200)));
    }

    [Fact]
    public void ThirdRapidClickAfterManage_StartsFreshSingleClick()
    {
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var node = new object();

        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(0)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Manage, arbiter.Register(node, At(100)));
        // The streak resets after a manage so a rapid third click selects instead of managing again.
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(150)));
    }

    [Fact]
    public void RepeatedManage_RequiresAFreshPairEachTime()
    {
        var arbiter = new BuilderCanvasClickArbiter(Window);
        var node = new object();

        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(0)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Manage, arbiter.Register(node, At(100)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Select, arbiter.Register(node, At(200)));
        Assert.Equal(BuilderCanvasClickArbiter.ClickResult.Manage, arbiter.Register(node, At(300)));
    }

    private static DateTimeOffset At(int milliseconds)
        => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromMilliseconds(milliseconds);
}
