---
# AUTO-GENERATED from .github/instructions/genesis/consumers.instructions.md — do not edit
paths:
  - "**/*Consumer.cs"
---
# MassTransit Consumer Rules

## Interface

Implement `IConsumer<TMessage>`:

```csharp
public sealed partial class CreateThingConsumer(
    ILogger<CreateThingConsumer> _logger,
    ICreateThingUnitOfWork _createThingUnitOfWork) :
    IConsumer<CreateThingContract>
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Consumer} received message for owner user ID {OwnerUserId}.")]
    private partial void LogReceived(string consumer, string ownerUserId);

    public async Task Consume(ConsumeContext<CreateThingContract> context)
    {
        LogReceived(nameof(CreateThingConsumer), context.Message.OwnerUserId.ToString());

        var result = await _createThingUnitOfWork
            .TryCreateAsync(
                ownerUserId: context.Message.OwnerUserId,
                cancellationToken: context.CancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            await context.RespondAsync(new CreateThingConsumerResponse(result.Value))
                .ConfigureAwait(false);
        }
        else
        {
            await context.RespondAsync(ConsumerError.CreateResponse(result.Error))
                .ConfigureAwait(false);
        }
    }
}
```

## `Consume()` must be thin

- Always log at the start of `Consume()` using **source-generated `[LoggerMessage]`** — never `_logger.LogInformation("...", params)` directly
- Immediately delegate to a `IXxxUnitOfWork` — NEVER put business logic directly in `Consume()`
- Use `context.RespondAsync(...)` for request-response consumers; use `ConsumerError.CreateResponse(error)` for error responses
- Use `context.CancellationToken` (not `CancellationToken.None`)

## Consumer response naming

MassTransit consumer responses use the **`*ConsumerResponse`** suffix — NOT `*Response`:

```csharp
// ✅ CORRECT
await context.RespondAsync(new CreateThingConsumerResponse(result.Value));

// ❌ WRONG — *Response is reserved for web API (Carter module) boundaries
await context.RespondAsync(new CreateThingResponse(result.Value));
```

Both `*Contract` and `*ConsumerResponse` types live in the **SDK project**. See the MassTransit contract instructions for full rules.

## Auto-discovery

Consumers are auto-discovered by Needlr. Do NOT manually register them in a plugin's `Configure()`.
