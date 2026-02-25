namespace LabAssistant.Services.HyperV;

public interface IHyperVFailureDiagnosticsProvider
{
    IReadOnlyDictionary<string, object?>? LastFailureMetadata { get; }
    void ClearLastFailureMetadata();
}
