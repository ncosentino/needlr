---
description: Run multi-step agentic workloads with O(n) token cost using Needlr's iterative agent loop -- workspace-driven prompt construction eliminates conversation history accumulation.
---

# Iterative Agent Loop

The **iterative agent loop** (`IIterativeAgentLoop`) is an alternative execution model for agentic LLM workloads that eliminates the O(n²) token accumulation inherent in `FunctionInvokingChatClient`'s conversation-history approach.

Instead of appending every tool call and result to a growing conversation, the iterative loop constructs a **fresh prompt each iteration** from workspace files. The workspace IS the memory — not the conversation.

---

## The Problem: O(n²) Token Cost

When an agent makes tool calls through `FunctionInvokingChatClient` (FIC), every call and result is appended to the conversation history. Each subsequent LLM call re-sends the entire history:

| LLM Call | Tokens Sent | Cumulative |
|----------|------------|------------|
| 1 | ~1,000 | 1,000 |
| 2 | ~2,000 | 3,000 |
| 3 | ~3,000 | 6,000 |
| ... | ... | ... |
| 30 | ~30,000 | **465,000** |

For complex agentic workloads (article writers, code generators, trip planners) this produces catastrophic token bills. A real-world article pipeline measured **2.26 million tokens** in a single writer stage.

## The Solution: Workspace-Driven Iterations

The iterative loop decouples agent memory from conversation history:

```
┌─────────────────────────────────────────────────┐
│                  Iteration N                     │
│                                                  │
│  1. PromptFactory reads workspace files          │
│  2. Builds fresh [system, user] messages         │
│  3. LLM responds with tool calls                 │
│  4. Tools execute, update workspace              │
│  5. (Optional) Results sent back to model        │
│  6. Iteration ends — conversation discarded      │
│                                                  │
│  Next iteration starts with a FRESH conversation │
└─────────────────────────────────────────────────┘
```

Each iteration's input token count is bounded by the workspace size, not the conversation history. Total cost grows **linearly** with work done.

---

## Quick Start

```csharp
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Iterative;
using NexusLabs.Needlr.Workflows;

// 1. Create a workspace (files = agent memory)
var workspace = new InMemoryWorkspace();
workspace.WriteFile("config.json", """{"topic": "async patterns in C#"}""");

// 2. Define tools
var tools = new List<AITool>
{
    AIFunctionFactory.Create((string query) =>
    {
        // search implementation
        return $"Results for: {query}";
    }, new AIFunctionFactoryOptions { Name = "search" }),
};

// 3. Configure the loop
var options = new IterativeLoopOptions
{
    LoopName = "article-writer",
    Instructions = "You are a technical writer. Use tools to research and write.",
    Tools = tools,
    PromptFactory = ctx =>
    {
        var config = ctx.Workspace.ReadFile("config.json");
        var article = ctx.Workspace.FileExists("article.md")
            ? ctx.Workspace.ReadFile("article.md")
            : "(not started)";
        return $"""
            Config: {config}
            Current article:
            {article}

            Continue working on the article.
            """;
    },
    MaxIterations = 10,
    IsComplete = ctx => ctx.Workspace.FileExists("done.txt"),
};

// 4. Run
var context = new IterativeContext { Workspace = workspace };
var result = await iterativeLoop.RunAsync(options, context);

Console.WriteLine($"Completed: {result.Succeeded}");
Console.WriteLine($"Iterations: {result.Iterations.Count}");
Console.WriteLine($"Total tokens: {result.Diagnostics?.AggregateTokenUsage.TotalTokens}");
```

---

## DI Registration

`IIterativeAgentLoop` is automatically registered when you use the Agent Framework syringe:

```csharp
var services = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(chatClient)
        .AddAgentFunctionGroupsFromAssemblies([typeof(MyTools).Assembly]))
    .BuildServiceProvider(configuration);

var loop = services.GetRequiredService<IIterativeAgentLoop>();
```

The loop depends on `IChatClientAccessor` (registered automatically). It also accepts optional dependencies that are injected when available:

| Optional Dependency | Purpose |
|---|---|
| `IAgentDiagnosticsWriter` | Publishes run diagnostics to `IAgentDiagnosticsAccessor` |
| `IAgentExecutionContextAccessor` | Bridges workspace to DI-resolved tools |

