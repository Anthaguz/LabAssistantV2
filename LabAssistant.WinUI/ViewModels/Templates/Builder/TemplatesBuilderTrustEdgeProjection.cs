namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A projected forest trust edge: a dashed connector drawn frame-to-frame between the two forests a trust
/// joins. Unlike <see cref="TemplatesBuilderTopologyEdgeProjection"/> (which links domain nodes), a trust
/// edge links forest frames by their node ids, so the canvas can terminate the line on the outer edge of
/// each green forest box rather than on a domain. Endpoints are resolved by the canvas view model from the
/// live frame geometry; only the identity of the two frames lives here.
/// </summary>
internal readonly record struct TemplatesBuilderTrustEdgeProjection(
    string TrustEdgeId,
    string SourceForestNodeId,
    string TargetForestNodeId);
