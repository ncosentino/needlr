using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentTopologyAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenHandoffsTo_TargetHasNeedlrAiAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenClassHasNoHandoffsTo()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF001_WhenHandoffsTo_TargetLacksNeedlrAiAgent()
    {
        // NDLRMAF001 is reported on the attribute application span
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class GeographyAgent { }

[NeedlrAiAgent]
[{|NDLRMAF001:AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")|}]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF003_WhenHandoffsTo_SourceLacksNeedlrAiAgent()
    {
        // NDLRMAF003 is reported on the class name (typeSymbol.Locations[0])
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class {|NDLRMAF003:TriageAgent|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BothDiagnostics_WhenBothSourceAndTargetLackNeedlrAiAgent()
    {
        // NDLRMAF001 on attribute application, NDLRMAF003 on class name
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class GeographyAgent { }

[{|NDLRMAF001:AgentHandoffsTo(typeof(GeographyAgent), ""geography"")|}]
public class {|NDLRMAF003:TriageAgent|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF001_MultipleHandoffTargets_OnlyReportsTheMissingOnes()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

public class LifestyleAgent { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography"")]
[{|NDLRMAF001:AgentHandoffsTo(typeof(LifestyleAgent), ""lifestyle"")|}]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
