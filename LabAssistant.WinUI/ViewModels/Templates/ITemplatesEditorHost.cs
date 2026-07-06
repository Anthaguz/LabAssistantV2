using System.Threading.Tasks;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Cross-subview seam the Templates Editor view model uses to reach shell affordances it does not
/// own: routing back to the Library subview, reloading the Library inventory after a save, and
/// confirming removal of a VM slot. The hosting <c>TemplatesPage</c> implements it, keeping the
/// Editor view model free of navigation and dialog concerns and unit-testable without a dispatcher.
/// </summary>
internal interface ITemplatesEditorHost
{
    /// <summary>Routes back to the Library subview.</summary>
    void NavigateToLibrary();

    /// <summary>Reloads the Library inventory (used after a successful save).</summary>
    Task ReloadLibraryAsync(bool forceRefresh);

    /// <summary>Shows the confirmation dialog for removing the VM slot named <paramref name="vmName"/>.</summary>
    Task<bool> ConfirmRemoveVmAsync(string vmName);
}
