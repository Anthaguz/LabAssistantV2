using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Shell owner of the right panel (the progress/results/details surface). It owns visibility,
/// width, compact fallback, and the user open/close toggle, and hosts whatever content the active
/// capability page pushes through <see cref="IShellRightPanel"/>. The manager itself is
/// capability-agnostic: it no longer knows about Deploy. A page sets content, title, and requests
/// auto-open; the shell decides whether the panel is actually shown.
/// </summary>
internal sealed class ShellPanelStateManager : IShellRightPanel
{
    public const double ShellRightPanelExpandedWidth = 380;
    private const string DefaultTitle = "Details";

    private readonly FrameworkElement _insightsPanel;
    private readonly ColumnDefinition _shellRightPanelColumn;
    private readonly TextBlock _rightPanelTitleTextBlock;
    private readonly ContentControl _rightPanelContentHost;
    private readonly Func<double> _getRootLayoutWidth;

    private bool _isShellRightPanelOpen;
    private bool _isShellRightPanelInCompactFallback;
    private bool _isAutoOpenRequested;
    private bool _isAutoOpenSuppressed;
    private bool _isShown;
    // Snapshot of the last state we told listeners about, so StateChanged fires on any real
    // transition regardless of whether the compact-fallback field was already mutated by the caller
    // (SetCompactFallback pre-writes it before calling ApplyRightPanelState).
    private bool _lastNotifiedShown;
    private bool _lastNotifiedUnavailable;
    private string _title = DefaultTitle;

    public ShellPanelStateManager(
        FrameworkElement insightsPanel,
        ColumnDefinition shellRightPanelColumn,
        TextBlock rightPanelTitleTextBlock,
        ContentControl rightPanelContentHost,
        Func<double> getRootLayoutWidth)
    {
        _insightsPanel = insightsPanel;
        _shellRightPanelColumn = shellRightPanelColumn;
        _rightPanelTitleTextBlock = rightPanelTitleTextBlock;
        _rightPanelContentHost = rightPanelContentHost;
        _getRootLayoutWidth = getRootLayoutWidth;
    }

    /// <summary>True when the active page currently owns right-panel content.</summary>
    private bool HasOwner => _rightPanelContentHost.Content is not null;

    // IShellRightPanel

    public void SetContent(UIElement? content)
    {
        if (ReferenceEquals(_rightPanelContentHost.Content, content))
        {
            return;
        }

        _rightPanelContentHost.Content = content;
        if (content is null)
        {
            // Clearing the owner ends any run-scoped panel state so the next owner starts clean.
            _isShellRightPanelOpen = false;
            _isAutoOpenRequested = false;
            _isAutoOpenSuppressed = false;
        }

        ApplyRightPanelState();
    }

    public void SetTitle(string? title)
    {
        _title = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title!;
        _rightPanelTitleTextBlock.Text = _title;
    }

    public void RequestAutoOpen()
    {
        _isAutoOpenRequested = true;
        ApplyRightPanelState();
    }

    public void Toggle() => TogglePanel();

    public bool IsShown => _isShown;

    public bool IsUnavailable => _isShellRightPanelInCompactFallback;

    /// <summary>Raised after <see cref="ApplyRightPanelState"/> changes panel show-state.</summary>
    public event Action? StateChanged;

    public void ApplyRightPanelState()
    {
        if (_getRootLayoutWidth() > 0)
        {
            _isShellRightPanelInCompactFallback = _getRootLayoutWidth() < ShellLayoutManager.ShellRightPanelCompactThreshold;
        }

        if (_isShellRightPanelInCompactFallback && _isShellRightPanelOpen)
        {
            _isShellRightPanelOpen = false;
        }

        // Auto-open is honored only while a request stands, an owner is present, the user has not
        // suppressed it for this run, and we are not in compact fallback.
        if (!_isAutoOpenRequested || !HasOwner)
        {
            _isAutoOpenSuppressed = false;
        }

        if (_isAutoOpenRequested && HasOwner && !_isAutoOpenSuppressed && !_isShellRightPanelInCompactFallback)
        {
            _isShellRightPanelOpen = true;
        }

        var showPanel = HasOwner && _isShellRightPanelOpen && !_isShellRightPanelInCompactFallback;
        _insightsPanel.Visibility = showPanel ? Visibility.Visible : Visibility.Collapsed;
        _shellRightPanelColumn.Width = showPanel ? new GridLength(ShellRightPanelExpandedWidth) : new GridLength(0);
        _rightPanelTitleTextBlock.Text = _title;
        _isShown = showPanel;

        if (_lastNotifiedShown != showPanel || _lastNotifiedUnavailable != _isShellRightPanelInCompactFallback)
        {
            _lastNotifiedShown = showPanel;
            _lastNotifiedUnavailable = _isShellRightPanelInCompactFallback;
            StateChanged?.Invoke();
        }
    }

    public void TogglePanel()
    {
        if (_isShellRightPanelInCompactFallback || !HasOwner)
        {
            return;
        }

        SetRightPanelOpenFromUserToggle(!_isShellRightPanelOpen);
    }

    public void ClosePanel()
    {
        if (_isShellRightPanelOpen)
        {
            SetRightPanelOpenFromUserToggle(false);
        }
    }

    public void SetCompactFallback(bool isCompact)
    {
        if (_isShellRightPanelInCompactFallback == isCompact)
        {
            return;
        }

        _isShellRightPanelInCompactFallback = isCompact;
        if (isCompact)
        {
            _isShellRightPanelOpen = false;
        }

        ApplyRightPanelState();
    }

    /// <summary>
    /// Clears all right-panel state when the shell switches capabilities. The outgoing page is torn
    /// down by the frame, so its content, title, and run-scoped open/auto-open flags must not leak
    /// into the incoming capability.
    /// </summary>
    public void ResetForCapabilitySwitch(string incomingCapabilityKey)
    {
        _ = incomingCapabilityKey;
        _isShellRightPanelOpen = false;
        _isAutoOpenRequested = false;
        _isAutoOpenSuppressed = false;
        _title = DefaultTitle;
        _rightPanelContentHost.Content = null;
    }

    private void SetRightPanelOpenFromUserToggle(bool isOpen)
    {
        _isShellRightPanelOpen = isOpen;
        // A user close during a standing auto-open request suppresses further auto-open for this run.
        _isAutoOpenSuppressed = !isOpen && HasOwner && _isAutoOpenRequested;
        ApplyRightPanelState();
    }
}
