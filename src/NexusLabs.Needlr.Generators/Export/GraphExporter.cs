using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.Export;

/// <summary>
/// Generates the Needlr dependency graph JSON for IDE tooling.
/// </summary>
internal static class GraphExporter
{
    /// <summary>
    /// Generates the needlr-graph.json content from the discovery result.
    /// </summary>
    public static string GenerateGraphJson(
        DiscoveryResult discoveryResult,
        string assemblyName,
        string? projectPath,
        IReadOnlyList<CollectedDiagnostic>? diagnostics = null,
        IReadOnlyDictionary<string, IReadOnlyList<DiscoveredType>>? referencedAssemblyTypes = null)
    {
        var graph = BuildGraph(discoveryResult, assemblyName, projectPath, diagnostics, referencedAssemblyTypes);
        return SerializeToJson(graph);
    }

    private static NeedlrGraph BuildGraph(
        DiscoveryResult discoveryResult,
        string assemblyName,
        string? projectPath,
        IReadOnlyList<CollectedDiagnostic>? diagnostics,
        IReadOnlyDictionary<string, IReadOnlyList<DiscoveredType>>? referencedAssemblyTypes)
    {
        var graph = new NeedlrGraph
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            ProjectPath = projectPath,
            AssemblyName = assemblyName
        };

        // Build type lookup for resolving dependencies (include referenced assembly types)
        var typeLookup = BuildTypeLookup(discoveryResult, referencedAssemblyTypes);

        // Map injectable types from current assembly to graph services
        foreach (var type in discoveryResult.InjectableTypes)
        {
            var service = MapToGraphService(type, assemblyName, typeLookup, discoveryResult);
            graph.Services.Add(service);
        }

        // Add types from referenced assemblies with [GenerateTypeRegistry]
        if (referencedAssemblyTypes != null)
        {
            foreach (var kvp in referencedAssemblyTypes)
            {
                var refAssemblyName = kvp.Key;
                var types = kvp.Value;
                foreach (var type in types)
                {
                    var service = MapToGraphService(type, refAssemblyName, typeLookup, discoveryResult);
                    graph.Services.Add(service);
                }
            }
        }

        // Add diagnostics if provided
        if (diagnostics != null)
        {
            foreach (var diag in diagnostics)
            {
                graph.Diagnostics.Add(new GraphDiagnostic
                {
                    Id = diag.Id,
                    Severity = diag.Severity,
                    Message = diag.Message,
                    Location = diag.FilePath != null ? new GraphLocation
                    {
                        FilePath = diag.FilePath,
                        Line = diag.Line,
                        Column = 0
                    } : null,
                    RelatedServices = diag.RelatedServices?.ToList() ?? new List<string>()
                });
            }
        }

        // Compute statistics (include referenced assembly types in count)
        graph.Statistics = ComputeStatistics(discoveryResult, referencedAssemblyTypes);

