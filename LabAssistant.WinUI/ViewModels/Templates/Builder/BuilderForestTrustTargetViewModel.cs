namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A candidate target forest for authoring a forest trust from the currently selected forest. Bound to the
/// "Add forest trust" combo box in the Level 1 detail panel: <see cref="Label"/> is the target forest's name
/// and <see cref="ForestIndex"/> is its index in the draft, which the add command pairs with the selected
/// source forest.
/// </summary>
public sealed class BuilderForestTrustTargetViewModel
{
    public BuilderForestTrustTargetViewModel(int forestIndex, string label)
    {
        ForestIndex = forestIndex;
        Label = label;
    }

    public int ForestIndex { get; }

    public string Label { get; }
}
