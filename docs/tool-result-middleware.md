---
description: How Needlr's ToolResultFunctionMiddleware translates tool exceptions and IToolResult returns into LLM-safe responses, and the trade-offs of enabling it.
---

# Tool Result Middleware

`ToolResultFunctionMiddleware` (in `NexusLabs.Needlr.AgentFramework.Workflows`) is the safety net for tool invocations. When enabled via `.UsingToolResultMiddleware()` on an `AgentFrameworkSyringe`, it catches unhandled exceptions inside `[AgentFunction]` tool bodies and translates them into structured `{ error: … }` results the LLM can understand and recover from — instead of letting the exception bubble all the way to `FunctionInvokingChatClient` and fail the entire agent turn.

This page explains exactly what the middleware does, the trade-offs of enabling it, and when you should turn it on.

## What the middleware does

Three behaviors, in order:

### 1. Catches unhandled tool exceptions

When a `[AgentFunction]` body throws (NRE, `ArgumentException`, database timeout, etc.), the middleware:

- Catches the exception via `catch (Exception ex)`.
- Wraps it in `ToolResult.UnhandledFailure(ex)`, which preserves the original exception on `IToolResult.Exception` for diagnostics.
- Returns `{ error: { message: "An unexpected error occurred (ExceptionTypeName). Please try again." } }` to the LLM. The exception type name is included so the model can self-recover (e.g. retry a `TimeoutException` but reformulate input for an `ArgumentException`); the `Exception.Message` body is **not** included to avoid leaking application internals or PII.

### 2. Unwraps `IToolResult` return types

Tools can return `ToolResult<TValue, TError>` (or the convenience shorthands `ToolResult.Ok<T>(value)` / `ToolResult.Fail<T>("message", suggestion: …)`) for graceful failure with hints. The middleware:

- On success, extracts `BoxedValue` and forwards it as the function result, so the LLM sees the typed payload directly.
- On failure, wraps `BoxedError` in `{ error: BoxedError }`. The `suggestion` field (if set) gives the LLM a hint for how to recover.

### 3. Pass-through for plain returns

If the tool body returns anything other than an `IToolResult`, the middleware passes it through unchanged. Existing tools that return plain strings or POCOs are unaffected.

### Cancellation

`OperationCanceledException` is **not** caught — it propagates so cooperative cancellation (parent timeouts, user cancels, structured-concurrency aborts) continues to function correctly. Tools that legitimately want to translate cancellation should do so inside the tool body, not rely on this middleware.

## Trade-offs of enabling it

Enabling `.UsingToolResultMiddleware()` is strongly recommended for production agents, but it's worth understanding the side effects:

| Concern | Behavior with middleware enabled |
|---|---|
| **Agent turn survives tool exceptions** | ✅ Yes — the LLM sees a structured error and can choose to retry, switch tools, or stop. |
| **LLM gets a recovery signal** | ✅ The exception type name is in the default error message. Tools that return `ToolResult.Fail("…", suggestion: "…")` give the LLM richer hints. |
| **Stack traces / `Exception.Message` exposed to the LLM** | ❌ No — the original exception is preserved on `IToolResult.Exception` for diagnostics but the LLM sees only the type name. |
| **Diagnostics (`DiagnosticsFunctionInvokingChatClient`) records the call** | ⚠️ As `Succeeded: true` because the middleware swallowed the exception before diagnostics could see it. The result body contains `{ error: … }` so the failure is observable, but per-tool failure counters from the diagnostics middleware will not increment. Read the result content if you need failure metrics. |
| **`OperationCanceledException` propagation** | ✅ Cancellation tokens still work — the middleware deliberately rethrows `OperationCanceledException`. |

## Plugin ordering

The middleware is registered as an `IAIAgentBuilderPlugin`. Order matters when combined with other middleware:

- **`UsingToolResultMiddleware()` after `UsingResilience()`** (recommended): the resilience layer retries first; if all retries fail, the exception escapes resilience and is caught by the result middleware. The LLM sees the final-failure error.
- **`UsingToolResultMiddleware()` before `UsingResilience()`** (anti-pattern): the result middleware catches exceptions before resilience can retry them. Retries are defeated.

```csharp
syringe
    .UsingResilience()           // First — innermost
    .UsingToolResultMiddleware(); // Then — outermost
```

## When to enable

**Always enable it for production agent setups.** The default behavior — exception bubbling all the way to `FunctionInvokingChatClient` — kills the entire agent turn on the first tool failure, with no way for the LLM to recover. With the middleware enabled, transient or recoverable failures become structured signals the LLM can act on.

The middleware is opt-in for two reasons:

1. The middleware lives in `NexusLabs.Needlr.AgentFramework.Workflows`, which is a separate package from the core `NexusLabs.Needlr.AgentFramework`. Some applications use only the core (e.g. running tools through their own custom orchestration) and don't need the workflow extensions.
2. Some test harnesses prefer exception bubbling so failures surface as immediate test failures rather than `{ error: … }` results that test code has to inspect.

For everything else, opt in.

## Example

```csharp
// In your bootstrap:
var factory = serviceProvider.UsingAgentFramework()
    .AddAgentFunctionsFromGenerated(GeneratedAgentFunctions.AllFunctionTypes)
    .UsingResilience()             // Resilience runs innermost
    .UsingToolResultMiddleware()   // Result middleware runs outermost
    .BuildAgentFactory();

// In a tool that wants to surface graceful, recoverable failures:
[AgentFunction]
[Description("Looks up an order by ID.")]
public ToolResult<OrderStatus, ToolError> GetOrderStatus(
    [Description("Order id.")] string orderId)
{
    if (string.IsNullOrWhiteSpace(orderId))
    {
        return ToolResult.Fail<OrderStatus>(
            "orderId is required",
            suggestion: "Pass a non-empty order ID like 'ORD-12345'.");
    }

    var order = _orders.Find(orderId);
    if (order is null)
    {
        return ToolResult.Fail<OrderStatus>(
            $"Order '{orderId}' not found",
            suggestion: "Verify the ID with the customer or list recent orders.",
            isTransient: false);
    }

    return ToolResult.Ok(order.Status);
}
```

The LLM sees a clear success payload (the `OrderStatus`) when the lookup works, or a clear error envelope with a recovery hint when it doesn't. Either way, the agent turn stays alive.

## Customizing the default failure message

The default LLM-facing message is `"An unexpected error occurred (ExceptionTypeName). Please try again."` — type-only, no message body. To override (e.g. for an application that wants the legacy bland message or a domain-specific format), pass a `formatter` to `ToolResult.UnhandledFailure`:

```csharp
ToolResult.UnhandledFailure(
    ex,
    formatter: e => new ToolError(
        Message: "Operation failed. Our team has been notified.",
        Suggestion: "Try a different request."));
```

The middleware itself doesn't expose a hook for the default formatter — to use a custom one for unhandled exceptions, you'd need to wrap your tool bodies in `try`/`catch` and call `UnhandledFailure(ex, formatter: …)` yourself, returning the result. Most applications find the default type-name message sufficient.

## See also

- [AI Integrations — Microsoft Agent Framework](ai-integrations.md#microsoft-agent-framework)
- [Iterative Agent Loop](iterative-agent-loop.md)
- [NDLRMAF030 — JSON-string parameter hint](analyzers/NDLRMAF030.md) — sibling reliability rule for tool argument shapes
