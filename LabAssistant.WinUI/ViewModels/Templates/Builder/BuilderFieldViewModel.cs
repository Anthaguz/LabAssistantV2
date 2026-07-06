using CommunityToolkit.Mvvm.ComponentModel;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// The kind of editor a <see cref="BuilderFieldViewModel"/> renders, selected by the field template
/// selector so the declarative Builder detail forms bind the right control per field.
/// </summary>
public enum BuilderFieldKind
{
    Text,
    ReadOnlyText,
    Combo,
    SwitchPicker,
    CheckBox
}

/// <summary>
/// A single bindable field in a Builder detail form. Replaces the imperative "tag a control with a
/// <c>BuilderDraftFieldKey</c> and scan the visual tree to read it back" pattern with a two-way bound
/// view model: the owning <see cref="TemplatesBuilderViewModel"/> subscribes to <see cref="Changed"/>
/// and reads values straight off the field view models for its scope. Programmatic value assignment is
/// routed through <see cref="Mutate"/> so refreshing the form does not re-enter the edit pipeline.
/// </summary>
public partial class BuilderFieldViewModel : ObservableObject
{
    private bool _suppressChange;

    internal BuilderFieldViewModel(BuilderDraftFieldKey fieldKey, string header, BuilderFieldKind kind)
    {
        FieldKey = fieldKey;
        Header = header;
        Kind = kind;
    }

    internal BuilderDraftFieldKey FieldKey { get; }

    public string Header { get; }

    public BuilderFieldKind Kind { get; }

    public bool IsReadOnly => Kind == BuilderFieldKind.ReadOnlyText;

    public IReadOnlyList<string> Options { get; init; } = [];

    public IReadOnlyList<BuilderSwitchOptionViewModel> SwitchOptions { get; init; } = [];

    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private BuilderSwitchOptionViewModel? _selectedSwitchOption;

    /// <summary>Raised when the user edits the field. Not raised for <see cref="Mutate"/> updates.</summary>
    internal Action<BuilderFieldViewModel>? Changed { get; set; }

    /// <summary>Applies a programmatic mutation without raising <see cref="Changed"/>.</summary>
    internal void Mutate(Action<BuilderFieldViewModel> mutate)
    {
        _suppressChange = true;
        try
        {
            mutate(this);
        }
        finally
        {
            _suppressChange = false;
        }
    }

    partial void OnValueChanged(string value) => RaiseChanged();

    partial void OnIsCheckedChanged(bool value) => RaiseChanged();

    partial void OnSelectedSwitchOptionChanged(BuilderSwitchOptionViewModel? value) => RaiseChanged();

    private void RaiseChanged()
    {
        if (!_suppressChange)
        {
            Changed?.Invoke(this);
        }
    }
}
