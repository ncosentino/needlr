# NDLRMAF030: AgentFunction string parameter described as JSON could be JsonElement

## Cause

An `[AgentFunction]` parameter is typed as `string` and EITHER:

- The parameter name ends with `Json` (Pascal case, e.g. `findingsJson`) or `_json` (snake case, e.g. `notes_json`); OR
- The parameter's `[Description]` attribute text contains `"JSON array"` or `"JSON object"` (case-insensitive).

## Rule Description

The Microsoft Extensions AI `IChatClient` you use determines how tool argument values reach your code. GitHub Copilot's `IChatClient` pre-stringifies values, so a JSON array sent by the model arrives as a `JsonElement` of kind `String`. Most other clients (e.g. `AzureOpenAIClient.AsIChatClient()`) parse the model's tool-call JSON literally — the array stays an `Array`-kind `JsonElement`.

The Needlr source generator emits a kind-tolerant extractor that handles both shapes by calling `JsonElement.GetRawText()` for non-string kinds, so a `string findingsJson` parameter still receives valid JSON text regardless of the underlying client.

This works, but it's a round-trip: the model produced structured JSON, the chat client parsed it, the generator serializes it back to text, and your tool body deserializes it. Typing the parameter as `System.Text.Json.JsonElement` skips the round-trip and gives the tool body direct, typed access to the parsed shape.

This rule is **informational** — code that follows the string-typed pattern still works correctly. It exists as a hint that switching to `JsonElement` improves clarity and avoids unnecessary serialization.

## How to Fix

### Option 1 — Switch to `JsonElement` (recommended for typed access)

```csharp
[AgentFunction]
[Description("Records all findings in a single call.")]
public RecordResult RecordFindings(
    [Description("JSON array of findings.")] JsonElement findings)
{
    if (findings.ValueKind != JsonValueKind.Array)
    {
        return RecordResult.InvalidShape();
    }

    foreach (var entry in findings.EnumerateArray())
    {
        // …
    }

    return RecordResult.Ok();
}
```

### Option 2 — Keep the string parameter (if you prefer text round-tripping)

If your tool body uses `JsonSerializer.Deserialize<T[]>(arg)` and you want to keep that pattern, the existing code already works correctly because the generator coerces non-string kinds to canonical JSON text via `GetRawText()`. To suppress the rule for a specific parameter, use a `#pragma warning disable` block:

```csharp
[AgentFunction]
[Description("Records all findings in a single call.")]
public RecordResult RecordFindings(
#pragma warning disable NDLRMAF030
    [Description("JSON array of findings.")] string findingsJson
#pragma warning restore NDLRMAF030
    )
{
    var findings = JsonSerializer.Deserialize<FindingDto[]>(findingsJson, …);
    // …
}
```

## When to Suppress

Suppress this diagnostic when you have a deliberate reason to receive the JSON as text — for example, when forwarding the raw JSON to another system, when storing it verbatim, or when the deserialization shape is dynamic and you want to defer parsing to a domain-specific helper. The rule is informational, not a defect; suppression is fine when the string-typed shape matches your intent.

## See Also

- [Agent Framework — Tool Functions](../ai-integrations.md)
- [NDLRMAF012](NDLRMAF012.md) — `[AgentFunction]` method missing `[Description]`
- [NDLRMAF013](NDLRMAF013.md) — `[AgentFunction]` parameter missing `[Description]`
