using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
        BuilderDeploymentProfileComboBox.SelectionChanged += BuilderSelectionControl_Changed;
        BuilderConfirmSaveCheckBox.Checked += BuilderConfirmSaveCheckBox_Changed;
        BuilderConfirmSaveCheckBox.Unchecked += BuilderConfirmSaveCheckBox_Changed;
        BuilderAddNetworkButton.Click += BuilderAddNetworkButton_Click;
        BuilderAddCredentialSlotButton.Click += BuilderAddCredentialSlotButton_Click;
        BuilderAddForestButton.Click += BuilderAddForestButton_Click;
        BuilderAddDomainButton.Click += BuilderAddDomainButton_Click;
        BuilderAddVmButton.Click += BuilderAddVmButton_Click;
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
            BuilderNetworksPanel.Children.OfType<StackPanel>().Select(ReadNetwork).ToList(),
            BuilderCredentialSlotsPanel.Children.OfType<StackPanel>().Select(ReadCredentialSlot).ToList(),
            BuilderForestsPanel.Children.OfType<StackPanel>().Select(ReadForest).ToList(),
            BuilderDomainsPanel.Children.OfType<StackPanel>().Select(ReadDomain).ToList(),
            BuilderVmsPanel.Children.OfType<StackPanel>().Select(ReadVm).ToList(),
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
            RenderDraftRows(state.Draft);
            BuilderConfirmSaveCheckBox.IsChecked = state.Draft.IsSaveConfirmed;
            BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(state.Draft);
        }
        finally
        {
            _isUpdatingDraft = false;
        }
    }

    internal void UpdateActionState(TemplatesBuilderActionState state)
    {
        BuilderAddNetworkButton.IsEnabled = state.CanValidate;
        BuilderAddCredentialSlotButton.IsEnabled = state.CanValidate;
        BuilderAddForestButton.IsEnabled = state.CanValidate;
        BuilderAddDomainButton.IsEnabled = state.CanValidate;
        BuilderAddVmButton.IsEnabled = state.CanValidate;
        BuilderApplySuggestionsButton.IsEnabled = state.CanApplySuggestions;
        BuilderValidateButton.IsEnabled = state.CanValidate;
        BuilderSaveButton.IsEnabled = state.CanSave;
        BuilderSaveAsButton.IsEnabled = state.CanSaveAs;
        BuilderBackToLibraryButton.IsEnabled = state.CanBackToLibrary;
    }

    internal void UpdateConfirmationState(bool isSaveConfirmed)
    {
        _isUpdatingDraft = true;
        try
        {
            BuilderConfirmSaveCheckBox.IsChecked = isSaveConfirmed;
        }
        finally
        {
            _isUpdatingDraft = false;
        }
    }

    private void RenderDraftRows(TemplatesBuilderDraftSnapshot draft)
    {
        RenderRows(BuilderNetworksPanel, draft.LabNetworks, CreateNetworkRow);
        RenderRows(BuilderCredentialSlotsPanel, draft.CredentialSlots, CreateCredentialSlotRow);
        RenderRows(BuilderForestsPanel, draft.Forests, CreateForestRow);
        RenderRows(BuilderDomainsPanel, draft.Domains, CreateDomainRow);
        RenderVmRows(draft.Vms);
    }

    private static void RenderRows<T>(StackPanel panel, IReadOnlyList<T> items, Func<T, StackPanel> createRow)
    {
        panel.Children.Clear();
        foreach (var item in items)
        {
            panel.Children.Add(createRow(item));
        }
    }

    private void RenderVmRows(IReadOnlyList<TemplatesBuilderVmDraft> vms)
    {
        BuilderVmsPanel.Children.Clear();
        for (var i = 0; i < vms.Count; i++)
        {
            BuilderVmsPanel.Children.Add(CreateVmRow(vms[i], i));
        }
    }

    private StackPanel CreateNetworkRow(TemplatesBuilderLabNetworkDraft network)
    {
        var row = CreateRow();
        row.Children.Add(CreateRowTitle("Network"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("Network ID", "network.NetworkId", network.NetworkId),
            CreateTextBox("Name", "network.Name", network.Name),
            CreateTextBox("Switch", "network.SwitchName", network.SwitchName),
            CreateTextBox("Subnet", "network.Subnet", network.Subnet),
            CreateTextBox("Notes", "network.Notes", network.Notes)));
        return row;
    }

    private StackPanel CreateCredentialSlotRow(TemplatesBuilderCredentialSlotDraft slot)
    {
        var row = CreateRow();
        row.Children.Add(CreateRowTitle("Credential Slot Reference"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("Slot Key", "credential.SlotKey", slot.SlotKey),
            CreateTextBox("Label", "credential.Label", slot.Label),
            CreateTextBox("Scope", "credential.ScopeHint", slot.ScopeHint)));
        return row;
    }

    private StackPanel CreateForestRow(TemplatesBuilderForestDraft forest)
    {
        var row = CreateRow();
        row.Children.Add(CreateRowTitle("Forest"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("Forest ID", "forest.ForestId", forest.ForestId),
            CreateTextBox("Root Domain ID", "forest.RootDomainId", forest.RootDomainId)));
        return row;
    }

    private StackPanel CreateDomainRow(TemplatesBuilderDomainDraft domain)
    {
        var row = CreateRow();
        row.Children.Add(CreateRowTitle("Domain"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("Domain ID", "domain.DomainId", domain.DomainId),
            CreateTextBox("DNS Name", "domain.DnsName", domain.DnsName),
            CreateTextBox("NetBIOS", "domain.NetBiosName", domain.NetBiosName),
            CreateTextBox("Forest ID", "domain.ForestId", domain.ForestId),
            CreateComboBox("Relation", "domain.RelationKind", domain.RelationKind, nameof(V2DomainRelationKind.Root), nameof(V2DomainRelationKind.Child), nameof(V2DomainRelationKind.Tree)),
            CreateTextBox("Parent Domain ID", "domain.ParentDomainId", domain.ParentDomainId)));
        return row;
    }

    private StackPanel CreateVmRow(TemplatesBuilderVmDraft vm, int vmIndex)
    {
        var row = CreateRow();
        row.Children.Add(CreateRowTitle($"VM {vmIndex + 1}"));
        row.Children.Add(CreateSubhead("Basics"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("VM ID", "vm.VmId", vm.VmId),
            CreateTextBox("Name", "vm.Name", vm.Name),
            CreateTextBox("VHDX ID", "vm.VhdxId", vm.VhdxId)));
        row.Children.Add(CreateSubhead("Compute"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("Memory MB", "vm.MemoryMb", vm.MemoryMb.ToString()),
            CreateTextBox("CPU Count", "vm.CpuCount", vm.CpuCount.ToString())));
        row.Children.Add(CreateSubhead("Membership"));
        row.Children.Add(CreateFieldGrid(
            CreateComboBox("Membership", "vm.MembershipMode", vm.MembershipMode, V2MembershipModeCatalog.DomainMember, V2MembershipModeCatalog.Standalone),
            CreateTextBox("Domain ID", "vm.DomainId", vm.DomainId)));
        row.Children.Add(CreateSubhead("Roles"));
        row.Children.Add(CreateCheckBox("Active Directory Domain Controller", "vm.IsActiveDirectoryDomainController", vm.IsActiveDirectoryDomainController));
        row.Children.Add(CreateSubhead("Credentials"));
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("Local Bootstrap Slot", "vm.LocalBootstrap", vm.CredentialSlots.LocalBootstrap),
            CreateTextBox("Domain Admin Slot", "vm.DomainAdmin", vm.CredentialSlots.DomainAdmin),
            CreateTextBox("Domain Join Slot", "vm.DomainJoin", vm.CredentialSlots.DomainJoin),
            CreateTextBox("DSRM Slot", "vm.Dsrm", vm.CredentialSlots.Dsrm),
            CreateTextBox("Parent Domain Admin Slot", "vm.ParentDomainAdmin", vm.CredentialSlots.ParentDomainAdmin)));
        row.Children.Add(CreateSubhead("Networking"));
        var addNicButton = new Button { Content = "Add NIC", Tag = vmIndex };
        addNicButton.Click += BuilderAddNicButton_Click;
        row.Children.Add(addNicButton);
        var nicsPanel = new StackPanel { Spacing = 6, Tag = "vm.NicsPanel" };
        foreach (var nic in vm.Nics)
        {
            nicsPanel.Children.Add(CreateNicRow(nic));
        }

        row.Children.Add(nicsPanel);
        return row;
    }

    private StackPanel CreateNicRow(TemplatesBuilderNicDraft nic)
    {
        var row = CreateRow();
        row.Tag = "nic.Row";
        row.Children.Add(CreateFieldGrid(
            CreateTextBox("NIC ID", "nic.NicId", nic.NicId),
            CreateTextBox("Name", "nic.Name", nic.Name),
            CreateTextBox("Network ID", "nic.NetworkId", nic.NetworkId),
            CreateTextBox("Switch", "nic.SwitchName", nic.SwitchName),
            CreateTextBox("IP Address", "nic.IpAddress", nic.IpAddress),
            CreateTextBox("Prefix", "nic.PrefixLength", nic.PrefixLength?.ToString() ?? string.Empty),
            CreateTextBox("Gateway", "nic.DefaultGateway", nic.DefaultGateway),
            CreateTextBox("DNS Servers", "nic.DnsServers", string.Join(", ", nic.DnsServers))));
        return row;
    }

    private TemplatesBuilderLabNetworkDraft ReadNetwork(StackPanel row)
        => new(
            GetText(row, "network.NetworkId"),
            GetText(row, "network.Name"),
            GetText(row, "network.SwitchName"),
            GetText(row, "network.Subnet"),
            GetText(row, "network.Notes"));

    private TemplatesBuilderCredentialSlotDraft ReadCredentialSlot(StackPanel row)
        => new(
            GetText(row, "credential.SlotKey"),
            GetText(row, "credential.Label"),
            GetText(row, "credential.ScopeHint"));

    private TemplatesBuilderForestDraft ReadForest(StackPanel row)
        => new(GetText(row, "forest.ForestId"), GetText(row, "forest.RootDomainId"));

    private TemplatesBuilderDomainDraft ReadDomain(StackPanel row)
        => new(
            GetText(row, "domain.DomainId"),
            GetText(row, "domain.DnsName"),
            GetText(row, "domain.NetBiosName"),
            GetText(row, "domain.ForestId"),
            GetComboValue(row, "domain.RelationKind"),
            GetText(row, "domain.ParentDomainId"));

    private TemplatesBuilderVmDraft ReadVm(StackPanel row)
    {
        var nicsPanel = FindDescendants<StackPanel>(row).FirstOrDefault(panel => string.Equals(panel.Tag as string, "vm.NicsPanel", StringComparison.Ordinal));
        var nics = nicsPanel?.Children.OfType<StackPanel>().Select(ReadNic).ToList() ?? [];

        return new TemplatesBuilderVmDraft(
            GetText(row, "vm.VmId"),
            GetText(row, "vm.Name"),
            ParsePositiveInt(GetText(row, "vm.MemoryMb")),
            ParsePositiveInt(GetText(row, "vm.CpuCount")),
            GetText(row, "vm.VhdxId"),
            GetComboValue(row, "vm.MembershipMode"),
            GetText(row, "vm.DomainId"),
            GetCheckBoxValue(row, "vm.IsActiveDirectoryDomainController"),
            new TemplatesBuilderVmCredentialSlotDraft(
                GetText(row, "vm.LocalBootstrap"),
                GetText(row, "vm.DomainAdmin"),
                GetText(row, "vm.DomainJoin"),
                GetText(row, "vm.Dsrm"),
                GetText(row, "vm.ParentDomainAdmin")),
            nics);
    }

    private TemplatesBuilderNicDraft ReadNic(StackPanel row)
        => new(
            GetText(row, "nic.NicId"),
            GetText(row, "nic.Name"),
            GetText(row, "nic.NetworkId"),
            GetText(row, "nic.SwitchName"),
            GetText(row, "nic.IpAddress"),
            ParseNullableInt(GetText(row, "nic.PrefixLength")),
            GetText(row, "nic.DefaultGateway"),
            SplitList(GetText(row, "nic.DnsServers")));

    private void BuilderDraftControl_Changed(object sender, TextChangedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderSelectionControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderConfirmSaveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        NotifyDraftChanged();
    }

    private void BuilderAddNetworkButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        RenderAndNotify(draft with
        {
            LabNetworks = draft.LabNetworks.Append(new TemplatesBuilderLabNetworkDraft($"lab-network-{draft.LabNetworks.Count + 1}", "Network", string.Empty, string.Empty, string.Empty)).ToList(),
            IsSaveConfirmed = false
        });
    }

    private void BuilderAddCredentialSlotButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        RenderAndNotify(draft with
        {
            CredentialSlots = draft.CredentialSlots.Append(new TemplatesBuilderCredentialSlotDraft($"slot-{draft.CredentialSlots.Count + 1}", "Credential slot", "template reference")).ToList(),
            IsSaveConfirmed = false
        });
    }

    private void BuilderAddForestButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        RenderAndNotify(draft with
        {
            Forests = draft.Forests.Append(new TemplatesBuilderForestDraft($"forest-{draft.Forests.Count + 1}", string.Empty)).ToList(),
            IsSaveConfirmed = false
        });
    }

    private void BuilderAddDomainButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        var forestId = draft.Forests.FirstOrDefault().ForestId;
        RenderAndNotify(draft with
        {
            Domains = draft.Domains.Append(new TemplatesBuilderDomainDraft($"domain-{draft.Domains.Count + 1}", "example.local", "EXAMPLE", forestId, nameof(V2DomainRelationKind.Root), string.Empty)).ToList(),
            IsSaveConfirmed = false
        });
    }

    private void BuilderAddVmButton_Click(object sender, RoutedEventArgs e)
    {
        var draft = CaptureDraft();
        RenderAndNotify(draft with
        {
            Vms = draft.Vms.Append(new TemplatesBuilderVmDraft(
                $"vm-{draft.Vms.Count + 1}",
                "new-vm",
                4096,
                2,
                string.Empty,
                V2MembershipModeCatalog.Standalone,
                string.Empty,
                false,
                new TemplatesBuilderVmCredentialSlotDraft(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                [])).ToList(),
            IsSaveConfirmed = false
        });
    }

    private void BuilderAddNicButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int vmIndex })
        {
            return;
        }

        var draft = CaptureDraft();
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count)
        {
            return;
        }

        var vms = draft.Vms.ToList();
        var vm = vms[vmIndex];
        var nics = vm.Nics.ToList();
        nics.Add(new TemplatesBuilderNicDraft($"nic-{nics.Count + 1}", "Lab", draft.LabNetworks.FirstOrDefault().NetworkId, string.Empty, string.Empty, null, string.Empty, []));
        vms[vmIndex] = vm with { Nics = nics };
        RenderAndNotify(draft with { Vms = vms, IsSaveConfirmed = false });
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

    private void RenderAndNotify(TemplatesBuilderDraftSnapshot draft)
    {
        _isUpdatingDraft = true;
        try
        {
            RenderDraftRows(draft);
            BuilderConfirmSaveCheckBox.IsChecked = draft.IsSaveConfirmed;
            BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(draft);
        }
        finally
        {
            _isUpdatingDraft = false;
        }

        DraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyDraftChanged()
    {
        if (_isUpdatingDraft)
        {
            return;
        }

        BuilderReviewSummaryTextBlock.Text = BuildReviewSummary(CaptureDraft());
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

    private TextBox CreateTextBox(string header, string tag, string value)
    {
        var textBox = new TextBox
        {
            Header = header,
            Tag = tag,
            Text = value,
            MinWidth = 150
        };
        textBox.TextChanged += BuilderDraftControl_Changed;
        return textBox;
    }

    private ComboBox CreateComboBox(string header, string tag, string value, params string[] options)
    {
        var comboBox = new ComboBox
        {
            Header = header,
            Tag = tag,
            MinWidth = 150
        };

        foreach (var option in options)
        {
            comboBox.Items.Add(new ComboBoxItem { Content = option });
        }

        SetComboBoxValue(comboBox, value);
        comboBox.SelectionChanged += BuilderSelectionControl_Changed;
        return comboBox;
    }

    private CheckBox CreateCheckBox(string content, string tag, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Content = content,
            Tag = tag,
            IsChecked = isChecked
        };
        checkBox.Checked += BuilderConfirmSaveCheckBox_Changed;
        checkBox.Unchecked += BuilderConfirmSaveCheckBox_Changed;
        return checkBox;
    }

    private static StackPanel CreateRow()
        => new()
        {
            Spacing = 6,
            Padding = new Thickness(8),
            Background = Application.Current.Resources["ShellBackgroundBrush"] as Microsoft.UI.Xaml.Media.Brush
        };

    private static TextBlock CreateRowTitle(string text)
        => new()
        {
            Foreground = Application.Current.Resources["ShellTextPrimaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = text
        };

    private static TextBlock CreateSubhead(string text)
        => new()
        {
            Foreground = Application.Current.Resources["ShellTextSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = text
        };

    private static Grid CreateFieldGrid(params FrameworkElement[] fields)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        for (var i = 0; i < fields.Length; i++)
        {
            var row = i / 2;
            if (grid.RowDefinitions.Count <= row)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            Grid.SetRow(fields[i], row);
            Grid.SetColumn(fields[i], i % 2);
            grid.Children.Add(fields[i]);
        }

        return grid;
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

    private static string GetText(DependencyObject root, string tag)
        => FindDescendants<TextBox>(root).FirstOrDefault(textBox => string.Equals(textBox.Tag as string, tag, StringComparison.Ordinal))?.Text ?? string.Empty;

    private static string GetComboValue(DependencyObject root, string tag)
    {
        var comboBox = FindDescendants<ComboBox>(root).FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
        if (comboBox?.SelectedItem is ComboBoxItem { Content: string content })
        {
            return content;
        }

        return string.Empty;
    }

    private static bool GetCheckBoxValue(DependencyObject root, string tag)
        => FindDescendants<CheckBox>(root).FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal))?.IsChecked == true;

    private static void SetComboBoxValue(ComboBox comboBox, string value)
    {
        var normalized = value.Trim();
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Content is string content &&
                string.Equals(content, normalized, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static int ParsePositiveInt(string value)
        => int.TryParse(value, out var parsed) ? parsed : 0;

    private static int? ParseNullableInt(string value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static IReadOnlyList<string> SplitList(string value)
        => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

    private static string BuildReviewSummary(TemplatesBuilderDraftSnapshot draft)
    {
        var dcCount = draft.Vms.Count(vm => vm.IsActiveDirectoryDomainController);
        var nicCount = draft.Vms.Sum(vm => vm.Nics.Count);
        return $"{draft.LabNetworks.Count} networks, {draft.CredentialSlots.Count} credential slot references, {draft.Forests.Count} forests, {draft.Domains.Count} domains, {draft.Vms.Count} VMs, {dcCount} Active Directory Domain Controller role assignments, {nicCount} NICs.";
    }
}
