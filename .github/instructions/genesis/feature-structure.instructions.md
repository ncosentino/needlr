---
applyTo: "*.Features.*/**/*.cs,**/Features/**/*.cs"
---

# Feature Project Structure Rules

## Vertical slice first

Folder structure is organized by **business capability first**, then by technical layer within that capability. NEVER group by technical type at the feature root.

### FORBIDDEN top-level groupings

Do NOT create these as standalone folders at the feature project root:

- `Models/`
- `DTOs/`
- `Repositories/`
- `Services/`
- `Ids/`
- `Handlers/`

### Correct structure

Each sub-feature owns its own slice. All types related to that capability live together:

```
MyProduct.Features.Ordering/
  Orders/
    OrderId.cs                     ← StronglyTypedId
    Order.cs                       ← immutable domain record
    OrderDto.cs                    ← private DB mapping DTO (internal to repository)
    OrdersRepository.cs
    CreateOrderUnitOfWork.cs
    ICreateOrderUnitOfWork.cs
    OrdersCarterModule.cs
    CreateOrderRequest.cs
    OrderResponse.cs
  OrderItems/
    OrderItemId.cs
    OrderItem.cs
    OrderItemsRepository.cs
    AddOrderItemUnitOfWork.cs
    IAddOrderItemUnitOfWork.cs
  Fulfilment/
    FulfilmentId.cs
    Fulfilment.cs
    FulfilmentRepository.cs
    FulfilmentCarterModule.cs
  OrderingPlugin.cs                ← at project root, only if needed
```

## Nesting and sub-projects

Slices can nest when a capability grows complex enough to warrant sub-grouping within the project. However, when a sub-feature becomes mature and large enough, it may graduate into its own dedicated project (e.g. `MyProduct.Features.Ordering.Fulfilment`). A separate project is a strong forcing function for isolation — do this only when the sub-feature is stable and well-understood, not speculatively.

## Plugin placement

Plugins are a Needlr concept and only exist if manual DI registration is required. In an ideal codebase, no plugins are needed at all. If a plugin is required:

- It lives at the **root of the feature project**
- Sub-features may have their own plugin if they have isolated registration needs and it keeps scope smaller

## Cross-cutting concerns and shared libraries

When logic is useful across multiple features, extract it into a **shared library** — not a shared layer that features are forced to depend on.

The distinction matters:

- ✅ A shared library provides **convenience utilities** (e.g. helpers for working with database connections, serialization extensions). Features may adopt it optionally.
- ❌ A shared data access layer that every feature must route through **couples all features together**. This is forbidden.

Nobody should be forced to use a shared library. If a feature finds it useful, it can. If it doesn't, it won't. Forced coupling through a common layer defeats the purpose of vertical slicing.

## Inter-feature communication — strictly forbidden direct references

Feature projects **MUST NOT** reference each other directly. This is a hard rule with no exceptions. Each feature must remain fully isolated from every other feature.

The mechanism for cross-feature communication is an **SDK layer** that all features are permitted to depend on. Feature A never sees Feature B — it only sees interfaces exposed through the SDK.

### The pattern

If Feature A needs something Feature B owns:

1. A **client interface** is defined in the SDK (e.g. `IOrderingClient`)
2. Feature A depends only on that interface
3. Feature B (or the SDK itself) provides the implementation — with several options for how:

### Implementation options

**Option 1 — Feature B implements the SDK interface directly (discouraged)**

Feature B registers a class that implements `IOrderingClient`. Needlr picks it up automatically. This technically works but is an anti-pattern: Feature A is still implicitly coupled to Feature B being present in the same process. Acceptable as a starting point, not as a long-term design.

**Option 2 — SDK client delegates over MassTransit (preferred)**

The SDK contains a client implementation that sends a MassTransit message to a consumer in Feature B. Feature A calls the SDK interface; the SDK sends a message; Feature B's consumer handles it. Neither side knows about the other's internals. The transport can be swapped without changing either feature.

This is the most common and recommended approach.

**Option 3 — Alternative transport (situational)**

If the architecture is already moving toward another transport (e.g. gRPC, HTTP between services), the same pattern applies — SDK interface, SDK client implementation using the chosen transport, Feature B consumer on the other end. Use when the codebase is deliberately evolving toward stronger service separation and MassTransit is not the right fit.

