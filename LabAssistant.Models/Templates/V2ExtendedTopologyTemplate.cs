namespace LabAssistant.Models.Templates;

/// <summary>
/// Reserved V2 topology seam for future multi-domain and multi-forest declarations.
/// </summary>
public class V2ExtendedTopologyTemplate
{
    public List<V2ChildDomainTemplate>? ChildDomains { get; set; }

    public List<V2AdditionalForestTemplate>? AdditionalForests { get; set; }

    public List<V2TreeDomainTemplate>? TreeDomains { get; set; }
}

public class V2ChildDomainTemplate
{
    public string TopologyId { get; set; } = string.Empty;

    public string ParentDomainRef { get; set; } = string.Empty;

    public string ChildLabel { get; set; } = string.Empty;

    public string? DomainFqdn { get; set; }

    public string? NetBiosName { get; set; }

    public string FirstDomainControllerVmId { get; set; } = string.Empty;
}

public class V2AdditionalForestTemplate
{
    public string TopologyId { get; set; } = string.Empty;

    public string ForestRootDomainFqdn { get; set; } = string.Empty;

    public string? NetBiosName { get; set; }

    public string FirstDomainControllerVmId { get; set; } = string.Empty;
}

/// <summary>
/// Reserved placeholder for future tree-domain declarations. Shape stays intentionally minimal in #729.
/// </summary>
public class V2TreeDomainTemplate
{
    public string TopologyId { get; set; } = string.Empty;
}
