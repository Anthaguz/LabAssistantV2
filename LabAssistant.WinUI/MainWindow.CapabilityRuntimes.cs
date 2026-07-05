using LabAssistant.Business.Assets;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.HyperV;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Deploy;
using LabAssistant.WinUI.ViewModels.Machines;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow
{
    private TemplatesCapabilityRuntime CreateTemplatesCapabilityRuntime()
    {
        var workspaceHost = TemplatesWorkspacePanel;
        TemplatesCapabilityRuntime? runtime = null;
        var libraryComposition = CreateTemplatesLibraryWorkspaceComposition(
            () => runtime?.IsLoading ?? false,
            isLoading => runtime?.SetLoading(isLoading),
            () => runtime?.ApplyUiState(),
            (document, statusText) => runtime?.ShowEditorDocumentAsync(document, statusText) ?? Task.CompletedTask,
            statusText => runtime?.SetEditorStatus(statusText),
            _ => { });
        var editorComposition = CreateTemplatesEditorWorkspaceComposition(
            () => runtime?.IsLoading ?? false,
            isLoading => runtime?.SetLoading(isLoading),
            () => runtime?.ApplyUiState(),
            forceRefresh => runtime?.EnsureLibraryAsync(forceRefresh) ?? Task.CompletedTask,
            forceRefresh => runtime?.LoadEditorReferenceDataAsync(forceRefresh)
                ?? Task.FromResult(new TemplatesEditorReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>())));
        var builderComposition = CreateTemplatesBuilderWorkspaceComposition(
            () => runtime?.IsLoading ?? false,
            isLoading => runtime?.SetLoading(isLoading),
            () => runtime?.ApplyUiState(),
            forceRefresh => runtime?.EnsureLibraryAsync(forceRefresh) ?? Task.CompletedTask,
            forceRefresh => runtime?.LoadBuilderReferenceDataAsync(forceRefresh)
                ?? Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>())));
        var shellBridge = CreateTemplatesWorkspaceShellBridge();

        async Task<IReadOnlyList<string>> loadAvailableVmSwitchesAsync()
        {
            try
            {
                var switches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
                return switches
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        async Task<IReadOnlyList<V2AvailableSwitchInfo>> loadAvailableVmSwitchInfoAsync()
        {
            try
            {
                var result = await _assetsSwitchesCapabilityService.LoadAsync();
                return result.Items
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var item = group.First();
                        return new V2AvailableSwitchInfo
                        {
                            Name = item.Name.Trim(),
                            SwitchType = string.IsNullOrWhiteSpace(item.SwitchType) ? "Unknown" : item.SwitchType.Trim()
                        };
                    })
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception)
            {
                return Array.Empty<V2AvailableSwitchInfo>();
            }
        }

        async Task<IReadOnlyList<TemplateVhdxCatalogOption>> loadVhdxCatalogOptionsAsync()
        {
            var result = await _templatesCapabilityService.LoadVhdxCatalogOptionsAsync();
            return result.Items
                .Select(item => new TemplateVhdxCatalogOption(
                    item.Id,
                    item.Path,
                    item.OsName,
                    item.OsVersion,
                    item.Generation,
                    item.Signature))
                .ToList();
        }

        runtime = new TemplatesCapabilityRuntime(
            workspaceHost,
            libraryComposition,
            editorComposition,
            builderComposition,
            shellBridge,
            loadAvailableVmSwitchesAsync,
            loadAvailableVmSwitchInfoAsync,
            loadVhdxCatalogOptionsAsync,
            () => { });
        return runtime;
    }

    private TemplatesLibraryWorkspaceComposition CreateTemplatesLibraryWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Action<string> setTemplateEditorStatus,
        Action<IReadOnlyList<TemplateLibraryItem>> reconcileDeployTemplateSelection)
    {
        var host = CreateTemplatesLibraryWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            showTemplateEditorAsync,
            document => _templatesCapabilityRuntime?.ShowBuilderDocumentAsync(document) ?? Task.CompletedTask,
            () => _templatesCapabilityRuntime?.CreateBuilderDraftAsync() ?? Task.CompletedTask,
            setTemplateEditorStatus,
            reconcileDeployTemplateSelection);

        return new TemplatesLibraryWorkspaceComposition(_templatesCapabilityService, TemplatesLibraryViewHost, host);
    }

    private TemplatesLibraryWorkspaceHost CreateTemplatesLibraryWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Func<TemplateEditorDocument, Task> showTemplateBuilderAsync,
        Func<Task> createTemplateBuilderDraftAsync,
        Action<string> setTemplateEditorStatus,
        Action<IReadOnlyList<TemplateLibraryItem>> reconcileDeployTemplateSelection)
    {
        return new TemplatesLibraryWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            showTemplateEditorAsync,
            showTemplateBuilderAsync,
            createTemplateBuilderDraftAsync,
            setTemplateEditorStatus,
            PickTemplateFileForOpenAsync,
            PickTemplateFileForSaveAsync,
            ShowDeleteTemplateConfirmationDialogAsync,
            reconcileDeployTemplateSelection);
    }

    private TemplatesEditorWorkspaceComposition CreateTemplatesEditorWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesEditorReferenceData>> loadReferenceDataAsync)
    {
        var host = CreateTemplatesEditorWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync);

        return new TemplatesEditorWorkspaceComposition(_templatesCapabilityService, TemplatesEditorViewHost, host);
    }

    private TemplatesEditorWorkspaceHost CreateTemplatesEditorWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesEditorReferenceData>> loadReferenceDataAsync)
    {
        return new TemplatesEditorWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync,
            PickTemplateFileForSaveAsync,
            ShowRemoveTemplateVmConfirmationDialogAsync,
            () => NavigateToRoute(ShellRouteKeys.TemplatesEditor),
            () => NavigateToRoute(ShellRouteKeys.TemplatesLibrary));
    }

    private TemplatesBuilderWorkspaceComposition CreateTemplatesBuilderWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesBuilderReferenceData>> loadReferenceDataAsync)
    {
        var host = CreateTemplatesBuilderWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync);

        return new TemplatesBuilderWorkspaceComposition(_templatesCapabilityService, TemplatesBuilderViewHost, host);
    }

    private TemplatesBuilderWorkspaceHost CreateTemplatesBuilderWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesBuilderReferenceData>> loadReferenceDataAsync)
    {
        return new TemplatesBuilderWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync,
            PickTemplateFileForSaveAsync,
            () => NavigateToRoute(ShellRouteKeys.TemplatesBuilder),
            () => NavigateToRoute(ShellRouteKeys.TemplatesLibrary));
    }

    private TemplatesWorkspaceShellBridge CreateTemplatesWorkspaceShellBridge()
    {
        return new TemplatesWorkspaceShellBridge(
            () => IsTemplatesCapabilityActive,
            () => IsTemplatesLibraryActive,
            () => IsTemplatesEditorActive,
            () => IsTemplatesBuilderActive);
    }

    private void SetInitialSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void ConfigureShellIcons()
    {
        HamburgerButton.Content = CreateIconGlyph(ShellIconToken.Menu);
        InsightsToggleButton.Content = CreateIconGlyph(ShellIconToken.Insights);
    }

    private TextBlock CreateIconGlyph(string token)
    {
        return new TextBlock
        {
            Text = ShellIconCatalog.GetGlyph(token),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        };
    }
}
