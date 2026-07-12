using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Templates Builder subview: the V2 template authoring wizard. Binds directly to
/// <see cref="TemplatesBuilderViewModel"/> via <c>x:Bind</c>; every step panel, navigator row, detail
/// field, and command is bound declaratively with no imperative view-state marshalling or visual-tree
/// scanning. Resolves its own transient view model from DI; the hosting <c>TemplatesPage</c> owns the
/// view-model lifecycle (initialize/cleanup) so switching between the Templates tabs does not tear the
/// view model down. Reference data, library reload, file dialogs, and cross-subview navigation are
/// provided by the hosting page via <see cref="ViewModels.Templates.Builder.ITemplatesBuilderHost"/>.
///
/// The directory-topology canvas is the one place this view keeps imperative code: pointer-driven node
/// dragging cannot be expressed as a binding. The handlers translate pointer deltas (measured against the
/// stationary canvas surface, so moving the dragged node never feeds back into the delta) into
/// <see cref="BuilderTopologyCanvasViewModel.MoveNode"/> calls, and treat a press with no meaningful
/// movement as a node selection.
/// </summary>
public sealed partial class TemplatesBuilderView : UserControl
{
    private const double DragThreshold = 4;
    private static readonly TimeSpan DoubleClickWindow = TimeSpan.FromMilliseconds(400);

    private BuilderCanvasNodeViewModel? _dragNode;
    private Point _dragStartPointer;
    private double _dragStartX;
    private double _dragStartY;
    private bool _dragMoved;

    // Manual double-click detection: the node Border captures the pointer and marks its pointer events handled
    // for dragging, which suppresses the framework Tapped/DoubleTapped gestures, so we recognize a double-click
    // from two threshold-free releases on the same node within the window. The select-vs-manage timing decision
    // lives in a pure, unit-tested arbiter; the view only owns the pointer plumbing around it.
    private readonly BuilderCanvasClickArbiter _clickArbiter = new(DoubleClickWindow);

    public TemplatesBuilderViewModel ViewModel { get; }

