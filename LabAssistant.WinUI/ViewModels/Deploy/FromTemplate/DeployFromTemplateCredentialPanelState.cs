namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Immutable projection of the From Template credential-slot right panel. The lane view model builds
/// this from its V2 review state and the host page applies it to the credential right-panel view
/// model, keeping the right panel a passive display surface.
/// </summary>
/// <param name="Kind">Which panel state to render.</param>
/// <param name="StatusMessage">Status text to show in the panel header.</param>
/// <param name="Slots">Credential slots to display when <see cref="Kind"/> is
/// <see cref="DeployFromTemplateCredentialPanelKind.Slots"/>; otherwise empty.</param>
/// <param name="AllSlotsResolved">Whether all credential slots for the active plan are resolved.</param>
internal sealed record DeployFromTemplateCredentialPanelState(
    DeployFromTemplateCredentialPanelKind Kind,
    string StatusMessage,
    IReadOnlyList<CredentialSlotItem> Slots,
    bool AllSlotsResolved)
{
    /// <summary>Creates a reset/empty projection with the supplied status message.</summary>
    public static DeployFromTemplateCredentialPanelState Reset(string statusMessage) =>
        new(DeployFromTemplateCredentialPanelKind.Reset, statusMessage, [], false);

    /// <summary>Creates a loading projection with the supplied status message.</summary>
    public static DeployFromTemplateCredentialPanelState Loading(string statusMessage) =>
        new(DeployFromTemplateCredentialPanelKind.Loading, statusMessage, [], false);

    /// <summary>Creates a slots projection with the supplied credential slots and status message.</summary>
    public static DeployFromTemplateCredentialPanelState WithSlots(
        IReadOnlyList<CredentialSlotItem> slots,
        bool allSlotsResolved,
        string statusMessage) =>
        new(DeployFromTemplateCredentialPanelKind.Slots, statusMessage, slots, allSlotsResolved);
}
