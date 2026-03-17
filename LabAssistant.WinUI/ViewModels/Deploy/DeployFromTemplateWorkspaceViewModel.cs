using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceViewModel
{
    public TemplateLibraryItem? SelectedTemplateLibraryItem { get; private set; }

    public string? SelectedTemplateFilePath { get; private set; }

    public TemplateEditorDocument? ActiveTemplateDocument { get; private set; }

    public string TemplateSummaryText { get; private set; } =
        "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";

    public string TemplateRemediationText { get; private set; } =
        "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";

    public string ActionStatusText { get; private set; } = "No action selected.";

    public void SetSelectedTemplateLibraryItem(TemplateLibraryItem? selectedTemplateLibraryItem)
    {
        SelectedTemplateLibraryItem = selectedTemplateLibraryItem;
        SelectedTemplateFilePath = selectedTemplateLibraryItem?.FilePath;
    }

    public void ClearSelection(string actionStatusText)
    {
        SelectedTemplateLibraryItem = null;
        SelectedTemplateFilePath = null;
        ActiveTemplateDocument = null;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)
    {
        ActiveTemplateDocument = document;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    public void SetSelectionLoadFailed(string actionStatusText)
    {
        ActiveTemplateDocument = null;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    public void SetActionStatus(string actionStatusText)
    {
        ActionStatusText = actionStatusText;
    }

    public void RefreshReviewState(bool hasBlockingFailures)
    {
        if (ActiveTemplateDocument is null)
        {
            TemplateSummaryText = "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";
            TemplateRemediationText = "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";
            return;
        }

        TemplateSummaryText =
            $"Template '{ActiveTemplateDocument.Template.Name}' will deploy {ActiveTemplateDocument.Template.VmTemplates.Count} VM(s). Review shared environment blockers here before deciding whether to remediate or open the template editor.";
        TemplateRemediationText = hasBlockingFailures
            ? "Blocking issues are grouped below when possible. Use Resolve Suggestions for safe shared remaps, or Open in Templates Editor for structural fixes."
            : "This surface is for template review and remediation. Use Open in Templates Editor only when the template itself needs structural changes.";
    }

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        if (string.IsNullOrWhiteSpace(SelectedTemplateFilePath))
        {
            SelectedTemplateLibraryItem = null;
            return;
        }

        SelectedTemplateLibraryItem = items
            .FirstOrDefault(item => string.Equals(item.FilePath, SelectedTemplateFilePath, StringComparison.OrdinalIgnoreCase));

        if (SelectedTemplateLibraryItem is null)
        {
            SelectedTemplateFilePath = null;
            ActiveTemplateDocument = null;
            RefreshReviewState(hasBlockingFailures: false);
        }
    }
}
