using System;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Capability-agnostic handle a capability page uses to drive the shell right panel (the
/// progress/results/details surface). A page hosts its own right-panel content element, sets the
/// panel title, requests auto-open, and observes whether the panel is currently shown; the
/// shell owns visibility, width, compact fallback, and the user open/close toggle. This replaces
/// the former Deploy-specific right-panel wiring that lived in <c>MainWindow</c> and
/// <c>DeployCapabilityRuntime</c>.
/// </summary>
internal interface IShellRightPanel
{
    /// <summary>
    /// Hosts <paramref name="content"/> in the right panel, or clears the panel owner when null.
    /// Clearing collapses the panel and resets its auto-open state.
    /// </summary>
    void SetContent(UIElement? content);

    /// <summary>Sets the right-panel header title. Falls back to a default when null or blank.</summary>
    void SetTitle(string? title);

    /// <summary>
    /// Requests the panel auto-open (for example when a deployment run starts). Honored while the
    /// active page owns content and the layout is not in compact fallback, unless the user has
    /// explicitly closed the panel for the current run.
    /// </summary>
    void RequestAutoOpen();

    /// <summary>
    /// Toggles the panel open/closed on behalf of an in-page control (for example a
    /// "Open Progress / Results" button). No-op while in compact fallback or without an owner.
    /// </summary>
    void Toggle();

    /// <summary>True when the panel is currently visible.</summary>
    bool IsShown { get; }

    /// <summary>True when the layout is too narrow to host the panel (compact fallback).</summary>
    bool IsUnavailable { get; }

    /// <summary>
    /// Raised after the shell recomputes panel show-state (visibility or compact fallback), so the
    /// owning page can reflect it in its in-panel content (for example a launcher button label or
    /// an empty-state hint).
    /// </summary>
    event Action? StateChanged;
}
