using Newtonsoft.Json;
using System.Collections.Generic;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Needlr Graph Types - matches the JSON schema from needlr-graph-v1.schema.json
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
        public string? AssemblyName { get; set; }

        [JsonProperty("services")]
        public List<GraphService> Services { get; set; } = new();

        [JsonProperty("diagnostics")]
        public List<GraphDiagnostic> Diagnostics { get; set; } = new();

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

        [JsonProperty("interceptors")]
        public List<string> Interceptors { get; set; } = new();

        [JsonProperty("attributes")]
        public List<string> Attributes { get; set; } = new();

        [JsonProperty("serviceKeys")]
        public List<string> ServiceKeys { get; set; } = new();

        [JsonProperty("metadata")]
        public GraphServiceMetadata Metadata { get; set; } = new();
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

        [JsonProperty("isKeyed")]
        public bool IsKeyed { get; set; }

        [JsonProperty("serviceKey")]
        public string? ServiceKey { get; set; }
    }

    public class GraphDecorator
    {
        [JsonProperty("typeName")]
        public string TypeName { get; set; } = "";

        [JsonProperty("order")]
        public int Order { get; set; }
    }

    public class GraphServiceMetadata
    {
        [JsonProperty("hasFactory")]
        public bool HasFactory { get; set; }

        [JsonProperty("hasOptions")]
        public bool HasOptions { get; set; }

        [JsonProperty("isHostedService")]
        public bool IsHostedService { get; set; }

        [JsonProperty("isDisposable")]
        public bool IsDisposable { get; set; }

        [JsonProperty("isPlugin")]
        public bool IsPlugin { get; set; }
    }

    public class GraphDiagnostic
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("severity")]
        public string Severity { get; set; } = "";

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        [JsonProperty("location")]
        public GraphLocation? Location { get; set; }

        [JsonProperty("relatedServices")]
        public List<string> RelatedServices { get; set; } = new();
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

        [JsonProperty("decorators")]
        public int Decorators { get; set; }

        [JsonProperty("interceptors")]
        public int Interceptors { get; set; }

        [JsonProperty("factories")]
        public int Factories { get; set; }

        [JsonProperty("options")]
        public int Options { get; set; }

        [JsonProperty("hostedServices")]
        public int HostedServices { get; set; }

        [JsonProperty("plugins")]
        public int Plugins { get; set; }
    }
}
