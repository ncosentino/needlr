using System.Collections.Generic;
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
    private bool _enableDiagnostics;

    public MafGeneratorTestRunner WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    public MafGeneratorTestRunner WithDiagnostics(bool enabled = true)
    {
        _enableDiagnostics = enabled;
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
        CSharpGeneratorDriver driver;

        if (_enableDiagnostics)
        {
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(
                new Dictionary<string, string> { ["build_property.NeedlrDiagnostics"] = "true" });
            driver = CSharpGeneratorDriver.Create(
                generators: [generator.AsSourceGenerator()],
                optionsProvider: optionsProvider);
        }
        else
        {
            driver = CSharpGeneratorDriver.Create(generator);
        }

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

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class WorkflowRunTerminationConditionAttribute : System.Attribute
            {
                public WorkflowRunTerminationConditionAttribute(System.Type conditionType, params object[] ctorArgs) { }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class ProgressSinksAttribute : System.Attribute
            {
                public ProgressSinksAttribute(params System.Type[] sinkTypes) { SinkTypes = sinkTypes; }
                public System.Type[] SinkTypes { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentGraphEdgeAttribute : System.Attribute
            {
                public AgentGraphEdgeAttribute(string graphName, System.Type targetAgentType)
                {
                    GraphName = graphName;
                    TargetAgentType = targetAgentType;
                }
                public string GraphName { get; }
                public System.Type TargetAgentType { get; }
                public string Condition { get; set; }
                public bool IsRequired { get; set; } = true;
                private int _nodeRoutingMode;
                public int NodeRoutingMode
                {
                    get => _nodeRoutingMode;
                    set { _nodeRoutingMode = value; HasNodeRoutingMode = true; }
                }
                public bool HasNodeRoutingMode { get; private set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentGraphEntryAttribute : System.Attribute
            {
                public AgentGraphEntryAttribute(string graphName) { GraphName = graphName; }
                public string GraphName { get; }
                public int RoutingMode { get; set; } = 0;
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentGraphNodeAttribute : System.Attribute
            {
                public AgentGraphNodeAttribute(string graphName) { GraphName = graphName; }
                public string GraphName { get; }
                public int JoinMode { get; set; } = 0;
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class AgentGraphReducerAttribute : System.Attribute
            {
                public AgentGraphReducerAttribute(string graphName) { GraphName = graphName; }
                public string GraphName { get; }
                public string ReducerMethod { get; set; }
            }

            public interface IWorkflowTerminationCondition { }

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

                public static void RegisterGraphTopology(
                    System.Func<System.Collections.Generic.IReadOnlyDictionary<string, GraphTopologyRegistration>> graphTopology)
                { }

                public static void RegisterAIFunctionProvider(object provider)
                { }
            }

            public sealed class GraphTopologyRegistration
            {
                public GraphTopologyRegistration(
                    System.Type entryType,
                    int routingMode,
                    System.Collections.Generic.IReadOnlyList<(System.Type Source, System.Type Target, string Condition, bool IsRequired, int? NodeRoutingMode)> edges,
                    System.Collections.Generic.IReadOnlyList<(System.Type NodeType, int JoinMode)> nodes,
                    (System.Type ReducerType, string ReducerMethod)? reducer)
                { }
            }

            public interface IWorkflowFactory
            {
                Microsoft.Agents.AI.Workflows.Workflow CreateGraphWorkflow(string graphName);
            }

            public interface IGraphWorkflowRunner
            {
                System.Threading.Tasks.Task<NexusLabs.Needlr.AgentFramework.Diagnostics.IDagRunResult> RunGraphAsync(
                    string graphName,
                    string input,
                    NexusLabs.Needlr.AgentFramework.Progress.IProgressReporter progress = null,
                    System.Threading.CancellationToken cancellationToken = default);
            }

            [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("")]
            [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("")]
            public sealed record AgentFrameworkSyringe
            {
                public required System.IServiceProvider ServiceProvider { get; init; }

                [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("AgentFramework function setup uses reflection to discover [AgentFunction] methods.")]
                [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("AgentFramework function setup uses reflection APIs that require dynamic code generation.")]
                public AgentFrameworkSyringe AddAgentFunctions(System.Collections.Generic.IReadOnlyList<System.Type> functionTypes) => this;
            }
        }

        namespace Microsoft.Agents.AI.Workflows
        {
            public sealed class Workflow { }
        }

        namespace NexusLabs.Needlr.AgentFramework.Diagnostics
        {
            public interface IPipelineRunResult { }
            public interface IDagRunResult : IPipelineRunResult { }
        }

        namespace NexusLabs.Needlr.AgentFramework.Progress
        {
            public interface IProgressReporter { }
        }

        namespace NexusLabs.Needlr.AgentFramework.Workflows
        {
            public class KeywordTerminationCondition : NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition
            {
                public KeywordTerminationCondition(string keyword, string agentId = null) { }
            }

            public class RegexTerminationCondition : NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition
            {
                public RegexTerminationCondition(string pattern, string agentId = null) { }
            }

            public static class StreamingRunWorkflowExtensions
            {
                public static System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyDictionary<string, string>> RunAsync(
                    Microsoft.Agents.AI.Workflows.Workflow workflow,
                    string message,
                    System.Collections.Generic.IReadOnlyList<NexusLabs.Needlr.AgentFramework.IWorkflowTerminationCondition> conditions,
                    System.Threading.CancellationToken cancellationToken = default) => null;
            }
        }
        """;
}

/// <summary>A generated file path and its source text content.</summary>
internal sealed record GeneratedFile(string FilePath, string Content);

/// <summary>
/// Provides build property values for tests that need analyzer config options
/// (e.g. <c>NeedlrDiagnostics=true</c>).
/// </summary>
internal sealed class TestAnalyzerConfigOptionsProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
{
    private readonly TestGlobalOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalOptions)
    {
        _globalOptions = new TestGlobalOptions(globalOptions);
    }

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
        TestGlobalOptions.Empty;

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
        TestGlobalOptions.Empty;

    private sealed class TestGlobalOptions : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        public static readonly TestGlobalOptions Empty = new(new Dictionary<string, string>());
        private readonly Dictionary<string, string> _values;

        public TestGlobalOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        {
            if (_values.TryGetValue(key, out var v))
            {
                value = v;
                return true;
            }

            value = null;
            return false;
        }
    }
}
