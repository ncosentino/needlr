---
description: Track agent and workflow execution in real-time with Needlr's progress reporting system -- auto-discovered sinks, per-orchestration isolation, and ambient async context.
---

# Progress Reporting

Needlr's Agent Framework emits structured progress events during agent and workflow execution — LLM calls, tool invocations, budget updates, workflow start/completion, and failures. Your code receives these events through **sinks** (`IProgressSink`) and uses them to build SSE streams, console displays, cost dashboards, trace diagrams, or any other real-time reporting surface.

---

## Quick Start

### 1. Implement `IProgressSink`

```csharp
public sealed class ConsoleSink : IProgressSink
{
    public ValueTask OnEventAsync(
        IProgressEvent progressEvent,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{progressEvent.WorkflowId}] {progressEvent.GetType().Name}");
        return ValueTask.CompletedTask;
    }
}
```

### 2. Run a workflow

If you're using Needlr's source generation or reflection-based scanning, the sink above is **auto-discovered** and registered in DI as a default. No explicit registration is needed.

```csharp
var progressFactory = sp.GetRequiredService<IProgressReporterFactory>();
var progressAccessor = sp.GetRequiredService<IProgressReporterAccessor>();

// Create() with one argument uses all auto-discovered default sinks.
var reporter = progressFactory.Create("my-workflow-run-123");

using (progressAccessor.BeginScope(reporter))
{
    await workflow.RunAsync("Generate a blog post", ct);
}
// ConsoleSink receives LlmCallStarted, LlmCallCompleted, WorkflowCompleted, etc.
```

That's it for simple applications. Read on for multi-workflow and multi-tenant scenarios.

---

## Two Creation Patterns

`IProgressReporterFactory` offers two `Create` overloads that serve fundamentally different use cases:

### Default sinks — `Create(workflowId)`

Uses all `IProgressSink` instances registered in DI as defaults. This includes:

- Sinks **auto-discovered** by Needlr (any class implementing `IProgressSink` that isn't decorated with `[DoNotAutoRegister]`)
- Sinks **manually registered** via `services.AddSingleton<IProgressSink, MySink>()`

Best for: **simple applications** with a single agentic workflow where all events go to the same place.

```csharp
var reporter = progressFactory.Create("my-workflow");
```

### Per-orchestration sinks — `Create(workflowId, sinks)`

Uses **only** the sinks you provide. Default DI sinks are **not** included. The caller has full control.

Best for: **complex server applications** (multi-tenant, parallel workflows) where each orchestration requires its own isolated reporting channel — per-tenant SSE streams, per-workflow log files, etc.

```csharp
// Each tenant gets their own sink — events are completely isolated.
var tenantSink = new TenantSseSink(tenantId, httpContext);
var reporter = progressFactory.Create($"tenant-{tenantId}-run-{runId}", [tenantSink]);
```

---

## Auto-Discovery

When Needlr's source generator or reflection scanning is active, any class implementing `IProgressSink` is automatically registered in DI as a singleton. This is the "zero-config" path — define the class, and it works.

Auto-discovery requires that Needlr is scanning the assembly containing the sink. This happens automatically when you use `UsingSourceGen()` or `UsingReflection()` on the `Syringe`.

### Opting Out with `[DoNotAutoRegister]`

Apply `[DoNotAutoRegister]` to prevent a sink from being auto-discovered:

```csharp
[DoNotAutoRegister]
public sealed class TenantSseSink : IProgressSink
{
    // This sink is only used via the per-orchestration Create() overload.
    // It requires tenant-specific constructor arguments that DI can't provide.
    public TenantSseSink(string tenantId, HttpContext httpContext) { /* ... */ }

    public ValueTask OnEventAsync(IProgressEvent e, CancellationToken ct) { /* ... */ }
}
```

Use `[DoNotAutoRegister]` when:

- The sink requires constructor arguments that aren't available in DI
- You want the sink used only for specific orchestrations, not as a global default
- You're registering the sink manually with a specific lifetime or factory

---

## Declaring Sinks per Agent with `[ProgressSinks]`

The `[ProgressSinks]` attribute lets you declare at compile time which sinks an agent should use. The source generator emits a helper method that creates a properly scoped reporter:

```csharp
[NeedlrAiAgent(Instructions = "Write blog articles.")]
[ProgressSinks(typeof(CostTrackingSink), typeof(AuditSink))]
public partial class WriterAgent { }

// Generated: sp.BeginWriterAgentProgressScope(workflowId?)
// Returns an IDisposable that manages the reporter scope and sink lifetime.
```

This is a convenience for the per-orchestration pattern — the generated code calls `Create(workflowId, sinks)` under the hood.

---

## Ambient Context with `IProgressReporterAccessor`

`IProgressReporterAccessor` provides ambient access to the current reporter via `AsyncLocal<T>`, following the same pattern as `IHttpContextAccessor`. This lets middleware (chat client wrappers, function-calling middleware) emit events without needing the reporter passed as a parameter.

```csharp
// Set the reporter for the current async flow.
using (progressAccessor.BeginScope(reporter))
{
    // All code in this scope (including middleware) sees this reporter.
    await agent.RunAsync("Hello", cancellationToken: ct);
}

// Outside the scope, accessor.Current returns NullProgressReporter.Instance
// (a zero-overhead no-op).
```

Concurrent orchestrations in the same process each get their own reporter — `AsyncLocal` ensures isolation.

---

## Error Handling

By default, if a sink's `OnEventAsync` throws, the exception is silently swallowed so that a broken sink doesn't crash the agent pipeline. To customize this behavior, register an `IProgressReporterErrorHandler`:

```csharp
services.AddSingleton<IProgressReporterErrorHandler, MyErrorHandler>();
```

---

## Event Types

All events implement `IProgressEvent` which provides:

| Property | Description |
|---|---|
| `Timestamp` | When the event occurred |
| `WorkflowId` | Correlation ID for the orchestration run |
| `AgentId` | Which agent emitted the event (null for workflow-level events) |
| `CallSequence` | Groups related events (e.g., start/complete pairs) |
| `SequenceNumber` | Monotonically increasing per-reporter for ordering |

Built-in event types include:

- `WorkflowStartedEvent` / `WorkflowCompletedEvent`
- `AgentStartedEvent` / `AgentCompletedEvent` / `AgentFailedEvent`
- `LlmCallStartedEvent` / `LlmCallCompletedEvent` (with model, tokens, duration)
- `ToolCallStartedEvent` / `ToolCallCompletedEvent`
- `BudgetUpdatedEvent` (when token budgets are active)

---

## Non-Blocking Delivery

For sinks that perform I/O (HTTP calls, database writes, file logging), use `ChannelProgressReporter` to decouple event production from consumption. Events are buffered in a `Channel<T>` and delivered on a background task, so a slow sink never blocks the agent pipeline.