### Tool Resolution

Use `IAgentFactory.ResolveTools()` to get DI-wired tool instances instead of hand-wiring `AIFunctionFactory.Create()`:

```csharp
var agentFactory = services.GetRequiredService<IAgentFactory>();

// Resolve all tools in a function group
var tools = agentFactory.ResolveTools(opts =>
    opts.FunctionGroups = ["trip-planner"]);

// Or resolve all registered tools
var allTools = agentFactory.ResolveTools();
```

This resolves tool classes through DI, so constructor-injected services (like `IAgentExecutionContextAccessor`) are available inside tool methods.

---

## Configuration Reference

### IterativeLoopOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LoopName` | `string` | `"iterative-loop"` | Human-readable name for diagnostics |
| `Instructions` | `string` | *(required)* | System prompt — constant across iterations |
| `Tools` | `IReadOnlyList<AITool>` | *(required)* | Available tool functions |
| `PromptFactory` | `Func<IterativeContext, string>` | *(required)* | Builds the user message each iteration |
| `MaxIterations` | `int` | `25` | Hard stop — loop terminates after this many iterations |
| `IsComplete` | `Func<IterativeContext, bool>?` | `null` | Domain-specific termination predicate |
| `ToolResultMode` | `ToolResultMode` | `OneRoundTrip` | How tool results feed back within an iteration |
| `MaxToolRoundsPerIteration` | `int` | `5` | Safety valve for `MultiRound` mode |
| `OnIterationStart` | `Func<int, IterativeContext, Task>?` | `null` | Async callback fired before each iteration's prompt factory |
| `OnToolCall` | `Func<int, ToolCallResult, Task>?` | `null` | Async callback fired after each tool executes (includes iteration number) |
| `OnIterationEnd` | `Func<IterationRecord, Task>?` | `null` | Async callback fired after each iteration completes |
| `ExecutionContext` | `IAgentExecutionContext?` | `null` | Explicit execution context (auto-created from workspace if omitted) |

### ToolResultMode

| Mode | LLM Calls / Iteration | Description |
|------|----------------------|-------------|
| `SingleCall` | 1 | Tool results are NOT sent back. Stored in `LastToolResults` for the next iteration's prompt factory. Maximum cost control. |
| `OneRoundTrip` | ≤ 2 | Tool results sent back once. Model gets one follow-up chance. **Recommended default.** |
| `MultiRound` | ≤ `MaxToolRoundsPerIteration + 1` | Tool results sent back repeatedly until model stops requesting tools. Bounded by `MaxToolRoundsPerIteration`. |

---

## The Prompt Factory

The prompt factory is the core extensibility point. It receives an `IterativeContext` with:

- **`Workspace`** — the `IWorkspace` containing all agent files
- **`LastToolResults`** — results from the previous iteration's tool calls (if any)
- **`Iteration`** — current iteration index (0-based)
- **`State`** — a `Dictionary<string, object?>` for arbitrary cross-iteration data

### Design principles

1. **Read workspace files** to understand current state — these are the agent's memory.
2. **Include only relevant context** — don't dump every file. Summarize or select.
3. **Guide the model** toward the next action — nudge, don't over-prescribe.
4. **Cap unbounded content** — if research notes grow, show only the last N entries.

### Example: phase-aware prompt

```csharp
PromptFactory = ctx =>
{
    var sb = new StringBuilder();
    var status = JsonSerializer.Deserialize<Status>(
        ctx.Workspace.ReadFile("status.json"));

    sb.AppendLine($"## Current State ({status.Phase} phase)");
    sb.AppendLine($"Budget remaining: ${status.BudgetRemaining}");

    if (ctx.Workspace.FileExists("itinerary.json"))
        sb.AppendLine($"Itinerary: {ctx.Workspace.ReadFile("itinerary.json")}");

    // Phase-specific nudges
    if (status.Phase == "research")
        sb.AppendLine(">>> Search for options before committing. <<<");
    else if (status.Phase == "fix")
        sb.AppendLine(">>> Validation failed. Fix the issues above. <<<");

    return sb.ToString();
}
```

---

