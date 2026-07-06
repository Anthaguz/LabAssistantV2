namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A single row in a Builder detail form's two-column field grid. Holds the left field and an
/// optional right field, reproducing the row-major two-column layout the imperative form builder
/// produced with <c>CreateFieldGrid</c>.
/// </summary>
public sealed class BuilderFieldRowViewModel
{
    public BuilderFieldRowViewModel(BuilderFieldViewModel left, BuilderFieldViewModel? right)
    {
        Left = left;
        Right = right;
    }

    public BuilderFieldViewModel Left { get; }

    public BuilderFieldViewModel? Right { get; }

    public bool HasRight => Right is not null;
}
