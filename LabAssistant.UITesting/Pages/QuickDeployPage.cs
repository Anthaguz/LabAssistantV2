using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Pages;

/// <summary>
/// Drives the Deploy &gt; Quick Deploy tab: selects the tab, fills the single-VM
/// editor (name, memory, cpu, base disk, switch), reads readiness, and starts a
/// deploy. Selectors come from the x:Names promoted to runtime AutomationIds on
/// the Quick Deploy view.
/// </summary>
public sealed class QuickDeployPage
{
    private readonly AppHost _host;
    private readonly ShellNav _nav;

    public QuickDeployPage(AppHost host)
    {
        _host = host;
        _nav = new ShellNav(host);
    }

    private AutomationElement Window => _host.MainWindow;

    /// <summary>Navigates to Deploy and activates the Quick Deploy sub-tab.</summary>
    public void Open()
    {
        _nav.NavigateTo("Deploy");
        var tab = Window.WaitForAutomationId("QuickDeployTabViewItem", TimeSpan.FromSeconds(10));
        tab.Activate();
        Thread.Sleep(600);
    }

    /// <summary>
    /// Selects the first VM entry row so the editor fields become enabled. The
    /// VM Properties editor is disabled until an entry is selected.
    /// </summary>
    public void SelectFirstVm()
    {
        var list = Window.WaitForAutomationId("QuickDeployVmEntriesListView", TimeSpan.FromSeconds(10)).AsListBox();
        var first = Retry.WhileNull(
            () => list.Items.FirstOrDefault(),
            TimeSpan.FromSeconds(5)).Result
            ?? throw new InvalidOperationException("Quick Deploy has no VM entry rows to select.");
        first.Select();
        Thread.Sleep(400);
    }

    /// <summary>Types the VM name into the editor's Name field.</summary>
    public void SetVmName(string name)
        => Window.WaitForAutomationId("QuickDeployVmNameTextBox").SetValue(name);

    /// <summary>Returns the combined text of the first VM entry row (entry-derived).</summary>
    public string FirstRowText()
    {
        var list = Window.WaitForAutomationId("QuickDeployVmEntriesListView", TimeSpan.FromSeconds(10)).AsListBox();
        var item = list.Items.FirstOrDefault();
        if (item is null)
        {
            return string.Empty;
        }

        var parts = new List<string> { item.SafeName() ?? string.Empty };
        parts.AddRange(item
            .FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Select(t => t.SafeName() ?? string.Empty));
        return string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// Drives the single-VM editor to a deploy-ready state and returns true once
    /// Start Deploy is enabled. The Quick Deploy editor is a debounced draft with no
    /// visible Save button: commits are dropped when they fire during an in-flight
    /// readiness evaluation, and row rebuilds re-sync the draft from the (not-yet-
    /// committed) entry, wiping whichever field was set earliest. Fighting one field
    /// at a time is unwinnable, so this converges: each pass reads the ENTRY-derived
    /// row text and re-applies only the fields the entry is still missing, letting a
    /// full commit+resync cycle settle between passes, until readiness clears. The
    /// entry name is what the deploy assigns to the Hyper-V VM, so a tagged name in
    /// the row is also the guarantee the created VM is sweepable.
    /// </summary>
    public bool ConfigureSingleVmAndWaitReady(
        string vmName, int memoryMb, int cpu, string baseDiskSubstring, string switchName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            EnsureFirstVmSelected();
            var row = FirstRowText();

            if (!row.Contains(vmName, StringComparison.OrdinalIgnoreCase))
            {
                SetVmName(vmName);
            }

            if (!row.Contains($"{memoryMb} MB", StringComparison.OrdinalIgnoreCase))
            {
                SetMemoryMb(memoryMb);
            }

            if (!row.Contains($"{cpu} vCPU", StringComparison.OrdinalIgnoreCase))
            {
                SetCpu(cpu);
            }

            if (row.Contains("No base disk", StringComparison.OrdinalIgnoreCase) ||
                !BaseDiskSelectionContains(baseDiskSubstring))
            {
                TrySelectBaseDisk(baseDiskSubstring);
            }

            if (!HasSwitchRow())
            {
                TryAddSwitch(switchName);
            }

            Thread.Sleep(3000);

            if (CanStartDeploy())
            {
                return true;
            }
        }

        return CanStartDeploy();
    }

