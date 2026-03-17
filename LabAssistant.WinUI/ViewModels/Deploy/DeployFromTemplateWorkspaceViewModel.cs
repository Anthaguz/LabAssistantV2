using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using System.Collections.ObjectModel;

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

    public ObservableCollection<DeployIssueRow> IssueRows { get; } = [];

    public ObservableCollection<string> SharedIssueSummaries { get; } = [];

    public string ReadinessSummaryText { get; private set; } =
        "Select a template to evaluate readiness and run deploy.";

    public string SharedIssuesSummaryText { get; private set; } =
        "Shared review items appear here when multiple VMs need the same remediation.";

    public string GlobalIssuesBadgeText { get; private set; } = "Issues: 0";

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

    public void SetReadinessSummary(string readinessSummaryText)
    {
        ReadinessSummaryText = readinessSummaryText;
    }

    public void ClearGroupedIssueState()
    {
        IssueRows.Clear();
        SharedIssueSummaries.Clear();
        GlobalIssuesBadgeText = "Issues: 0";
        SharedIssuesSummaryText = "Shared review items appear here when multiple VMs need the same remediation.";
    }

    public void ReplaceIssueRows(IReadOnlyList<DeployIssueRow> issueRows)
    {
        IssueRows.Clear();
        foreach (var issueRow in issueRows)
        {
            IssueRows.Add(issueRow);
        }

        GlobalIssuesBadgeText = $"Issues: {IssueRows.Count}";
        RefreshSharedIssueSummaries();
    }

    public void RefreshReviewState(bool hasBlockingFailures)
    {
        if (ActiveTemplateDocument is null)
        {
            TemplateSummaryText = "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";
            TemplateRemediationText = "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";
            ReadinessSummaryText = "Select a template to evaluate readiness and run deploy.";
            ClearGroupedIssueState();
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

    private void RefreshSharedIssueSummaries()
    {
        SharedIssueSummaries.Clear();

        var groupedIssues = IssueRows
            .Where(issue => !string.Equals(issue.Scope, "Global", StringComparison.OrdinalIgnoreCase))
            .GroupBy(issue => $"{issue.Severity}|{issue.Message}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(issue => issue.Scope).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .OrderByDescending(group => group.Key.StartsWith("Block|", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(group => group.Count())
            .ToList();

        foreach (var group in groupedIssues)
        {
            var scopes = group
                .Select(issue => issue.Scope)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var message = group.First().Message;
            var severity = group.First().Severity;
            SharedIssueSummaries.Add($"{severity}: {message} Shared across {scopes.Count} VM(s): {string.Join(", ", scopes)}");
        }

        SharedIssuesSummaryText = SharedIssueSummaries.Count > 0
            ? "Shared environment and compatibility issues detected across multiple VMs. Fix them here when safe, or open Templates Editor for structural changes."
            : "No shared review items are currently grouped. Review the readiness summary, then use the main actions below.";
    }
}
