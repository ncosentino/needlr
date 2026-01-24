using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects circular dependencies in service registrations.
/// A circular dependency occurs when a service directly or indirectly depends on itself.
/// </summary>
/// <remarks>
/// Examples:
/// - A → B → A (direct cycle)
/// - A → B → C → A (indirect cycle)
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CircularDependencyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.CircularDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // We need to analyze at compilation level to build the full dependency graph
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var dependencyGraph = new DependencyGraphBuilder();

            // First pass: collect all types and their dependencies
            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => CollectDependencies(nodeContext, dependencyGraph),
                SyntaxKind.ClassDeclaration);

            // End of compilation: analyze for cycles
            compilationContext.RegisterCompilationEndAction(
                endContext => AnalyzeForCycles(endContext, dependencyGraph));
        });
    }

    private static void CollectDependencies(SyntaxNodeAnalysisContext context, DependencyGraphBuilder graph)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Skip abstract classes
        if (classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return;
        }

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
        {
            return;
        }

        // Check if this is a registered service (has registration attributes)
        if (!IsRegisteredService(classSymbol))
        {
            return;
        }

        var dependencies = new List<INamedTypeSymbol>();
        var location = classDeclaration.Identifier.GetLocation();

        // Collect dependencies from primary constructor
        if (classDeclaration.ParameterList != null)
        {
            foreach (var parameter in classDeclaration.ParameterList.Parameters)
            {
                if (parameter.Type == null) continue;

                var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
                if (typeInfo.Type is INamedTypeSymbol paramType)
                {
                    dependencies.Add(paramType);
                }
            }
        }

        // Collect dependencies from explicit constructors
        var constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();

        foreach (var constructor in constructors)
        {
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                if (parameter.Type == null) continue;

                var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
                if (typeInfo.Type is INamedTypeSymbol paramType)
                {
                    dependencies.Add(paramType);
                }
            }
        }

        graph.AddNode(classSymbol, dependencies, location);
    }

    private static void AnalyzeForCycles(CompilationAnalysisContext context, DependencyGraphBuilder graph)
    {
        var cycles = graph.DetectCycles();

        foreach (var cycle in cycles)
        {
            var cycleDescription = string.Join(" → ", cycle.Path.Select(t => t.Name)) + " → " + cycle.Path[0].Name;

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CircularDependency,
                cycle.Location,
                cycleDescription);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsRegisteredService(INamedTypeSymbol typeSymbol)
    {
        var registrationAttributes = new[]
        {
            "RegisterAsAttribute", "RegisterAs",
            "SingletonAttribute", "Singleton",
            "ScopedAttribute", "Scoped",
            "TransientAttribute", "Transient",
            "AutoRegisterAttribute", "AutoRegister"
        };

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name;
            if (attributeName != null && registrationAttributes.Contains(attributeName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a dependency graph and detects cycles.
    /// </summary>
    private class DependencyGraphBuilder
    {
        private readonly Dictionary<INamedTypeSymbol, NodeInfo> _nodes = new(SymbolEqualityComparer.Default);
        private readonly object _lock = new();

        public void AddNode(INamedTypeSymbol type, List<INamedTypeSymbol> dependencies, Location location)
        {
            lock (_lock)
            {
                _nodes[type] = new NodeInfo(dependencies, location);
            }
        }

        public List<CycleInfo> DetectCycles()
        {
            var cycles = new List<CycleInfo>();
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var recursionStack = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var path = new List<INamedTypeSymbol>();

            // Sort by full type name for deterministic iteration order
            var sortedNodes = _nodes.Keys.OrderBy(n => n.ToDisplayString()).ToList();
            foreach (var node in sortedNodes)
            {
                if (!visited.Contains(node))
                {
                    DetectCyclesDfs(node, visited, recursionStack, path, cycles);
                }
            }

            return cycles;
        }

        private void DetectCyclesDfs(
            INamedTypeSymbol current,
            HashSet<INamedTypeSymbol> visited,
            HashSet<INamedTypeSymbol> recursionStack,
            List<INamedTypeSymbol> path,
            List<CycleInfo> cycles)
        {
            visited.Add(current);
            recursionStack.Add(current);
            path.Add(current);

            if (_nodes.TryGetValue(current, out var nodeInfo))
            {
                foreach (var dependency in nodeInfo.Dependencies)
                {
                    // Resolve interface to implementation if possible
                    var resolvedDep = ResolveDependency(dependency);

                    if (!visited.Contains(resolvedDep))
                    {
                        DetectCyclesDfs(resolvedDep, visited, recursionStack, path, cycles);
                    }
                    else if (recursionStack.Contains(resolvedDep))
                    {
                        // Found a cycle - extract the cycle path
                        var cycleStartIndex = path.IndexOf(resolvedDep);
                        if (cycleStartIndex >= 0)
                        {
                            var cyclePath = path.Skip(cycleStartIndex).ToList();
                            cycles.Add(new CycleInfo(cyclePath, nodeInfo.Location));
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            recursionStack.Remove(current);
        }

        private INamedTypeSymbol ResolveDependency(INamedTypeSymbol dependency)
        {
            // If it's an interface or abstract, try to find an implementation in our graph
            if (dependency.TypeKind == TypeKind.Interface || dependency.IsAbstract)
            {
                foreach (var kvp in _nodes)
                {
                    var type = kvp.Key;
                    if (type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, dependency)) ||
                        (type.BaseType != null && SymbolEqualityComparer.Default.Equals(type.BaseType, dependency)))
                    {
                        return type;
                    }
                }
            }

            return dependency;
        }

        private sealed class NodeInfo
        {
            public List<INamedTypeSymbol> Dependencies { get; }
            public Location Location { get; }

            public NodeInfo(List<INamedTypeSymbol> dependencies, Location location)
            {
                Dependencies = dependencies;
                Location = location;
            }
        }
    }

    private sealed class CycleInfo
    {
        public List<INamedTypeSymbol> Path { get; }
        public Location Location { get; }

        public CycleInfo(List<INamedTypeSymbol> path, Location location)
        {
            Path = path;
            Location = location;
        }
    }
}
