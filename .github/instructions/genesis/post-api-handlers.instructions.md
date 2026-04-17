---
applyTo: "**/*PostApi.cs"
---

# Post API Handler Rules (`IPostContentHandler`)

Every `*PostApi` class is a posting handler for a specific social platform.

## Interface

Must implement `IPostContentHandler`:

```csharp
public sealed class MyPlatformPostApi : IPostContentHandler
{
    public bool CanHandle(SocialPlatformId socialPlatformId)
        => MyPlatform.Id == socialPlatformId;

    public async Task<TriedEx<PostResult>> TryPostAsync(
        Content content,
        CancellationToken cancellationToken) => await
    Try.GetAsync<PostResult>(_logger, async () =>
    {
        // implementation
    });
}
```

## `CanHandle`

Return `true` for **only** this platform's hardcoded `SocialPlatformId`. Never return true for multiple platforms in the same handler.

## `TryPostAsync` — required steps in order

1. **Validate required content keys first:**
   ```csharp
   var missingError = content.GetErrorIfRequiredDataMissing("Slug", "Title");
   if (missingError is not null) return missingError;
   ```

2. **Wrap the full body in `Try.GetAsync<PostResult>`** — never throw; return errors via the Try pattern.

3. **Fetch auth credentials** via the platform's auth client. Return an error immediately if auth is not found:
   ```csharp
   var authResult = await _authClient.TryGetAuthAsync(content.SocialAccountId, cancellationToken);
   if (!authResult.Success) return authResult.Error;
   ```

4. **Check for post splitting** before delegating:
   ```csharp
   var splitPost = content.ShouldSplitPost();
   ```

5. **Delegate the actual API call** to a separate `*Client` class — `*PostApi` must NOT make direct HTTP calls. The handler orchestrates; the client executes.

## Auto-discovery

`IPostContentHandler` implementations are auto-discovered by Needlr's source generator and registered as **Transient** automatically. Do NOT register them in a plugin.

## Reference

- `documentation/architecture/patterns/social-media-posting.md`
