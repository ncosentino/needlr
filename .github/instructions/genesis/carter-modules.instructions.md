---
applyTo: "**/*CarterModule.cs"
---

# Carter Module Rules

## Class declaration

Carter modules **MUST** be `public sealed class`. Never `internal`. Carter discovers modules via reflection and will silently skip `internal` types, resulting in 404s with no build error.

```csharp
// ✅ CORRECT
public sealed class MyFeatureCarterModule : ICarterModule

// ❌ WRONG — Carter cannot discover internal types; routes will not be registered
internal sealed class MyFeatureCarterModule : ICarterModule
```

## Endpoint shape

Endpoints must be thin:

1. Parse and validate input from the request
2. Call a UnitOfWork (never a repository directly)
3. Return result object
4. Do not throw exceptions

```csharp
routeGroup.MapPost("create", async (
    HttpContext context,
    [FromBody] CreateThingRequest request,
    CreateThingUnitOfWork createThingUow,
    CancellationToken cancellationToken) =>
{
    var userId = context.GetRequiredUserId();
    var result = await createThingUow.TryCreateAsync(userId, request.Name, cancellationToken);
    return result.ConvertToResult(id => Results.Ok(new { Id = id.ToString() }));
});
```

## No throwing to return errors

- Use structured data to send back an error instead of throwing
- Exceptions that do escape will be caught by the global error handling middleware

## No repositories at the endpoint boundary

NEVER inject or call a repository directly from an endpoint. All data access flows through a UnitOfWork or service. The endpoint has no awareness of the database.

## Business logic → UnitOfWork

If an endpoint would contain:
- Conditional logic
- Multiple sequential operations
- Calls to more than one service/repository
- Side effects (writes + events + cache invalidation)

...extract that logic into a dedicated `*UnitOfWork` class. The endpoint becomes a one-liner call.

## IDs on responses

`long` is **FORBIDDEN** in response types. Convert all IDs to `string`:

```csharp
Id = Convert.ToString(id.Value, CultureInfo.InvariantCulture)
```

This is because other languages have issues with hangling Int64.

## Request DTO → service-layer input translation

The Carter module owns the translation boundary between HTTP and the service layer. **NEVER pass a web request DTO into a UnitOfWork**. Conversely, never pass a UnitOfWork's result directly over the web API boundary.

Map from the request type to a service-layer `*Input` type before calling the UoW:

```csharp
private static async Task<IResult> CreateSite(
    HttpContext context,
    CreateSiteRequest request,
    ICreateSiteUnitOfWork unitOfWork,
    CancellationToken cancellationToken)
{
    var userId = context.GetRequiredUserId();
    var input = new CreateSiteInput(
        SiteSlug: request.SiteSlug,
        CompanyName: request.CompanyName,
        ...);
    var result = await unitOfWork.TryCreateAsync(input, userId.Value, cancellationToken);
    return result.ConvertToResult(site => Results.Created(..., MapToResponse(site)));
}
```

This enforces the critical boundary: web layer types never bleed into business logic. The UoW can evolve independently of how the HTTP request is shaped, and vice versa.

## Request/response records

Define request or response records in new dedicated files, not inside the Carter module file.

