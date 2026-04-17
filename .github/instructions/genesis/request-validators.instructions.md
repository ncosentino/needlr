---
applyTo: "**/*RequestValidator.cs"
---

# Request Validator Rules (FluentValidation)

`*RequestValidator` classes implement FluentValidation's `AbstractValidator<TRequest>` and validate incoming web API request bodies before the endpoint delegates to a UnitOfWork.

## Shape

```csharp
using FluentValidation;

namespace NexusAI.Features.Posting.Scheduling;

internal sealed class SchedulePostRequestValidator : AbstractValidator<SchedulePostRequest>
{
    public SchedulePostRequestValidator()
    {
        RuleFor(x => x.SocialAccountIds)
            .NotNull()
            .NotEmpty();

        RuleFor(x => x.TargetDateTimeUtc)
            .Must(x => x is not null && DateTime.TryParse(x, CultureInfo.InvariantCulture, out _))
            .When(x => x.Now is null || x.Now == false)
            .WithMessage("Must specify 'TargetDateTimeUtc' when 'Now' is not set.");

        RuleFor(x => x.Fields)
            .NotNull()
            .NotEmpty();
    }
}
```

## Rules

### Always provide `WithMessage`

Every `RuleFor` chain that can fail with a non-obvious error **must** end with `.WithMessage(...)`. The default FluentValidation messages are generic — provide a message that tells the caller exactly what went wrong and how to fix it:

```csharp
// ❌ WRONG — no message; caller gets "The specified condition was not met"
RuleFor(x => x.ExpiresAt)
    .Must(x => x > DateTimeOffset.UtcNow);

// ✅ CORRECT — explicit message
RuleFor(x => x.ExpiresAt)
    .Must(x => x > DateTimeOffset.UtcNow)
    .WithMessage("'ExpiresAt' must be a future date.");
```

### How to invoke in Carter modules

Call `ValidateAsync()` and return early with the result-pattern conversion — **never throw** to signal validation failure:

```csharp
// In the Carter module endpoint handler:
var validationResult = await validator
    .ValidateAsync(request, cancellationToken)
    .ConfigureAwait(false);
if (!validationResult.IsValid)
{
    return validationResult.ToResult();
}
```

`ThrowIfInvalidAsync()` is **FORBIDDEN** — it uses exceptions for control flow. Always use the `ValidateAsync()` + `ToResult()` pattern shown above.

### Placement

The validator lives in the **same namespace and folder** as the request type it validates. `SchedulePostRequest.cs` and `SchedulePostRequestValidator.cs` are siblings.

### Auto-discovery

Validators are auto-discovered and registered as singletons by Needlr (they are concrete classes implementing `IValidator<T>`). Do NOT manually register them in a plugin's `Configure()`.

### Inject via interface in Carter modules

Always inject `IValidator<TRequest>`, not the concrete class:

```csharp
private static async Task<IResult> SchedulePost(
    [FromBody] SchedulePostRequest request,
    IValidator<SchedulePostRequest> validator,
    ISchedulePostUnitOfWork unitOfWork,
    CancellationToken cancellationToken)
{
    var validationResult = await validator
        .ValidateAsync(request, cancellationToken)
        .ConfigureAwait(false);
    if (!validationResult.IsValid)
    {
        return validationResult.ToResult();
    }

    var result = await unitOfWork.TryScheduleAsync(..., cancellationToken);
    return result.ConvertToResult(...);
}
```

### Scope validation rules

Validation rules belong in the validator — **never** in the Carter module handler or UnitOfWork. The UnitOfWork may enforce invariants via the Try pattern, but syntactic/structural validation of the request (null checks, format checks, range checks) is the validator's job.

### Cross-field rules

Use `.When(...)` for conditional rules and `.Must(...)` for custom predicates. Do not duplicate validation logic across multiple validators or across the validator and the UnitOfWork.
