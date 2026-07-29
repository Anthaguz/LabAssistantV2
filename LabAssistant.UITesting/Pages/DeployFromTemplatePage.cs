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

    private const int MaxFillAttempts = 4;
    private static readonly TimeSpan PersistBudget = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PersistPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FocusBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FocusPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Credential-slot resolution. The minimal standalone template needs none, but if a plan
    /// surfaces unresolved slots this fills each and drives the app's real Save so the store gains
    /// the record and the plan becomes startable. Each save re-plans, shrinking the unresolved list,
    /// so the loop re-reads the first row until none remain. Returns how many slots it filled.
    ///
    /// The fill is persistence-verified, not fire-and-forget. The WinUI PasswordBox has no Value
    /// pattern, so the password is entered as real keystrokes after focusing the box; a focus miss
    /// would type an empty password, no-op the Save, and leave the plan wedged while the loop spun to
    /// a false success. So each attempt asserts keyboard focus landed before typing, then verifies the
    /// slot actually left the unresolved list (the app re-planned off a persisted record) before
    /// counting it - retrying focus+type+Save on a bounded budget. If a slot still will not persist,
    /// it throws <see cref="CredentialFillException"/> rather than mask the failure: with focus and a
    /// non-empty password proven, a non-persisting Save is a genuine product Save/Upsert bug, and a
    /// focus that never lands is a harness fill-miss - the message says which.
    /// </summary>
    public int ResolveCredentialSlots(string username, string password, TimeSpan timeout)
    {
        var store = new CredentialSlotSeeder(new AppDataLocations());
        int resolved = 0;
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            var list = Window.ByAutomationId("DeployV2CredentialSlotsListView")?.AsListBox();
            int rowsBefore = list?.Items.Length ?? 0;
            var first = list?.Items.FirstOrDefault();
            if (first is null)
            {
                break; // no unresolved slots remain
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new CredentialFillException(
                    $"Credential slots did not all resolve within {timeout.TotalSeconds:0}s; {rowsBefore} slot(s) still unresolved.");
            }

            int storeBefore = store.Count();
            bool saveEverFired = false;

            var outcome = CredentialSlotFill.FillSlotWithRetry(
                attemptFocus: () => SelectRowAndFocusPassword(username),
                typePassword: () => Keyboard.Type(password),
                save: () => saveEverFired |= TrySaveCredentialSlot(),
                hasPersisted: () => HasSlotPersisted(store, storeBefore, rowsBefore),
                maxAttempts: MaxFillAttempts,
                persistBudget: PersistBudget,
                pollInterval: PersistPollInterval,
                utcNow: () => DateTime.UtcNow,
                sleep: Thread.Sleep);

            if (!outcome.Persisted)
            {
                throw new CredentialFillException(
                    DescribeFillFailure(outcome, saveEverFired, reachedStore: store.Count() > storeBefore));
            }

            resolved++;
        }

        return resolved;
    }

    /// <summary>
    /// True once the current Save has demonstrably taken: the app's store gained a record (ground-truth
    /// Upsert) or the slot left the unresolved list (the app re-planned off a persisted credential). The
    /// row-count signal is gated on the list actually being found, so a transient UIA-tree null cannot be
    /// mistaken for a resolved slot - the very false-success class this hardening exists to prevent.
    /// </summary>
    private bool HasSlotPersisted(CredentialSlotSeeder store, int storeCountBefore, int rowsBefore)
    {
        if (store.Count() > storeCountBefore)
        {
            return true;
        }

        var list = Window.ByAutomationId("DeployV2CredentialSlotsListView")?.AsListBox();
        return list is not null && list.Items.Length < rowsBefore;
    }

    /// <summary>
    /// Builds an honest, self-attributing failure message that never silently masks a stuck fill. It
    /// separates the two clear harness fill-misses (focus never landed; or the Save button never enabled,
    /// so nothing was typed in) and the one clear product bug (Save reached the store but the plan still
    /// lists the slot). Crucially, the focus-landed + Save-fired + nothing-persisted case is left OPEN
    /// between an empty-password harness type-miss and a product Save/Upsert failure - the two cannot be
    /// told apart from the harness side alone (a landed focus does not prove the keystrokes registered),
    /// so the message says "password-empty suspected" and points at the product-side Save telemetry as
    /// the deciding cross-check rather than prematurely blaming the product.
    /// </summary>
    internal static string DescribeFillFailure(CredentialFillOutcome outcome, bool saveEverFired, bool reachedStore)
    {
        if (!outcome.FocusEverLanded)
        {
            return $"keyboard focus never landed on DeployV2CredentialSlotPasswordBox after {outcome.Attempts} " +
                   "attempt(s), so the password could not be typed into the box (harness fill-miss)";
        }

        if (!saveEverFired)
        {
            return $"the credential Save button never became enabled across {outcome.Attempts} attempt(s) even " +
                   "though the password box was focused - the typed password did not populate the box (harness fill-miss)";
        }

        if (reachedStore)
        {
            return $"credential Save reached the store but the slot never left the unresolved list after " +
                   $"{outcome.Attempts} attempt(s) - the plan still reports it unresolved (product re-plan/resolver issue)";
        }

        return $"credential Save did not persist after {outcome.Attempts} attempt(s) despite the password box " +
               "being focused and Save firing (password-empty suspected) - either an empty-password harness " +
               "type-miss or a product Save/Upsert failure; cross-check the product Save telemetry for whether " +
               "the password was non-empty at Upsert to decide which";
    }

    /// <summary>
    /// Re-reads the current first unresolved slot row, selects it, sets the username, and focuses the
    /// password box, returning true only once keyboard focus is confirmed on that box. The row is
    /// re-queried on every attempt (never a stale handle captured before a Save re-rendered the list),
    /// so a retry always acts on the live first row. Re-asserts focus while polling so a transient miss
    /// does not defeat the attempt; a persistent miss returns false so the caller retries rather than
    /// type into the void.
    /// </summary>
    private bool SelectRowAndFocusPassword(string username)
    {
        var row = Window.ByAutomationId("DeployV2CredentialSlotsListView")?.AsListBox()?.Items.FirstOrDefault();
        if (row is null)
        {
            return false;
        }

        row.Select();
        Thread.Sleep(200);
        Window.ByAutomationId("DeployV2CredentialSlotUsernameTextBox")?.SetValue(username);

        var pass = Window.ByAutomationId("DeployV2CredentialSlotPasswordBox");
        if (pass is null)
        {
            return false;
        }

        var focusDeadline = DateTime.UtcNow + FocusBudget;
        while (true)
        {
            pass.Focus();
            if (HasKeyboardFocus(pass))
            {
                return true;
            }

            if (DateTime.UtcNow >= focusDeadline)
            {
                return false;
            }

            Thread.Sleep(FocusPollInterval);
        }
    }

    /// <summary>
    /// Activates the credential Save button, returning true only when it was actually clickable (present
    /// and enabled) so the caller can tell a fired Save from one that never happened because the button
    /// stayed disabled. A no-op returning false when the button is absent or disabled.
    /// </summary>
    private bool TrySaveCredentialSlot()
    {
        var save = Window.ByAutomationId("DeployV2CredentialSlotSaveButton");
        if (save is not null && save.IsEnabled)
        {
            save.Activate();
            return true;
        }

        return false;
    }

    /// <summary>Reads an element's keyboard-focus state safely (false when the property is unavailable).</summary>
    private static bool HasKeyboardFocus(AutomationElement element)
    {
        try
        {
            return element.Properties.HasKeyboardFocus.ValueOrDefault;
        }
        catch
        {
            return false;
        }
    }
}
