using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A row in the selected forest's existing-trusts list in the Level 1 detail panel. Shows the trust
/// <see cref="Label"/> (the partner forest it joins) and carries a <see cref="RemoveCommand"/> that removes
/// this trust. The command lives on the row so the item template can bind it directly without reaching back
/// to the owning view model.
/// </summary>
public sealed class BuilderForestTrustRowViewModel
{
    public BuilderForestTrustRowViewModel(string trustId, string label, IRelayCommand removeCommand)
    {
        TrustId = trustId;
        Label = label;
        RemoveCommand = removeCommand;
    }

    public string TrustId { get; }

    public string Label { get; }

    public IRelayCommand RemoveCommand { get; }
}
