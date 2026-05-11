using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Defines the From Template lane boundary as seen from Deploy capability-level composition and runtime.
/// </summary>
internal interface IDeployFromTemplateLane : IDeployResultsPanelParticipant
{
    bool IsLoadingTemplates { get; }

    Task EnsureTemplatesLoadedAsync(bool forceRefresh);

    void ApplyShellState(bool isActive);

    void RefreshUi();

    void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items);

    void ResetPanelState();
}
