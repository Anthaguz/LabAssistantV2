using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
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

    private BuilderCanvasNodeViewModel? _dragNode;
    private Point _dragStartPointer;
    private double _dragStartX;
    private double _dragStartY;
    private bool _dragMoved;

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

        var node = _dragNode;
        var moved = _dragMoved;
        if (sender is FrameworkElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        _dragNode = null;
        _dragMoved = false;

        // A press that never crossed the drag threshold is a selection, not a move.
        if (!moved)
        {
            node.SelectCommand?.Execute(null);
        }

        e.Handled = true;
    }
}
