using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>Small wait/find helpers layered over FlaUI for readable page objects.</summary>
public static class UiaExtensions
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Finds the first descendant with the given AutomationId, or null.</summary>
    public static AutomationElement? ByAutomationId(this AutomationElement root, string automationId)
        => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    /// <summary>Finds the first descendant with the given Name, or null.</summary>
    public static AutomationElement? ByName(this AutomationElement root, string name)
        => root.FindFirstDescendant(cf => cf.ByName(name));

    /// <summary>Waits until a descendant with the given AutomationId exists, then returns it.</summary>
    public static AutomationElement WaitForAutomationId(this AutomationElement root, string automationId, TimeSpan? timeout = null)
    {
        var found = Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout ?? DefaultTimeout).Result;

        return found ?? throw new TimeoutException($"Element with AutomationId '{automationId}' not found within timeout.");
    }

    /// <summary>Waits until a descendant with the given Name exists, then returns it.</summary>
    public static AutomationElement WaitForName(this AutomationElement root, string name, TimeSpan? timeout = null)
    {
        var found = Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByName(name)),
            timeout ?? DefaultTimeout).Result;

        return found ?? throw new TimeoutException($"Element named '{name}' not found within timeout.");
    }

    /// <summary>
    /// Robustly activates a control: tries the Invoke pattern, then SelectionItem
    /// Select, then a physical click. Returns when one succeeds.
    /// </summary>
    public static void Activate(this AutomationElement element)
    {
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
            return;
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return;
        }

        element.Click();
    }

    /// <summary>Reads an element's Name safely (empty string on failure).</summary>
    public static string SafeName(this AutomationElement element)
    {
        try
        {
            return element.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Sets a value-pattern control's text (TextBox etc.); falls back to focus+type.</summary>
    public static void SetValue(this AutomationElement element, string value)
    {
        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(value);
            return;
        }

        element.Focus();
        element.AsTextBox().Text = value;
    }
}
