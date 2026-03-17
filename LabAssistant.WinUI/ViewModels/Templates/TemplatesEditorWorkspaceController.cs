using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesEditorWorkspaceControllerHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyWorkspaceState();

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);
}

internal sealed class TemplatesEditorWorkspaceController
{
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly TemplatesEditorWorkspaceViewModel _workspace;
    private readonly ITemplatesEditorWorkspaceControllerHost _host;

    public TemplatesEditorWorkspaceController(
        ITemplatesCapabilityService templatesCapabilityService,
        TemplatesEditorWorkspaceViewModel workspace,
        ITemplatesEditorWorkspaceControllerHost host)
    {
        _templatesCapabilityService = templatesCapabilityService;
        _workspace = workspace;
        _host = host;
    }

    public bool ApplySelectedVmDraft(bool showSuccessStatus)
    {
        if (_workspace.ActiveDocument is null)
        {
            _workspace.SetStatusText("No template loaded.");
            _host.ApplyWorkspaceState();
            return false;
        }

        if (!TryApplyEditorFieldsToDocument(showSuccessStatus))
        {
            _host.ApplyWorkspaceState();
            return false;
        }

        _host.ApplyWorkspaceState();
        return true;
    }

    public async Task SaveAsync()
    {
        if (_workspace.ActiveDocument is null)
        {
            _workspace.SetStatusText("No template loaded.");
            _host.ApplyWorkspaceState();
            return;
        }

        if (!TryApplyEditorFieldsToDocument(showSuccessStatus: false))
        {
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        _host.ApplyWorkspaceState();

        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_workspace.ActiveDocument);
            _workspace.SetStatusText(result.UserMessage);
            if (result.Success)
            {
                _workspace.SetDocument(new TemplateEditorDocument
                {
                    Template = _workspace.ActiveDocument.Template,
                    SourceFilePath = result.FilePath
                });
                await _host.EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public async Task SaveAsAsync()
    {
        if (_workspace.ActiveDocument is null)
        {
            _workspace.SetStatusText("No template loaded.");
            _host.ApplyWorkspaceState();
            return;
        }

        if (!TryApplyEditorFieldsToDocument(showSuccessStatus: false))
        {
            _host.ApplyWorkspaceState();
            return;
        }

        var suggestedName = string.IsNullOrWhiteSpace(_workspace.ActiveDocument.Template.Name)
            ? "lab-template"
            : _workspace.ActiveDocument.Template.Name;
        var destinationPath = await _host.PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            _workspace.SetStatusText("Save As cancelled.");
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        _host.ApplyWorkspaceState();

        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_workspace.ActiveDocument, destinationPath, saveAs: true);
            _workspace.SetStatusText(result.UserMessage);
            if (result.Success)
            {
                _workspace.SetDocument(new TemplateEditorDocument
                {
                    Template = _workspace.ActiveDocument.Template,
                    SourceFilePath = result.FilePath
                });
                await _host.EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public async Task ValidateAsync()
    {
        if (_workspace.ActiveDocument is null)
        {
            _workspace.SetStatusText("No template loaded.");
            _host.ApplyWorkspaceState();
            return;
        }

        if (!TryApplyEditorFieldsToDocument(showSuccessStatus: false))
        {
            _host.ApplyWorkspaceState();
            return;
        }

        var result = await _templatesCapabilityService.ValidateAsync(_workspace.ActiveDocument);
        _workspace.SetStatusText(result.IsValid
            ? "Template validation passed."
            : "Validation failed: " + string.Join(" ", result.Errors));
        _host.ApplyWorkspaceState();
    }

    private bool TryApplyEditorFieldsToDocument(bool showSuccessStatus)
    {
        var document = _workspace.ActiveDocument;
        if (document is null)
        {
            return false;
        }

        if (!TryApplySelectedVmDraft(showSuccessStatus))
        {
            return false;
        }

        var template = document.Template;
        template.Name = _workspace.TemplateName.Trim();
        var trimmedDescription = _workspace.TemplateDescription.Trim();
        template.Description = string.IsNullOrWhiteSpace(trimmedDescription) ? null : trimmedDescription;
        template.VmTemplates = _workspace.VmEntries.ToList();
        _workspace.SetVmCount(template.VmTemplates.Count);
        _workspace.SetDocument(document);
        return true;
    }

    private bool TryApplySelectedVmDraft(bool showSuccessStatus)
    {
        if (_workspace.SelectedVmEntry is null)
        {
            return true;
        }

        var vmName = _workspace.VmNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            _workspace.SetStatusText("VM name is required.");
            return false;
        }

        if (!int.TryParse(_workspace.VmMemoryDraft, out var memoryMb) || memoryMb <= 0)
        {
            _workspace.SetStatusText("Memory must be a positive integer.");
            return false;
        }

        if (!int.TryParse(_workspace.VmCpuDraft, out var cpuCount) || cpuCount <= 0)
        {
            _workspace.SetStatusText("CPU count must be a positive integer.");
            return false;
        }

        if (!TryValidateSelectedSwitches(_workspace.SelectedVmSwitches, out var switchValidationError))
        {
            _workspace.SetStatusText(switchValidationError ?? "Switch validation failed.");
            return false;
        }

        var selectedVmEntry = _workspace.SelectedVmEntry;
        selectedVmEntry.Name = vmName;
        selectedVmEntry.MemoryMb = memoryMb;
        selectedVmEntry.CpuCount = cpuCount;

        var selectedSwitches = _workspace.SelectedVmSwitches.ToList();
        selectedVmEntry.SwitchNames = selectedSwitches.Count > 0 ? selectedSwitches : null;
        selectedVmEntry.SwitchName = selectedSwitches.Count > 0 ? selectedSwitches[0] : null;

        if (_workspace.SelectedVmVhdxCatalogOption is TemplateVhdxCatalogOption selectedCatalogOption)
        {
            selectedVmEntry.VhdxId = selectedCatalogOption.Id;
            selectedVmEntry.VhdPath = selectedCatalogOption.Path;
            selectedVmEntry.VhdxSignature = selectedCatalogOption.Signature;
        }
        else if (_workspace.RequiresVmVhdxResolution)
        {
            _workspace.SetStatusText(_workspace.VmVhdxGuidanceText);
            return false;
        }

        if (showSuccessStatus)
        {
            _workspace.SetStatusText($"Updated VM entry '{vmName}'.");
        }

        return true;
    }

    private bool TryValidateSelectedSwitches(
        IReadOnlyList<string> selectedSwitches,
        out string? validationError)
    {
        validationError = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selectedSwitch in selectedSwitches)
        {
            if (string.IsNullOrWhiteSpace(selectedSwitch))
            {
                validationError = "Each switch row must have a selected host switch or be removed.";
                return false;
            }

            if (!_workspace.AvailableVmSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
            {
                validationError = $"Switch '{selectedSwitch}' is not available on this host.";
                return false;
            }

            if (!seen.Add(selectedSwitch))
            {
                validationError = $"Duplicate switch '{selectedSwitch}' is not allowed.";
                return false;
            }
        }

        return true;
    }
}
