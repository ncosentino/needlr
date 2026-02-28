---
description: Integrate AI agent frameworks with Needlr in .NET -- automatic function discovery, DI wiring, and lifecycle management for Semantic Kernel and Microsoft Agent Framework.
---

# AI Integrations

Needlr provides first-class integrations for AI agent frameworks, taking care of function discovery, DI wiring, and factory lifecycle so that you focus on writing agent logic rather than plumbing.

Two integrations are supported:

- **Microsoft Agent Framework** (`NexusLabs.Needlr.AgentFramework`) — for `[AgentFunction]`-annotated tools wired into `AIAgent` instances via `Microsoft.Extensions.AI`
- **Semantic Kernel** (`NexusLabs.Needlr.SemanticKernel`) — for `[KernelFunction]`-annotated plugin classes wired into a `Kernel` via `Microsoft.SemanticKernel`

Both integrations follow the same two-layer architecture that is explained below.

---

## The Two-Layer Model

Understanding what Needlr owns vs. what the upstream framework owns is the key to understanding which parts are AOT-compatible and which are not.

### Layer 1 — Discovery (Needlr)

**What**: Identifying which types in an assembly contain annotated methods.

For MAF this means finding classes with `[AgentFunction]` methods. For SK this means finding classes with `[KernelFunction]` methods. This is purely a type-collection step — no instances are created, no schemas are built.

Needlr provides two paths for this layer:

| Path | How | AOT safe? |
|---|---|---|
| **Source generation** | At compile time, a Roslyn generator scans for the attribute and emits a static `IReadOnlyList<Type>` | ✅ Yes |
| **Reflection** | At runtime, assemblies are scanned for the attribute | ❌ No (`[RequiresUnreferencedCode]`) |

The reflection overloads (`AddAgentFunctionsFromAssemblies()`, `AddSemanticKernelPluginsFromAssemblies()`, etc.) are annotated with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` to surface this at the call site.

### Layer 2 — Instantiation (upstream framework)

**What**: Taking the discovered types and turning them into actual tool objects with JSON schemas that the LLM understands.

For MAF this is `AIFunctionFactory.Create(MethodInfo, target)` from `Microsoft.Extensions.AI`. For SK this is `KernelPlugin.CreateFromObject(instance)` from `Microsoft.SemanticKernel`. Both use reflection internally to build JSON schemas from method signatures.

**This layer is not controlled by Needlr.** Both MAF and SK use reflection here regardless of which Needlr discovery path you choose. Neither `Microsoft.Extensions.AI` nor `Microsoft.SemanticKernel` are fully AOT-safe for tool/plugin schema generation at this time.

### What this means in practice

If you use the source gen path, you eliminate reflection from Layer 1 (Needlr's responsibility). You do not eliminate reflection from Layer 2 (the upstream framework's responsibility). The practical effect is:

- No `[RequiresUnreferencedCode]` warnings from Needlr's own code
- Faster startup (no runtime assembly scanning)
- The upstream framework may still emit its own reflection-related warnings

If full AOT support is important to you, watch the upstream framework's own AOT roadmap — Needlr will update its Layer 1 surface to match as those paths become available.

---

## Microsoft Agent Framework

### Packages

```xml
<!-- Runtime -->
<PackageReference Include="NexusLabs.Needlr.AgentFramework" />

<!-- Source generator (add as analyzer — no runtime dep) -->
<PackageReference Include="NexusLabs.Needlr.AgentFramework.Generators"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### Quick start

```csharp
using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

internal sealed class WeatherTools
{
    [AgentFunction]
    [Description("Gets the current temperature for a city.")]
    public string GetTemperature(string city) => $"22°C in {city}";
}

var agentFactory = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(sp => sp.GetRequiredService<IChatClient>())
        .AddAgentFunctionsFromAssemblies())   // reflection path
    .BuildServiceProvider(configuration)
    .GetRequiredService<IAgentFactory>();

var agent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are a helpful weather assistant.";
    opts.FunctionTypes = [typeof(WeatherTools)];
});
```

