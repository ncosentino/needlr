---
applyTo: "**/*Contract.cs,**/*ConsumerResponse.cs"
---

# MassTransit Contract and Consumer Response Rules

MassTransit message types use two distinct suffixes to clearly separate them from web API DTOs:

| Suffix | Role | Direction |
|--------|------|-----------|
| `*Contract` | Message sent **to** a consumer | Publisher → Consumer |
| `*ConsumerResponse` | Response sent **from** a consumer | Consumer → Publisher |

## `*Contract` — message sent to a consumer

```csharp
// In NexusAI.SDK / shared namespace
public sealed record CreateScheduledPostContract(
    UserId OwnerUserId,
    DateTimeOffset TargetDateTime,
    DateTimeOffset ExpiresDateTime,
    IReadOnlyList<SocialAccountId> SocialAccountIds,
    IReadOnlyDictionary<string, string?> Fields);
```

## `*ConsumerResponse` — response from a consumer

```csharp
// In NexusAI.SDK / shared namespace
public sealed record CreateScheduledPostConsumerResponse(
    ContentId ContentId);
```

Used in the consumer like this:

```csharp
await context.RespondAsync(new CreateScheduledPostConsumerResponse(result.Value.ContentId))
    .ConfigureAwait(false);
```

## Rules

### Location — always in the SDK

Both `*Contract` and `*ConsumerResponse` types **MUST live in the SDK project** (or a shared boundary project), never in the consumer's feature project. The consumer feature project references the SDK; the publisher does the same. This avoids circular project dependencies.

### Shape

- **Immutable positional records** — always
- **Strong-typed IDs** — use `ContentId`, `UserId`, `SocialAccountId`, etc. These types are in-process; they do not cross a web boundary and are not subject to the `long`-to-`string` serialization rule
- **No success/failure properties** — use `ConsumerError.CreateResponse(error)` for error responses; never add `bool Success` to a contract or response

### Error responses

When a consumer operation fails, respond with the standard error shape:

```csharp
if (!result.Success)
{
    await context.RespondAsync(ConsumerError.CreateResponse(result.Error))
        .ConfigureAwait(false);
}
```

Never invent custom error wrapper properties on the response type.

### Auto-discovery

`*Contract` and `*ConsumerResponse` types are plain records — no registration is required. They are discovered at build time by MassTransit's topology configuration.

### Naming — distinguish from web API types

- `*Response.cs` → **web API** response (Carter module boundary)
- `*ConsumerResponse.cs` → **MassTransit** consumer response

Never use `*Response.cs` for MassTransit responses. The suffix disambiguates the contract boundary.
