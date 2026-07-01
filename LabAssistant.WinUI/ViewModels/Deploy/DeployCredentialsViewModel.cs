using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LabAssistant.WinUI.Infrastructure;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

public partial class DeployCredentialsViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<CredentialSlotItem> _credentialSlots = new();

    [ObservableProperty]
    private bool _allSlotsResolved;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IReadOnlyList<CredentialSlotItem> CredentialSlotItems => CredentialSlots;

    public string StatusMessageText => StatusMessage;

    public bool HasCredentialSlots => CredentialSlots.Count > 0;

    public Visibility CredentialSlotsVisibility => HasCredentialSlots ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => !IsLoading && !HasCredentialSlots ? Visibility.Visible : Visibility.Collapsed;

    public string EmptyStateMessage => AllSlotsResolved
        ? "All credential slots are resolved."
        : string.IsNullOrWhiteSpace(StatusMessage)
            ? "Credential slots appear here after V2 plan review."
            : StatusMessage;

    public void Reset(string statusMessage = "Credential slots appear here after V2 plan review.")
    {
        IsLoading = false;
        AllSlotsResolved = false;
        StatusMessage = statusMessage;
        ReplaceItems(CredentialSlots, []);
        NotifyDerivedStateChanged();
    }

    public void ShowLoading(string statusMessage)
    {
        IsLoading = true;
        AllSlotsResolved = false;
        StatusMessage = statusMessage;
        ReplaceItems(CredentialSlots, []);
        NotifyDerivedStateChanged();
    }

    public void UpdateSlots(IEnumerable<CredentialSlotItem> slots, bool allSlotsResolved, string statusMessage)
    {
        IsLoading = false;
        AllSlotsResolved = allSlotsResolved;
        StatusMessage = statusMessage;
        ReplaceItems(CredentialSlots, slots);
        NotifyDerivedStateChanged();
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(StatusMessageText));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private void NotifyDerivedStateChanged()
    {
        OnPropertyChanged(nameof(CredentialSlotItems));
        OnPropertyChanged(nameof(StatusMessageText));
        OnPropertyChanged(nameof(HasCredentialSlots));
        OnPropertyChanged(nameof(CredentialSlotsVisibility));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