### Source gen path (recommended)

When `NexusLabs.Needlr.AgentFramework.Generators` is referenced as an analyzer, it runs at compile time and emits a class in your assembly's namespace:

```csharp
// Generated: AgentFrameworkFunctionRegistry.g.cs
namespace YourAssemblyName.Generated;

public static class AgentFrameworkFunctionRegistry
{
    public static IReadOnlyList<Type> AllFunctionTypes { get; } = new Type[]
    {
        typeof(WeatherTools),
        // ... all other types with [AgentFunction] methods
    };
}
```

Pass this to `AddAgentFunctionsFromGenerated` instead of the assembly scanning overload:

```csharp
.UsingAgentFramework(af => af
    .UsingChatClient(sp => sp.GetRequiredService<IChatClient>())
    .AddAgentFunctionsFromGenerated(
        YourAssemblyName.Generated.AgentFrameworkFunctionRegistry.AllFunctionTypes))
```

`AddAgentFunctionsFromGenerated` carries no `[RequiresUnreferencedCode]` annotation — it is the AOT-safe discovery path.

### Per-agent tool scoping

Multiple agents can be created from the same `IAgentFactory`, each with a tailored subset of the registered tools:

```csharp
// Agent 1: geography tools only
var geographyAgent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are a geography expert.";
    opts.FunctionTypes = [typeof(GeographyFunctions)];
});

// Agent 2: no tools (pure reasoning)
var writerAgent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are a technical writer.";
    opts.FunctionTypes = [];
});

// Agent 3: all registered tools (default when FunctionTypes is null)
var generalAgent = agentFactory.CreateAgent();
```

`FunctionTypes = null` means all registered types are available. `FunctionTypes = []` means no tools.

---

## Semantic Kernel

### Packages

```xml
<!-- Runtime -->
<PackageReference Include="NexusLabs.Needlr.SemanticKernel" />

<!-- Source generator (add as analyzer — no runtime dep) -->
<PackageReference Include="NexusLabs.Needlr.SemanticKernel.Generators"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### Quick start

```csharp
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.SemanticKernel;

internal sealed class WeatherPlugin
{
    [KernelFunction]
    [Description("Gets the current temperature for a city.")]
    public string GetTemperature(string city) => $"22°C in {city}";
}

var kernelFactory = new Syringe()
    .UsingReflection()
    .UsingSemanticKernel(sk => sk
        .Configure(opts => opts.KernelBuilderFactory = sp =>
            Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey))
        .AddSemanticKernelPluginsFromAssemblies())   // reflection path
    .BuildServiceProvider(configuration)
    .GetRequiredService<IKernelFactory>();

var kernel = kernelFactory.CreateKernel();
var result = await kernel.InvokePromptAsync("What is the weather today?");
```

### Source gen path (recommended)

When `NexusLabs.Needlr.SemanticKernel.Generators` is referenced as an analyzer, it emits a compile-time registry:

```csharp
// Generated: SemanticKernelPlugins.g.cs
namespace YourAssemblyName.Generated;

public static class KernelPluginRegistry
{
    public static IReadOnlyList<(Type PluginType, bool IsStatic)> Entries { get; } = new (Type, bool)[]
    {
        (typeof(WeatherPlugin), false),
        // ...
    };
}
```

Pass this to `AddSemanticKernelPluginsFromGenerated`:

```csharp
.UsingSemanticKernel(sk => sk
    .Configure(opts => opts.KernelBuilderFactory = sp => ...)
    .AddSemanticKernelPluginsFromGenerated(
        YourAssemblyName.Generated.KernelPluginRegistry.Entries))
```

---

## Multi-Agent Orchestration

Needlr extends the IoC principle from the tool layer upward to the agent and topology layers. Agents are declared as plain C# classes with attributes; Needlr discovers them, builds the workflow graph, and emits source-generated factory methods. Adding a new agent role means adding a class, not editing orchestration wiring.

### Packages

```xml
<!-- Runtime: agents, workflows, termination conditions -->
<PackageReference Include="NexusLabs.Needlr.AgentFramework" />
<PackageReference Include="NexusLabs.Needlr.AgentFramework.Workflows" />

