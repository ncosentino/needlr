# RegisterAs&lt;T&gt; - Explicit Interface Registration

The `[RegisterAs<T>]` attribute gives you explicit control over which interfaces a class is registered as in dependency injection. By default, Needlr registers a class as all non-system interfaces it implements. Use `[RegisterAs<T>]` when you want to limit which interfaces are publicly resolvable.

## Basic Usage

```csharp
public interface IReader { string Read(); }
public interface IWriter { void Write(string data); }
public interface ILogger { void Log(string message); }

// Only registered as IReader - not as IWriter or ILogger
[RegisterAs<IReader>]
public class FileService : IReader, IWriter, ILogger
{
    public string Read() => "file content";
    public void Write(string data) { /* ... */ }
    public void Log(string message) { /* ... */ }
}
```

With this configuration:
- ✅ `provider.GetService<IReader>()` returns `FileService`
- ✅ `provider.GetService<FileService>()` returns `FileService` (concrete type always registered)
- ❌ `provider.GetService<IWriter>()` returns `null`
- ❌ `provider.GetService<ILogger>()` returns `null`

## Multiple RegisterAs Attributes

You can apply multiple `[RegisterAs<T>]` attributes to register as multiple specific interfaces:

```csharp
[RegisterAs<IReader>]
[RegisterAs<IWriter>]
public class FileService : IReader, IWriter, ILogger
{
    // Registered as IReader and IWriter, but NOT as ILogger
}
```

## Use Cases

### 1. Controlling Public API Surface

When a class implements internal utility interfaces that shouldn't be exposed:

```csharp
public interface IOrderService { void PlaceOrder(); }
internal interface IOrderValidator { bool Validate(); }
internal interface IAuditLogger { void Log(); }

[RegisterAs<IOrderService>]
public class OrderService : IOrderService, IOrderValidator, IAuditLogger
{
    // Only IOrderService is resolvable - internal interfaces are hidden
}
```

### 2. Avoiding Ambiguous Registrations

When multiple classes implement the same interface but you want specific resolution:

```csharp
public interface INotificationSender { void Send(); }
public interface IEmailSender { void SendEmail(); }
public interface ISmsSender { void SendSms(); }

[RegisterAs<IEmailSender>]
public class EmailService : INotificationSender, IEmailSender
{
    // Only registered as IEmailSender, not INotificationSender
}

[RegisterAs<ISmsSender>]
public class SmsService : INotificationSender, ISmsSender
{
    // Only registered as ISmsSender, not INotificationSender
}
```

### 3. Interface Hierarchies

You can register as a base interface even if the class implements a derived interface:

```csharp
public interface IRepository { }
public interface IUserRepository : IRepository { User Get(int id); }

[RegisterAs<IRepository>]
public class UserRepository : IUserRepository
{
    public User Get(int id) => /* ... */;
    
    // Registered as IRepository, not IUserRepository
}
```

## Behavior

| Scenario | Result |
|----------|--------|
| No `[RegisterAs<T>]` | All non-system interfaces are registered |
| One `[RegisterAs<T>]` | Only that interface is registered |
| Multiple `[RegisterAs<T>]` | Only specified interfaces are registered |
| Concrete type | Always registered as itself |

## Analyzer Support

Needlr includes compile-time validation:

| Rule | Severity | Description |
|------|----------|-------------|
| [NDLRCOR015](analyzers/NDLRCOR015.md) | Error | `T` must be an interface implemented by the class |

## Works With Both Paths

`[RegisterAs<T>]` works identically in both:
- **Source Generation** - Full AOT/trimming compatibility
- **Reflection** - Traditional runtime discovery

## Lifetime Behavior

`[RegisterAs<T>]` does not affect lifetime. Use it alongside lifetime attributes:

```csharp
[Transient]
[RegisterAs<IReader>]
public class TransientReader : IReader, IWriter
{
    // Transient, registered only as IReader
}
```

## See Also

- [Getting Started](getting-started.md)
- [DoNotAutoRegister](getting-started.md#excluding-types) - Exclude from all registration
- [NDLRCOR015](analyzers/NDLRCOR015.md) - Analyzer documentation