        return graph;
    }

    private static Dictionary<string, DiscoveredType> BuildTypeLookup(
        DiscoveryResult discoveryResult,
        IReadOnlyDictionary<string, IReadOnlyList<DiscoveredType>>? referencedAssemblyTypes)
    {
        var lookup = new Dictionary<string, DiscoveredType>();
        
        // Add types from current assembly
        foreach (var type in discoveryResult.InjectableTypes)
        {
            lookup[type.TypeName] = type;
            foreach (var iface in type.InterfaceNames)
            {
                if (!lookup.ContainsKey(iface))
                {
                    lookup[iface] = type;
                }
            }
        }

        // Add types from referenced assemblies for dependency resolution
        if (referencedAssemblyTypes != null)
        {
            foreach (var kvp in referencedAssemblyTypes)
            {
                foreach (var type in kvp.Value)
                {
                    if (!lookup.ContainsKey(type.TypeName))
                    {
                        lookup[type.TypeName] = type;
                    }
                    foreach (var iface in type.InterfaceNames)
                    {
                        if (!lookup.ContainsKey(iface))
                        {
                            lookup[iface] = type;
                        }
                    }
                }
            }
        }
        
        return lookup;
    }

    private static GraphService MapToGraphService(
        DiscoveredType type,
        string assemblyName,
        Dictionary<string, DiscoveredType> typeLookup,
        DiscoveryResult discoveryResult)
    {
        var service = new GraphService
        {
            Id = type.TypeName,
            TypeName = GetSimpleTypeName(type.TypeName),
            FullTypeName = type.TypeName,
            AssemblyName = assemblyName,
            Lifetime = type.Lifetime.ToString(),
            Location = type.SourceFilePath != null ? new GraphLocation
            {
                FilePath = type.SourceFilePath,
                Line = type.SourceLine,
                Column = 0
            } : null,
            ServiceKeys = type.ServiceKeys.ToList(),
            Metadata = new GraphServiceMetadata
            {
                IsDisposable = type.IsDisposable,
                HasFactory = discoveryResult.Factories.Any(f => f.TypeName == type.TypeName),
                IsHostedService = discoveryResult.HostedServices.Any(h => h.TypeName == type.TypeName),
                IsPlugin = discoveryResult.PluginTypes.Any(p => p.TypeName == type.TypeName)
            }
        };

        // Map interfaces
        foreach (var iface in type.InterfaceNames)
        {
            service.Interfaces.Add(new GraphInterface
            {
                Name = GetSimpleTypeName(iface),
                FullName = iface
            });
        }

        // Map dependencies from constructor parameters
        foreach (var param in type.ConstructorParameters)
        {
            var dependency = new GraphDependency
            {
                ParameterName = param.ParameterName ?? string.Empty,
                TypeName = GetSimpleTypeName(param.TypeName),
                FullTypeName = param.TypeName,
                IsKeyed = param.IsKeyed,
                ServiceKey = param.ServiceKey
            };

            // Try to resolve the dependency
            if (typeLookup.TryGetValue(param.TypeName, out var resolved))
            {
                dependency.ResolvedTo = resolved.TypeName;
                dependency.ResolvedLifetime = resolved.Lifetime.ToString();
            }

            service.Dependencies.Add(dependency);
        }

        // Map decorators
        var decorators = discoveryResult.Decorators
            .Where(d => type.InterfaceNames.Contains(d.ServiceTypeName))
            .OrderBy(d => d.Order);
        
        foreach (var decorator in decorators)
        {
            service.Decorators.Add(new GraphDecorator
            {
                TypeName = decorator.DecoratorTypeName,
                Order = decorator.Order
            });
        }

        // Map interceptors
        var intercepted = discoveryResult.InterceptedServices
            .FirstOrDefault(i => i.TypeName == type.TypeName);
        
        if (intercepted.TypeName != null)
        {
            service.Interceptors = intercepted.AllInterceptorTypeNames.ToList();
        }

        // Collect attributes
        service.Attributes.Add(type.Lifetime.ToString());
        if (type.IsKeyed)
        {
            service.Attributes.Add("Keyed");
        }

        return service;
    }

    private static GraphStatistics ComputeStatistics(
        DiscoveryResult discoveryResult,
        IReadOnlyDictionary<string, IReadOnlyList<DiscoveredType>>? referencedAssemblyTypes)
    {
        // Get all types for statistics - current assembly + referenced assemblies
        var allTypes = new List<DiscoveredType>(discoveryResult.InjectableTypes);
        if (referencedAssemblyTypes != null)
        {
            foreach (var kvp in referencedAssemblyTypes)
            {
                allTypes.AddRange(kvp.Value);
            }
        }

        return new GraphStatistics
        {
            TotalServices = allTypes.Count,
            Singletons = allTypes.Count(t => t.Lifetime == GeneratorLifetime.Singleton),
            Scoped = allTypes.Count(t => t.Lifetime == GeneratorLifetime.Scoped),
            Transient = allTypes.Count(t => t.Lifetime == GeneratorLifetime.Transient),
            Decorators = discoveryResult.Decorators.Count,
            Interceptors = discoveryResult.InterceptedServices.Count,
            Factories = discoveryResult.Factories.Count,
            Options = discoveryResult.Options.Count,
            HostedServices = discoveryResult.HostedServices.Count,
            Plugins = discoveryResult.PluginTypes.Count
        };
    }

    private static string GetSimpleTypeName(string fullTypeName)
    {
        // Remove global:: prefix
        var name = fullTypeName;
        if (name.StartsWith("global::"))
        {
            name = name.Substring(8);
        }
        
        // Handle generic types like Lazy<T> or IReadOnlyList<Assembly>
        // We want to preserve the generic structure but simplify inner types
        var genericStart = name.IndexOf('<');
        if (genericStart >= 0)
        {
            // Get the outer type name (before generic params)
            var outerPart = name.Substring(0, genericStart);
            var lastDot = outerPart.LastIndexOf('.');
            var simpleOuter = lastDot >= 0 ? outerPart.Substring(lastDot + 1) : outerPart;
            
            // Get the generic parameters and simplify them recursively
            var genericEnd = name.LastIndexOf('>');
            if (genericEnd > genericStart)
            {
                var genericParams = name.Substring(genericStart + 1, genericEnd - genericStart - 1);
                // Simplify each generic parameter (split by comma, handle nested generics)
                var simplifiedParams = SimplifyGenericParameters(genericParams);
                return $"{simpleOuter}<{simplifiedParams}>";
            }
            
            return simpleOuter;
        }
        
        // Get just the type name (after last dot)
        var idx = name.LastIndexOf('.');
        return idx >= 0 ? name.Substring(idx + 1) : name;
    }

    private static string SimplifyGenericParameters(string genericParams)
    {
        // Handle nested generics by tracking depth
        var result = new StringBuilder();
        var depth = 0;
        var currentParam = new StringBuilder();
        
        foreach (var c in genericParams)
        {
            if (c == '<')
            {
                depth++;
                currentParam.Append(c);
            }
            else if (c == '>')
            {
                depth--;
                currentParam.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                // End of parameter at top level
                if (result.Length > 0)
                {
                    result.Append(", ");
                }
                result.Append(GetSimpleTypeName(currentParam.ToString().Trim()));
                currentParam.Clear();
            }
            else
            {
                currentParam.Append(c);
            }
        }
        
        // Add last parameter
        if (currentParam.Length > 0)
        {
            if (result.Length > 0)
            {
                result.Append(", ");
            }
            result.Append(GetSimpleTypeName(currentParam.ToString().Trim()));
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Serializes the graph to JSON without using System.Text.Json (not available in all targets).
    /// Uses simple string building for source generator compatibility.
    /// </summary>
    private static string SerializeToJson(NeedlrGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"schemaVersion\": \"{Escape(graph.SchemaVersion)}\",");
        sb.AppendLine($"  \"generatedAt\": \"{Escape(graph.GeneratedAt)}\",");
        sb.AppendLine($"  \"projectPath\": {NullableString(graph.ProjectPath)},");
        sb.AppendLine($"  \"assemblyName\": {NullableString(graph.AssemblyName)},");
        
        // Services array
        sb.AppendLine("  \"services\": [");
        for (int i = 0; i < graph.Services.Count; i++)
        {
            SerializeService(sb, graph.Services[i], i == graph.Services.Count - 1);
        }
        sb.AppendLine("  ],");
        
        // Diagnostics array
        sb.AppendLine("  \"diagnostics\": [");
        for (int i = 0; i < graph.Diagnostics.Count; i++)
        {
            SerializeDiagnostic(sb, graph.Diagnostics[i], i == graph.Diagnostics.Count - 1);
        }
        sb.AppendLine("  ],");
        
        // Statistics object
        sb.AppendLine("  \"statistics\": {");
        sb.AppendLine($"    \"totalServices\": {graph.Statistics.TotalServices},");
        sb.AppendLine($"    \"singletons\": {graph.Statistics.Singletons},");
        sb.AppendLine($"    \"scoped\": {graph.Statistics.Scoped},");
        sb.AppendLine($"    \"transient\": {graph.Statistics.Transient},");
        sb.AppendLine($"    \"decorators\": {graph.Statistics.Decorators},");
        sb.AppendLine($"    \"interceptors\": {graph.Statistics.Interceptors},");
        sb.AppendLine($"    \"factories\": {graph.Statistics.Factories},");
        sb.AppendLine($"    \"options\": {graph.Statistics.Options},");
        sb.AppendLine($"    \"hostedServices\": {graph.Statistics.HostedServices},");
        sb.AppendLine($"    \"plugins\": {graph.Statistics.Plugins}");
        sb.AppendLine("  }");
        
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void SerializeService(StringBuilder sb, GraphService service, bool isLast)
    {
        sb.AppendLine("    {");
        sb.AppendLine($"      \"id\": \"{Escape(service.Id)}\",");
        sb.AppendLine($"      \"typeName\": \"{Escape(service.TypeName)}\",");
        sb.AppendLine($"      \"fullTypeName\": \"{Escape(service.FullTypeName)}\",");
        sb.AppendLine($"      \"assemblyName\": {NullableString(service.AssemblyName)},");
        
        // Interfaces
        sb.AppendLine("      \"interfaces\": [");
        for (int i = 0; i < service.Interfaces.Count; i++)
        {
            var iface = service.Interfaces[i];
            var comma = i < service.Interfaces.Count - 1 ? "," : "";
            sb.AppendLine($"        {{ \"name\": \"{Escape(iface.Name)}\", \"fullName\": \"{Escape(iface.FullName)}\" }}{comma}");
        }
        sb.AppendLine("      ],");
        
        sb.AppendLine($"      \"lifetime\": \"{Escape(service.Lifetime)}\",");
        
        // Location
        if (service.Location != null)
        {
            sb.AppendLine("      \"location\": {");
            sb.AppendLine($"        \"filePath\": {NullableString(service.Location.FilePath)},");
            sb.AppendLine($"        \"line\": {service.Location.Line},");
            sb.AppendLine($"        \"column\": {service.Location.Column}");
            sb.AppendLine("      },");
        }
        else
        {
            sb.AppendLine("      \"location\": null,");
        }
        
        // Dependencies
        sb.AppendLine("      \"dependencies\": [");
        for (int i = 0; i < service.Dependencies.Count; i++)
        {
            var dep = service.Dependencies[i];
            var comma = i < service.Dependencies.Count - 1 ? "," : "";
            sb.AppendLine("        {");
            sb.AppendLine($"          \"parameterName\": \"{Escape(dep.ParameterName)}\",");
            sb.AppendLine($"          \"typeName\": \"{Escape(dep.TypeName)}\",");
            sb.AppendLine($"          \"fullTypeName\": \"{Escape(dep.FullTypeName)}\",");
            sb.AppendLine($"          \"resolvedTo\": {NullableString(dep.ResolvedTo)},");
            sb.AppendLine($"          \"resolvedLifetime\": {NullableString(dep.ResolvedLifetime)},");
            sb.AppendLine($"          \"isKeyed\": {dep.IsKeyed.ToString().ToLowerInvariant()},");
            sb.AppendLine($"          \"serviceKey\": {NullableString(dep.ServiceKey)}");
            sb.AppendLine($"        }}{comma}");
        }
        sb.AppendLine("      ],");
        
        // Decorators
        sb.AppendLine("      \"decorators\": [");
        for (int i = 0; i < service.Decorators.Count; i++)
        {
            var dec = service.Decorators[i];
            var comma = i < service.Decorators.Count - 1 ? "," : "";
            sb.AppendLine($"        {{ \"typeName\": \"{Escape(dec.TypeName)}\", \"order\": {dec.Order} }}{comma}");
        }
        sb.AppendLine("      ],");
        
        // Interceptors
        sb.Append("      \"interceptors\": [");
        sb.Append(string.Join(", ", service.Interceptors.Select(i => $"\"{Escape(i)}\"")));
        sb.AppendLine("],");
        
        // Attributes
        sb.Append("      \"attributes\": [");
        sb.Append(string.Join(", ", service.Attributes.Select(a => $"\"{Escape(a)}\"")));
        sb.AppendLine("],");
        
        // Service keys
        sb.Append("      \"serviceKeys\": [");
        sb.Append(string.Join(", ", service.ServiceKeys.Select(k => $"\"{Escape(k)}\"")));
        sb.AppendLine("],");
        
        // Metadata
        sb.AppendLine("      \"metadata\": {");
        sb.AppendLine($"        \"hasFactory\": {service.Metadata.HasFactory.ToString().ToLowerInvariant()},");
        sb.AppendLine($"        \"hasOptions\": {service.Metadata.HasOptions.ToString().ToLowerInvariant()},");
        sb.AppendLine($"        \"isHostedService\": {service.Metadata.IsHostedService.ToString().ToLowerInvariant()},");
        sb.AppendLine($"        \"isDisposable\": {service.Metadata.IsDisposable.ToString().ToLowerInvariant()},");
        sb.AppendLine($"        \"isPlugin\": {service.Metadata.IsPlugin.ToString().ToLowerInvariant()}");
        sb.AppendLine("      }");
        
        sb.AppendLine(isLast ? "    }" : "    },");
    }

    private static void SerializeDiagnostic(StringBuilder sb, GraphDiagnostic diagnostic, bool isLast)
    {
        sb.AppendLine("    {");
        sb.AppendLine($"      \"id\": \"{Escape(diagnostic.Id)}\",");
        sb.AppendLine($"      \"severity\": \"{Escape(diagnostic.Severity)}\",");
        sb.AppendLine($"      \"message\": \"{Escape(diagnostic.Message)}\",");
        
        if (diagnostic.Location != null)
        {
            sb.AppendLine("      \"location\": {");
            sb.AppendLine($"        \"filePath\": {NullableString(diagnostic.Location.FilePath)},");
            sb.AppendLine($"        \"line\": {diagnostic.Location.Line},");
            sb.AppendLine($"        \"column\": {diagnostic.Location.Column}");
            sb.AppendLine("      },");
        }
        else
        {
            sb.AppendLine("      \"location\": null,");
        }
        
        sb.Append("      \"relatedServices\": [");
        sb.Append(string.Join(", ", diagnostic.RelatedServices.Select(s => $"\"{Escape(s)}\"")));
        sb.AppendLine("]");
        
        sb.AppendLine(isLast ? "    }" : "    },");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
            
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string NullableString(string? value)
    {
        return value == null ? "null" : $"\"{Escape(value)}\"";
    }
}

/// <summary>
/// Represents a diagnostic collected during generation for inclusion in the graph.
/// </summary>
internal sealed class CollectedDiagnostic
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int Line { get; set; }
    public IReadOnlyList<string>? RelatedServices { get; set; }
}
