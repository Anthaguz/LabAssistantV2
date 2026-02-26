using System.Collections.Generic;

namespace LabAssistant.Models.Templates;

/// <summary>
/// Base persisted toggle for a guest-step execution setting.
/// </summary>
public class GuestStepToggleConfig
{
    public bool Enabled { get; set; }
}

/// <summary>
/// Persisted time zone guest-step settings (implemented toggle, future-ready payload).
/// </summary>
public class TimeZoneStepConfig : GuestStepToggleConfig
{
    public string? TimeZoneId { get; set; }
}

/// <summary>
/// Persisted software guest-step settings (implemented toggle, future-ready payload).
/// </summary>
public class SoftwareStepConfig : GuestStepToggleConfig
{
    public List<string>? Packages { get; set; }
}

/// <summary>
/// Persisted role guest-step settings (implemented toggle, future-ready payload).
/// </summary>
public class RoleStepConfig : GuestStepToggleConfig
{
    public List<string>? Roles { get; set; }
}

/// <summary>
/// Future guest OS network configuration payload (placeholder-safe, optional).
/// </summary>
public class GuestNetworkStepConfig : GuestStepToggleConfig
{
    public string? IpAddress { get; set; }
    public string? DefaultGateway { get; set; }
    public List<string>? DnsServers { get; set; }
}
