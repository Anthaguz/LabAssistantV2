using System;
using System.Collections.Generic;

namespace LabAssistant.Models.Templates;

/// <summary>
/// VM definition within a lab template.
/// </summary>
public class VmTemplate
{
    /// <summary>
    /// Immutable VM entry identifier for traceability (required).
    /// </summary>
    public string VmId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// VM name (required).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Memory in MB (required).
    /// </summary>
    public int MemoryMb { get; set; }

    /// <summary>
    /// CPU count (required).
    /// </summary>
    public int CpuCount { get; set; }

    /// <summary>
    /// Reference to VHDX catalog item id (optional).
    /// </summary>
    public string? VhdxId { get; set; }

    /// <summary>
    /// Fallback VHDX path if catalog id is missing or unresolved (optional).
    /// </summary>
    public string? VhdPath { get; set; }

    /// <summary>
    /// Signature of the selected VHDX (optional, helps with portability).
    /// </summary>
    public string? VhdxSignature { get; set; }

    /// <summary>
    /// Virtual switch name override (optional).
    /// </summary>
    public string? SwitchName { get; set; }

    /// <summary>
    /// Canonical VM switch assignments for multi-NIC/multi-switch templates (optional).
    /// </summary>
    public List<string>? SwitchNames { get; set; }

    /// <summary>
    /// Optional persisted settings for the implemented time zone guest step.
    /// </summary>
    public TimeZoneStepConfig? TimeZoneConfig { get; set; }

    /// <summary>
    /// Optional persisted settings for the implemented software guest step.
    /// </summary>
    public SoftwareStepConfig? SoftwareConfig { get; set; }

    /// <summary>
    /// Optional persisted settings for the implemented role guest step.
    /// </summary>
    public RoleStepConfig? RoleConfig { get; set; }

    /// <summary>
    /// Optional future guest network payload. Placeholder-safe and omitted by default.
    /// </summary>
    public GuestNetworkStepConfig? GuestNetworkConfig { get; set; }

    /// <summary>
    /// Optional V2 topology role that influences orchestration ordering and dependency semantics.
    /// </summary>
    public string? TopologyRole { get; set; }

    /// <summary>
    /// Optional V2 domain reference used to bind the VM to a canonical directory-topology domain.
    /// </summary>
    public string? DomainId { get; set; }

    /// <summary>
    /// Optional additive V2 capability roles that request additional guest work without replacing topology intent.
    /// </summary>
    public List<string>? CapabilityRoles { get; set; }

    /// <summary>
    /// Optional explicit dependency references for V2 planning.
    /// </summary>
    public List<string>? DependsOn { get; set; }

    /// <summary>
    /// Optional V2 template-side credential slot references.
    /// </summary>
    public VmCredentialSlotBindings? CredentialSlots { get; set; }

    /// <summary>
    /// Optional V2 bootstrap-profile reference associated with the selected base image.
    /// </summary>
    public string? BootstrapProfileRef { get; set; }

    /// <summary>
    /// Optional V2 NIC collection for explicit multi-NIC guest/network planning.
    /// </summary>
    public List<VmNetworkInterfaceTemplate>? Nics { get; set; }
}
