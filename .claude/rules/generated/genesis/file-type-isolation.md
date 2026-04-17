---
# AUTO-GENERATED from .github/instructions/genesis/file-type-isolation.instructions.md — do not edit
paths:
  - "**/*.cs"
---
# File and Type Isolation Rules

## One public/internal type per file

Every non-private type must be in its **own dedicated file**. The filename must exactly match the type name:

```
FooBar.cs          → public sealed class FooBar
IFooBar.cs         → public interface IFooBar
FooBarResult.cs    → public sealed record FooBarResult
FooBarId.cs        → public readonly record struct FooBarId
```

```csharp
// ❌ WRONG — two non-private types in one file
// File: AzureSpeechBatchClient.cs

internal sealed record AzureTranscriptionSubmitResult(Uri Self, Uri FilesLink);  // must be its own file
internal sealed record AzureTranscriptionStatus(string Status, string? Error);   // must be its own file

internal sealed class AzureSpeechBatchClient(...) { ... }

// ✅ CORRECT
// AzureTranscriptionSubmitResult.cs → internal sealed record AzureTranscriptionSubmitResult(...)
// AzureTranscriptionStatus.cs       → internal sealed record AzureTranscriptionStatus(...)
// AzureSpeechBatchClient.cs         → internal sealed class AzureSpeechBatchClient(...)
```

## Allowed exceptions

The following nested or co-located types are explicitly allowed and do NOT need their own file:

### 1. Private deserialization DTOs inside an HTTP client

JSON response shapes used solely for deserializing external API responses may be `private sealed record` types nested within the client class:

```csharp
internal sealed class AzureSpeechBatchClient(...)
{
    // ✅ Private deserialization types — used only by this class, never escape it
    private sealed record AzureTranscriptionSubmitResponse(Uri? Self, AzureLinks? Links, string Status);
    private sealed record AzureLinks(Uri? Files);
}
```

The key test: **does the type escape the class?** If any public or internal method returns it or takes it as a parameter, it must be in its own file.

### 2. Private DB row DTOs inside a repository

Dapper mapping types used only within a single repository method are allowed as private nested records. Repository-specific instructions provide more detail.

### 3. Small inline request/response records in a Carter module

Small `*Request` or `*Response` records may be defined in the same file as the Carter module that exclusively uses them. Carter module-specific instructions provide more detail.

## Interfaces alongside implementations — NOT allowed

Do not co-locate an interface and its implementation in the same file:

```csharp
// ❌ WRONG — interface and class in the same file
public interface IFooBarScheduler { ... }
public sealed class FooBarScheduler : IFooBarScheduler { ... }

// ✅ CORRECT — separate files
// IFooBarScheduler.cs
// FooBarScheduler.cs
```

## Summary decision table

| Type visibility | Used outside the defining class? | Rule |
|----------------|----------------------------------|------|
| `public` or `internal` | — | Own file, always |
| `private` | No | May be nested inside the class |
| `private` | Yes (returned or passed externally) | Must be extracted to its own file |
