namespace NeedlrCodeLens;

using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Represents the Needlr dependency graph JSON structure.
/// </summary>
public class NeedlrGraph
{
    [JsonProperty("schemaVersion")]
    public string SchemaVersion { get; set; } = "";

    [JsonProperty("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonProperty("projectPath")]
    public string? ProjectPath { get; set; }

    [JsonProperty("assemblyName")]
    public string AssemblyName { get; set; } = "";

    [JsonProperty("services")]
    public List<GraphService> Services { get; set; } = new();

    [JsonProperty("statistics")]
    public GraphStatistics Statistics { get; set; } = new();
}

public class GraphService
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("typeName")]
    public string TypeName { get; set; } = "";

    [JsonProperty("fullTypeName")]
    public string FullTypeName { get; set; } = "";

    [JsonProperty("assemblyName")]
    public string? AssemblyName { get; set; }

    [JsonProperty("interfaces")]
    public List<GraphInterface> Interfaces { get; set; } = new();

    [JsonProperty("lifetime")]
    public string Lifetime { get; set; } = "";

    [JsonProperty("location")]
    public GraphLocation? Location { get; set; }

    [JsonProperty("dependencies")]
    public List<GraphDependency> Dependencies { get; set; } = new();

    [JsonProperty("decorators")]
    public List<GraphDecorator> Decorators { get; set; } = new();
}

public class GraphInterface
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("fullName")]
    public string FullName { get; set; } = "";
}

public class GraphLocation
{
    [JsonProperty("filePath")]
    public string? FilePath { get; set; }

    [JsonProperty("line")]
    public int Line { get; set; }

    [JsonProperty("column")]
    public int Column { get; set; }
}

public class GraphDependency
{
    [JsonProperty("parameterName")]
    public string ParameterName { get; set; } = "";

    [JsonProperty("typeName")]
    public string TypeName { get; set; } = "";

    [JsonProperty("fullTypeName")]
    public string FullTypeName { get; set; } = "";

    [JsonProperty("resolvedTo")]
    public string? ResolvedTo { get; set; }

    [JsonProperty("resolvedLifetime")]
    public string? ResolvedLifetime { get; set; }
}

public class GraphDecorator
{
    [JsonProperty("typeName")]
    public string TypeName { get; set; } = "";

    [JsonProperty("order")]
    public int Order { get; set; }
}

public class GraphStatistics
{
    [JsonProperty("totalServices")]
    public int TotalServices { get; set; }

    [JsonProperty("singletons")]
    public int Singletons { get; set; }

    [JsonProperty("scoped")]
    public int Scoped { get; set; }

    [JsonProperty("transient")]
    public int Transient { get; set; }
}
