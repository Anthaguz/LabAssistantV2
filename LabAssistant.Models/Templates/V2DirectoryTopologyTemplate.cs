namespace LabAssistant.Models.Templates;

using System.Text.Json.Serialization;

public class V2DirectoryTopologyTemplate
{
    public List<V2ForestTemplate>? Forests { get; set; }

    public List<V2DomainTemplate>? Domains { get; set; }

    public List<V2TrustTemplate>? Trusts { get; set; }
}

public class V2ForestTemplate
{
    public string ForestId { get; set; } = string.Empty;

    public string RootDomainId { get; set; } = string.Empty;
}

// Persisted inside LabTemplate.DirectoryTopology. Serialized as its string name so that inserting or
// reordering enum members never silently reinterprets the meaning of an already-saved template.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum V2DomainRelationKind
{
    Root = 0,
    Child = 1,
    Tree = 2
}

public class V2DomainTemplate
{
    public string DomainId { get; set; } = string.Empty;

    public string DnsName { get; set; } = string.Empty;

    public string NetBiosName { get; set; } = string.Empty;

    public string ForestId { get; set; } = string.Empty;

    public V2DomainRelationKind RelationKind { get; set; } = V2DomainRelationKind.Root;

    public string? ParentDomainId { get; set; }

    public string FirstDomainControllerVmId { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum V2TrustType
{
    External = 0,
    Forest = 1,
    Realm = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum V2TrustDirection
{
    Inbound = 0,
    Outbound = 1,
    Bidirectional = 2
}

public class V2TrustTemplate
{
    public string TrustId { get; set; } = string.Empty;

    public string SourceDomainId { get; set; } = string.Empty;

    public string TargetDomainId { get; set; } = string.Empty;

    public V2TrustType TrustType { get; set; } = V2TrustType.External;

    public V2TrustDirection Direction { get; set; } = V2TrustDirection.Bidirectional;
}