## Lifecycle Hooks

The iterative loop provides async lifecycle hooks for real-time progress reporting. These are essential for scenarios like sending updates over SignalR or updating a UI.

```csharp
var options = new IterativeLoopOptions
{
    // ... other options ...

    OnIterationStart = async (iteration, ctx) =>
    {
        await hub.SendAsync("IterationStarted", iteration);
    },

    OnToolCall = async (iteration, toolResult) =>
    {
        await hub.SendAsync("ToolExecuted", new
        {
            Iteration = iteration,
            Tool = toolResult.FunctionName,
            Succeeded = toolResult.Succeeded,
        });
    },

    OnIterationEnd = async (record) =>
    {
        await hub.SendAsync("IterationCompleted", new
        {
            record.Iteration,
            ToolCount = record.ToolCalls.Count,
            record.Duration,
        });
    },
};
```

### Hook behavior

- **Hooks are async** (`Func<..., Task>`) — await I/O operations safely.
- **Hook exceptions propagate** to the caller — they are not swallowed by the loop's error handling. If a hook throws, the loop terminates with that exception.
- **Null hooks are safe** — omitting any hook has no effect.
- **`OnToolCall` receives the iteration number** as the first parameter, so progress reporters know which iteration a tool call belongs to.

---

## Diagnostics Accessor

The loop automatically publishes diagnostics to `IAgentDiagnosticsAccessor` when the service is registered. This eliminates the need to inspect `IterationRecord.Tokens` directly.

### Setup

Call `BeginCapture()` before running the loop so the diagnostics are visible to the caller after the run completes:

```csharp
var diagnosticsAccessor = services.GetRequiredService<IAgentDiagnosticsAccessor>();

// Create a capture scope — diagnostics written inside RunAsync will be
// visible via LastRunDiagnostics after the run completes.
using var scope = diagnosticsAccessor.BeginCapture();

var result = await loop.RunAsync(options, context);

// Read diagnostics from the accessor (same data as result.Diagnostics)
var diag = diagnosticsAccessor.LastRunDiagnostics!;
Console.WriteLine($"LLM calls: {diag.ChatCompletions.Count}");
Console.WriteLine($"Tool calls: {diag.ToolCalls.Count}");
Console.WriteLine($"Tokens: {diag.AggregateTokenUsage.TotalTokens}");
```

!!! warning "BeginCapture is required"
    Without `BeginCapture()`, `LastRunDiagnostics` will be `null` after the run. The loop writes diagnostics into an `AsyncLocal<T>` holder — `BeginCapture()` creates the shared holder that both the loop and the caller can access.

---

## Execution Context Bridge

DI-resolved tools often need access to the workspace. The iterative loop automatically bridges its `IterativeContext.Workspace` to `IAgentExecutionContextAccessor` so that tool classes can read/write workspace files.

### How it works

When `IAgentExecutionContextAccessor` is available via DI, the loop:

1. Creates an `AgentExecutionContext` with the workspace from `IterativeContext`
2. Calls `accessor.BeginScope(context)` before the first iteration
3. Disposes the scope after the loop completes

### DI-resolved tool example

```csharp
[AgentFunctionGroup("my-tools")]
public class MyTools
{
    private readonly IAgentExecutionContextAccessor _contextAccessor;

    public MyTools(IAgentExecutionContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    [AgentFunction]
    public string ReadConfig()
    {
        var workspace = _contextAccessor.Current!.GetRequiredWorkspace();
        return workspace.ReadFile("config.json");
    }
}
```

### Explicit context

If you need custom `UserId` or `OrchestrationId` values, provide an explicit execution context:

```csharp
var options = new IterativeLoopOptions
{
    // ... other options ...
    ExecutionContext = new AgentExecutionContext(
        UserId: "user-123",
        OrchestrationId: "trip-planner-run-42",
        Workspace: workspace),
};
```

---

## Results and Diagnostics

`IterativeLoopResult` provides full introspection:

