using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Testing;

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
/// <para>
/// Function resolution flows through <see cref="ToolInvocationRunner"/>'s public surface: the
/// same path downstream consumers use to write tool-level tests against their own
/// <c>[AgentFunction]</c>-decorated tools.
/// </para>
/// </remarks>
public class AIFunctionWrapperEndToEndTests
{
    private static AIFunction ResolveFunction<TTool>(string methodName, object capture)
        where TTool : class
    {
        var runner = ToolInvocationRunner.CreateFor<TTool>(s => s.AddSingleton(capture.GetType(), capture));
        runner.AssertGeneratedProviderAvailable();
        return runner.GetFunction<TTool>(methodName);
    }

    private static AIFunctionArguments Args(string key, JsonElement value) =>
        new() { [key] = value };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Wrapper_StringParam_AcceptsArrayLiteral()
    {
        var capture = new E2EStringTool.Capture();
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record), capture);

        await fn.InvokeAsync(
            Args("findingsJson", Parse("[{\"severity\":\"Warning\"}]")),
            TestContext.Current.CancellationToken);

        Assert.Equal("[{\"severity\":\"Warning\"}]", capture.Value);
    }

    [Fact]
    public async Task Wrapper_StringParam_AcceptsObjectLiteral()
    {
        var capture = new E2EStringTool.Capture();
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record), capture);

        await fn.InvokeAsync(
            Args("findingsJson", Parse("{\"k\":1}")),
            TestContext.Current.CancellationToken);

        Assert.Equal("{\"k\":1}", capture.Value);
    }

    [Fact]
    public async Task Wrapper_StringParam_RequiredNullJson_ThrowsArgumentException()
    {
        var capture = new E2EStringTool.Capture();
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record), capture);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await fn.InvokeAsync(
                Args("findingsJson", Parse("null")),
                TestContext.Current.CancellationToken));

        Assert.Contains("Required argument", ex.Message);
        Assert.Contains("findingsJson", ex.Message);
    }

    [Fact]
    public async Task Wrapper_StringParam_AcceptsStringLiteralAsIs()
    {
        var capture = new E2EStringTool.Capture();
        var fn = ResolveFunction<E2EStringTool>(nameof(E2EStringTool.Record), capture);

        await fn.InvokeAsync(
            Args("findingsJson", Parse("\"plain text\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal("plain text", capture.Value);
    }

    [Fact]
    public async Task Wrapper_IntParam_AcceptsNumericString()
    {
        var capture = new E2EIntTool.Capture();
        var fn = ResolveFunction<E2EIntTool>(nameof(E2EIntTool.SetMax), capture);

        await fn.InvokeAsync(
            Args("max", Parse("\"42\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(42, capture.Value);
    }

    [Fact]
    public async Task Wrapper_IntParam_AcceptsNumberLiteral()
    {
        var capture = new E2EIntTool.Capture();
        var fn = ResolveFunction<E2EIntTool>(nameof(E2EIntTool.SetMax), capture);

        await fn.InvokeAsync(
            Args("max", Parse("42")),
            TestContext.Current.CancellationToken);

        Assert.Equal(42, capture.Value);
    }

    [Fact]
    public async Task Wrapper_BoolParam_AcceptsStringTrue()
    {
        var capture = new E2EBoolTool.Capture();
        var fn = ResolveFunction<E2EBoolTool>(nameof(E2EBoolTool.SetFlag), capture);

        await fn.InvokeAsync(
            Args("flag", Parse("\"true\"")),
            TestContext.Current.CancellationToken);

        Assert.True(capture.Value);
    }

    [Fact]
    public async Task Wrapper_BoolParam_RejectsNumericLiteral()
    {
        var capture = new E2EBoolTool.Capture();
        var fn = ResolveFunction<E2EBoolTool>(nameof(E2EBoolTool.SetFlag), capture);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fn.InvokeAsync(
                Args("flag", Parse("1")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapper_StringArrayParam_AcceptsArrayOfNumbers()
    {
        var capture = new E2EStringArrayTool.Capture();
        var fn = ResolveFunction<E2EStringArrayTool>(nameof(E2EStringArrayTool.Tag), capture);

        await fn.InvokeAsync(
            Args("tags", Parse("[1,2,3]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal(["1", "2", "3"], capture.Value);
    }

    [Fact]
    public async Task Wrapper_ObjectArrayParam_ExtractsTypicalShape()
    {
        var capture = new E2EObjectArrayTool.Capture();
        var fn = ResolveFunction<E2EObjectArrayTool>(nameof(E2EObjectArrayTool.Save), capture);

        await fn.InvokeAsync(
            Args("entries", Parse("[{\"name\":\"alpha\",\"count\":3}]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        var single = Assert.Single(capture.Value!);
        Assert.Equal("alpha", single.Name);
        Assert.Equal(3, single.Count);
    }

    [Fact]
    public async Task Wrapper_ObjectArrayParam_StringPropertyAcceptsArrayLiteral()
    {
        var capture = new E2EObjectArrayTool.Capture();
        var fn = ResolveFunction<E2EObjectArrayTool>(nameof(E2EObjectArrayTool.Save), capture);

        await fn.InvokeAsync(
            Args("entries", Parse("[{\"name\":[1,2],\"count\":3}]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        var single = Assert.Single(capture.Value!);
        Assert.Equal("[1,2]", single.Name);
        Assert.Equal(3, single.Count);
    }

    [Fact]
    public async Task Wrapper_GuidParam_AcceptsStringLiteral()
    {
        var capture = new E2EGuidTool.Capture();
        var fn = ResolveFunction<E2EGuidTool>(nameof(E2EGuidTool.Record), capture);

        await fn.InvokeAsync(
            Args("id", Parse("\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), capture.Value);
    }

    [Fact]
    public async Task Wrapper_GuidParam_RejectsInvalidGuidString()
    {
        var capture = new E2EGuidTool.Capture();
        var fn = ResolveFunction<E2EGuidTool>(nameof(E2EGuidTool.Record), capture);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fn.InvokeAsync(
                Args("id", Parse("\"not-a-guid\"")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapper_DateTimeParam_AcceptsIso8601String()
    {
        var capture = new E2EDateTimeTool.Capture();
        var fn = ResolveFunction<E2EDateTimeTool>(nameof(E2EDateTimeTool.Record), capture);

        await fn.InvokeAsync(
            Args("when", Parse("\"2026-05-05T18:30:00Z\"")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal(2026, capture.Value!.Value.Year);
        Assert.Equal(5, capture.Value!.Value.Month);
        Assert.Equal(5, capture.Value!.Value.Day);
    }

    [Fact]
    public async Task Wrapper_DateTimeOffsetParam_PreservesOffset()
    {
        var capture = new E2EDateTimeOffsetTool.Capture();
        var fn = ResolveFunction<E2EDateTimeOffsetTool>(nameof(E2EDateTimeOffsetTool.Record), capture);

        await fn.InvokeAsync(
            Args("stamp", Parse("\"2026-05-05T18:30:00+05:00\"")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal(TimeSpan.FromHours(5), capture.Value!.Value.Offset);
    }

    [Fact]
    public async Task Wrapper_TimeSpanParam_AcceptsDotNetFormat()
    {
        var capture = new E2ETimeSpanTool.Capture();
        var fn = ResolveFunction<E2ETimeSpanTool>(nameof(E2ETimeSpanTool.Record), capture);

        await fn.InvokeAsync(
            Args("duration", Parse("\"01:30:00\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(90), capture.Value);
    }

    [Fact]
    public async Task Wrapper_TimeSpanParam_AcceptsIso8601Duration()
    {
        var capture = new E2ETimeSpanTool.Capture();
        var fn = ResolveFunction<E2ETimeSpanTool>(nameof(E2ETimeSpanTool.Record), capture);

        await fn.InvokeAsync(
            Args("duration", Parse("\"PT1H30M\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(90), capture.Value);
    }

    [Fact]
    public async Task Wrapper_TimeSpanParam_RejectsInvalidString()
    {
        var capture = new E2ETimeSpanTool.Capture();
        var fn = ResolveFunction<E2ETimeSpanTool>(nameof(E2ETimeSpanTool.Record), capture);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fn.InvokeAsync(
                Args("duration", Parse("\"abc\"")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Wrapper_ObjectArrayParam_TemporalPropertiesExtractCorrectly()
    {
        var capture = new E2ETemporalArrayTool.Capture();
        var fn = ResolveFunction<E2ETemporalArrayTool>(nameof(E2ETemporalArrayTool.Record), capture);

        await fn.InvokeAsync(
            Args("entries", Parse(
                "[{\"id\":\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"," +
                "\"when\":\"2026-05-05T18:30:00Z\"," +
                "\"duration\":\"PT1H30M\"}]")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        var single = Assert.Single(capture.Value!);
        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), single.Id);
        Assert.Equal(2026, single.When.Year);
        Assert.Equal(TimeSpan.FromMinutes(90), single.Duration);
    }

    [Fact]
    public async Task Wrapper_DtoParam_ExtractsAllPropertiesFromObjectLiteral()
    {
        var capture = new E2EDtoTool.Capture();
        var fn = ResolveFunction<E2EDtoTool>(nameof(E2EDtoTool.Record), capture);

        await fn.InvokeAsync(
            Args("metadata", Parse(
                "{\"source\":\"news-feed\"," +
                "\"priority\":3," +
                "\"correlationId\":\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"," +
                "\"when\":\"2026-05-05T18:30:00Z\"}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal("news-feed", capture.Value!.Source);
        Assert.Equal(3, capture.Value.Priority);
        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), capture.Value.CorrelationId);
        Assert.Equal(2026, capture.Value.When.Year);
    }

    [Fact]
    public async Task Wrapper_DtoParam_HandlesPartialJson()
    {
        var capture = new E2EDtoTool.Capture();
        var fn = ResolveFunction<E2EDtoTool>(nameof(E2EDtoTool.Record), capture);

        // Model omits some properties — the wrapper must populate the present ones
        // and leave the rest at their default (no exception, no null reference).
        await fn.InvokeAsync(
            Args("metadata", Parse("{\"source\":\"partial\"}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal("partial", capture.Value!.Source);
        Assert.Equal(0, capture.Value.Priority);
        Assert.Equal(Guid.Empty, capture.Value.CorrelationId);
        Assert.Equal(default, capture.Value.When);
    }

    [Fact]
    public async Task Wrapper_DtoParam_PropertyKindCoercionWorks()
    {
        var capture = new E2EDtoTool.Capture();
        var fn = ResolveFunction<E2EDtoTool>(nameof(E2EDtoTool.Record), capture);

        // Model sends 'priority' as a numeric String instead of a Number — the per-property
        // helper coerces via int.TryParse, mirroring the top-level int parameter behavior.
        await fn.InvokeAsync(
            Args("metadata", Parse("{\"source\":\"x\",\"priority\":\"7\"}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal(7, capture.Value!.Priority);
    }

    [Fact]
    public async Task Wrapper_RequiredBool_KeyAbsent_ThrowsArgumentException()
    {
        var capture = new E2ERequiredBoolTool.Capture();
        var fn = ResolveFunction<E2ERequiredBoolTool>(nameof(E2ERequiredBoolTool.SetFlag), capture);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken));

        Assert.Contains("Required argument", ex.Message);
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public async Task Wrapper_RequiredBool_JsonNull_ThrowsArgumentException()
    {
        var capture = new E2ERequiredBoolTool.Capture();
        var fn = ResolveFunction<E2ERequiredBoolTool>(nameof(E2ERequiredBoolTool.SetFlag), capture);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await fn.InvokeAsync(
                Args("required", Parse("null")),
                TestContext.Current.CancellationToken));

        Assert.Contains("Required argument", ex.Message);
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public async Task Wrapper_RequiredBool_JsonTrue_PassesTrue()
    {
        var capture = new E2ERequiredBoolTool.Capture();
        var fn = ResolveFunction<E2ERequiredBoolTool>(nameof(E2ERequiredBoolTool.SetFlag), capture);

        await fn.InvokeAsync(
            Args("required", Parse("true")),
            TestContext.Current.CancellationToken);

        Assert.True(capture.Value);
    }

    [Fact]
    public async Task Wrapper_OptionalBoolFalse_KeyAbsent_PassesDefaultFalse()
    {
        var capture = new E2EOptionalBoolTool.Capture();
        var fn = ResolveFunction<E2EOptionalBoolTool>(nameof(E2EOptionalBoolTool.SetFlagDefaultFalse), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.False(capture.DefaultFalse);
    }

    [Fact]
    public async Task Wrapper_OptionalBoolFalse_JsonNull_PassesDefaultFalse()
    {
        var capture = new E2EOptionalBoolTool.Capture();
        var fn = ResolveFunction<E2EOptionalBoolTool>(nameof(E2EOptionalBoolTool.SetFlagDefaultFalse), capture);

        await fn.InvokeAsync(
            Args("flag", Parse("null")),
            TestContext.Current.CancellationToken);

        Assert.False(capture.DefaultFalse);
    }

    [Fact]
    public async Task Wrapper_OptionalBoolFalse_JsonUndefined_PassesDefaultFalse()
    {
        var capture = new E2EOptionalBoolTool.Capture();
        var fn = ResolveFunction<E2EOptionalBoolTool>(nameof(E2EOptionalBoolTool.SetFlagDefaultFalse), capture);

        var args = new AIFunctionArguments { ["flag"] = default(JsonElement) };
        await fn.InvokeAsync(args, TestContext.Current.CancellationToken);

        Assert.False(capture.DefaultFalse);
    }

    [Fact]
    public async Task Wrapper_OptionalBoolFalse_JsonTrue_PassesTrue()
    {
        var capture = new E2EOptionalBoolTool.Capture();
        var fn = ResolveFunction<E2EOptionalBoolTool>(nameof(E2EOptionalBoolTool.SetFlagDefaultFalse), capture);

        await fn.InvokeAsync(
            Args("flag", Parse("true")),
            TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultFalse);
    }

    [Fact]
    public async Task Wrapper_OptionalBoolTrue_KeyAbsent_PassesDefaultTrue()
    {
        var capture = new E2EOptionalBoolTool.Capture();
        var fn = ResolveFunction<E2EOptionalBoolTool>(nameof(E2EOptionalBoolTool.SetFlagDefaultTrue), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultTrue);
    }

    [Fact]
    public async Task Wrapper_OptionalIntZero_KeyAbsent_PassesZero()
    {
        var capture = new E2EOptionalIntTool.Capture();
        var fn = ResolveFunction<E2EOptionalIntTool>(nameof(E2EOptionalIntTool.SetMaxDefaultZero), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(0, capture.DefaultZero);
    }

    [Fact]
    public async Task Wrapper_OptionalIntFive_KeyAbsent_PassesDefaultFive()
    {
        var capture = new E2EOptionalIntTool.Capture();
        var fn = ResolveFunction<E2EOptionalIntTool>(nameof(E2EOptionalIntTool.SetMaxDefaultFive), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(5, capture.DefaultFive);
    }

    [Fact]
    public async Task Wrapper_OptionalIntFive_JsonNull_PassesDefaultFive()
    {
        var capture = new E2EOptionalIntTool.Capture();
        var fn = ResolveFunction<E2EOptionalIntTool>(nameof(E2EOptionalIntTool.SetMaxDefaultFive), capture);

        await fn.InvokeAsync(
            Args("max", Parse("null")),
            TestContext.Current.CancellationToken);

        Assert.Equal(5, capture.DefaultFive);
    }

    [Fact]
    public async Task Wrapper_OptionalIntFive_JsonNumber_PassesNumber()
    {
        var capture = new E2EOptionalIntTool.Capture();
        var fn = ResolveFunction<E2EOptionalIntTool>(nameof(E2EOptionalIntTool.SetMaxDefaultFive), capture);

        await fn.InvokeAsync(
            Args("max", Parse("7")),
            TestContext.Current.CancellationToken);

        Assert.Equal(7, capture.DefaultFive);
    }

    [Fact]
    public async Task Wrapper_OptionalNullableStringNullDefault_KeyAbsent_PassesNull()
    {
        var capture = new E2EOptionalStringTool.Capture();
        var fn = ResolveFunction<E2EOptionalStringTool>(nameof(E2EOptionalStringTool.RecordDefaultNull), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultNullWasSet);
        Assert.Null(capture.DefaultNull);
    }

    [Fact]
    public async Task Wrapper_OptionalNullableStringNullDefault_JsonNull_PassesNull()
    {
        var capture = new E2EOptionalStringTool.Capture();
        var fn = ResolveFunction<E2EOptionalStringTool>(nameof(E2EOptionalStringTool.RecordDefaultNull), capture);

        await fn.InvokeAsync(
            Args("label", Parse("null")),
            TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultNullWasSet);
        Assert.Null(capture.DefaultNull);
    }

    [Fact]
    public async Task Wrapper_OptionalNullableStringNullDefault_JsonString_PassesString()
    {
        var capture = new E2EOptionalStringTool.Capture();
        var fn = ResolveFunction<E2EOptionalStringTool>(nameof(E2EOptionalStringTool.RecordDefaultNull), capture);

        await fn.InvokeAsync(
            Args("label", Parse("\"x\"")),
            TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultNullWasSet);
        Assert.Equal("x", capture.DefaultNull);
    }

    [Fact]
    public async Task Wrapper_OptionalNullableStringLiteralDefault_KeyAbsent_PassesLiteral()
    {
        var capture = new E2EOptionalStringTool.Capture();
        var fn = ResolveFunction<E2EOptionalStringTool>(nameof(E2EOptionalStringTool.RecordDefaultLiteral), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal("x", capture.DefaultLiteral);
    }

    [Fact]
    public async Task Wrapper_NullableIntNullDefault_KeyAbsent_PassesNull()
    {
        var capture = new E2ENullableIntTool.Capture();
        var fn = ResolveFunction<E2ENullableIntTool>(nameof(E2ENullableIntTool.RecordDefaultNull), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultNullWasSet);
        Assert.Null(capture.DefaultNull);
    }

    [Fact]
    public async Task Wrapper_NullableIntNullDefault_JsonNumber_PassesNumber()
    {
        var capture = new E2ENullableIntTool.Capture();
        var fn = ResolveFunction<E2ENullableIntTool>(nameof(E2ENullableIntTool.RecordDefaultNull), capture);

        await fn.InvokeAsync(
            Args("offset", Parse("7")),
            TestContext.Current.CancellationToken);

        Assert.True(capture.DefaultNullWasSet);
        Assert.Equal(7, capture.DefaultNull);
    }

    [Fact]
    public async Task Wrapper_NullableIntFiveDefault_KeyAbsent_PassesDefaultFive()
    {
        var capture = new E2ENullableIntTool.Capture();
        var fn = ResolveFunction<E2ENullableIntTool>(nameof(E2ENullableIntTool.RecordDefaultFive), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(5, capture.DefaultFive);
    }

    [Fact]
    public async Task Wrapper_DtoParam_StringPropertyWithInitDefault_NullPayloadKeepsDefault()
    {
        var capture = new E2EDtoWithDefaultsTool.Capture();
        var fn = ResolveFunction<E2EDtoWithDefaultsTool>(nameof(E2EDtoWithDefaultsTool.Record), capture);

        await fn.InvokeAsync(
            Args("dto", Parse("{\"foo\":null}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal("default", capture.Value!.Foo);
    }

    [Fact]
    public async Task Wrapper_DtoParam_NullableStringProperty_NullPayloadResolvesToNull()
    {
        var capture = new E2EDtoWithDefaultsTool.Capture();
        var fn = ResolveFunction<E2EDtoWithDefaultsTool>(nameof(E2EDtoWithDefaultsTool.Record), capture);

        await fn.InvokeAsync(
            Args("dto", Parse("{\"bar\":null}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Null(capture.Value!.Bar);
    }

    [Fact]
    public async Task Wrapper_DtoParam_IntPropertyWithInitDefault_NullPayloadKeepsDefault()
    {
        var capture = new E2EDtoWithDefaultsTool.Capture();
        var fn = ResolveFunction<E2EDtoWithDefaultsTool>(nameof(E2EDtoWithDefaultsTool.Record), capture);

        await fn.InvokeAsync(
            Args("dto", Parse("{\"count\":null}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal(5, capture.Value!.Count);
    }

    [Fact]
    public async Task Wrapper_DtoParam_AllPropertiesPresent_PopulatesEverything()
    {
        var capture = new E2EDtoWithDefaultsTool.Capture();
        var fn = ResolveFunction<E2EDtoWithDefaultsTool>(nameof(E2EDtoWithDefaultsTool.Record), capture);

        await fn.InvokeAsync(
            Args("dto", Parse("{\"foo\":\"override\",\"bar\":\"present\",\"count\":42}")),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capture.Value);
        Assert.Equal("override", capture.Value!.Foo);
        Assert.Equal("present", capture.Value.Bar);
        Assert.Equal(42, capture.Value.Count);
    }

    [Fact]
    public async Task Wrapper_RequiredEnumParam_StringValue_ParsesToEnum()
    {
        var capture = new E2EEnumTool.Capture();
        var fn = ResolveFunction<E2EEnumTool>(nameof(E2EEnumTool.SetMode), capture);

        await fn.InvokeAsync(
            Args("mode", Parse("\"Write\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(E2EMode.Write, capture.Required);
    }

    [Fact]
    public async Task Wrapper_RequiredEnumParam_LowercaseString_ParsesCaseInsensitively()
    {
        var capture = new E2EEnumTool.Capture();
        var fn = ResolveFunction<E2EEnumTool>(nameof(E2EEnumTool.SetMode), capture);

        await fn.InvokeAsync(
            Args("mode", Parse("\"append\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(E2EMode.Append, capture.Required);
    }

    [Fact]
    public async Task Wrapper_RequiredEnumParam_MissingKey_ThrowsArgumentException()
    {
        var capture = new E2EEnumTool.Capture();
        var fn = ResolveFunction<E2EEnumTool>(nameof(E2EEnumTool.SetMode), capture);

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken));

        Assert.Contains("Required argument", ex.Message);
        Assert.Contains("mode", ex.Message);
    }

    [Fact]
    public async Task Wrapper_DefaultedEnumParam_KeyAbsent_FallsBackToDefaultMember()
    {
        var capture = new E2EEnumTool.Capture();
        var fn = ResolveFunction<E2EEnumTool>(nameof(E2EEnumTool.SetModeDefault), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(E2EMode.Append, capture.Default);
    }

    [Fact]
    public async Task Wrapper_DefaultedEnumParam_StringValue_ParsesToEnum()
    {
        var capture = new E2EEnumTool.Capture();
        var fn = ResolveFunction<E2EEnumTool>(nameof(E2EEnumTool.SetModeDefault), capture);

        await fn.InvokeAsync(
            Args("mode", Parse("\"Read\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(E2EMode.Read, capture.Default);
    }

    [Fact]
    public async Task Wrapper_DefaultedGuidParam_KeyAbsent_FallsBackToEmpty()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordGuid), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Empty, capture.Guid);
    }

    [Fact]
    public async Task Wrapper_DefaultedGuidParam_StringValue_ParsesGuid()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordGuid), capture);

        await fn.InvokeAsync(
            Args("id", Parse("\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"")),
            TestContext.Current.CancellationToken);

        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), capture.Guid);
    }

    [Fact]
    public async Task Wrapper_DefaultedDateTimeParam_KeyAbsent_FallsBackToMinValue()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordDateTime), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(default(DateTime), capture.DateTime);
    }

    [Fact]
    public async Task Wrapper_DefaultedDateTimeOffsetParam_KeyAbsent_FallsBackToMinValue()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordDateTimeOffset), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(default(DateTimeOffset), capture.DateTimeOffset);
    }

    [Fact]
    public async Task Wrapper_DefaultedTimeSpanParam_KeyAbsent_FallsBackToZero()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordTimeSpan), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.Zero, capture.TimeSpan);
    }

    [Fact]
    public async Task Wrapper_DefaultedDecimalParam_KeyAbsent_FallsBackToLiteral()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordDecimal), capture);

        await fn.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Equal(9.99m, capture.Decimal);
    }

    [Fact]
    public async Task Wrapper_DefaultedDecimalParam_NumericValue_ExtractsCorrectly()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        var fn = ResolveFunction<E2EDefaultedTemporalsTool>(nameof(E2EDefaultedTemporalsTool.RecordDecimal), capture);

        await fn.InvokeAsync(
            Args("price", Parse("19.95")),
            TestContext.Current.CancellationToken);

        Assert.Equal(19.95m, capture.Decimal);
    }
}
