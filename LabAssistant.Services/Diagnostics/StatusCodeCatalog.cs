using System.Collections.Generic;
using System.Linq;

namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// The in-memory table of every registered status code. The data half of this partial class is
/// generated from <c>Diagnostics/status-codes.yaml</c> (see <c>StatusCodeCatalog.Generated.g.cs</c>).
/// </summary>
public static partial class StatusCodeCatalog
{
    private static readonly IReadOnlyList<StatusCodeDescriptor> AllCodes = BuildAll();
    private static readonly IReadOnlyList<StatusFacility> AllFacilities = BuildFacilities();
    private static readonly IReadOnlyDictionary<uint, StatusCodeDescriptor> ByCode =
        AllCodes.ToDictionary(descriptor => descriptor.Code);

    /// <summary>Every registered code, ordered by numeric value.</summary>
    public static IReadOnlyList<StatusCodeDescriptor> All => AllCodes;

    /// <summary>Every registered facility.</summary>
    public static IReadOnlyList<StatusFacility> Facilities => AllFacilities;

    /// <summary>Looks up the descriptor for an exact code.</summary>
    public static bool TryGet(uint code, out StatusCodeDescriptor descriptor)
        => ByCode.TryGetValue(code, out descriptor!);

    /// <summary>Looks up a facility by its byte value.</summary>
    public static StatusFacility? FacilityFor(byte value)
        => AllFacilities.FirstOrDefault(facility => facility.Value == value);
}
