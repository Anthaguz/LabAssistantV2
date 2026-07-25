using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// Audits how addressable the UI is for automation. For each capability it walks
/// the live UI Automation tree under the shell content host and records every
/// authored, interactive control that lacks a stable AutomationId - the seam a
/// harness needs to drive a control reliably. It creates no Hyper-V resources
/// (<see cref="ScenarioRequirements.None"/>), so it is a cheap, always-safe pass
/// that keeps the whole-UI harness honest as new screens are built.
///
/// Findings are advisory: missing ids are recorded as <see cref="FindingSeverity.Warning"/>
/// and a per-capability coverage number as <see cref="FindingSeverity.Info"/>, so
/// the nightly run surfaces gaps without failing on them. The convention this
/// enforces is documented in docs/07-testing/ui-automation-ids.md.
/// </summary>
public sealed class AutomationIdCoverageScenario : IScenario
{
    public string Name => "automation-id-coverage";

    public string Capability => "Shell";

    public ScenarioRequirements Requirements => ScenarioRequirements.None;

    /// <summary>
    /// Control types the AutomationId convention targets: authored, individually
    /// operable controls a harness clicks or types into. Deliberately excludes
    /// container/navigation types (Tab, List, Tree, Group) and their data items,
    /// which are dynamic collections rather than fixed chrome, so the audit stays
    /// focused on stable controls that should carry a stable id.
    /// </summary>
    private static readonly HashSet<ControlType> TargetTypes = new()
    {
        ControlType.Button,
        ControlType.CheckBox,
        ControlType.RadioButton,
        ControlType.ComboBox,
        ControlType.Edit,
        ControlType.Slider,
        ControlType.Hyperlink,
        ControlType.SplitButton,
        ControlType.Spinner,
    };

    public void Run(ScenarioContext context)
    {
        var nav = new ShellNav(context.Host);

        foreach (var capability in ShellNav.Capabilities)
        {
            if (context.Host.Application.HasExited)
            {
                context.Recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = capability,
                    Severity = FindingSeverity.Crash,
                    Title = "App exited during AutomationId audit",
                    Detail = $"The app process was gone before auditing '{capability}'.",
                });
                return;
            }

