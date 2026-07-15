using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// View-only sentinel that flows as the trailing tile in the Level 2 machine grid so the
/// "+ Add computer" affordance wraps in the same layout as the machine cards. Kept out of
/// <c>MachineCards</c> (which stays machine-only for the tests) and surfaced through the separate
/// <c>MachineGridItems</c> source. Carries the add command plus a reactive enabled flag the owning
/// view model keeps in sync with <c>AddVmEnabled</c>.
/// </summary>
public sealed partial class BuilderAddMachinePlaceholder : ObservableObject
{
    public BuilderAddMachinePlaceholder(string label, IRelayCommand addCommand)
    {
        Label = label;
        AddCommand = addCommand;
    }

    public string Label { get; }

    public IRelayCommand AddCommand { get; }

    /// <summary>Mirrors <c>AddVmEnabled</c>; disables the placeholder tile when adding is not allowed.</summary>
    [ObservableProperty] private bool _isEnabled;
}
