namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Identifies which credential-slot right-panel state the From Template lane wants the host page to
/// render. The lane view model projects one of these so the shell right panel stays a pure display
/// surface and the projection logic remains runtime-independent and unit-testable.
/// </summary>
internal enum DeployFromTemplateCredentialPanelKind
{
    /// <summary>No credential slots are relevant; show the reset/empty message.</summary>
    Reset,

    /// <summary>A V2 plan is being built; show the loading indicator.</summary>
    Loading,

    /// <summary>Credential slots (possibly none remaining) are available for review.</summary>
    Slots
}
