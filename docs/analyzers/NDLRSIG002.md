# NDLRSIG002: HubType must be a typeof expression

## Cause

The `hubType` parameter of `HubPathAttribute` is not a `typeof()` expression.

## Rule Description

SignalR hub type references must use the `typeof()` operator for proper type resolution at compile time. This ensures AOT compatibility and allows the source generator to properly analyze the hub type.

## Example

### Incorrect

```csharp
[HubPath("/chat", someTypeVariable)] // NDLRSIG002
public class ChatHubRegistration : ISignalRHub { }
```

### Correct

```csharp
[HubPath("/chat", typeof(ChatHub))]
public class ChatHubRegistration : ISignalRHub { }
```

## How to Fix

Always use `typeof(YourHubClass)` for the hub type parameter:

```csharp
[HubPath("/notifications", typeof(NotificationHub))]
public class NotificationHubRegistration : ISignalRHub { }
```

## When to Suppress

This warning should not be suppressed. Non-typeof expressions will cause compilation errors in the C# compiler itself (CS0182).

## See Also

- [SignalR Integration](../advanced-usage.md#signalr)
- [NDLRSIG001: HubPath must be a constant](NDLRSIG001.md)