```csharp
var result = await loop.RunAsync(options, context);

// Overall status
Console.WriteLine($"Success: {result.Succeeded}");
Console.WriteLine($"Error: {result.ErrorMessage}");

// Per-iteration detail
foreach (var iter in result.Iterations)
{
    Console.WriteLine($"Iteration {iter.Iteration}: " +
        $"{iter.ToolCalls.Count} tools, " +
        $"{iter.Tokens.InputTokenCount} in / {iter.Tokens.OutputTokenCount} out, " +
        $"{iter.Duration.TotalSeconds:F1}s");

    foreach (var tool in iter.ToolCalls)
    {
        var status = tool.Succeeded ? "✓" : "✗";
        Console.WriteLine($"  {status} {tool.FunctionName} ({tool.Duration.TotalMilliseconds:F0}ms)");
    }
}

// Aggregate diagnostics
var diag = result.Diagnostics;
Console.WriteLine($"Total tokens: {diag?.AggregateTokenUsage.TotalTokens}");
```

### IterationRecord fields

| Field | Type | Description |
|-------|------|-------------|
| `Iteration` | `int` | 0-based iteration index |
| `ToolCalls` | `IReadOnlyList<ToolCallResult>` | All tool calls made this iteration |
| `ResponseText` | `string?` | Final model text response (if any) |
| `Tokens` | `TokenUsage` | Input/output token counts for this iteration |
| `Duration` | `TimeSpan` | Wall-clock time for this iteration |
| `LlmCallCount` | `int` | Number of LLM API calls made this iteration |

### ToolCallResult fields

| Field | Type | Description |
|-------|------|-------------|
| `FunctionName` | `string` | Tool function name |
| `Arguments` | `IReadOnlyDictionary<string, object?>` | Arguments passed to the tool |
| `Result` | `object?` | Return value from the tool |
| `Duration` | `TimeSpan` | Tool execution time |
| `Succeeded` | `bool` | Whether the tool executed without error |
| `ErrorMessage` | `string?` | Error message if `Succeeded` is false |

---

## When to Use the Iterative Loop

### Good fit

- **Multi-step agentic workloads** — research → plan → execute → validate → fix
- **Budget-sensitive deployments** — token cost must stay predictable
- **Complex tool workflows** — many tool calls across many iterations
- **Workspace-centric tasks** — the output is files (articles, code, plans, itineraries)

### Not ideal for

- **Conversational agents** — where conversation history IS the product
- **Simple single-tool workflows** — where FIC overhead is negligible
- **Stateless Q&A** — no workspace needed, one LLM call suffices

---

## Example: Trip Planner

The `IterativeTripPlannerApp` example in `src/Examples/AgentFramework/` demonstrates the full pattern — matching the architecture of production consumers like BrandGhost:

```
dotnet run --project src/Examples/AgentFramework/IterativeTripPlannerApp
```

This example plans a multi-stop trip from New York to Tokyo on a tight budget with constraints (3.5★ minimum hotel rating, 2+ intermediate stops). It demonstrates:

- **DI-resolved tools** — `TripPlannerFunctions` class with `[AgentFunctionGroup("trip-planner")]`, resolved via `IAgentFactory.ResolveTools()`
- **Workspace access via DI** — tools read/write workspace through `IAgentExecutionContextAccessor` (not captured closures)
- **Lifecycle hooks** — progress output driven by `OnIterationStart`, `OnToolCall`, `OnIterationEnd`
- **Diagnostics accessor** — aggregate metrics read from `IAgentDiagnosticsAccessor.LastRunDiagnostics` after the run
- **Execution context bridge** — workspace automatically available to DI-resolved tools
- **Budget failures** — the preferred European route exceeds the budget
- **Route pivots** — the model discovers cheaper US West Coast alternatives
- **Fix cycles** — validation failures trigger leg removal, hotel swaps, and replanning
- **Full diagnostics** — per-iteration token counts, tool call logs, and O(n²) comparison

Configure via `appsettings.json`:

```json
{
  "TripPlanner": {
    "Origin": "New York",
    "Destination": "Tokyo",
    "Budget": "1600",
    "MaxStops": 3
  }
}
```

Set `UseMockClient` to `false` and provide Azure OpenAI credentials in `appsettings.Development.json` for real LLM execution.

---

## See Also

- [AI Integrations](ai-integrations.md) — agent framework overview and function discovery
- [Progress Reporting](progress-reporting.md) — real-time event tracking for agent runs
