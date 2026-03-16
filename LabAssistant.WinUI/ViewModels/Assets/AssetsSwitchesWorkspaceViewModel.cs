using System.Collections.ObjectModel;
using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal sealed class AssetsSwitchesWorkspaceViewModel
{
    public ObservableCollection<AssetsSwitchListRow> Inventory { get; } = [];

    public ObservableCollection<string> AttachedVmNames { get; } = [];

    public AssetsSwitchListRow? SelectedRow { get; set; }

    public AssetsSwitchDraft? PendingDraft { get; set; }

    public bool IsLoading { get; set; }

    public bool IsSaving { get; set; }

    public bool IsDeleting { get; set; }

    public bool IsUpdatingEditor { get; set; }

    public bool HasErrorState { get; set; }

    public int ValidationRequestVersion { get; set; }

    public int AssessmentRequestVersion { get; set; }

    public string StatusText { get; set; } = "Select a virtual switch or click New to begin.";

    public string SelectedSwitchValidationText { get; set; } = "Select a switch or click New to begin.";

    public string DeleteConstraintText { get; set; } = string.Empty;

    public string ErrorStateText { get; set; } = "No switch load or action errors.";

    public string AttachedVmHintText { get; set; } = "No attached VMs.";
}