<!-- Source generator (analyzer — no runtime dep) -->
<PackageReference Include="NexusLabs.Needlr.AgentFramework.Generators"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />

<!-- Roslyn analyzers (optional but recommended) -->
<PackageReference Include="NexusLabs.Needlr.AgentFramework.Analyzers"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### Declaring agents

The `[NeedlrAiAgent]` attribute marks a class as a registered agent. It accepts `Instructions`, `Description`, `FunctionTypes`, and `FunctionGroups` to configure the agent's identity and tool access.

```csharp
[NeedlrAiAgent(
    Instructions = "You are a geography expert. Answer questions about countries and cities.",
    Description  = "Handles geographic queries")]
public class GeographyAgent { }

[NeedlrAiAgent(
    Instructions = "You are a travel writer. Produce engaging summaries.",
    FunctionGroups = ["travel"])]           // scoped to the "travel" function group
public class TravelWriterAgent { }

[NeedlrAiAgent(
    Instructions = "You triage requests and route them.",
    FunctionTypes = [])]                    // empty array → no tools (pure routing)
public class TriageAgent { }
```

Needle discovers all `[NeedlrAiAgent]` classes in the compilation and emits a static agent registry at build time.

### Function groups

`[AgentFunctionGroup]` buckets related tool methods under a named group. Agents reference groups by name via `FunctionGroups`, keeping tool scoping declarative and typo-detectable ([NDLRMAF005](analyzers/NDLRMAF005.md)).

```csharp
[AgentFunctionGroup("geography")]
internal sealed class GeographyFunctions
{
    [AgentFunction]
    [Description("Returns countries the user has lived in.")]
    public IReadOnlyList<string> GetCountriesLived() => ["Canada", "USA"];

    [AgentFunction]
    [Description("Returns the user's favourite cities.")]
    public IReadOnlyList<string> GetFavoriteCities() => ["Toronto", "New York"];
}
```

Scoping rules:

| `FunctionTypes` | `FunctionGroups` | Tools agent receives |
|---|---|---|
| `null` | `null` | All registered types (default) |
| `[]` (empty) | — | No tools |
| `[typeof(T)]` | — | Only `T` |
| — | `["group"]` | All types in the named group |

### Topology types

Needlr supports three topology patterns. Each is declared with attributes; the source generator emits a corresponding typed factory method on `IWorkflowFactory`.

#### Handoff

One agent handles a request and optionally routes it to another agent when a condition is met. The routing decision is made by the LLM at runtime; the `When` parameter describes the condition as a natural language hint for the model.

```csharp
[NeedlrAiAgent(Instructions = "Triage the request and hand off.")]
[AgentHandoffsTo(typeof(GeographyAgent), When = "The question is about geography")]
[AgentHandoffsTo(typeof(TravelWriterAgent), When = "The question is about travel writing")]
public class TriageAgent { }
```

Generator emits: `factory.CreateTriageAgentHandoffWorkflow()`

#### Group chat

Multiple agents collaborate in a shared round-robin conversation. All participants are peers; the workflow runs for up to `maxIterations` turns unless a termination condition triggers earlier.

```csharp
[NeedlrAiAgent(Instructions = "Review code for correctness.")]
[AgentGroupChatMember("code-review")]
public class ReviewerAgent { }

[NeedlrAiAgent(Instructions = "Author code changes based on review feedback.")]
[AgentGroupChatMember("code-review")]
public class AuthorAgent { }
```

Generator emits: `factory.CreateCodeReviewGroupChatWorkflow()`

A group chat requires at least two members ([NDLRMAF002](analyzers/NDLRMAF002.md)).

#### Sequential pipeline

Agents run in a fixed order, each receiving the prior agent's output. Use `Order` to control the sequence.

