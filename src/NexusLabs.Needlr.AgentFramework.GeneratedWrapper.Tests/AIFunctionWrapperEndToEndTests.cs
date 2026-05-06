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

    [Fact]
    public async Task Wrapper_GuidParam_AcceptsStringLiteral()
    {
        E2EGuidTool.Captured = null;
        var fn = ResolveFunction<E2EGuidTool>(nameof(E2EGuidTool.Record));

        await fn.InvokeAsync(
            Args("id", Parse("\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), E2EGuidTool.Captured);
    }

    [Fact]
    public async Task Wrapper_GuidParam_RejectsInvalidGuidString()
    {
        var fn = ResolveFunction<E2EGuidTool>(nameof(E2EGuidTool.Record));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fn.InvokeAsync(
                Args("id", Parse("\"not-a-guid\"")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapper_DateTimeParam_AcceptsIso8601String()
    {
        E2EDateTimeTool.Captured = null;
        var fn = ResolveFunction<E2EDateTimeTool>(nameof(E2EDateTimeTool.Record));

        await fn.InvokeAsync(
            Args("when", Parse("\"2026-05-05T18:30:00Z\"")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EDateTimeTool.Captured);
        Assert.Equal(2026, E2EDateTimeTool.Captured!.Value.Year);
        Assert.Equal(5, E2EDateTimeTool.Captured!.Value.Month);
        Assert.Equal(5, E2EDateTimeTool.Captured!.Value.Day);
    }

    [Fact]
    public async Task Wrapper_DateTimeOffsetParam_PreservesOffset()
    {
        E2EDateTimeOffsetTool.Captured = null;
        var fn = ResolveFunction<E2EDateTimeOffsetTool>(nameof(E2EDateTimeOffsetTool.Record));

        await fn.InvokeAsync(
            Args("stamp", Parse("\"2026-05-05T18:30:00+05:00\"")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EDateTimeOffsetTool.Captured);
        Assert.Equal(TimeSpan.FromHours(5), E2EDateTimeOffsetTool.Captured!.Value.Offset);
    }

    [Fact]
    public async Task Wrapper_TimeSpanParam_AcceptsDotNetFormat()
    {
        E2ETimeSpanTool.Captured = null;
        var fn = ResolveFunction<E2ETimeSpanTool>(nameof(E2ETimeSpanTool.Record));

        await fn.InvokeAsync(
            Args("duration", Parse("\"01:30:00\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(90), E2ETimeSpanTool.Captured);
    }

    [Fact]
    public async Task Wrapper_TimeSpanParam_AcceptsIso8601Duration()
    {
        E2ETimeSpanTool.Captured = null;
        var fn = ResolveFunction<E2ETimeSpanTool>(nameof(E2ETimeSpanTool.Record));

        await fn.InvokeAsync(
            Args("duration", Parse("\"PT1H30M\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(90), E2ETimeSpanTool.Captured);
    }

    [Fact]
    public async Task Wrapper_TimeSpanParam_RejectsInvalidString()
    {
        var fn = ResolveFunction<E2ETimeSpanTool>(nameof(E2ETimeSpanTool.Record));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fn.InvokeAsync(
                Args("duration", Parse("\"abc\"")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapper_ObjectArrayParam_TemporalPropertiesExtractCorrectly()
    {
        E2ETemporalArrayTool.Captured = null;
        var fn = ResolveFunction<E2ETemporalArrayTool>(nameof(E2ETemporalArrayTool.Record));

        await fn.InvokeAsync(
            Args("entries", Parse(
                "[{\"id\":\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"," +
                "\"when\":\"2026-05-05T18:30:00Z\"," +
                "\"duration\":\"PT1H30M\"}]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2ETemporalArrayTool.Captured);
        var single = Assert.Single(E2ETemporalArrayTool.Captured!);
        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), single.Id);
        Assert.Equal(2026, single.When.Year);
        Assert.Equal(TimeSpan.FromMinutes(90), single.Duration);
    }

    [Fact]
    public async Task Wrapper_DtoParam_ExtractsAllPropertiesFromObjectLiteral()
    {
        E2EDtoTool.Captured = null;
        var fn = ResolveFunction<E2EDtoTool>(nameof(E2EDtoTool.Record));

        await fn.InvokeAsync(
            Args("metadata", Parse(
                "{\"source\":\"news-feed\"," +
                "\"priority\":3," +
                "\"correlationId\":\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"," +
                "\"when\":\"2026-05-05T18:30:00Z\"}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EDtoTool.Captured);
        Assert.Equal("news-feed", E2EDtoTool.Captured!.Source);
        Assert.Equal(3, E2EDtoTool.Captured.Priority);
        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), E2EDtoTool.Captured.CorrelationId);
        Assert.Equal(2026, E2EDtoTool.Captured.When.Year);
    }

    [Fact]
    public async Task Wrapper_DtoParam_HandlesPartialJson()
    {
        E2EDtoTool.Captured = null;
        var fn = ResolveFunction<E2EDtoTool>(nameof(E2EDtoTool.Record));

        // Model omits some properties — the wrapper must populate the present ones
        // and leave the rest at their default (no exception, no null reference).
        await fn.InvokeAsync(
            Args("metadata", Parse("{\"source\":\"partial\"}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EDtoTool.Captured);
        Assert.Equal("partial", E2EDtoTool.Captured!.Source);
        Assert.Equal(0, E2EDtoTool.Captured.Priority);
        Assert.Equal(Guid.Empty, E2EDtoTool.Captured.CorrelationId);
        Assert.Equal(default, E2EDtoTool.Captured.When);
    }

    [Fact]
    public async Task Wrapper_DtoParam_PropertyKindCoercionWorks()
    {
        E2EDtoTool.Captured = null;
        var fn = ResolveFunction<E2EDtoTool>(nameof(E2EDtoTool.Record));

        // Model sends 'priority' as a numeric String instead of a Number — the per-property
        // helper coerces via int.TryParse, mirroring the top-level int parameter behavior.
        await fn.InvokeAsync(
            Args("metadata", Parse("{\"source\":\"x\",\"priority\":\"7\"}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(E2EDtoTool.Captured);
        Assert.Equal(7, E2EDtoTool.Captured!.Priority);
    }
}
