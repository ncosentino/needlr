---
applyTo: "**/*Options.cs"
---

# Options Pattern Rules (`*Options.cs`)

`*Options.cs` types are plain POCOs decorated with Needlr's `[Options]` attribute. Needlr's source
generator automatically registers the type with the DI container — **no plugin `Configure()` call
needed, ever**.

---

## Minimal example

```csharp
using System.ComponentModel.DataAnnotations;
using NexusLabs.Needlr.Generators;

[Options(ValidateOnStart = true)]
public sealed class MyFeatureOptions
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
```

---

## How `[Options]` works

### Section name

The config section is **inferred** from the class name by stripping the `Options` suffix:

| Class name | Inferred section |
|---|---|
| `VideoTranscriptionOptions` | `VideoTranscription` |
| `StripeOptions` | `Stripe` |

Provide an explicit section name whenever inference doesn't match the actual appsettings key:

```csharp
[Options("AzureCognitiveServices:Speech")]
public sealed class SpeechOptions { ... }
```

Nested section paths use `:` as the delimiter.

### Validation at startup

`ValidateOnStart = true` emits both `.ValidateDataAnnotations()` and `.ValidateOnStart()`. This
causes the app to refuse to start when a `[Required]` field is missing or any data annotation
fails, rather than throwing at the first runtime use.

Always use `ValidateOnStart = true` unless you have a deliberate reason not to.

### What gets registered

All three options interfaces are registered automatically:

| Interface | Lifetime | Use when |
|---|---|---|
| `IOptions<T>` | Singleton | Default; values never reload |
| `IOptionsSnapshot<T>` | Scoped | Values must reload per request |
| `IOptionsMonitor<T>` | Singleton with change events | Long-lived singleton that needs live reload |

Inject `IOptions<T>` unless you specifically need one of the other two.

---

## Consuming options

```csharp
internal sealed class MyService(IOptions<MyFeatureOptions> _options)
{
    public void DoWork()
    {
        var key = _options.Value.ApiKey;
        // ...
    }
}
```

---

## Named options (multiple instances of the same type)

When you need two configurations of the same options class (e.g., primary vs. replica):

```csharp
[Options("Databases:Primary", Name = "Primary")]
[Options("Databases:Replica", Name = "Replica")]
public sealed class DatabaseConnectionOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public bool ReadOnly { get; set; }
}
```

Resolve named options via `IOptionsSnapshot<T>.Get(name)` or `IOptionsMonitor<T>.Get(name)`.

---

## Rules

- Classes are `public sealed class` — required for the options binding infrastructure.
- No business logic — only property declarations and optional validation methods.
- Never manually register options in a plugin; `[Options]` handles all registration.
- Always apply `[Required]` to properties that must be present in configuration.
- Always use `ValidateOnStart = true` so failures surface at startup, not at runtime.