    private void EnsureFirstVmSelected()
    {
        var list = Window.WaitForAutomationId("QuickDeployVmEntriesListView", TimeSpan.FromSeconds(10)).AsListBox();
        var first = list.Items.FirstOrDefault();
        if (first is not null && first.Patterns.SelectionItem.IsSupported &&
            !first.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
        {
            first.Select();
            Thread.Sleep(200);
        }
    }

    private bool BaseDiskSelectionContains(string substring)
    {
        var combo = Window.ByAutomationId("QuickDeployBaseDiskComboBox")?.AsComboBox();
        var selected = combo?.SelectedItem?.SafeName() ?? combo?.SafeName() ?? string.Empty;
        return selected.Contains(substring, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the editor currently shows at least one switch row.</summary>
    public bool HasSwitchRow()
        => Window.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Host switch"))) is not null;

    private void TrySelectBaseDisk(string substring)
    {
        try { SelectBaseDiskContaining(substring); }
        catch { /* transient during rebuild; next pass retries */ }
    }

    private void TryAddSwitch(string switchName)
    {
        try { AddSwitch(switchName); }
        catch { /* Add Switch disabled during eval; next pass retries */ }
    }

    /// <summary>Types memory (MB) into the editor.</summary>
    public void SetMemoryMb(int memoryMb)
        => Window.WaitForAutomationId("QuickDeployVmMemoryTextBox").SetValue(memoryMb.ToString());

    /// <summary>Types the vCPU count into the editor.</summary>
    public void SetCpu(int cpu)
        => Window.WaitForAutomationId("QuickDeployVmCpuTextBox").SetValue(cpu.ToString());

    /// <summary>Selects the base disk whose display label contains the given text.</summary>
    public void SelectBaseDiskContaining(string labelSubstring)
    {
        var combo = Window.WaitForAutomationId("QuickDeployBaseDiskComboBox").AsComboBox();
        combo.Expand();
        Thread.Sleep(300);

        var match = Retry.WhileNull(
            () => combo.Items.FirstOrDefault(i =>
                i.Name.Contains(labelSubstring, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(5)).Result
            ?? throw new InvalidOperationException(
                $"Base disk option containing '{labelSubstring}' not found. Options: " +
                string.Join(" | ", combo.Items.Select(i => i.Name)));

        match.Select();
        combo.Collapse();
        Thread.Sleep(300);
    }

    /// <summary>
    /// Waits until a VM entry row's text contains the given name, confirming the
    /// debounced editor draft committed to the underlying entry. This is the name
    /// the deploy will assign to the Hyper-V VM, so a harness-tagged name here is
    /// what makes the created VM sweepable. Returns false if it never commits.
    /// </summary>
    public bool WaitForVmRowNamed(string name, TimeSpan timeout)
    {
        var list = Window.WaitForAutomationId("QuickDeployVmEntriesListView", TimeSpan.FromSeconds(10)).AsListBox();
        return Retry.WhileFalse(() => RowExists(list, name), timeout).Success;
    }

    private static bool RowExists(FlaUI.Core.AutomationElements.ListBox list, string name)
    {
        foreach (var item in list.Items)
        {
            if ((item.SafeName() ?? string.Empty).Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Fall back to the item's descendant text, in case the ListViewItem's
            // composite automation Name does not surface the bound DisplayName.
            var texts = item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            if (texts.Any(t => (t.SafeName() ?? string.Empty).Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds a switch row and selects the given host switch by name.</summary>
    public void AddSwitch(string switchName)
    {
        var addButton = Window.WaitForAutomationId("QuickDeployAddSwitchButton");
        // Add Switch is disabled while a readiness evaluation is in flight; wait it out.
        Retry.WhileFalse(() => addButton.IsEnabled, TimeSpan.FromSeconds(20));
        addButton.Activate();
        Thread.Sleep(400);

        var combo = Retry.WhileNull(
            () => Window.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.ComboBox).And(cf.ByName("Host switch"))),
            TimeSpan.FromSeconds(5)).Result
            ?? throw new InvalidOperationException("Host switch combo did not appear after Add Switch.");

        var box = combo.AsComboBox();
        box.Expand();
        Thread.Sleep(300);

        var match = Retry.WhileNull(
            () => box.Items.FirstOrDefault(i =>
                string.Equals(i.Name, switchName, StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(5)).Result
            ?? throw new InvalidOperationException(
                $"Host switch '{switchName}' not found. Options: " +
                string.Join(" | ", box.Items.Select(i => i.Name)));

        match.Select();
        box.Collapse();
        Thread.Sleep(300);
    }

    /// <summary>True when the Start Deploy button is enabled (readiness cleared).</summary>
    public bool CanStartDeploy()
        => Window.ByAutomationId("QuickDeployStartButton")?.IsEnabled ?? false;

    /// <summary>Waits until Start Deploy becomes enabled, or times out.</summary>
    public bool WaitForReady(TimeSpan timeout)
        => Retry.WhileFalse(CanStartDeploy, timeout).Success;

    /// <summary>The current lifecycle/readiness text shown in the summary panel.</summary>
    public string ReadinessText()
    {
        var panel = Window.ByAutomationId("DeployQuickDeployReadinessSummaryPanel");
        return panel?.SafeName() ?? string.Empty;
    }

    /// <summary>Clicks Start Deploy. Throws if it is disabled.</summary>
    public void StartDeploy()
    {
        var start = Window.WaitForAutomationId("QuickDeployStartButton");
        if (!start.IsEnabled)
        {
            throw new InvalidOperationException("Start Deploy is disabled; readiness has not cleared.");
        }

        start.Activate();
    }
}
