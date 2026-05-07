---
description: Paved-path testing for [AgentFunction]-decorated tools. Use ToolInvocationRunner to exercise the source-generated AIFunction wrapper with seeded workspace state, zero hand-rolled DI, and the same code path FunctionInvokingChatClient takes in production.
---

# Testing Tool Integrations

When you write a Needlr tool — a method decorated with `[AgentFunction]`, typically grouped under `[AgentFunctionGroup("name")]` — the source generator emits an `AIFunction` wrapper that translates `AIFunctionArguments` JSON into typed C# parameters and invokes your method. **That wrapper is what production agents go through, not your method directly.**

A test that calls `tool.DoIt(...)` directly never exercises the generated wrapper. Bugs in argument extraction (null handling, JSON kind coercion, DTO property mapping) won't surface until a real LLM sends a payload your test never simulated.

This page covers the paved path for testing tools through the generated wrapper, without hand-rolling a service provider.

---

## When to use what

| You want to… | Use |
|---|---|
| Invoke one tool method and assert on its result, with seeded workspace state | [`ToolInvocationRunner`](#toolinvocationrunner) |
| Stress-test the generated `AIFunction` wrapper with raw `JsonElement` inputs | [`ToolInvocationRunner.GetFunction<TTool>(name)`](#wrapper-edge-case-tests) |
| Run a full multi-turn agent loop with a fake LLM | [`AgentScenarioRunner`](#full-agent-scenarios) |

For the "fake LLM" question specifically — see the [follow-up note](#scripted-llm-planned-follow-up) below.

---

## `ToolInvocationRunner`

`ToolInvocationRunner` lives in **`NexusLabs.Needlr.AgentFramework.Testing`**. It removes the boilerplate consumers used to write by hand:

- Build a service provider with the right registrations.
- Look up the source-generated `IAIFunctionProvider`.
- Find the right `AIFunction` by name.
- Construct `AIFunctionArguments`.
- Establish an ambient `IAgentExecutionContext` so the tool can read `accessor.Current.GetWorkspace()`.

### Minimal example

```csharp
using NexusLabs.Needlr.AgentFramework.Testing;

public sealed class GrepToolTests
{
    [Fact]
    public async Task Grep_FindsMatches()
    {
        var runner = ToolInvocationRunner
            .CreateFor<GrepTool>()
            .WithWorkspace(ws => ws.TryWriteFile("a.txt", "hi there"));

        var result = await runner.InvokeAsync<GrepTool>(
            nameof(GrepTool.GrepFiles),
            args =>
            {
                args["pattern"] = "hi";
                args["path"]    = "/";
            });

        result.AssertSuccess();
        result.AssertResultContains("a.txt");
    }
}
```

The runner:

1. Built a fresh `IServiceProvider` with `GrepTool` registered as a singleton plus the Needlr accessors (`IAgentExecutionContextAccessor`, `IAgentDiagnosticsAccessor`, `IAgentDiagnosticsWriter`).
2. Created an in-memory workspace, ran your seed action, and attached it to the execution context.
3. Resolved the source-generated `AIFunction` for `GrepTool.GrepFiles` via the `[ModuleInitializer]`-registered `IAIFunctionProvider`.
4. Built `AIFunctionArguments`, established a `BeginScope` for the duration of the call, invoked, and captured the result.

### Bring your own service provider

If your test fixture already builds a Syringe-based provider, hand it to the runner directly:

```csharp
var sp = new Syringe()
    .UsingReflection()
    .UsingAgentFramework()
    .BuildServiceProvider(config);

var runner = new ToolInvocationRunner(sp)
    .WithWorkspace(ws => ws.TryWriteFile("a.txt", "hi"));
```

Or build a minimal `IServiceCollection` yourself:

```csharp
using NexusLabs.Needlr.AgentFramework;

var sp = new ServiceCollection()
    .AddAgentFrameworkAccessors()
    .AddSingleton<GrepTool>()
    .AddSingleton<IMyDependency>(fakeDep)
    .BuildServiceProvider();

var runner = new ToolInvocationRunner(sp);
```

`AddAgentFrameworkAccessors()` is the new public extension that registers the small set of Needlr accessor singletons without dragging in the rest of the Agent Framework wiring.

### Immutability and lifetime

Every `With*` method returns a **new** runner instance:

```csharp
var baseRunner = ToolInvocationRunner.CreateFor<GrepTool>();
var withFiles  = baseRunner.WithWorkspace(ws => ws.TryWriteFile("a.txt", "hi"));
var withMore   = baseRunner.WithWorkspace(ws => ws.TryWriteFile("b.txt", "bye"));
```

`baseRunner`, `withFiles`, and `withMore` are independent. This matters because tools read ambient state — `IAgentExecutionContextAccessor.Current` is `AsyncLocal`-backed — and you don't want one test's seeded workspace leaking into the next. Each `InvokeAsync` call establishes its own `BeginScope` and disposes it before returning.

The runner also creates an `IServiceScope` per invocation when an `IServiceScopeFactory` is available, so tools that depend on scoped services (HTTP clients, tenant state, EF Core contexts) behave correctly.

### Surfacing the workspace for assertions

If the tool wrote to the workspace, the same workspace instance comes back on the result:

```csharp
var result = await runner
    .WithWorkspace(ws => ws.TryWriteFile("input.txt", "raw"))
    .InvokeAsync<TransformTool>(nameof(TransformTool.Transform), args =>
    {
        args["inputPath"]  = "input.txt";
        args["outputPath"] = "output.txt";
    });

result.AssertSuccess();
Assert.True(result.Workspace!.FileExists("output.txt"));
```

---

## Wrapper edge-case tests

When you specifically want to test the generated wrapper's argument-extraction behavior — what happens when an LLM sends `JsonValueKind.Null`, an array literal where the schema expects a string, a numeric literal where it expects a bool, etc. — go through `GetFunction` and call the `AIFunction` directly with crafted `AIFunctionArguments`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;

var runner = ToolInvocationRunner.CreateFor<MyTool>();
runner.AssertGeneratedProviderAvailable();

var fn = runner.GetFunction<MyTool>(nameof(MyTool.Record));

var args = new AIFunctionArguments
{
    ["findingsJson"] = JsonDocument.Parse("[{\"severity\":\"Warning\"}]").RootElement.Clone(),
};

await fn.InvokeAsync(args, TestContext.Current.CancellationToken);
```

This is the path Needlr uses internally to test wrapper behavior — see `NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests/AIFunctionWrapperEndToEndTests.cs`.

### Per-test source-gen scoping

Consumer test projects often contain many `[AgentFunction]` types. By default, `ToolInvocationRunner` resolves against the global generated provider — which sees every type the generator emitted. To limit the visible set for a single test, use `LimitToTools`:

```csharp
var runner = ToolInvocationRunner
    .CreateFor<GrepTool>()
    .LimitToTools(typeof(GrepTool));   // hides every other [AgentFunction] type for this runner

var fn = runner.GetFunction<GrepTool>(nameof(GrepTool.GrepFiles));
```

`LimitToTools` wraps `AgentFrameworkGeneratedBootstrap.BeginTestScope` for the duration of resolution. The override is `AsyncLocal`-scoped, so concurrent tests in different async flows don't interfere.

---

## Reflection fallback (advanced, not AOT)

If you're testing a tool in a project that does **not** have the Needlr Agent Framework source generator wired up, the default `GetFunction` throws with a clear error message pointing you at the fix. If you actually want to exercise the reflection-based discovery path on purpose, use the explicitly-named, AOT-incompatible variant:

```csharp
[RequiresUnreferencedCode("...")]   // applies because we crossed into the reflection branch
[Fact]
public void ReflectionFallbackPath()
{
    var runner = ToolInvocationRunner.CreateFor<MyTool>();

    var fn = runner.GetFunctionAllowingReflection<MyTool>(nameof(MyTool.DoIt));
    // ...
}
```

`GetFunctionAllowingReflection` prefers the generated provider when one exists for the type, falling back to `AIFunctionFactory.Create(MethodInfo, target)` when it doesn't. The reflection branch is annotated with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` because it is incompatible with NativeAOT.

---

## Full agent scenarios

For end-to-end tests of an agent's behavior — including system prompt, multi-turn LLM dialogue, tool calls, termination — use [`AgentScenarioRunner`](agent-functions.md) with a fake `IChatClient` wired through `AgentFrameworkSyringeExtensions.UsingChatClient(...)`:

```csharp
var sp = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af.UsingChatClient(myFakeChatClient))
    .BuildServiceProvider(config);

var runner = new AgentScenarioRunner(
    sp.GetRequiredService<IAgentFactory>(),
    sp.GetRequiredService<IAgentExecutionContextAccessor>(),
    sp.GetRequiredService<IAgentDiagnosticsAccessor>());

var result = await runner.RunAsync(myScenario);
```

---

## Scripted LLM (planned follow-up)

A reusable, fluent `ScriptedChatClient` that handles multi-turn dialogues (turn 1: tool call, turn 2: text response) and call recording is a planned follow-up for the Testing package — see [ADR-0002](adr/adr-0002-build-scriptedchatclient-locally.md).

Until then, agent-loop tests roll their own `IChatClient` fakes. The cleanest existing example in the Needlr codebase is `RecordingChatClient` in `NexusLabs.Needlr.AgentFramework.Evaluation.Tests` — it's a callback-based client with `CallCount`/`StreamingCallCount` recording that you can copy-paste into your own test project as a stop-gap.

---

## Anti-patterns to avoid

- **Calling `tool.DoIt(...)` directly in tests.** This bypasses the source-generated wrapper entirely. Bugs in argument extraction won't surface until production.
- **Hand-rolling `new ServiceCollection()` and registering only `IAgentExecutionContextAccessor` manually.** The implementation type is `internal` — use `AddAgentFrameworkAccessors()` instead.
- **Sharing a mutable `ToolInvocationRunner` across tests with shared state in `WithExecutionContext`.** The runner is immutable by design — every `With*` returns a new instance. If you find yourself reaching for a `Reset()` method, you're holding it wrong.
- **Asserting `IsGeneratedProviderAvailable == false` to verify the source generator didn't run.** The Needlr Agent Framework's own `[ModuleInitializer]` always registers a (possibly empty) provider when the assembly loads — so `IsGeneratedProviderAvailable` is `true` in any consumer process. Use `runner.GetFunction<TTool>(name)` instead and check whether *your specific tool* is resolvable.
