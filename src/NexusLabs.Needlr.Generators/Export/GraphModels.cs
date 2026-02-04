using System;
using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Export;

/// <summary>
/// Root model for the Needlr dependency graph export.
/// Serialized to needlr-graph.json for IDE tooling consumption.
/// </summary>
internal sealed class NeedlrGraph
{
    public string SchemaVersion { get; set; } = "1.0";
    public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("O");
    public string? ProjectPath { get; set; }
    public string? AssemblyName { get; set; }
    public List<GraphService> Services { get; set; } = new();
    public List<GraphDiagnostic> Diagnostics { get; set; } = new();
    public GraphStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Represents a discovered service in the dependency graph.
/// </summary>
internal sealed class GraphService
{
    public string Id { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string FullTypeName { get; set; } = string.Empty;
    public string? AssemblyName { get; set; }
    public List<GraphInterface> Interfaces { get; set; } = new();
    public string Lifetime { get; set; } = string.Empty;
    public GraphLocation? Location { get; set; }
    public List<GraphDependency> Dependencies { get; set; } = new();
    public List<GraphDecorator> Decorators { get; set; } = new();
    public List<string> Interceptors { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public List<string> ServiceKeys { get; set; } = new();
    public GraphServiceMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Represents an interface implemented by a service.
/// </summary>
internal sealed class GraphInterface
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a source file location.
/// </summary>
internal sealed class GraphLocation
{
    public string? FilePath { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

/// <summary>
/// Represents a dependency of a service.
/// </summary>
internal sealed class GraphDependency
{
    public string ParameterName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string FullTypeName { get; set; } = string.Empty;
    public string? ResolvedTo { get; set; }
    public string? ResolvedLifetime { get; set; }
    public bool IsKeyed { get; set; }
    public string? ServiceKey { get; set; }
}

/// <summary>
/// Represents a decorator applied to a service.
/// </summary>
internal sealed class GraphDecorator
{
    public string TypeName { get; set; } = string.Empty;
    public int Order { get; set; }
}

/// <summary>
/// Additional metadata about a service.
/// </summary>
internal sealed class GraphServiceMetadata
{
    public bool HasFactory { get; set; }
    public bool HasOptions { get; set; }
    public bool IsHostedService { get; set; }
    public bool IsDisposable { get; set; }
    public bool IsPlugin { get; set; }
}

/// <summary>
/// Represents a diagnostic (warning/error) from generation.
/// </summary>
internal sealed class GraphDiagnostic
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public GraphLocation? Location { get; set; }
    public List<string> RelatedServices { get; set; } = new();
}

/// <summary>
/// Statistics about the generated graph.
/// </summary>
internal sealed class GraphStatistics
{
    public int TotalServices { get; set; }
    public int Singletons { get; set; }
    public int Scoped { get; set; }
    public int Transient { get; set; }
    public int Decorators { get; set; }
    public int Interceptors { get; set; }
    public int Factories { get; set; }
    public int Options { get; set; }
    public int HostedServices { get; set; }
    public int Plugins { get; set; }
}
