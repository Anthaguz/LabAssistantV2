using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Templates Library subview. Binds directly to <see cref="TemplatesLibraryViewModel"/> via
/// <c>x:Bind</c>; the inventory list, search inputs, selection detail, and commands are all bound with
/// no imperative view-state marshalling. Resolves its own transient view model from DI; the hosting
/// <c>TemplatesPage</c> owns the view-model lifecycle (initialize/cleanup) so switching between the
/// Templates tabs does not tear the view model down. Cross-subview navigation and dialogs are provided
/// by the hosting page via <see cref="ITemplatesLibraryHost"/>.
/// </summary>
public sealed partial class TemplatesLibraryView : UserControl
{
    public TemplatesLibraryViewModel ViewModel { get; }

    public TemplatesLibraryView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesLibraryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    // Enter in the search box applies the current filter, matching the Apply button. The bound
    // SearchText is synced from the box first because the two-way binding otherwise commits on focus
    // loss, which Enter does not trigger.
    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (sender is TextBox searchBox)
        {
            ViewModel.SearchText = searchBox.Text;
        }

        // Gate on the same predicate as the Apply button (CanApplySearch = not already loading) so
        // Enter cannot start an overlapping reload while another load is in flight.
        if (ViewModel.CanApplySearch && ViewModel.ApplySearchCommand.CanExecute(null))
        {
            ViewModel.ApplySearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}
