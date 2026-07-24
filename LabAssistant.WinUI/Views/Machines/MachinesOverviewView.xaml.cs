using System.Linq;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Machines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Machines;

public sealed partial class MachinesOverviewView : UserControl
{
    // Guards the two-way sync between the ListView selection and the ViewModel so a
    // programmatic reapply (after a refresh) does not echo back as a user selection change.
    private bool _suppressSelectionSync;

    public MachinesViewModel ViewModel { get; }

    public MachinesOverviewView()
    {
        ViewModel = App.Services.GetRequiredService<MachinesViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.SelectionReapplyRequested += OnSelectionReapplyRequested;
        Loaded += (_, _) => ViewLifecycle.Run(() => ViewModel.InitializeAsync(), "MachinesOverviewView.Initialize");
        Unloaded += (_, _) =>
        {
            ViewModel.SelectionReapplyRequested -= OnSelectionReapplyRequested;
            ViewLifecycle.Run(() => ViewModel.CleanupAsync(), "MachinesOverviewView.Cleanup");
        };
    }

    // WinUI ListView.SelectedItems is not bindable, so the view is the only place that can
    // observe the multi-selection. All decision logic lives in the ViewModel; this handler
    // just forwards the current selection so it stays unit-testable without a live ListView.
    private void OnMachinesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        var selection = MachinesListView.SelectedItems
            .OfType<MachineListItem>()
            .ToList();
        ViewModel.UpdateSelection(selection);
    }

    // Reapply the ViewModel's authoritative selection to the ListView after an inventory
    // refresh remaps rows by key. Suppressed so it does not re-enter UpdateSelection.
    private void OnSelectionReapplyRequested()
    {
        _suppressSelectionSync = true;
        try
        {
            MachinesListView.SelectedItems.Clear();
            foreach (var item in ViewModel.SelectedMachines)
            {
                MachinesListView.SelectedItems.Add(item);
            }
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }
}
