# NDLRGEN021: Positional record must be partial for [Options]

## Cause

A positional record (a record with primary constructor parameters) has the `[Options]` attribute but is not declared as `partial`.

## Rule Description

Positional records like `record Foo(string Bar, int Baz)` do not have parameterless constructors. Microsoft's configuration binder uses `Activator.CreateInstance()` to instantiate options types, which requires a parameterless constructor.

When a positional record is declared as `partial`, Needlr's source generator emits a parameterless constructor that chains to the primary constructor with default values. This enables configuration binding to work.

Non-partial records cannot be extended by source generators, so configuration binding will fail at runtime with a `MissingMethodException`.

## How to Fix

Add the `partial` modifier to the positional record:

```csharp
// ❌ Before: Will fail at runtime
[Options("Database")]
public record DatabaseConfig(string Host, int Port);

// ✅ After: Generator creates parameterless constructor
[Options("Database")]
public partial record DatabaseConfig(string Host, int Port);
```

Alternatively, use a record with init-only properties instead of positional parameters:

```csharp
// ✅ Alternative: Init-only properties have parameterless constructor
[Options("Database")]
public record DatabaseConfig
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 5432;
}
```

## Generated Code

For a partial positional record, Needlr generates:

```csharp
// User code:
[Options("Redis")]
public partial record RedisConfig(string Host, int Port);

// Generated code (OptionsConstructors.g.cs):
public partial record RedisConfig
{
    public RedisConfig() : this(string.Empty, default) { }
}
```

The generated constructor uses:
- `string.Empty` for string parameters
- `default` for value types (0 for int, false for bool, etc.)
- `default!` for other reference types

## When to Suppress

Suppress this warning only if you are manually providing a parameterless constructor or if you are not using the record with configuration binding:

```csharp
#pragma warning disable NDLRGEN021
[Options("Custom")]
public record CustomConfig(string Value)
{
    public CustomConfig() : this("default-value") { }
}
#pragma warning restore NDLRGEN021
```

## See Also

- [Options Binding](../options.md)
- [NDLRGEN020 - Options not compatible with AOT](NDLRGEN020.md)
