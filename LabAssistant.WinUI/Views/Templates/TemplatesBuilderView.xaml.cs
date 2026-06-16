using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

internal readonly record struct TemplatesBuilderViewState(
    string ContextText,
    string ReferenceText,
    string StatusText,
    bool IsStatusVisible,
    bool HasActiveDraft,
    TemplatesBuilderDraftSnapshot Draft);

internal readonly record struct TemplatesBuilderActionState(
    bool CanApplySuggestions,
    bool CanValidate,
    bool CanSave,
    bool CanSaveAs,
    bool CanBackToLibrary);

public sealed partial class TemplatesBuilderView : UserControl
{
    private bool _isUpdatingDraft;

    public event EventHandler? DraftChanged;
    public event EventHandler? ApplySuggestionsRequested;
    public event EventHandler? ValidateRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? BackToLibraryRequested;

    public TemplatesBuilderView()
    {
        InitializeComponent();
        BuilderDeploymentProfileComboBox.SelectedIndex = 1;
        BuilderTemplateNameTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderTemplateDescriptionTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderDeploymentProfileComboBox.SelectionChanged += BuilderDeploymentProfileComboBox_SelectionChanged;
        BuilderLabNetworksTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderForestsTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderDomainsTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderVmsTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderNicsTextBox.TextChanged += BuilderDraftControl_Changed;
        BuilderConfirmSaveCheckBox.Checked += BuilderConfirmSaveCheckBox_Changed;
        BuilderConfirmSaveCheckBox.Unchecked += BuilderConfirmSaveCheckBox_Changed;
        BuilderApplySuggestionsButton.Click += BuilderApplySuggestionsButton_Click;
        BuilderValidateButton.Click += BuilderValidateButton_Click;
        BuilderSaveButton.Click += BuilderSaveButton_Click;
        BuilderSaveAsButton.Click += BuilderSaveAsButton_Click;
        BuilderBackToLibraryButton.Click += BuilderBackToLibraryButton_Click;
    }

    internal TemplatesBuilderDraftSnapshot CaptureDraft()
    {
        return new TemplatesBuilderDraftSnapshot(
            BuilderTemplateNameTextBox.Text,
            BuilderTemplateDescriptionTextBox.Text,
            GetSelectedProfile(),
            BuilderLabNetworksTextBox.Text,
            BuilderForestsTextBox.Text,
            BuilderDomainsTextBox.Text,
            BuilderVmsTextBox.Text,
            BuilderNicsTextBox.Text,
            BuilderConfirmSaveCheckBox.IsChecked == true);
    }

    internal void UpdateViewState(TemplatesBuilderViewState state)
    {
        _isUpdatingDraft = true;
        try
        {
            SetTextIfChanged(BuilderContextTextBlock, state.ContextText);
            SetTextIfChanged(BuilderReferenceTextBlock, state.ReferenceText);
            SetTextIfChanged(BuilderStatusTextBlock, state.StatusText);
            BuilderStatusTextBlock.Visibility = state.IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;

            SetTextIfChanged(BuilderTemplateNameTextBox, state.Draft.TemplateName);
            SetTextIfChanged(BuilderTemplateDescriptionTextBox, state.Draft.TemplateDescription);
            SetSelectedProfile(state.Draft.DeploymentProfile);
            SetTextIfChanged(BuilderLabNetworksTextBox, state.Draft.LabNetworksText);
            SetTextIfChanged(BuilderForestsTextBox, state.Draft.ForestsText);
            SetTextIfChanged(BuilderDomainsTextBox, state.Draft.DomainsText);
            SetTextIfChanged(BuilderVmsTextBox, state.Draft.VmsText);
            SetTextIfChanged(BuilderNicsTextBox, state.Draft.NicsText);
            BuilderConfirmSaveCheckBox.IsChecked = state.Draft.IsSaveConfirmed;
        }
        finally
        {
            _isUpdatingDraft = false;
        }
    }

    internal void UpdateActionState(TemplatesBuilderActionState state)
    {
        BuilderApplySuggestionsButton.IsEnabled = state.CanApplySuggestions;
        BuilderValidateButton.IsEnabled = state.CanValidate;
        BuilderSaveButton.IsEnabled = state.CanSave;
        BuilderSaveAsButton.IsEnabled = state.CanSaveAs;
        BuilderBackToLibraryButton.IsEnabled = state.CanBackToLibrary;
    }

    private void BuilderDraftControl_Changed(object sender, TextChangedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderDeploymentProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderConfirmSaveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderApplySuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySuggestionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderValidateButton_Click(object sender, RoutedEventArgs e)
    {
        ValidateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderSaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderSaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuilderBackToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        BackToLibraryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyDraftChanged()
    {
        if (_isUpdatingDraft)
        {
            return;
        }

        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private string GetSelectedProfile()
    {
        if (BuilderDeploymentProfileComboBox.SelectedItem is ComboBoxItem item &&
            item.Content is string content)
        {
            return content;
        }

        return "Balanced";
    }

    private void SetSelectedProfile(string profile)
    {
        var normalized = string.IsNullOrWhiteSpace(profile) ? "Balanced" : profile.Trim();
        foreach (var item in BuilderDeploymentProfileComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content is string content &&
                string.Equals(content, normalized, StringComparison.OrdinalIgnoreCase))
            {
                BuilderDeploymentProfileComboBox.SelectedItem = item;
                return;
            }
        }

        BuilderDeploymentProfileComboBox.SelectedIndex = 1;
    }

    private static void SetTextIfChanged(TextBox textBox, string value)
    {
        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
        {
            textBox.Text = value;
        }
    }

    private static void SetTextIfChanged(TextBlock textBlock, string value)
    {
        if (!string.Equals(textBlock.Text, value, StringComparison.Ordinal))
        {
            textBlock.Text = value;
        }
    }
}

