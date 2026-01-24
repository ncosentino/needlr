# NDLRCOR006: Circular dependency detected

## Cause

A service directly or indirectly depends on itself, creating a circular reference chain. This will cause a runtime exception when the dependency injection container attempts to resolve the service.

## Rule Description

A **circular dependency** occurs when the dependency graph contains a cycle:

```
A → B → C → A  (cycle!)
```

This is impossible to resolve because:
- To create `A`, we need `B`
- To create `B`, we need `C`
- To create `C`, we need `A` ← but we haven't finished creating `A` yet!

```csharp
// ⚠️ NDLRCOR006: Circular dependency: OrderService → InventoryService → OrderService
[Scoped]
public class OrderService(IInventoryService inventory)
{
    public void PlaceOrder() => inventory.Reserve();
}

[Scoped]
public class InventoryService(IOrderService orders)  // Depends back on OrderService!
{
    public void Reserve() => orders.ValidateStock();
}
```

**What happens at runtime:**

```
System.InvalidOperationException: A circular dependency was detected for the service of type 'IOrderService'.
IOrderService -> OrderService -> IInventoryService -> InventoryService -> IOrderService
```

Or worse, a `StackOverflowException` if the container doesn't detect the cycle.

## Common Circular Dependency Patterns

### Direct Cycle (A → B → A)

```csharp
[Scoped] public class UserService(IRoleService roles) { }
[Scoped] public class RoleService(IUserService users) { }
```

### Indirect Cycle (A → B → C → A)

```csharp
[Scoped] public class OrderService(IPaymentService payments) { }
[Scoped] public class PaymentService(INotificationService notifications) { }
[Scoped] public class NotificationService(IOrderService orders) { }  // Cycle!
```

### Self-Reference (A → A)

```csharp
[Scoped] 
public class TreeNode(ITreeNode parent) { }  // References itself
```

## How to Fix

### Option 1: Refactor to Remove the Cycle (Recommended)

Often circular dependencies indicate a design problem. Consider:

**Extract shared logic:**
```csharp
// Before: OrderService ↔ InventoryService

// After: Both depend on shared service
[Scoped] public class StockValidator { }  // Shared logic here
[Scoped] public class OrderService(IStockValidator validator) { }
[Scoped] public class InventoryService(IStockValidator validator) { }
```

**Use events/mediator:**
```csharp
// Before: Direct dependency
[Scoped] public class OrderService(IInventoryService inventory) { }

// After: Decoupled via events
[Scoped] public class OrderService(IEventBus events)
{
    public void PlaceOrder() => events.Publish(new OrderPlacedEvent());
}

[Scoped] public class InventoryHandler : IEventHandler<OrderPlacedEvent> { }
```

### Option 2: Use Lazy<T> (Deferred Resolution)

Break the cycle by deferring one dependency:

```csharp
[Scoped]
public class OrderService(IInventoryService inventory) { }

[Scoped]
public class InventoryService(Lazy<IOrderService> orders)  // Lazy breaks the cycle
{
    public void Reserve() => orders.Value.ValidateStock();
}
```

**Note:** This only delays the problem—the cycle still exists and may cause issues.

### Option 3: Use Factory/Func<T>

Similar to Lazy<T>, create instances on-demand:

```csharp
[Scoped]
public class InventoryService(Func<IOrderService> orderFactory)
{
    public void Reserve()
    {
        var orders = orderFactory();
        orders.ValidateStock();
    }
}
```

### Option 4: Property Injection (Not Recommended)

Some DI containers support property injection to break cycles:

```csharp
[Scoped]
public class InventoryService
{
    [Inject] public IOrderService Orders { get; set; }  // Set after construction
}
```

**Warning:** Property injection hides dependencies and makes testing harder. Use sparingly.

## Prevention Best Practices

1. **Single Responsibility**: Each service should do one thing. Large services often develop circular dependencies.

2. **Dependency Direction**: Dependencies should flow one way (e.g., Controllers → Services → Repositories).

3. **Interface Segregation**: Split large interfaces into smaller, focused ones.

4. **Layer Architecture**: Use clear layers where higher layers depend on lower layers, never the reverse.

```
Controllers (top)
    ↓
Services
    ↓
Repositories (bottom)
```

## Runtime Detection

For comprehensive cycle detection including dynamic registrations:

```csharp
// Detect all cycles at startup
services.Verify(VerificationOptions.Strict);

// Or get detailed chain information
var result = services.VerifyWithDiagnostics();
foreach (var issue in result.Issues.Where(i => i.Type == VerificationIssueType.CircularDependency))
{
    Console.WriteLine(issue.DetailedMessage);
}
```

## Detection Limitations

This analyzer detects cycles at compile-time by:

1. Building a dependency graph from constructor parameters
2. Traversing the graph using DFS to find back-edges

Limitations:

- Only analyzes types with Needlr registration attributes
- Cannot detect cycles involving factory-registered services
- Cannot detect cycles created by runtime configuration

Use the runtime `Verify()` method for complete coverage.

## When to Suppress

Generally, **do not suppress** this diagnostic. Circular dependencies will cause runtime failures.

However, if you're using `Lazy<T>` or `Func<T>` to intentionally break the cycle:

```csharp
#pragma warning disable NDLRCOR006
[Scoped] public class ServiceA(Lazy<IServiceB> b) { }  // Lazy breaks cycle
[Scoped] public class ServiceB(IServiceA a) { }
#pragma warning restore NDLRCOR006
```

## See Also

- [Advanced Usage - Container Verification](../advanced-usage.md#container-verification)
- [NDLRCOR005: Lifetime mismatch](NDLRCOR005.md)
- [Martin Fowler: Inversion of Control Containers](https://martinfowler.com/articles/injection.html)
