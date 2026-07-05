namespace LabAssistant.WinUI.Models.Deploy;

internal sealed record DeployV2PlanSummaryRow(
    string TemplateName,
    string ExecutionEngine,
    string DeploymentProfile,
    int VmCount,
    int NodeCount,
    int UnresolvedRequirementCount,
    string RouterSummary,
    string DomainSummary,
    string StartabilitySummary);

public sealed record DeployV2BlockerRow(
    string Severity,
    string Scope,
    string Message);

public sealed record DeployV2CredentialSlotRow(
    string SlotKey,
    string PurposeSummary,
    string AffectedVmSummary,
    string ExistingUsername,
    bool HasStoredValue);

public sealed record DeployV2WaveRow(
    int WaveNumber,
    string DisplayName,
    int NodeCount,
    string Summary);

public sealed record DeployV2DiagnosticRow(
    string Title,
    string Detail);

internal sealed record DeployV2BaseRemoteAccessRow(
    bool EnableRemoteDesktop,
    bool SetPrivateNetworkProfile,
    bool DisableFirewall,
    bool DisableRdpNla);
