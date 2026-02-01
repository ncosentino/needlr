# Factory Delegates (Source Generation Only)

When a class has constructor parameters that cannot be auto-injected (value types, strings, delegates), Needlr typically excludes it from auto-registration. The `[GenerateFactory]` attribute solves this by generating factory interfaces and delegates that handle the injectable parameters while letting you provide the runtime parameters.

## Basic Usage

```csharp
using NexusLabs.Needlr.Generators;

// This service has both injectable (ILogger) and runtime (connectionString) parameters
[GenerateFactory]
public sealed class DatabaseConnection
{
    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <param name="connectionString">The database connection string.</param>
    public DatabaseConnection(ILogger<DatabaseConnection> logger, string connectionString)
    {
        // ...
    }
}
```

The source generator creates:

- **`IDatabaseConnectionFactory`** - Interface with `Create(string connectionString)` method
- **`Func<string, DatabaseConnection>`** - Delegate for functional-style creation

Both are registered as Singletons. XML documentation from your constructor parameters is propagated to the factory's `Create` method for rich IntelliSense.

## Resolving Factories

```csharp
// Using the factory interface (recommended for testability)
var factory = serviceProvider.GetRequiredService<MyApp.Generated.IDatabaseConnectionFactory>();
var prodDb = factory.Create("Server=prod;Database=app");
var testDb = factory.Create("Server=localhost;Database=test");

// Using the Func delegate (concise syntax)
var createDb = serviceProvider.GetRequiredService<Func<string, DatabaseConnection>>();
var db = createDb("Server=dev;Database=sandbox");
```

## Generic Factory for Interface Return Types

Use `[GenerateFactory<TInterface>]` to have the factory return an interface instead of the concrete type. This enables mocking both the factory and its returned instances:

```csharp
public interface IRequestHandler
{
    Guid CorrelationId { get; }
    string Handle(string request);
}

[GenerateFactory<IRequestHandler>]
public sealed class RequestHandler : IRequestHandler
{
    /// <summary>Creates a new request handler.</summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="correlationId">Unique ID for tracing this request.</param>
    public RequestHandler(IConfiguration config, Guid correlationId)
    {
        CorrelationId = correlationId;
        // ...
    }
    
    // ...
}
```

The generated factory:
```csharp
public interface IRequestHandlerFactory
{
    /// <summary>Creates a new instance of RequestHandler.</summary>
    /// <param name="correlationId">Unique ID for tracing this request.</param>
    IRequestHandler Create(Guid correlationId);  // Returns interface, not concrete type
}

// Func also returns the interface
Func<Guid, IRequestHandler>
```

## Multiple Constructors

Each public constructor with runtime parameters gets its own `Create()` overload:

```csharp
[GenerateFactory]
public sealed class ReportGenerator
{
    /// <param name="timeProvider">Time provider for timestamps.</param>
    /// <param name="title">Report title.</param>
    public ReportGenerator(ITimeProvider timeProvider, string title) { }

    /// <param name="timeProvider">Time provider for timestamps.</param>
    /// <param name="title">Report title.</param>
    /// <param name="maxItems">Maximum items to include.</param>
    public ReportGenerator(ITimeProvider timeProvider, string title, int maxItems) { }
}
```

Generated interface:
```csharp
public interface IReportGeneratorFactory
{
    /// <summary>Creates a new instance of ReportGenerator.</summary>
    /// <param name="title">Report title.</param>
    ReportGenerator Create(string title);
    
    /// <summary>Creates a new instance of ReportGenerator.</summary>
    /// <param name="title">Report title.</param>
    /// <param name="maxItems">Maximum items to include.</param>
    ReportGenerator Create(string title, int maxItems);
}
```

## Controlling Generation Mode

By default, both the `Func<>` delegate and `IFactory` interface are generated. Use the `Mode` property to control this:

```csharp
// Only generate Func<> delegate
[GenerateFactory(Mode = FactoryGenerationMode.Func)]
public class FuncOnlyService { }

// Only generate IFactory interface
[GenerateFactory(Mode = FactoryGenerationMode.Interface)]
public class InterfaceOnlyService { }

// Generate both (default)
[GenerateFactory(Mode = FactoryGenerationMode.All)]
public class BothService { }
```

## Parameter Classification

Needlr classifies constructor parameters as:

- **Injectable**: Interfaces and classes (resolved from the container)
- **Runtime**: Value types, strings, delegates, arrays (must be provided to `Create()`)

Only constructors with at least one runtime parameter are eligible for factory generation.

## Analyzers

Needlr includes analyzers to help with factory usage:

| Diagnostic | Severity | Description |
|------------|----------|-------------|
| [NDLRGEN003](analyzers/NDLRGEN003.md) | Warning | All parameters are injectable - factory provides no value |
| [NDLRGEN004](analyzers/NDLRGEN004.md) | Warning | No parameters are injectable - low value factory |
| [NDLRGEN005](analyzers/NDLRGEN005.md) | Error | Generic type T in `[GenerateFactory<T>]` is not implemented by the class |

## Namespace

Generated factories are placed in `{AssemblyName}.Generated` namespace:

```csharp
using MyApp.Generated;  // Contains IDatabaseConnectionFactory, IReportGeneratorFactory, etc.
```

## When to Use Factories

**Use `[GenerateFactory]` when:**

- Your class requires runtime configuration (connection strings, API keys, correlation IDs)
- You need to create multiple instances with different runtime parameters
- You want to avoid manual `IServiceProvider.GetRequiredService<>()` calls in factory methods

**Use `[GenerateFactory<TInterface>]` when:**

- You want to mock the factory in unit tests
- You want to mock the returned instances
- You prefer interface-based dependency injection

## Complete Example

```csharp
using NexusLabs.Needlr.Generators;
using MyApp.Generated;

// Define your service with mixed parameters
[GenerateFactory]
public sealed class EmailSender
{
    private readonly ISmtpClient _smtp;
    private readonly ILogger<EmailSender> _logger;
    
    public string FromAddress { get; }
    
    /// <summary>Creates a new email sender.</summary>
    /// <param name="smtp">SMTP client for sending emails.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="fromAddress">The sender email address.</param>
    public EmailSender(ISmtpClient smtp, ILogger<EmailSender> logger, string fromAddress)
    {
        _smtp = smtp;
        _logger = logger;
        FromAddress = fromAddress;
    }
    
    public Task SendAsync(string to, string subject, string body) { /* ... */ }
}

// Use the generated factory
public class NotificationService
{
    private readonly IEmailSenderFactory _emailFactory;
    
    public NotificationService(IEmailSenderFactory emailFactory)
    {
        _emailFactory = emailFactory;
    }
    
    public async Task NotifyUserAsync(User user, string message)
    {
        // Create sender with appropriate from address
        var sender = _emailFactory.Create("noreply@myapp.com");
        await sender.SendAsync(user.Email, "Notification", message);
    }
    
    public async Task NotifySupportAsync(string issue)
    {
        var sender = _emailFactory.Create("support@myapp.com");
        await sender.SendAsync("team@myapp.com", "Support Issue", issue);
    }
}
```
