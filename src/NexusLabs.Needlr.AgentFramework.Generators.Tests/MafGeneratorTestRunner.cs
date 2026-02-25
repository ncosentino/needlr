using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace NexusLabs.Needlr.AgentFramework.Generators.Tests;

/// <summary>
/// Test infrastructure for running <see cref="AgentFrameworkFunctionRegistryGenerator"/>
/// against in-memory source code and inspecting the generated output files.
/// </summary>
internal sealed class MafGeneratorTestRunner
{
    private readonly List<string> _sources = [];

    public MafGeneratorTestRunner WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    /// <summary>Runs the generator and returns all generated .g.cs files.</summary>
    public GeneratedFile[] RunGenerator()
    {
        var syntaxTrees = _sources
            .Select(s => CSharpSyntaxTree.ParseText(s))
            .ToArray();

        var references = Basic.Reference.Assemblies.Net100.References.All;

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new AgentFrameworkFunctionRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);

        return outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .Select(t => new GeneratedFile(t.FilePath, t.GetText().ToString()))
            .ToArray();
    }

    /// <summary>Runs the generator and returns the content of a specific file by path fragment.</summary>
    public string GetFile(string pathFragment)
    {
        var files = RunGenerator();
        return files.FirstOrDefault(f => f.FilePath.Contains(pathFragment))?.Content ?? string.Empty;
    }

    /// <summary>Creates a runner pre-loaded with the MAF attribute stubs source.</summary>
    public static MafGeneratorTestRunner Create() =>
        new MafGeneratorTestRunner().WithSource(MafAttributeDefinitions);

    /// <summary>
    /// Inline stubs for all MAF attribute types and supporting infrastructure.
    /// Matches the exact FQNs used by <see cref="AgentFrameworkFunctionRegistryGenerator"/>
    /// via <c>ForAttributeWithMetadataName</c> and direct type comparisons.
    /// </summary>
    public const string MafAttributeDefinitions = """
        namespace NexusLabs.Needlr.AgentFramework
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class NeedlrAiAgentAttribute : System.Attribute
            {
                public string? Instructions { get; set; }
                public string[]? FunctionGroups { get; set; }
                public System.Type[]? FunctionTypes { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
            public sealed class AgentFunctionAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentFunctionGroupAttribute : System.Attribute
            {
                public AgentFunctionGroupAttribute(string groupName) { GroupName = groupName; }
                public string GroupName { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentHandoffsToAttribute : System.Attribute
            {
                public AgentHandoffsToAttribute(System.Type targetAgentType) { }
                public AgentHandoffsToAttribute(System.Type targetAgentType, string reason) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentGroupChatMemberAttribute : System.Attribute
            {
                public AgentGroupChatMemberAttribute(string groupName) { GroupName = groupName; }
                public string GroupName { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentSequenceMemberAttribute : System.Attribute
            {
                public AgentSequenceMemberAttribute(string pipelineName, int order)
                {
                    PipelineName = pipelineName;
                    Order = order;
                }
                public string PipelineName { get; }
                public int Order { get; }
            }

            public static class AgentFrameworkGeneratedBootstrap
            {
                public static void Register(
                    System.Func<System.Collections.Generic.IReadOnlyList<System.Type>> functionTypes,
                    System.Func<System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<System.Type>>> groupTypes,
                    System.Func<System.Collections.Generic.IReadOnlyList<System.Type>> agentTypes,
                    System.Func<System.Collections.Generic.IReadOnlyDictionary<System.Type, System.Collections.Generic.IReadOnlyList<(System.Type, string?)>>> handoffTopology,
                    System.Func<System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<System.Type>>> groupChatTopology = null,
                    System.Func<System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<System.Type>>> sequentialTopology = null)
                { }
            }

            public interface IWorkflowFactory { }
        }

        namespace Microsoft.Agents.AI.Workflows
        {
            public sealed class Workflow { }
        }
        """;
}

/// <summary>A generated file path and its source text content.</summary>
internal sealed record GeneratedFile(string FilePath, string Content);
