using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphReducerMethodAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenReducerMethodExists()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphReducer(""Pipeline"", ReducerMethod = ""Merge"")]
public class MyReducer
{
    public static string Merge(IReadOnlyList<string> inputs) => string.Join("", "", inputs);
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoReducerAttribute()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF029_WhenReducerMethodNotFound()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF029:AgentGraphReducer(""Pipeline"", ReducerMethod = ""NonexistentMethod"")|}]
public class MyReducer { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF029_WhenReducerMethodNotStatic()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF029:AgentGraphReducer(""Pipeline"", ReducerMethod = ""Merge"")|}]
public class MyReducer
{
    public string Merge(IReadOnlyList<string> inputs) => string.Join("", "", inputs);
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF029_WhenReducerMethodWrongReturnType()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF029:AgentGraphReducer(""Pipeline"", ReducerMethod = ""Merge"")|}]
public class MyReducer
{
    public static int Merge(IReadOnlyList<string> inputs) => inputs.Count;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF029_WhenReducerMethodWrongParameter()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF029:AgentGraphReducer(""Pipeline"", ReducerMethod = ""Merge"")|}]
public class MyReducer
{
    public static string Merge(string input) => input;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF029_WhenReducerMethodNotPublic()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF029:AgentGraphReducer(""Pipeline"", ReducerMethod = ""MergeResults"")|}]
public class MyReducer
{
    private static string MergeResults(IReadOnlyList<string> x) => string.Join("", "", x);
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenReducerMethodDefinedOnBaseClass()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr.AgentFramework;

public class BaseReducer
{
    public static string Merge(IReadOnlyList<string> inputs) => string.Join("", "", inputs);
}

[NeedlrAiAgent]
[AgentGraphReducer(""Pipeline"", ReducerMethod = ""Merge"")]
public class MyReducer : BaseReducer { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF029_WhenIReadOnlyListFromDifferentNamespace()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

namespace Custom.Collections
{
    public interface IReadOnlyList<T> { }
}

[NeedlrAiAgent]
[{|NDLRMAF029:AgentGraphReducer(""Pipeline"", ReducerMethod = ""Merge"")|}]
public class MyReducer
{
    public static string Merge(Custom.Collections.IReadOnlyList<string> inputs) => ""merged"";
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphReducerMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
