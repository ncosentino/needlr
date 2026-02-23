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

## Philosophy: IoC for AI Components

Needlr applies the same inversion-of-control principle to AI components that a DI container applies to services. Rather than manually creating tool objects and passing them to an agent, you declare what a method is (`[AgentFunction]` / `[KernelFunction]`) and Needlr assembles the tools automatically. The component declares itself; the framework wires it.

Today this applies at the **tool layer**: methods are discovered, schema-built, and injected into the right agent or kernel instances.

The natural extension of this principle goes upward:

- **Agent layer**: Agent definitions (instructions, tool groups, name) declared as types, auto-discovered and instantiated from the registry — the same pattern `[AgentFunction]` already establishes, just one level up
- **Topology layer**: Relationships between agents declared as attributes — which agent hands off to which, which agents form a group chat — with Needlr building the orchestration graph automatically

These represent the direction Needlr's AI integration is heading: from IoC for tools, to IoC for agents, to IoC for multi-agent topologies. Adding a new agent role to a system would eventually mean adding a class, not touching orchestration setup code — the same promise Needlr already delivers for services.
