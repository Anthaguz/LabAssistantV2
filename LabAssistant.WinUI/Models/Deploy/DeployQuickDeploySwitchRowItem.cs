using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabAssistant.WinUI.Models.Deploy;

/// <summary>
/// Editable row backing a single host-switch selection in the Quick Deploy VM editor.
/// Exposes runtime-independent state so the switch editor binds through x:Bind and the type
/// stays unit-testable without the WinUI runtime.
/// </summary>
public sealed class DeployQuickDeploySwitchRowItem : INotifyPropertyChanged
{
    /// <summary>
    /// Sentinel option shown when no host switch is selected for this row.
    /// </summary>
    public const string Placeholder = "(Select switch)";

    private string _selectedSwitch;

    public DeployQuickDeploySwitchRowItem(IReadOnlyList<string> options, string? selectedSwitch)
    {
        Options = options;
        _selectedSwitch = !string.IsNullOrWhiteSpace(selectedSwitch) &&
                          options.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase)
            ? options.First(option => string.Equals(option, selectedSwitch, StringComparison.OrdinalIgnoreCase))
            : Placeholder;
    }

    /// <summary>
    /// Selectable options, always led by <see cref="Placeholder"/> followed by the available host switches.
    /// </summary>
    public IReadOnlyList<string> Options { get; }

    public string SelectedSwitch
    {
        get => _selectedSwitch;
        set => SetProperty(ref _selectedSwitch, value ?? Placeholder);
    }

    /// <summary>
    /// The chosen host switch, or an empty string when the placeholder is still selected.
    /// </summary>
    public string EffectiveSwitchName =>
        string.Equals(_selectedSwitch, Placeholder, StringComparison.Ordinal) ? string.Empty : _selectedSwitch.Trim();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
