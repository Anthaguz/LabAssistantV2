using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Cross-subview seam the Templates Builder view model uses to reach shell affordances it does not
/// own: loading builder reference data, reloading the Library inventory after a save, choosing a file
/// for Save As, and routing between the Builder and Library subviews. The hosting <c>TemplatesPage</c>
/// implements it, keeping the Builder view model free of navigation, reference-data, and dialog
/// concerns and unit-testable without a dispatcher or Hyper-V.
/// </summary>
internal interface ITemplatesBuilderHost
{
    /// <summary>Loads deterministic builder reference data (switch inventory + VHDX catalog).</summary>
    Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh);

    /// <summary>Reloads the Library inventory (used after a successful save).</summary>
    Task ReloadLibraryAsync(bool forceRefresh);

    /// <summary>Shows the Save As file picker and returns the chosen path, or null if cancelled.</summary>
    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    /// <summary>Routes to the Builder subview.</summary>
    void NavigateToBuilder();

    /// <summary>Routes back to the Library subview.</summary>
    void NavigateToLibrary();
}
