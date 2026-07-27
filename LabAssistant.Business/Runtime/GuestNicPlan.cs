namespace LabAssistant.Business.Runtime;

/// <summary>
/// A single guest NIC to configure in-guest during PrepareGuestNetwork, correlated to its host-side Hyper-V
/// adapter MAC. Matching by MAC lets the in-guest script bind each static IP to the adapter that actually sits on
/// the intended switch, instead of guessing by OS enumeration order, which silently mis-binds on multi-NIC VMs.
/// </summary>
internal sealed class GuestNicPlan
{
    /// <summary>Template NIC identifier, used only for stable ordering and diagnostics.</summary>
    public string NicId { get; init; } = string.Empty;

    /// <summary>Host-side MAC of the Hyper-V adapter bound to this NIC's switch.</summary>
    public string MacAddress { get; init; } = string.Empty;

    /// <summary>Static IPv4 address to assign, or null to leave the adapter on DHCP.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Prefix length paired with <see cref="IpAddress"/>.</summary>
    public int? PrefixLength { get; init; }

    /// <summary>Optional default gateway paired with <see cref="IpAddress"/>.</summary>
    public string? DefaultGateway { get; init; }

    /// <summary>DNS servers to set on the adapter.</summary>
    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();
}
