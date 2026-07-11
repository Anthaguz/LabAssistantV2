using System;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Pure timing state machine that decides whether a threshold-free pointer release on a canvas node is a
/// single click (select the node) or the second of a double click (manage - zoom into the node's Level 2
/// machines). The node Border captures the pointer and marks its pointer events handled to drive dragging,
/// which suppresses the framework Tapped / DoubleTapped gestures, so double-click recognition lives here
/// instead. Kept free of any XAML or pointer types so the select-vs-manage semantics are unit-testable
/// without a UI host, and so the re-entrancy hazard around pointer-capture release stays isolated in the view.
/// </summary>
public sealed class BuilderCanvasClickArbiter
{
    /// <summary>The action a recorded click resolves to.</summary>
    public enum ClickResult
    {
        /// <summary>A single click: select the node and show its detail.</summary>
        Select,

        /// <summary>The second click of a double click: manage the node (zoom into its Level 2 machines).</summary>
        Manage
    }

    private readonly TimeSpan _doubleClickWindow;
    private object? _lastClickNode;
    private DateTimeOffset _lastClickTime;

    /// <summary>
    /// Creates an arbiter that treats two clicks on the same node within <paramref name="doubleClickWindow"/>
    /// as a double click.
    /// </summary>
    public BuilderCanvasClickArbiter(TimeSpan doubleClickWindow)
    {
        _doubleClickWindow = doubleClickWindow;
    }

    /// <summary>
    /// Records a click on <paramref name="node"/> at <paramref name="now"/> and returns whether it resolves to
    /// a select or a manage. A click on the same node within the window (measured from the previous click)
    /// resolves to <see cref="ClickResult.Manage"/>; any other click resolves to <see cref="ClickResult.Select"/>.
    /// After a manage the streak resets, so a third rapid click on the same node starts a fresh single click
    /// rather than immediately managing again.
    /// </summary>
    public ClickResult Register(object node, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(node);

        var isDoubleClick = ReferenceEquals(node, _lastClickNode) && (now - _lastClickTime) <= _doubleClickWindow;
        if (isDoubleClick)
        {
            _lastClickNode = null;
            return ClickResult.Manage;
        }

        _lastClickNode = node;
        _lastClickTime = now;
        return ClickResult.Select;
    }
}