```csharp
[NeedlrAiAgent(Instructions = "Extract key facts from the source material.")]
[AgentSequenceMember("content-pipeline", Order = 1)]
public class ContentExtractorAgent { }

[NeedlrAiAgent(Instructions = "Enrich the extracted facts with examples.")]
[AgentSequenceMember("content-pipeline", Order = 2)]
public class ContentEnricherAgent { }

[NeedlrAiAgent(Instructions = "Publish the enriched content.")]
[AgentSequenceMember("content-pipeline", Order = 3)]
public class ContentPublisherAgent { }
```

Generator emits: `factory.CreateContentPipelineSequentialWorkflow()`

### Running workflows

Obtain an `IWorkflowFactory` from DI and use the generated extension methods. The factory resolves and wires agents automatically.

```csharp
var factory = serviceProvider.GetRequiredService<IWorkflowFactory>();

var workflow = factory.CreateTriageAgentHandoffWorkflow();
var responses = await workflow.RunAsync("Which countries has Nick visited?");
// responses: IReadOnlyDictionary<string, string> (agentId → text)
```

`RunAsync` is an extension method from `NexusLabs.Needlr.AgentFramework.Workflows` that wraps the underlying MAF streaming execution.

### Termination conditions

Termination conditions let you stop a workflow early when a content-based criterion is met. Two layers are available:

**Layer 1 — group chat (per-agent, fires before the next turn):**
`[AgentTerminationCondition]` on a group chat member. The condition is evaluated inside MAF's group chat loop after each agent response. When it triggers, the current turn is the last one — the next agent is never called.

```csharp
[AgentGroupChatMember("code-review")]
[AgentTerminationCondition(typeof(KeywordTerminationCondition), "APPROVED")]
public class ReviewerAgent { }
```

**Layer 2 — workflow-level (fires after a response is fully emitted):**
`[WorkflowRunTerminationCondition]` on any agent. The condition is evaluated in Needlr's `RunAsync` event loop after the agent's complete response is received. Works for all topology types; for group chat, prefer Layer 1 ([NDLRMAF011](analyzers/NDLRMAF011.md)).

```csharp
[AgentSequenceMember("content-pipeline", Order = 1)]
[WorkflowRunTerminationCondition(typeof(KeywordTerminationCondition), "EXTRACTION_FAILED")]
public class ContentExtractorAgent { }
```

When an agent carries `[WorkflowRunTerminationCondition]`, the generator emits a paired `Run*Async` method that packages creation and execution together with the declared conditions already wired in:

```csharp
// Generated Run*Async bundles conditions automatically
var responses = await factory.RunContentPipelineSequentialWorkflowAsync(message);
```

Built-in conditions: `KeywordTerminationCondition`, `RegexTerminationCondition`. Custom conditions implement `IWorkflowTerminationCondition`.

### Topology graph diagnostic

Set `NeedlrDiagnostics=true` in the agent class library's project properties to emit a Mermaid diagram of the agent topology at build time:

```xml
<PropertyGroup>
  <NeedlrDiagnostics>true</NeedlrDiagnostics>
</PropertyGroup>
```

The diagram is written to `bin/{Configuration}/{TFM}/NeedlrDiagnostics/AgentTopologyGraph.md` and visualises all declared handoff, group chat, and sequential topologies in the compilation.

---

## Philosophy: IoC for AI Components

Needlr applies the same inversion-of-control principle to AI components that a DI container applies to services. Rather than manually creating tool objects and passing them to an agent, you declare what a method is (`[AgentFunction]` / `[KernelFunction]`) and Needlr assembles the tools automatically. The component declares itself; the framework wires it.

This principle now applies at three layers:

- **Tool layer**: methods are discovered, schema-built, and injected into the right agent or kernel instance
- **Agent layer**: agent definitions (instructions, tool groups, name) are declared as types, auto-discovered, and instantiated from the registry — the same pattern `[AgentFunction]` establishes, one level up
- **Topology layer**: relationships between agents are declared as attributes; Needlr builds the orchestration graph and emits typed factory methods automatically

Adding a new agent role to a system means adding a class. Adding it to a topology means adding an attribute. The orchestration wiring is owned by the framework, not the application — the same promise Needlr delivers for services.
