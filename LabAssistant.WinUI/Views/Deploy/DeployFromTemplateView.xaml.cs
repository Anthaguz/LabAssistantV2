using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

/// <summary>
/// From Template subview. Under the MVVM rewrite this is a thin x:Bind host: it exposes the
/// <see cref="DeployFromTemplateViewModel"/> assigned by <see cref="DeployPage"/>, drives the
/// view-model lifecycle from Loaded/Unloaded, and keeps only the genuinely control-specific
/// interactions (credential-slot selection and the write-only PasswordBox) that compiled bindings
/// cannot express. All workflow state and commands live on the view model.
/// </summary>
public sealed partial class DeployFromTemplateView : UserControl
{
    private DeployFromTemplateViewModel? _viewModel;

    public DeployFromTemplateView()
    {
        InitializeComponent();
        Loaded += DeployFromTemplateView_Loaded;
        Unloaded += DeployFromTemplateView_Unloaded;
    }

    /// <summary>
    /// The lane view model, assigned by the host page. Setting it refreshes the compiled bindings.
    /// </summary>
    internal DeployFromTemplateViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            Bindings.Update();
        }
    }

    private void DeployFromTemplateView_Loaded(object sender, RoutedEventArgs e)
    {
        _ = _viewModel?.InitializeAsync();
    }

    private void DeployFromTemplateView_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup/cancellation policy: navigate-away cancels any in-flight deploy so the coordinator
        // and V2 runtime tear down resources they created rather than leaking VMs, disks, or switches.
        _ = _viewModel?.CleanupAsync();
    }

    // The credential-slot list selection carries the slot key into the view model; the PasswordBox is
    // write-only and cannot be data-bound, so it is cleared here whenever the active slot changes.
    private void OnDeployV2CredentialSlotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedSlotKey = (DeployV2CredentialSlotsListView.SelectedItem as DeployV2CredentialSlotRow)?.SlotKey;
        DeployV2CredentialSlotPasswordBox.Password = string.Empty;
        _viewModel?.SelectV2CredentialSlot(selectedSlotKey);
    }

    // The PasswordBox value is read here (bindings cannot reach it) and handed to the save command;
    // the box is cleared afterwards so the secret does not linger in the UI.
    private void OnDeployV2SaveCredentialSlotRequested(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var password = DeployV2CredentialSlotPasswordBox.Password;
        DeployV2CredentialSlotPasswordBox.Password = string.Empty;
        if (_viewModel.SaveV2CredentialSlotCommand.CanExecute(password))
        {
            _viewModel.SaveV2CredentialSlotCommand.Execute(password);
        }
    }
}