            try
            {
                nav.NavigateTo(capability);

                // Let the capability's content render before we snapshot its tree.
                Thread.Sleep(600);
                AuditCapability(context, capability);
            }
            catch (TimeoutException ex)
            {
                context.Recorder.RecordFailure(
                    context.Host, Name, capability, FindingSeverity.Error,
                    $"Could not reach {capability} to audit it",
                    $"Navigation to '{capability}' timed out, so its controls could not be audited.",
                    ex);
            }
            catch (Exception ex)
            {
                // A stale/unavailable UIA element (COMException, ElementNotAvailable)
                // while walking one capability must degrade only that capability, not
                // abort the whole audit. Record it and move on to the next screen.
                context.Recorder.RecordFailure(
                    context.Host, Name, capability, FindingSeverity.Error,
                    $"Audit of {capability} failed: {ex.GetType().Name}",
                    $"An error occurred while auditing '{capability}'; the remaining capabilities were still audited.",
                    ex);
            }
        }
    }

    /// <summary>
    /// Audits a single capability. If the capability content hosts tabs, each tab
    /// is activated and audited so tab-hidden controls are covered too; otherwise
    /// the default-rendered content is audited once. Results are de-duplicated per
    /// capability by on-screen rectangle so a control that persists across tabs is
    /// only counted once, while distinct controls are never merged.
    /// </summary>
    private void AuditCapability(ScenarioContext context, string capability)
    {
        var root = SafeFindCapabilityRoot(context.Host)
            ?? context.Host.MainWindow;

        var missing = new List<MissingControl>();
        int total = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AuditCurrentView()
        {
            Walk(root, root, insideInteractive: false, control =>
            {
                // Dedupe on the control's on-screen bounding rectangle (plus type):
                // two distinct controls occupy different rectangles, so this never
                // collapses separate gaps (the failure mode of a name-based key when
                // several unlabeled controls share an empty Name), while a persistent
                // control audited again after a tab switch keeps the same rectangle
                // and is counted once. Controls with no reliable rectangle fall back
                // to a unique per-instance key so they are never merged.
                if (!seen.Add(control.DedupKey))
                {
                    return;
                }

                total++;
                if (string.IsNullOrEmpty(control.AutomationId))
                {
                    missing.Add(control);
                }
            });
        }

        var tabs = FindContentTabs(root);
        if (tabs.Count == 0)
        {
            AuditCurrentView();
        }
        else
        {
            foreach (var tab in tabs)
            {
                try
                {
                    tab.Activate();
                    Thread.Sleep(500);
                }
                catch
                {
                    // A tab that will not activate is still worth auditing in its
                    // current state; skip the switch and audit what is rendered.
                }

                AuditCurrentView();
            }
        }

        RecordResults(context, capability, total, missing);
    }

    /// <summary>
    /// Finds direct tab headers hosted within the capability content so each tab's
    /// controls can be audited. Returns an empty list when the capability is not
    /// tabbed.
    /// </summary>
    private static List<AutomationElement> FindContentTabs(AutomationElement root)
    {
        try
        {
            return root
                .FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                .Where(t => !SafeOffscreen(t))
                .ToList();
        }
        catch
        {
            return new List<AutomationElement>();
        }
    }

    private void RecordResults(
        ScenarioContext context, string capability, int total, List<MissingControl> missing)
    {
        int covered = total - missing.Count;
        double pct = total == 0 ? 100.0 : covered * 100.0 / total;

        context.Recorder.Record(new Finding
        {
            Scenario = Name,
            Step = capability,
            Severity = FindingSeverity.Info,
            Title = $"{capability}: {covered}/{total} interactive controls carry a stable AutomationId ({pct:0}% covered)",
            Detail = missing.Count == 0
                ? $"Every audited interactive control under '{capability}' is addressable by AutomationId."
                : $"{missing.Count} interactive control(s) under '{capability}' lack a stable AutomationId. See the per-control findings below.",
        });

        if (missing.Count == 0)
        {
            return;
        }

        // One screenshot per capability documents the audited surface; per-control
        // rows carry the actionable detail (suggested id) an agent can act on.
        string? shot = context.Recorder.Capture(context.Host, $"automationid-{capability}");

        foreach (var control in missing)
        {
            context.Recorder.Record(new Finding
            {
                Scenario = Name,
                Step = capability,
                Severity = FindingSeverity.Warning,
                Title = $"Missing AutomationId: {control.Type} \"{Trim(control.Name)}\"",
                Detail = BuildDetail(capability, control),
                ScreenshotFile = shot is null ? null : Path.GetRelativePath(context.Recorder.RunDir, shot),
            });
        }
    }

    private static string BuildDetail(string capability, MissingControl control)
    {
        var sb = new StringBuilder();
        sb.Append("Control type: ").Append(control.Type).Append('.');
        sb.Append(" Name: ").Append(string.IsNullOrEmpty(control.Name) ? "(none)" : $"\"{control.Name}\"").Append('.');
        if (!string.IsNullOrEmpty(control.ClassName))
        {
            sb.Append(" Class: ").Append(control.ClassName).Append('.');
        }

        sb.Append(" This control has no stable AutomationId (neither AutomationProperties.AutomationId nor x:Name), ");
        sb.Append("so the harness can only reach it by Name or index, which is brittle. ");
        sb.Append("Suggested id: ").Append(SuggestId(capability, control)).Append('.');
        return sb.ToString();
    }

    /// <summary>
    /// Depth-first walk of the automation subtree that reports only top-most
    /// interactive controls. Once inside an interactive control, descendants are
    /// treated as its internal parts (for example the edit and button inside a
    /// ComboBox) and are not reported, which collapses composite controls to one
    /// row. Offscreen elements and their subtrees are skipped because they are not
    /// currently visible to a user.
    /// </summary>
    private static void Walk(
        AutomationElement node,
        AutomationElement root,
        bool insideInteractive,
        Action<MissingControl> onControl)
    {
        if (SafeOffscreen(node))
        {
            return;
        }

        bool isTarget = false;
        ControlType type = ControlType.Custom;
        try
        {
            type = node.ControlType;
            isTarget = !insideInteractive && TargetTypes.Contains(type);
        }
        catch
        {
            // An element that throws on ControlType is transient/dead; skip it but
            // still try its children in case they are stable.
        }

        if (isTarget)
        {
            onControl(new MissingControl
            {
                Type = type,
                Name = node.SafeName(),
                AutomationId = SafeAutomationId(node),
                ClassName = SafeClassName(node),
                DedupKey = BuildDedupKey(type, node),
            });
        }

        AutomationElement[] children;
        try
        {
            children = node.FindAllChildren();
        }
        catch
        {
            return;
        }

        bool childInside = insideInteractive || isTarget;
        foreach (var child in children)
        {
            // Do not descend past the capability root's boundary; root itself is the
            // start so we always process its children.
            Walk(child, root, childInside, onControl);
        }
    }

    private static string SuggestId(string capability, MissingControl control)
    {
        string prefix = Pascal(capability);
        string namePart = Pascal(control.Name);
        string suffix = TypeSuffix(control.Type);

        if (string.IsNullOrEmpty(namePart))
        {
            return $"{prefix}{suffix}NeedsId";
        }

        // Avoid stuttering when the name already ends with the type word.
        return namePart.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? $"{prefix}{namePart}"
            : $"{prefix}{namePart}{suffix}";
    }

    private static string TypeSuffix(ControlType type) => type switch
    {
        ControlType.Button => "Button",
        ControlType.CheckBox => "CheckBox",
        ControlType.RadioButton => "RadioButton",
        ControlType.ComboBox => "ComboBox",
        ControlType.Edit => "TextBox",
        ControlType.Slider => "Slider",
        ControlType.Hyperlink => "Link",
        ControlType.SplitButton => "SplitButton",
        ControlType.Spinner => "Spinner",
        _ => "Control",
    };

    private static string Pascal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(raw.Length);
        bool upperNext = true;
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        return sb.ToString();
    }

    private static string Trim(string s)
        => s.Length <= 40 ? s : s[..37] + "...";

    private static bool SafeOffscreen(AutomationElement element)
    {
        try
        {
            return element.IsOffscreen;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the shell content host that scopes the audit to a capability's own
    /// controls. Guarded because the underlying lookup is a live UIA traversal that
    /// can throw on a momentarily unstable tree; the caller falls back to the whole
    /// window when this returns null.
    /// </summary>
    private static AutomationElement? SafeFindCapabilityRoot(AppHost host)
    {
        try
        {
            return host.MainWindow.ByAutomationId("CapabilityFrame");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds a per-instance dedup key from the control's on-screen rectangle, so
    /// distinct controls are never merged (even when several share an empty Name),
    /// while a persistent control re-observed after a tab switch keeps the same
    /// rectangle and is counted once. Falls back to a unique GUID when no reliable
    /// rectangle is available, which errs toward counting rather than hiding a gap.
    /// </summary>
    private static string BuildDedupKey(ControlType type, AutomationElement element)
    {
        try
        {
            var r = element.BoundingRectangle;
            if (r.Width > 0 && r.Height > 0)
            {
                return $"{type}|{r.X},{r.Y},{r.Width},{r.Height}";
            }
        }
        catch
        {
            // Fall through to a unique key below.
        }

        return $"{type}|{Guid.NewGuid():N}";
    }

    private static string SafeAutomationId(AutomationElement element)
    {
        try
        {
            return element.AutomationId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafeClassName(AutomationElement element)
    {
        try
        {
            return element.ClassName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>A single interactive control observed during the audit.</summary>
    private sealed class MissingControl
    {
        public ControlType Type { get; init; }
        public string Name { get; init; } = string.Empty;
        public string AutomationId { get; init; } = string.Empty;
        public string ClassName { get; init; } = string.Empty;

        /// <summary>Per-instance identity used to dedupe across tab activations.</summary>
        public string DedupKey { get; init; } = string.Empty;
    }
}
