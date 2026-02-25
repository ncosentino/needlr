using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentFunctionGroupReferenceAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenFunctionGroupsNotDeclared()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenFunctionGroupsIsEmpty()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(FunctionGroups = new string[0])]
public class TriageAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenAllGroupsAreRegistered()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(FunctionGroups = new[] { ""geography"" })]
public class GeographyAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenMultipleGroupsAllRegistered()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(FunctionGroups = new[] { ""geography"", ""lifestyle"" })]
public class ExpertAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }

[AgentFunctionGroup(""lifestyle"")]
public class LifestyleFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF005_WhenGroupNotRegistered()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF005:NeedlrAiAgent(FunctionGroups = new[] { ""unknown-group"" })|}]
public class GeographyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF005_WhenOneOfMultipleGroupsNotRegistered()
    {
        // "geography" is registered; "typo-group" is not
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF005:NeedlrAiAgent(FunctionGroups = new[] { ""geography"", ""typo-group"" })|}]
public class ExpertAgent { }

[AgentFunctionGroup(""geography"")]
public class GeographyFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF005_OnEachAgentWithUnknownGroup()
    {
        // Two agents each reference an unregistered group — two warnings
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF005:NeedlrAiAgent(FunctionGroups = new[] { ""missing-a"" })|}]
public class AgentA { }

[{|NDLRMAF005:NeedlrAiAgent(FunctionGroups = new[] { ""missing-b"" })|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenGroupRegisteredOnClassInSameCompilation()
    {
        // The [AgentFunctionGroup] class is in the same compilation — should be resolved
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(FunctionGroups = new[] { ""tools"" })]
public class HelperAgent { }

[AgentFunctionGroup(""tools"")]
[AgentFunctionGroup(""extras"")]
public class MultiGroupFunctions { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionGroupReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
