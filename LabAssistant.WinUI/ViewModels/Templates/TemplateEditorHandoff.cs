using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Default <see cref="ITemplateEditorHandoff"/> mediator. Registered as a DI singleton so the pending
/// editor document survives the transient Templates page: Deploy resolves this instance to request an
/// editor handoff, the shell registers a navigator, and the Templates page drains the pending document
/// on entry. This keeps the Templates-specific routing off the otherwise capability-agnostic shell host.
/// </summary>
internal sealed class TemplateEditorHandoff : ITemplateEditorHandoff
{
    private readonly object _gate = new();
    private Action? _navigator;
    private TemplateEditorDocument? _pendingDocument;
    private string _pendingStatusText = string.Empty;

    /// <inheritdoc />
    public Task ShowInEditorAsync(TemplateEditorDocument document, string statusText)
    {
        ArgumentNullException.ThrowIfNull(document);

        Action? navigator;
        lock (_gate)
        {
            _pendingDocument = document;
            _pendingStatusText = statusText ?? string.Empty;
            navigator = _navigator;
        }

        navigator?.Invoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SetNavigator(Action? navigator)
    {
        lock (_gate)
        {
            _navigator = navigator;
        }
    }

    /// <inheritdoc />
    public bool TryTakePendingDocument([NotNullWhen(true)] out TemplateEditorDocument? document, out string statusText)
    {
        lock (_gate)
        {
            if (_pendingDocument is null)
            {
                document = null;
                statusText = string.Empty;
                return false;
            }

            document = _pendingDocument;
            statusText = _pendingStatusText;
            _pendingDocument = null;
            _pendingStatusText = string.Empty;
            return true;
        }
    }
}
