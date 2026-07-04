using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Templates;

public partial class TemplatesBuilderViewModel : ViewModelBase
{
    private static readonly string[] StepTitles =
    [
        "General",
        "Networks",
        "Forests & Domains",
        "Credentials",
        "VMs",
        "Review"
    ];

    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private string _overviewText = "Configure the template overview, then move through the builder workflow one step at a time.";

    public int StepCount => StepTitles.Length;
    public string CurrentStepTitle => StepTitles[Math.Clamp(CurrentStepIndex, 0, StepTitles.Length - 1)];
    public string StepProgressText => $"Step {CurrentStepIndex + 1} of {StepCount}";
    public bool CanMovePrevious => CurrentStepIndex > 0;
    public bool CanMoveNext => CurrentStepIndex < StepCount - 1;

    [RelayCommand] private void MovePrevious() { if (CanMovePrevious) { CurrentStepIndex--; } }
    [RelayCommand] private void MoveNext() { if (CanMoveNext) { CurrentStepIndex++; } }
    [RelayCommand] private void GoToStep(int stepIndex) => CurrentStepIndex = Math.Clamp(stepIndex, 0, StepCount - 1);

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(StepProgressText));
        OnPropertyChanged(nameof(CanMovePrevious));
        OnPropertyChanged(nameof(CanMoveNext));
        MovePreviousCommand.NotifyCanExecuteChanged();
        MoveNextCommand.NotifyCanExecuteChanged();
    }
}
