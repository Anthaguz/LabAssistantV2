using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A pure IPv4 subnet value type backing the Builder's per-domain switch model. Each domain owns exactly one
/// switch whose subnet is auto-allocated by incrementing a base plan (10.0.0.0/24, 10.0.1.0/24, ...). Within a
/// subnet the addressing policy is fixed: the router holds the first usable address (.1), the LabAssistant host
/// holds the last usable address (.254 on a /24) so a client can RDP to a guest, and VMs are assigned from the
/// range in between (.2 .. .253 on a /24).
///
/// Everything here is integer math over a normalized 32-bit network address, so it is fully unit-testable
/// without a XAML host and never touches Hyper-V. It is the single source of truth for subnet parsing,
/// allocation, reserved-address policy, and host assignment; the authoring rules, validator, and detail UI all
/// build on it rather than re-deriving CIDR math.
/// </summary>
public readonly record struct BuilderLabSubnet
{
    /// <summary>The normalized network address (host bits cleared), e.g. 10.0.5.0 as a packed uint.</summary>
    public uint NetworkAddress { get; }

    /// <summary>The CIDR prefix length in bits (0-32). The lab model uses octet-aligned masks like /24 and /16.</summary>
    public int PrefixLength { get; }

    /// <summary>
    /// Creates a subnet from any address inside it plus a prefix; the address is normalized down to the network
    /// address so <c>new(10.0.5.37, 24)</c> and <c>new(10.0.5.0, 24)</c> are equal.
    /// </summary>
    public BuilderLabSubnet(uint address, int prefixLength)
    {
        if (prefixLength is < 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixLength), prefixLength, "Prefix length must be between 0 and 32.");
        }

        PrefixLength = prefixLength;
        NetworkAddress = address & MaskFor(prefixLength);
    }

    /// <summary>Number of addresses in the block (network + hosts + broadcast), e.g. 256 for a /24.</summary>
    public long BlockSize => 1L << (32 - PrefixLength);

    /// <summary>The directed broadcast address (all host bits set), e.g. 10.0.5.255 for 10.0.5.0/24.</summary>
    public uint BroadcastAddress => NetworkAddress | (uint)(BlockSize - 1);

    /// <summary>True when the block is large enough to seat a router, a host reservation, and at least one VM.</summary>
    public bool HasAssignableHosts => BlockSize >= 5;

    /// <summary>The router address: the first usable host (.1 on a /24). Null for degenerate tiny blocks.</summary>
    public uint? RouterAddress => HasAssignableHosts ? NetworkAddress + 1 : null;

    /// <summary>The LabAssistant-host reservation: the last usable host (.254 on a /24). Null for tiny blocks.</summary>
    public uint? HostReservedAddress => HasAssignableHosts ? BroadcastAddress - 1 : null;

    /// <summary>The first address assignable to a VM (.2 on a /24). Null for tiny blocks.</summary>
    public uint? FirstAssignableHost => HasAssignableHosts ? NetworkAddress + 2 : null;

    /// <summary>The last address assignable to a VM (.253 on a /24). Null for tiny blocks.</summary>
    public uint? LastAssignableHost => HasAssignableHosts ? BroadcastAddress - 2 : null;

    /// <summary>
    /// How many trailing octets the user edits for a host address: 1 for a /24 (last octet only), 2 for a /16.
    /// Computed as the octets that carry any host bits, so /25 still edits one octet and /23 edits two.
    /// </summary>
    public int EditableOctetCount => Math.Clamp(4 - (PrefixLength / 8), 1, 4);

    /// <summary>True when <paramref name="address"/> falls inside this subnet.</summary>
    public bool Contains(uint address) => (address & MaskFor(PrefixLength)) == NetworkAddress;

    /// <summary>True when the address is the network id or the broadcast address (never assignable).</summary>
    public bool IsNetworkOrBroadcast(uint address) => address == NetworkAddress || address == BroadcastAddress;

    /// <summary>True when the address is the router reservation (.1).</summary>
    public bool IsRouterReserved(uint address) => RouterAddress is { } router && address == router;

    /// <summary>True when the address is the LabAssistant-host reservation (.254 on a /24).</summary>
    public bool IsHostReserved(uint address) => HostReservedAddress is { } host && address == host;

    /// <summary>
    /// The ordered set of addresses a VM may take: inside the subnet, excluding the network, broadcast, router,
    /// and host reservations. Empty for degenerate tiny blocks.
    /// </summary>
    public IEnumerable<uint> AssignableHosts
    {
        get
        {
            if (!HasAssignableHosts)
            {
                yield break;
            }

            for (var address = FirstAssignableHost!.Value; address <= LastAssignableHost!.Value; address++)
            {
                yield return address;
            }
        }
    }

    /// <summary>
    /// The lowest assignable address not already present in <paramref name="used"/>, or null when the subnet is
    /// full. Reserved addresses (router / host / network / broadcast) are skipped implicitly by the range.
    /// </summary>
    public uint? NextFreeHost(IReadOnlyCollection<uint> used)
    {
        var taken = used as ISet<uint> ?? used.ToHashSet();
        foreach (var candidate in AssignableHosts)
        {
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Allocates the <paramref name="ordinal"/>-th subnet (0-based) after this base plan by advancing the network
    /// address one block at a time: 10.0.0.0/24 -> 10.0.1.0/24 -> ... Keeps the same prefix length.
    /// </summary>
    public BuilderLabSubnet AllocateAt(int ordinal)
    {
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Ordinal must be non-negative.");
        }

        var advanced = NetworkAddress + (uint)(BlockSize * ordinal);
        return new BuilderLabSubnet(advanced, PrefixLength);
    }

    /// <summary>
    /// Composes a full host address from the editable trailing octets (highest-order first). Supplying more or
    /// fewer octets than <see cref="EditableOctetCount"/> throws, so the caller and the subnet always agree on
    /// how many octets are user-editable.
    /// </summary>
    public uint ComposeHostAddress(IReadOnlyList<int> editableOctets)
    {
        ArgumentNullException.ThrowIfNull(editableOctets);
        if (editableOctets.Count != EditableOctetCount)
        {
            throw new ArgumentException($"Expected {EditableOctetCount} editable octet(s) for /{PrefixLength}.", nameof(editableOctets));
        }

        var address = NetworkAddress;
        var startOctet = 4 - EditableOctetCount;
        for (var i = 0; i < editableOctets.Count; i++)
        {
            var octet = editableOctets[i];
            if (octet is < 0 or > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(editableOctets), octet, "Each octet must be between 0 and 255.");
            }

            var shift = (3 - (startOctet + i)) * 8;
            address = (address & ~(0xFFu << shift)) | ((uint)octet << shift);
        }

        return address;
    }

    /// <summary>Extracts the editable trailing octets of <paramref name="address"/> (highest-order first).</summary>
    public IReadOnlyList<int> EditableOctetsOf(uint address)
    {
        var octets = new int[EditableOctetCount];
        var startOctet = 4 - EditableOctetCount;
        for (var i = 0; i < octets.Length; i++)
        {
            var shift = (3 - (startOctet + i)) * 8;
            octets[i] = (int)((address >> shift) & 0xFF);
        }

        return octets;
    }

    /// <summary>The fixed (non-editable) leading portion of a host address, e.g. "10.0.5." for a /24.</summary>
    public string FixedOctetPrefix
    {
        get
        {
            var fixedOctetCount = 4 - EditableOctetCount;
            var bytes = ToOctets(NetworkAddress);
            var head = string.Join('.', bytes.Take(fixedOctetCount));
            return fixedOctetCount == 0 ? string.Empty : head + ".";
        }
    }

    /// <summary>Renders the subnet in CIDR notation, e.g. "10.0.5.0/24".</summary>
    public string ToCidrString() => $"{FormatAddress(NetworkAddress)}/{PrefixLength}";

    /// <summary>Parses CIDR notation ("10.0.5.0/24") into a normalized subnet.</summary>
    public static bool TryParseCidr(string? value, out BuilderLabSubnet subnet)
    {
        subnet = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryParseAddress(parts[0], out var address) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix) ||
            prefix is < 0 or > 32)
        {
            return false;
        }

        subnet = new BuilderLabSubnet(address, prefix);
        return true;
    }

    /// <summary>
    /// Parses a strict dotted-quad IPv4 literal into a packed uint. Requires exactly four dot-separated octets,
    /// each a decimal integer 0-255 with no leading zeros (a lone "0" is allowed, "00"/"01" are not). Non-canonical
    /// forms that <c>IPAddress.TryParse</c> silently reinterprets - octal ("010.0.0.0"),
    /// hex ("0x0a.0.0.1"), and short forms ("10.5", "1.2.3", "10") - are rejected, because this type is the single
    /// source of CIDR truth and such input would otherwise be committed and reflow every VM onto a misparsed block.
    /// </summary>
    public static bool TryParseAddress(string? value, out uint address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var octetTexts = value.Trim().Split('.');
        if (octetTexts.Length != 4)
        {
            return false;
        }

        uint packed = 0;
        foreach (var octetText in octetTexts)
        {
            // Reject anything but plain base-10 digits: this also rules out signs, whitespace, "0x.." hex, and
            // empty segments, none of which int.TryParse alone would consistently exclude across cultures.
            if (octetText.Length == 0 || !octetText.All(char.IsAsciiDigit))
            {
                return false;
            }

            // No leading zeros: "00"/"01" are ambiguous (octal-looking) and never a canonical octet; a single "0" is fine.
            if (octetText.Length > 1 && octetText[0] == '0')
            {
                return false;
            }

            if (!int.TryParse(octetText, NumberStyles.None, CultureInfo.InvariantCulture, out var octet) ||
                octet is < 0 or > 255)
            {
                return false;
            }

            packed = (packed << 8) | (uint)octet;
        }

        address = packed;
        return true;
    }

    /// <summary>Renders a packed uint as a dotted-quad IPv4 literal.</summary>
    public static string FormatAddress(uint address)
    {
        var bytes = ToOctets(address);
        return string.Join('.', bytes);
    }

    private static uint MaskFor(int prefixLength)
        => prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);

    private static int[] ToOctets(uint address)
        => [(int)(address >> 24) & 0xFF, (int)(address >> 16) & 0xFF, (int)(address >> 8) & 0xFF, (int)address & 0xFF];
}
