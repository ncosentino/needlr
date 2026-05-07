using System.ComponentModel;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Testing;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class ToolInvocationRunnerTests
{
    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ToolInvocationRunner(null!));
    }

    [Fact]
    public void CreateFor_RegistersToolAndAccessors()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        Assert.NotNull(runner);
    }

    [Fact]
    public void Create_AllowsExternalServiceConfiguration()
    {
        var runner = ToolInvocationRunner.Create(s => s.AddSingleton<ReflectionOnlyTool>());

        Assert.NotNull(runner);
    }

    [Fact]
    public void GetFunction_TypeNotInGeneratedProvider_Throws()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            runner.GetFunction<ReflectionOnlyTool>(nameof(ReflectionOnlyTool.Echo)));

        Assert.Contains("source-generated IAIFunctionProvider", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ReflectionOnlyTool), ex.Message, StringComparison.Ordinal);
        Assert.Contains("[AgentFunctionGroup]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertGeneratedProviderAvailable_AgentFrameworkLoaded_DoesNotThrow()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        runner.AssertGeneratedProviderAvailable();
    }

    [Fact]
    public void IsGeneratedProviderAvailable_AgentFrameworkLoaded_ReturnsTrue()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        Assert.True(runner.IsGeneratedProviderAvailable);
    }

    [Fact]
    public async Task InvokeAsync_NoFunctionRegistered_ReturnsResultWithException()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        var result = await runner.InvokeAsync<ReflectionOnlyTool>(
            nameof(ReflectionOnlyTool.Echo),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Exception);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal(ToolFunctionSource.Generated, result.FunctionSource);
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public async Task GetFunctionAllowingReflection_NoGeneratedProvider_FallsBackToReflection()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        var fn = runner.GetFunctionAllowingReflection<ReflectionOnlyTool>(nameof(ReflectionOnlyTool.Echo));

        Assert.Equal(nameof(ReflectionOnlyTool.Echo), fn.Name);

        var result = await fn.InvokeAsync(
            new AIFunctionArguments { ["value"] = "hello" },
            TestContext.Current.CancellationToken);

        Assert.Equal("hello", result?.ToString());
    }

    [Fact]
    public void GetFunctionAllowingReflection_MissingMethod_Throws()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            runner.GetFunctionAllowingReflection<ReflectionOnlyTool>("nonexistent_method"));

        Assert.Contains("nonexistent_method", ex.Message, StringComparison.Ordinal);
        Assert.Contains("[AgentFunction]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFunction_NullToolType_Throws()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        Assert.Throws<ArgumentNullException>(() => runner.GetFunction(null!, "any"));
    }

    [Fact]
    public void GetFunction_EmptyMethodName_Throws()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        Assert.Throws<ArgumentException>(() =>
            runner.GetFunction(typeof(ReflectionOnlyTool), ""));
    }

    [Fact]
    public void LimitToTools_EmptyArray_Throws()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        var ex = Assert.Throws<ArgumentException>(() => runner.LimitToTools());

        Assert.Contains("At least one tool type", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void With_Methods_AreImmutable()
    {
        var original = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();
        var withCtx = original.WithExecutionContext(c => c.WithUserId("alice"));
        var withWs = original.WithWorkspace(_ => { });
        var limited = original.LimitToTools(typeof(ReflectionOnlyTool));

        Assert.NotSame(original, withCtx);
        Assert.NotSame(original, withWs);
        Assert.NotSame(original, limited);
        Assert.NotSame(withCtx, withWs);
    }

    [Fact]
    public async Task InvokeAsync_WithWorkspace_SurfacesWorkspaceOnResult()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>()
            .WithWorkspace(ws => ws.TryWriteFile("seed.txt", "seeded"));

        var result = await runner.InvokeAsync<ReflectionOnlyTool>(
            nameof(ReflectionOnlyTool.Echo),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result.Workspace);
        Assert.True(result.Workspace!.FileExists("seed.txt"));
    }

    [Fact]
    public async Task InvokeAsync_WithWorkspaceInstance_PreservesIdentity()
    {
        var ws = new InMemoryWorkspace();
        ws.TryWriteFile("hello.txt", "hi");

        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>()
            .WithWorkspace(ws);

        var result = await runner.InvokeAsync<ReflectionOnlyTool>(
            nameof(ReflectionOnlyTool.Echo),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(ws, result.Workspace);
    }

    [Fact]
    public async Task InvokeAsync_RestoresExecutionContextScopeAfterCall()
    {
        var sp = new ServiceCollection().AddAgentFrameworkAccessors().BuildServiceProvider();
        var accessor = sp.GetRequiredService<IAgentExecutionContextAccessor>();
        var outer = new AgentExecutionContext("outer-user", "outer-orch");

        using (accessor.BeginScope(outer))
        {
            Assert.Same(outer, accessor.Current);

            var runner = new ToolInvocationRunner(sp)
                .WithExecutionContext(c => c.WithUserId("inner-user"));

            await runner.InvokeAsync(
                typeof(NotRegisteredTool),
                "anything",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Same(outer, accessor.Current);
        }
    }

    [Fact]
    public async Task InvokeAsync_DictionaryOverload_AppliesAllArguments()
    {
        var runner = ToolInvocationRunner.CreateFor<ReflectionOnlyTool>();

        var fn = runner.GetFunctionAllowingReflection<ReflectionOnlyTool>(nameof(ReflectionOnlyTool.Echo));
        Assert.Equal("Echo", fn.Name);

        var sp = new ServiceCollection().AddAgentFrameworkAccessors().BuildServiceProvider();
        var accessor = sp.GetRequiredService<IAgentExecutionContextAccessor>();

        using (accessor.BeginScope(new AgentExecutionContext("u", "o")))
        {
            var output = await fn.InvokeAsync(
                new AIFunctionArguments { ["value"] = "world" },
                TestContext.Current.CancellationToken);
            Assert.Equal("world", output?.ToString());
        }
    }

    [Fact]
    public void AgentExecutionContextBuilder_NullArgs_Throw()
    {
        var b = new AgentExecutionContextBuilder();

        Assert.Throws<ArgumentException>(() => b.WithUserId(""));
        Assert.Throws<ArgumentException>(() => b.WithOrchestrationId(" "));
        Assert.Throws<ArgumentNullException>(() => b.WithWorkspace((Action<IWorkspace>)null!));
        Assert.Throws<ArgumentNullException>(() => b.WithWorkspace((IWorkspace)null!));
        Assert.Throws<ArgumentException>(() => b.WithProperty("", "v"));
        Assert.Throws<ArgumentNullException>(() => b.WithProperty("k", null!));
    }

    [Fact]
    public void ToolInvocationResult_AssertSuccess_WhenSucceeded_DoesNotThrow()
    {
        var result = new ToolInvocationResult(
            ReturnValue: "ok",
            Exception: null,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.FromMilliseconds(5));

        result.AssertSuccess();
    }

    [Fact]
    public void ToolInvocationResult_AssertSuccess_WhenFailed_ThrowsWithInner()
    {
        var inner = new InvalidCastException("kaboom");
        var result = new ToolInvocationResult(
            ReturnValue: null,
            Exception: inner,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.Zero);

        var ex = Assert.Throws<InvalidOperationException>(result.AssertSuccess);
        Assert.Same(inner, ex.InnerException);
        Assert.Contains("Generated", ex.Message, StringComparison.Ordinal);
        Assert.Contains("kaboom", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolInvocationResult_AssertResultContains_Match_DoesNotThrow()
    {
        var result = new ToolInvocationResult(
            ReturnValue: "hello world",
            Exception: null,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.Zero);

        result.AssertResultContains("world");
    }

    [Fact]
    public void ToolInvocationResult_AssertResultContains_NoMatch_Throws()
    {
        var result = new ToolInvocationResult(
            ReturnValue: "hello",
            Exception: null,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.Zero);

        var ex = Assert.Throws<InvalidOperationException>(() => result.AssertResultContains("missing"));
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("hello", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolInvocationResult_AssertResultContains_NullValue_Throws()
    {
        var result = new ToolInvocationResult(
            ReturnValue: null,
            Exception: null,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.Zero);

        Assert.Throws<InvalidOperationException>(() => result.AssertResultContains("anything"));
    }

    [Fact]
    public void ToolInvocationResult_GetValue_TypeMatch_Returns()
    {
        var result = new ToolInvocationResult(
            ReturnValue: 42,
            Exception: null,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.Zero);

        Assert.Equal(42, result.GetValue<int>());
    }

    [Fact]
    public void ToolInvocationResult_GetValue_TypeMismatch_ReturnsDefault()
    {
        var result = new ToolInvocationResult(
            ReturnValue: 42,
            Exception: null,
            FunctionSource: ToolFunctionSource.Generated,
            Workspace: null,
            Duration: TimeSpan.Zero);

        Assert.Null(result.GetValue<string>());
    }

    public sealed class ReflectionOnlyTool
    {
        [AgentFunction]
        [Description("Returns its input verbatim.")]
        public string Echo(
            [Description("Value to echo back.")] string value) => value;
    }

    public sealed class NotRegisteredTool
    {
    }
}
