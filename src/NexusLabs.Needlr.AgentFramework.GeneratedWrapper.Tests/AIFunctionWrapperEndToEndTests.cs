using System.ComponentModel;
using System.Text.Json;

using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

/// <summary>
/// End-to-end tests for the source-generated <see cref="AIFunction"/> wrapper. These exercise
/// the same path <c>FunctionInvokingChatClient</c> uses — resolving the generated
/// <see cref="IAIFunctionProvider"/>, building <see cref="AIFunctionArguments"/> with literal
/// <see cref="JsonElement"/> kinds (arrays, objects, numbers, …), and invoking the wrapper.
/// </summary>
/// <remarks>
/// <para>
/// The bug report calls out that hand-written tests typically bypass the generated wrapper —
/// they call the user method directly and never see kind-mismatch failures. This file is the
/// canonical pattern that closes that gap. New tools that have non-trivial argument shapes
/// should add an analogous test here.
/// </para>
/// <para>
/// This project exists separately from <c>NexusLabs.Needlr.AgentFramework.Tests</c> precisely
/// so the source generator can run on these fixture types without polluting the global
/// <c>AgentFrameworkGeneratedBootstrap</c> state used by the broader test suite (which has
/// many <c>[AgentFunctionGroup]</c> fixtures designed for the reflection-based scanner).
/// </para>
/// </remarks>
public class AIFunctionWrapperEndToEndTests
{
    private static AIFunction ResolveFunction<TTool>(string methodName)
        where TTool : class
    {
        Assert.True(
            AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider),
            "Generated IAIFunctionProvider was not registered — the source generator's ModuleInitializer didn't run.");

        var services = new Syringe()
            .UsingReflection()
            .BuildServiceProvider();

        Assert.True(
            provider!.TryGetFunctions(typeof(TTool), services, out var functions),
            $"No generated functions for {typeof(TTool).Name}.");

        return Assert.Single(functions!, f => f.Name == methodName);
    }

    private static AIFunctionArguments Args(string key, JsonElement value) =>
        new() { [key] = value };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Wrapper_StringParam_AcceptsArrayLiteral()
    {
        E2EStringTool.Captured = null;
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record));

        await fn.InvokeAsync(
            Args("findingsJson", Parse("[{\"severity\":\"Warning\"}]")),
            TestContext.Current.CancellationToken);

        Assert.Equal("[{\"severity\":\"Warning\"}]", E2EStringTool.Captured);
    }

    [Fact]
    public async Task Wrapper_StringParam_AcceptsObjectLiteral()
    {
        E2EStringTool.Captured = null;
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record));

        await fn.InvokeAsync(
            Args("findingsJson", Parse("{\"k\":1}")),
            TestContext.Current.CancellationToken);

        Assert.Equal("{\"k\":1}", E2EStringTool.Captured);
    }

    [Fact]
    public async Task Wrapper_StringParam_AcceptsNullJson()
    {
        E2EStringTool.Captured = null;
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record));

        await fn.InvokeAsync(
            Args("findingsJson", Parse("null")),
            TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, E2EStringTool.Captured);
    }

    [Fact]
    public async Task Wrapper_StringParam_AcceptsStringLiteralAsIs()
    {
        E2EStringTool.Captured = null;
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record));

        await fn.InvokeAsync(
            Args("findingsJson", Parse("\"plain text\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal("plain text", E2EStringTool.Captured);
    }

    [Fact]
    public async Task Wrapper_IntParam_AcceptsNumericString()
    {
        E2EIntTool.Captured = null;
        var fn = ResolveFunction<E2EIntTool>(nameof(E2EIntTool.SetMax));

        await fn.InvokeAsync(
            Args("max", Parse("\"42\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(42, E2EIntTool.Captured);
    }

    [Fact]
    public async Task Wrapper_IntParam_AcceptsNumberLiteral()
    {
        E2EIntTool.Captured = null;
        var fn = ResolveFunction<E2EIntTool>(nameof(E2EIntTool.SetMax));

        await fn.InvokeAsync(
            Args("max", Parse("42")),
            TestContext.Current.CancellationToken);

        Assert.Equal(42, E2EIntTool.Captured);
    }

    [Fact]
    public async Task Wrapper_BoolParam_AcceptsStringTrue()
    {
        E2EBoolTool.Captured = null;
        var fn = ResolveFunction<E2EBoolTool>(nameof(E2EBoolTool.SetFlag));

        await fn.InvokeAsync(
            Args("flag", Parse("\"true\"")),
            TestContext.Current.CancellationToken);

        Assert.True(E2EBoolTool.Captured);
    }

    [Fact]
    public async Task Wrapper_BoolParam_RejectsNumericLiteral()
    {
        E2EBoolTool.Captured = null;
        var fn = ResolveFunction<E2EBoolTool>(nameof(E2EBoolTool.SetFlag));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fn.InvokeAsync(
                Args("flag", Parse("1")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapper_StringArrayParam_AcceptsArrayOfNumbers()
    {
        E2EStringArrayTool.Captured = null;
        var fn = ResolveFunction<E2EStringArrayTool>(nameof(E2EStringArrayTool.Tag));

        await fn.InvokeAsync(
            Args("tags", Parse("[1,2,3]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EStringArrayTool.Captured);
        Assert.Equal(["1", "2", "3"], E2EStringArrayTool.Captured);
    }

    [Fact]
    public async Task Wrapper_ObjectArrayParam_ExtractsTypicalShape()
    {
        E2EObjectArrayTool.Captured = null;
        var fn = ResolveFunction<E2EObjectArrayTool>(nameof(E2EObjectArrayTool.Save));

        await fn.InvokeAsync(
            Args("entries", Parse("[{\"name\":\"alpha\",\"count\":3}]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EObjectArrayTool.Captured);
        var single = Assert.Single(E2EObjectArrayTool.Captured!);
        Assert.Equal("alpha", single.Name);
        Assert.Equal(3, single.Count);
    }

    [Fact]
    public async Task Wrapper_ObjectArrayParam_StringPropertyAcceptsArrayLiteral()
    {
        E2EObjectArrayTool.Captured = null;
        var fn = ResolveFunction<E2EObjectArrayTool>(nameof(E2EObjectArrayTool.Save));

        await fn.InvokeAsync(
            Args("entries", Parse("[{\"name\":[1,2],\"count\":3}]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EObjectArrayTool.Captured);
        var single = Assert.Single(E2EObjectArrayTool.Captured!);
        Assert.Equal("[1,2]", single.Name);
        Assert.Equal(3, single.Count);
    }
}
