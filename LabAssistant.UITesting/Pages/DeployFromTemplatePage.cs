using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Pages;

/// <summary>
/// Drives the Deploy &gt; From Template tab: reloads the template library, selects a
/// saved template, evaluates its V2 plan, optionally resolves credential slots, and
/// starts the deploy. Unlike Quick Deploy there is no per-field editor - the VM
/// name, memory, cpu, base disk, and switch all come from the selected template - so
/// the page object only needs to select, review, and start. Selectors are the
/// x:Names promoted to runtime AutomationIds on the From Template view.
/// </summary>
public sealed class DeployFromTemplatePage
{
    private readonly AppHost _host;
    private readonly ShellNav _nav;

    public DeployFromTemplatePage(AppHost host)
    {
        _host = host;
        _nav = new ShellNav(host);
    }

    private AutomationElement Window => _host.MainWindow;

    /// <summary>Navigates to Deploy and activates the From Template sub-tab.</summary>
    public void Open()
    {
        _nav.NavigateTo("Deploy");
        var tab = Window.WaitForAutomationId("FromTemplateTabViewItem", TimeSpan.FromSeconds(10));
        tab.Activate();
        Thread.Sleep(600);
    }

    /// <summary>Forces a library refresh so a just-seeded template file is picked up.</summary>
    public void ReloadLibrary()
    {
        var reload = Window.ByAutomationId("DeployTemplateReloadButton");
        if (reload is not null && reload.IsEnabled)
        {
            reload.Activate();
            Thread.Sleep(1500);
        }
    }

    /// <summary>
    /// Selects the template with the given library display name, reloading and
    /// retrying until it appears or the timeout elapses. Selecting a template raises
    /// the view's selection-changed path, which auto-evaluates the V2 plan.
    /// </summary>
    public bool SelectTemplateByName(string templateName, TimeSpan timeout)
    {
        var combo = Window.WaitForAutomationId("DeployTemplateSelectorComboBox", TimeSpan.FromSeconds(10)).AsComboBox();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            combo.Expand();
            Thread.Sleep(300);
            var match = combo.Items.FirstOrDefault(i =>
                string.Equals(i.Name, templateName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                match.Select();
                combo.Collapse();
                Thread.Sleep(600);
                return true;
            }

            combo.Collapse();
            // Not in the list yet: refresh the library from disk and retry.
            ReloadLibrary();
        }

        return false;
    }

    /// <summary>Triggers a V2 plan evaluation via the Evaluate/Refresh button (best-effort).</summary>
    public void EvaluatePlan()
    {
        var evaluate = Window.ByAutomationId("DeployTemplateEvaluateButton");
        if (evaluate is null)
        {
            return;
        }

        Retry.WhileFalse(() => evaluate.IsEnabled, TimeSpan.FromSeconds(20));
        if (evaluate.IsEnabled)
        {
            evaluate.Activate();
            Thread.Sleep(1500);
        }
    }

    /// <summary>True when Start Deploy is enabled (the V2 plan is startable).</summary>
    public bool CanStartDeploy()
        => Window.ByAutomationId("DeployTemplateStartButton")?.IsEnabled ?? false;

    /// <summary>Waits until Start Deploy becomes enabled, or times out.</summary>
    public bool WaitForStartEnabled(TimeSpan timeout)
        => Retry.WhileFalse(CanStartDeploy, timeout).Success;

    /// <summary>Clicks Start Deploy. Throws if it is disabled.</summary>
    public void StartDeploy()
    {
        var start = Window.WaitForAutomationId("DeployTemplateStartButton");
        if (!start.IsEnabled)
        {
            throw new InvalidOperationException("Start Deploy is disabled; the V2 plan is not startable.");
        }

        start.Activate();
    }

    /// <summary>The config-surface status line, which surfaces why a deploy is blocked.</summary>
    public string ActionStatusText()
        => Window.ByAutomationId("DeployTemplateActionStatusText")?.SafeName() ?? string.Empty;

    /// <summary>The current lifecycle state, read from whichever surface is visible.</summary>
    public string LifecycleState()
        => Window.ByAutomationId("DeployTemplateResultLifecycleText")?.SafeName()
            ?? Window.ByAutomationId("DeployTemplateProgressLifecycleText")?.SafeName()
            ?? string.Empty;

    /// <summary>
    /// Defensive credential-slot resolution. The minimal standalone template needs
    /// none, but if a plan surfaces unresolved slots this fills each with a throwaway
    /// credential - safe because guest configuration never runs on the non-bootable
    /// seeded base disk - so the harness can still reach a startable plan. Each save
    /// re-plans, shrinking the unresolved list, so the loop re-reads the first row
    /// until none remain. Returns how many slots it filled.
    /// </summary>
    public int ResolveCredentialSlots(string username, string password, TimeSpan timeout)
    {
        int resolved = 0;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var list = Window.ByAutomationId("DeployV2CredentialSlotsListView")?.AsListBox();
            var first = list?.Items.FirstOrDefault();
            if (first is null)
            {
                break;
            }

            try
            {
                first.Select();
                Thread.Sleep(300);
                Window.ByAutomationId("DeployV2CredentialSlotUsernameTextBox")?.SetValue(username);

                var pass = Window.ByAutomationId("DeployV2CredentialSlotPasswordBox");
                if (pass is not null)
                {
                    pass.Focus();
                    Keyboard.Type(password);
                }

                var save = Window.ByAutomationId("DeployV2CredentialSlotSaveButton");
                if (save is null)
                {
                    break;
                }

                save.Activate();
                resolved++;
                Thread.Sleep(2000); // save re-plans; loop re-reads remaining slots
            }
            catch
            {
                // Best-effort: a slot that cannot be driven should not crash the run; the
                // scenario reports the still-blocked plan as a finding instead.
                break;
            }
        }

        return resolved;
    }
}
