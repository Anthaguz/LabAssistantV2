namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// A single key/value row from a selected log entry's context, rendered in the Selected Context panel
/// as selectable text instead of one opaque JSON blob.
/// </summary>
public sealed class LogContextRow
{
    public LogContextRow(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>The context field name.</summary>
    public string Key { get; }

    /// <summary>The field value rendered as text (nested objects/arrays keep their JSON form).</summary>
    public string Value { get; }
}
