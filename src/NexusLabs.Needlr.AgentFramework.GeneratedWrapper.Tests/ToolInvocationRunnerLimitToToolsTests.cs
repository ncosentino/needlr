using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Testing;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

/// <summary>
/// Integration tests for <see cref="ToolInvocationRunner.LimitToTools(Type[])"/> — the per-test
/// scope wrapper that limits which generated <c>[AgentFunction]</c> types are visible during
/// resolution. These tests rely on the source-generator output for this project to produce
/// providers for several distinct tool types.
/// </summary>
public sealed class ToolInvocationRunnerLimitToToolsTests
{
    [Fact]
    public void LimitToTools_DoesNotPreventResolutionOfTypesPresentInGlobalProvider()
    {
        var stringCapture = new E2EStringTool.Capture();
        var intCapture = new E2EIntTool.Capture();
        var runner = ToolInvocationRunner
            .Create(s =>
            {
                s.AddSingleton(stringCapture);
                s.AddSingleton(intCapture);
                s.AddSingleton<E2EStringTool>();
                s.AddSingleton<E2EIntTool>();
            })
            .LimitToTools(typeof(E2EStringTool));

        var fn = runner.GetFunction<E2EStringTool>(nameof(E2EStringTool.Record));

        Assert.Equal(nameof(E2EStringTool.Record), fn.Name);
    }

    [Fact]
    public async Task LimitToTools_ResolvedFunctionStillInvokesCorrectly()
    {
        var capture = new E2EStringTool.Capture();
        var runner = ToolInvocationRunner
            .Create(s =>
            {
                s.AddSingleton(capture);
                s.AddSingleton<E2EStringTool>();
            })
            .LimitToTools(typeof(E2EStringTool));

        var result = await runner.InvokeAsync<E2EStringTool>(
            nameof(E2EStringTool.Record),
            args => args["findingsJson"] = "[]",
            TestContext.Current.CancellationToken);

        result.AssertSuccess();
        Assert.Equal(ToolFunctionSource.Generated, result.FunctionSource);
        Assert.Equal("[]", capture.Value);
    }

    [Fact]
    public void LimitToTools_ImmutableSemantics()
    {
        var capture = new E2EStringTool.Capture();
        var original = ToolInvocationRunner.Create(s =>
        {
            s.AddSingleton(capture);
            s.AddSingleton<E2EStringTool>();
        });
        var limited = original.LimitToTools(typeof(E2EStringTool));

        Assert.NotSame(original, limited);
    }
}
