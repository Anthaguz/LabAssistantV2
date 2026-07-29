using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// Classifies whether a router VM network attachment is the external/WAN egress side.
///
/// A router's WAN is either a true Hyper-V External switch or a NAT-capable host switch that already
/// provides outbound egress. Hyper-V's built-in "Default Switch" is such a NAT switch: it hands guests
/// NAT'd DHCP internet, yet Hyper-V reports its switch type as Internal. A strict type == External gate
/// therefore wrongly rejects it as a router WAN even though RRAS/NAT function over it in-guest (the guest
/// scripts target the external adapter by MAC and the external NIC simply takes the upstream DHCP lease).
/// This policy treats that well-known NAT switch as an external-equivalent egress attachment while leaving
/// genuine Internal/Private lab switches non-external, so the router's LAN side stays internal.
///
/// This recognition is intentionally additive: anything already classified External stays External, and
/// only the well-known NAT switch is newly accepted. Callers scope the NAT recognition to router VMs so
/// non-router switch reconciliation and member egress classification are unchanged.
/// </summary>
internal static class RouterExternalAttachmentPolicy
{
    // Hyper-V's built-in NAT switch. Its stable identity is the fixed well-known GUID; the display name is
    // consistent across hosts. Router external classification runs at the planning/runtime layer where only
    // the switch name is available (VM adapter and switch queries surface the name, not the GUID), so we
    // match the name and also accept the GUID form, documenting the GUID as the canonical identity behind it.
    private const string DefaultSwitchName = "Default Switch";
    private const string DefaultSwitchId = "c08cb7b8-9b3c-408e-8e30-5e16a3aeb444";

    /// <summary>
    /// Returns whether an attachment on the given switch counts as a router external/WAN egress side:
    /// a true Hyper-V External switch, or a NAT-capable host switch such as Hyper-V's Default Switch.
    /// </summary>
    public static bool IsExternalAttachment(string? switchType, string? switchName)
        => IsExternalSwitchType(switchType) || IsNatCapableEgressSwitch(switchName);

    /// <summary>
    /// Returns whether the switch type is the true Hyper-V External type.
    /// </summary>
    public static bool IsExternalSwitchType(string? switchType)
        => string.Equals(switchType?.Trim(), V2SwitchTypeCatalog.External, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether the named host switch is a NAT-capable egress switch (Hyper-V's Default Switch),
    /// matched by its stable well-known name or its fixed identity GUID.
    /// </summary>
    public static bool IsNatCapableEgressSwitch(string? switchName)
    {
        var trimmed = switchName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        return string.Equals(trimmed, DefaultSwitchName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, DefaultSwitchId, StringComparison.OrdinalIgnoreCase);
    }
}
