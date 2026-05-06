---
description: Parameter optionality, default-value resolution, and DTO null-handling semantics for [AgentFunction]-decorated tools in Needlr's source-generated AIFunction wrappers.
---

# Agent Functions

Methods decorated with `[AgentFunction]` (typically grouped under an `[AgentFunctionGroup("name")]` class) are discovered by the Needlr source generator and wrapped as `Microsoft.Extensions.AI.AIFunction` instances at compile time. The generator emits a JSON schema describing each parameter and an `InvokeCoreAsync` method that decodes `AIFunctionArguments` payloads into typed parameter values.

This page documents the **parameter optionality** and **default-value resolution** semantics — how the framework decides what value to pass when the model omits an argument, sends `null`, or sends an explicit value.

---

## Why this matters: provider-shape transparency

`Microsoft.Extensions.AI.IChatClient` implementations don't all deliver tool-call arguments the same way:

- **GitHub Copilot's `IChatClient`** materializes a typed default for every property in the schema before delivering arguments — even properties the model omitted from its tool call.
- **`AzureOpenAIClient.AsIChatClient()`** (and most other providers) parses the model's tool-call JSON literally — if the model omits a property, it is genuinely absent from the `AIFunctionArguments` dictionary or arrives as `JsonValueKind.Null` / `JsonValueKind.Undefined`.

Both behaviors are legal under the JSON Schema spec (any property not listed in `required` may be omitted by the producer). The generator absorbs this asymmetry so **swapping `IChatClient` providers is transparent to tool authors**. You write a normal C# method with normal C# defaults and nullability annotations; the generator enforces those semantics regardless of which client delivers the arguments.

---

## Top-level parameter resolution

For each non-`CancellationToken` parameter, the generator branches on three flags captured from the C# declaration: `IsRequired`, `HasDefault`, and `IsNullable`.

### Required (not nullable, no default)

```csharp
[AgentFunction]
public string DoIt(bool flag) => flag.ToString();
```

If the argument is **missing**, `null`, or `JsonValueKind.Null` / `JsonValueKind.Undefined`, the wrapper throws:

```text
System.ArgumentException: Required argument 'flag' was not supplied to AIFunction 'MyTool.DoIt'.
```

The exception message includes the parameter name, the type, and the method — operators get an actionable error instead of a cryptic `InvalidOperationException("Cannot extract bool argument from null.")` deep in the extractor stack.

### Defaulted (any nullability, with a C# default value)

```csharp
[AgentFunction]
public string DoIt(bool flag = false) => flag.ToString();          // → false
public string DoIt(int max = 5) => max.ToString();                 // → 5
public string DoIt(string? p = "x") => p ?? string.Empty;          // → "x"
public string DoIt(int? n = 5) => n.ToString() ?? string.Empty;    // → 5
```

If the argument is missing or `null` / `Undefined`, the wrapper falls back to the **C# default literal**. Note: **`HasDefault` wins over `IsNullable`** — `string? p = "x"` resolves to `"x"`, not `null`. The reasoning: the `?` annotation tells the framework "null is a legal value the model can send"; the `= "x"` clause tells it "but if the model omits it, use this fallback". Both flags coexist.

### Nullable, no default

```csharp
[AgentFunction]
public string DoIt(string? label) => label ?? string.Empty;        // → null
public string DoIt(int? offset) => offset?.ToString() ?? "";       // → null
```

If the argument is missing or `null` / `Undefined`, the wrapper falls back to `null`.

### Resolution priority summary

| Argument state \ Parameter | Required | `HasDefault` | `IsNullable`, no default |
| --- | --- | --- | --- |
| Missing key | `ArgumentException` | C# default literal | `null` |
| `JsonValueKind.Null` | `ArgumentException` | C# default literal | `null` |
| `JsonValueKind.Undefined` | `ArgumentException` | C# default literal | `null` |
| Typed value present | extractor decodes it | extractor decodes it | extractor decodes it |

---

## DTO property resolution

When an `[AgentFunction]` parameter is a complex DTO (e.g., `public string Record(MyDto dto)`), the generator emits per-property extraction inside the wrapper. Each property assignment is gated on **both** `TryGetProperty` (key present) and an inline `JsonValueKind.Null` / `Undefined` check (value supplied).

When a property's value is missing or null, the wrapper does **not** assign — it leaves the property at whatever `new MyDto()` established. This means:

- A property with a C# init default (`public string Foo { get; set; } = "default";`) keeps its default.
- A nullable property without an init default (`public string? Bar { get; set; }`) stays at the type's default — `null` for reference types, `0` for value types.
- A non-nullable value-type property with an init default (`public int Count { get; set; } = 5;`) keeps its default.

Example — given the DTO

```csharp
public sealed class MyDto
{
    public string Foo { get; set; } = "default";
    public string? Bar { get; set; }
    public int Count { get; set; } = 5;
}
```

and the payload `{"foo": null, "bar": null, "count": null}`, the wrapper produces a DTO with `Foo == "default"`, `Bar == null`, `Count == 5`.

This is the same outcome as if the model omitted the keys entirely — a `{"prop": null}` payload is treated symmetrically with an absent key.

> **Note:** Init-only properties (`{ get; init; }`) are *not* supported as `[AgentFunction]` DTO properties. The generator emits per-property assignments after `new T()`, which init-only properties reject. Use `{ get; set; }` for DTO properties whose values come from tool-call payloads.

---

## The shared gate: `AgentFrameworkArgumentExtractor.IsArgumentSupplied`

The generator emits a single call to `AgentFrameworkArgumentExtractor.IsArgumentSupplied(object? raw)` at every parameter site to decide between the extractor path and the fallback path. The helper returns:

- `false` when `raw` is `null`, `JsonValueKind.Null`, or `JsonValueKind.Undefined`.
- `true` for any other value (typed primitive, JSON string/number/boolean/array/object).

This is the documented contract mate to the kind-tolerant extractor methods on the same class — extractors assume the value is present and non-null; this helper is how callers verify that. You can use it directly when writing custom AIFunction wrappers by hand:

```csharp
if (!AgentFrameworkArgumentExtractor.IsArgumentSupplied(raw))
{
    // Apply your own fallback policy here.
}
```

---

## Strict extractor semantics

Every extractor method on `AgentFrameworkArgumentExtractor` (`GetStringArgument`, `GetBooleanArgument`, `GetInt32Argument`, `GetGuidArgument`, etc.) throws `InvalidOperationException` on `null`, `JsonValueKind.Null`, and `JsonValueKind.Undefined`. There are no silent coercions. This is intentional: the extractors handle kind-tolerance for *present* values; the generator handles presence/optionality at the wrapper layer where C# parameter metadata lives.

If you call an extractor directly and pass an unguarded value, you'll get a strict exception. Gate with `IsArgumentSupplied` first.

---

## Schema-vs-extraction invariant

The JSON schema's `required` array and the wrapper's extraction code are two views of the same fact. For every parameter:

- If the parameter is `IsRequired` (not nullable, no default), the schema lists it in `required: [...]` **and** the wrapper throws on missing / null / undefined.
- If the parameter is *not* `IsRequired`, the schema omits it from `required` **and** the wrapper falls back to the declared default (or `null` for nullable).

If you ever see a divergence between the schema and the runtime behavior, that's a generator bug — please file an issue.
