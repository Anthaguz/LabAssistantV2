using System.Collections.ObjectModel;
using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal sealed class AssetsBaseDisksWorkspaceViewModel
{
    public ObservableCollection<AssetsBaseDiskListRow> Inventory { get; } = [];

    public AssetsBaseDiskListRow? SelectedRow { get; set; }

    public AssetsBaseDiskDraft? PendingDraft { get; set; }

    public bool IsLoading { get; set; }

    public bool IsSaving { get; set; }

    public bool IsRemoving { get; set; }

    public bool IsUpdatingEditor { get; set; }

    public bool HasErrorState { get; set; }

    public string StatusText { get; set; } = "Select a base disk or import a VHDX to begin.";

    public string ErrorStateText { get; set; } = "No catalog load errors.";

    public string SelectedDiskSummaryText { get; set; } = "Select a base disk or import a VHDX to begin.";

    public string SelectedDiskValidationText { get; set; } = "Validation has not been evaluated.";

    public string ReferenceWarningText { get; set; } = "No removal assessment has been performed.";
}
