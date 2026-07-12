namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A directory trust authored in the Builder draft. Mirrors the persisted
/// <see cref="LabAssistant.Models.Templates.V2TrustTemplate"/> one-for-one but keeps the trust type and
/// direction as strings so the immutable draft stays free of the model enums and round-trips any
/// hand-authored value (including an External or Realm trust) losslessly. A forest trust is authored with
/// <see cref="SourceDomainId"/>/<see cref="TargetDomainId"/> set to the two forests' root domain ids,
/// <see cref="TrustType"/> = "Forest", and <see cref="Direction"/> = "Bidirectional".
/// </summary>
internal readonly record struct TemplatesBuilderTrustDraft(
    string TrustId,
    string SourceDomainId,
    string TargetDomainId,
    string TrustType,
    string Direction);
