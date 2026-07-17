namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// A facility (byte 2 of the status code): a subsystem or category that events are grouped under.
/// </summary>
public sealed class StatusFacility
{
    public StatusFacility(byte value, string name, string title)
    {
        Value = value;
        Name = name;
        Title = title;
    }

    /// <summary>The facility byte (0x00-0xFF).</summary>
    public byte Value { get; }

    /// <summary>The stable dotted facility name, for example <c>deploy.guest</c>.</summary>
    public string Name { get; }

    /// <summary>A human-friendly facility title, for example "Guest configuration".</summary>
    public string Title { get; }
}
