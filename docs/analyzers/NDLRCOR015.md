# NDLRCOR015: [RegisterAs\<T>] type argument not implemented

## Summary

The type specified in `[RegisterAs<T>]` must be an interface that the decorated class actually implements.

## Description

When using `[RegisterAs<T>]` to control which interface a class is registered as in dependency injection, the type argument `T` must be an interface that the class implements. If the class doesn't implement the specified interface, the registration would be invalid and the service could not be resolved.

## Severity

**Error** - This is a compile-time error because the service registration would fail at runtime.

## Example

### Invalid Code

```csharp
public interface IReader { string Read(); }
public interface IWriter { void Write(string data); }

// ❌ Error: MyService does not implement IWriter
[RegisterAs<IWriter>]
public class MyService : IReader
{
    public string Read() => "data";
}
```

### Valid Code

```csharp
public interface IReader { string Read(); }
public interface IWriter { void Write(string data); }
public interface ILogger { void Log(string message); }

// ✅ OK: MyService implements IReader
[RegisterAs<IReader>]
public class MyService : IReader, IWriter, ILogger
{
    public string Read() => "data";
    public void Write(string data) { }
    public void Log(string message) { }
}
```

## How to Fix

1. **Add the interface to the class** - Make the class implement the interface specified in `[RegisterAs<T>]`
2. **Change the type argument** - Use a different interface that the class already implements
3. **Remove the attribute** - If you want all interfaces registered, remove `[RegisterAs<T>]` entirely

## When to Suppress

Do not suppress this diagnostic. If the class doesn't implement the interface, the registration is invalid and will fail.

## See Also

- [RegisterAs Documentation](../register-as.md)
