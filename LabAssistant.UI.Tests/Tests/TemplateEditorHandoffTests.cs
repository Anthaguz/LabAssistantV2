using System;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Covers the <see cref="TemplateEditorHandoff"/> pending-document mailbox that lets a template handed
/// off by Deploy survive the transient Templates page: draining semantics, the navigator trigger, and
/// the precedence rule when a second document is handed off before the first is drained.
/// </summary>
public sealed class TemplateEditorHandoffTests
{
    private static TemplateEditorDocument Document(string id)
        => new() { Template = new() { Id = id }, SourceFilePath = id + ".json" };

    [Fact]
    public void TryTakePendingDocument_WhenEmpty_ReturnsFalse()
    {
        var handoff = new TemplateEditorHandoff();

        var taken = handoff.TryTakePendingDocument(out var document, out var statusText);

        Assert.False(taken);
        Assert.Null(document);
        Assert.Equal(string.Empty, statusText);
    }

    [Fact]
    public async Task ShowInEditorAsync_StoresPendingAndInvokesNavigator()
    {
        var handoff = new TemplateEditorHandoff();
        var navigatorInvocations = 0;
        handoff.SetNavigator(() => navigatorInvocations++);

        await handoff.ShowInEditorAsync(Document("t1"), "Edit template.");

        Assert.Equal(1, navigatorInvocations);
        Assert.True(handoff.TryTakePendingDocument(out var document, out var statusText));
        Assert.Equal("t1", document!.Template.Id);
        Assert.Equal("Edit template.", statusText);
    }

    [Fact]
    public async Task TryTakePendingDocument_DrainsExactlyOnce()
    {
        var handoff = new TemplateEditorHandoff();
        await handoff.ShowInEditorAsync(Document("t1"), "Edit template.");

        Assert.True(handoff.TryTakePendingDocument(out _, out _));
        Assert.False(handoff.TryTakePendingDocument(out var document, out var statusText));
        Assert.Null(document);
        Assert.Equal(string.Empty, statusText);
    }

    [Fact]
    public async Task ShowInEditorAsync_WhenAlreadyPending_LatestDocumentWins()
    {
        var handoff = new TemplateEditorHandoff();

        await handoff.ShowInEditorAsync(Document("first"), "First.");
        await handoff.ShowInEditorAsync(Document("second"), "Second.");

        Assert.True(handoff.TryTakePendingDocument(out var document, out var statusText));
        Assert.Equal("second", document!.Template.Id);
        Assert.Equal("Second.", statusText);
        Assert.False(handoff.TryTakePendingDocument(out _, out _));
    }

    [Fact]
    public async Task ShowInEditorAsync_WithoutNavigator_StillLeavesDocumentPending()
    {
        var handoff = new TemplateEditorHandoff();

        // No navigator registered (no page alive yet): the document must still be drainable on next entry.
        await handoff.ShowInEditorAsync(Document("t1"), "Edit template.");

        Assert.True(handoff.TryTakePendingDocument(out var document, out _));
        Assert.Equal("t1", document!.Template.Id);
    }

    [Fact]
    public async Task ShowInEditorAsync_NullDocument_Throws()
    {
        var handoff = new TemplateEditorHandoff();

        await Assert.ThrowsAsync<ArgumentNullException>(() => handoff.ShowInEditorAsync(null!, "status"));
    }
}