    public TemplatesBuilderView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesBuilderViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void OnNodePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not BuilderCanvasNodeViewModel node)
        {
            return;
        }

        _dragNode = node;
        _dragStartPointer = e.GetCurrentPoint(TopologyCanvasSurface).Position;
        _dragStartX = node.X;
        _dragStartY = node.Y;
        _dragMoved = false;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnNodePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragNode is null || sender is not FrameworkElement element || !ReferenceEquals(element.DataContext, _dragNode))
        {
            return;
        }

        var current = e.GetCurrentPoint(TopologyCanvasSurface).Position;
        var deltaX = current.X - _dragStartPointer.X;
        var deltaY = current.Y - _dragStartPointer.Y;
        if (!_dragMoved && (Math.Abs(deltaX) + Math.Abs(deltaY)) < DragThreshold)
        {
            return;
        }

        _dragMoved = true;
        ViewModel.TopologyCanvas?.MoveNode(_dragNode.NodeId, _dragStartX + deltaX, _dragStartY + deltaY);
        e.Handled = true;
    }

    private void OnNodePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragNode is null)
        {
            return;
        }

        // Snapshot and clear the drag state BEFORE releasing capture. ReleasePointerCapture synchronously
        // re-raises PointerCaptureLost; if _dragNode were still set, that re-entrant handler would run the
        // click-detection path a second time and a single physical click would be miscounted as a double-click
        // (zooming into the machine list instead of selecting the domain). Clearing first makes the re-entrant
        // PointerCaptureLost a no-op, so a click is recognized exactly once here.
        var node = _dragNode;
        var moved = _dragMoved;
        _dragNode = null;
        _dragMoved = false;

        if (sender is FrameworkElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        // A press that never crossed the drag threshold is a click. Two clicks on the same node within the
        // window zoom into its Level 2 machines (manage); a single click selects. The arbiter is keyed on the
        // stable NodeId, not the node instance: a select rebuilds the canvas and replaces every node view model,
        // so the second click sees a different object and would never match by reference.
        if (!moved)
        {
            var result = _clickArbiter.Register(node.NodeId, DateTimeOffset.UtcNow);
            if (result == BuilderCanvasClickArbiter.ClickResult.Manage && node.ManageMachinesCommand is not null)
            {
                node.ManageMachinesCommand.Execute(null);
            }
            else
            {
                node.SelectCommand?.Execute(null);
            }
        }

        e.Handled = true;
    }

    // Drag-state cleanup for pointer capture loss / cancellation. Deliberately does NOT run click detection:
    // ReleasePointerCapture inside OnNodePointerReleased re-raises PointerCaptureLost re-entrantly, and a click
    // is already recognized there. Running detection here too would double-count a single click as a zoom.
    private void OnNodePointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _dragNode = null;
        _dragMoved = false;
    }

    // A Level 2 machine card selects on tap; its delete button stops the pointer before this fires.
    private void OnMachineCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BuilderMachineCardViewModel card })
        {
            card.SelectCommand?.Execute(null);
            e.Handled = true;
        }
    }

    private void OnNodePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BuilderCanvasNodeViewModel node })
        {
            node.AffordancesRevealed = true;
        }
    }

    private void OnNodePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BuilderCanvasNodeViewModel node })
        {
            node.AffordancesRevealed = false;
        }
    }

    // An affordance button (delete / add child / add tree) owns its own press: marking it handled here stops
    // the event bubbling to the node Border, so clicking an affordance never starts a drag or a selection.
    private void OnNodeAffordancePointerPressed(object sender, PointerRoutedEventArgs e)
        => e.Handled = true;

    private void OnFrameHeaderPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BuilderCanvasForestFrameViewModel frame })
        {
            frame.AffordancesRevealed = true;
        }
    }

    private void OnFrameHeaderPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BuilderCanvasForestFrameViewModel frame })
        {
            frame.AffordancesRevealed = false;
        }
    }

    private void OnInspectorMachineNameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ViewModel.SelectedMachineInspector?.CommitMachineNameCommand?.Execute(textBox.Text);
        }
    }

    private void OnInspectorMachineCpuCountLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ViewModel.SelectedMachineInspector?.CommitMachineCpuCountCommand?.Execute(textBox.Text);
        }
    }

    private void OnInspectorMachineMemoryMbLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ViewModel.SelectedMachineInspector?.CommitMachineMemoryMbCommand?.Execute(textBox.Text);
        }
    }

    private void OnInspectorMachineBaseDiskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedId = (sender as ComboBox)?.SelectedValue as string;
        ViewModel.SelectedMachineInspector?.CommitMachineBaseDiskCommand?.Execute(selectedId);
    }

    private void OnInspectorMachineHostOctetLostFocus(object sender, RoutedEventArgs e)
    {
        if (IsFocusWithinHostOctetEditors())
        {
            return;
        }

        var inspector = ViewModel.SelectedMachineInspector;
        if (inspector is null || inspector.CommitMachineHostOctetsCommand is null)
        {
            return;
        }

        var octets = new List<int> { ParseOctet(InspectorHostOctetOneTextBox?.Text) };
        if (inspector.HasSecondHostOctet)
        {
            octets.Add(ParseOctet(InspectorHostOctetTwoTextBox?.Text));
        }

        inspector.CommitMachineHostOctetsCommand.Execute(octets);
    }

    private bool IsFocusWithinHostOctetEditors()
    {
        var root = XamlRoot;
        if (root is null)
        {
            return false;
        }

        var focusedElement = FocusManager.GetFocusedElement(root);
        return ReferenceEquals(focusedElement, InspectorHostOctetOneTextBox) ||
               ReferenceEquals(focusedElement, InspectorHostOctetTwoTextBox);
    }

    private static int ParseOctet(string? text)
        => int.TryParse(text?.Trim(), out var octet) ? octet : -1;
}
