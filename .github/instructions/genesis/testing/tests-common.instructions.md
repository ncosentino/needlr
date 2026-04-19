---
applyTo: "**/*.Tests/**/*.cs,**/*.Tests/*.cs"
---

# General Test Rules

These rules apply to **all** test files. Some tests will also match other instruction files that have more specific instructions that will take precedent over these.

## NEVER use `#region` or comment groups

- `#region` directives are FORBIDDEN in all test files.
- Comment separators are FORBIDDEN in all test files.
- Inline test-ID labels (e.g., `// A1`, `// B3`, `// F1`) are FORBIDDEN. These are ephemeral planning artifacts with no meaning outside the conversation that produced them. If a test needs a description, use an `<summary>` XML doc comment on the method.

```csharp
// ❌ WRONG — region usage
#region Tests on thing A
#endregion

// ❌ WRONG — comment separator
// --- Tests for thing A --------

// ❌ WRONG — inline test-ID label
// B3
[Fact]
public async Task SomeTest() { }

// ❌ WRONG — section divider comments for helpers
// -------------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------------
```

## Helper types belong in separate files

When a test file contains private helper types (fake implementations, test doubles, builder helpers), extract them into a dedicated `*TestHelpers.cs` file in the same directory. Do NOT use section-divider comments to group them inside the test file.

```csharp
// ❌ WRONG — helpers embedded in the test file with divider comments
// -------------------------------------------------------------------------
// Test infrastructure
// -------------------------------------------------------------------------
private sealed class FakeSink : IDiagnosticsSink { ... }
private sealed class ThrowingSink : IDiagnosticsSink { ... }

// ✅ CORRECT — helpers extracted to a separate file
// FooTestHelpers.cs
internal sealed class FakeSink : IDiagnosticsSink { ... }
internal sealed class ThrowingSink : IDiagnosticsSink { ... }
```

## No arrange/act/assert comments

- NEVER add `// Arrange`, `// Act`, or `// Assert` comments. 
- Use blank lines to separate logical groups.

## Asynchonous Concerns

- NEVER use `.ConfigureAwait` in tests. Test harnesses (like with xUnit) may break concurrency limits if `.ConfigureAwait(false)` is used.
- NEVER use `Task.Delay` or `Thread.Sleep`. Artificial synchronization makes tests slow and non-deterministic. It is FORBIDDEN.
- Poll for observable state instead using a while/deadline loop with `await Task.Yield()` between checks.
- You MUST use a cancellation token from the test context.
  - NEVER use a `default` or `CancellationToken.None` in tests.
  - It is permissable to make your own cancellation token source, but it must be linked to the test context one.
  - Assign the test context's token ONCE as an instance field. DO NOT assign it as a variable needlessly in every test -- it's extra lines for no reason.

## NullLogger

- For `ILogger<T>` dependencies that are not being verified, use `NullLogger<T>.Instance` — NEVER `new Mock<ILogger<T>>()`.
- Tests that are validating logging may use an `ILogger` created from a `MockRepository`.

## Mock setup rules

- Always use a `MockRepository` with `MockBehavior.Strict` — NEVER `new Mock<T>()` directly.
- If using a common dependency injection setup across tests within a class, use a static field to hold the created `IServiceProvider` so it can be shared across tests. Assign it in the constructor using `??=` syntax to initialize it once.
- NEVER add mock setups in the constructor or setup method unless the setup is GUARANTEED to be invoked in EVERY single test in the class.
- Call `mock.Reset()` on any static mock before each test so call records are cleared.
- Per-test setups belong inside the individual test method.
- Exception: property accessors read once during DI service construction may be set up after `Reset()`. Mark them with a comment indicating they are DI-construction requirements.

## Assertions

### Assertion message requirements

The following **must always include a message parameter**.
- `Assert.True`
- `Assert.False` 
- `Assert.GreaterThan`
- `Assert.LessThan` 
- `Assert.InRange` 

Do NOT use them with comparison operators — use dedicated assertion methods instead:

```csharp
// ❌ WRONG — no message
Assert.True(account.IsActive);

// ❌ WRONG — comparison inside Assert.True/False
Assert.True(count == 5, "Expected 5 items");
Assert.True(total > 0, "Expected positive total");

// ✅ CORRECT
Assert.True(account.IsActive, "Expected account to be active after creation");
Assert.Equal(5, count);
Assert.GreaterThan(total, 0, "Expected positive total");
```

### Strong Assertions

- NEVER have an assertion that can pass for multiple conditions. Test scenarios MUST be deterministic so you MUST assert the exact expected value.
- NEVER have assertions inside of conditional checks. The ONLY exception to this is if you are using an xUnit [Theory] (or similar) to run multiple scenarios, and the conditional is for the scenario type.
- ALWAYS check the count of a collection if you are asserting existince of something in the collection. It is a weak assertion to only check the presence of something without ensuring the state of what you are looking at.
- If you are using a MockRepository, you MUST use `.VerifyAll` so that you do not risk unused setups lingering in test code. This is also why we require strict mock usage.
- Assert something is not called (i.e. like a call to a different system) by using a mock and verifying a setup was NOT called. (i.e. in xUnit using `Times.Never`)
- If you are asserting a system was called a certain number of times, you must verify the times called. Otherwise, if it is not the focus of the test, `VerifyAll` will be sufficient.

### Exception assertions

`Record.ExceptionAsync` + `Assert.NotNull` is **FORBIDDEN**. Use:

```csharp
await Assert.ThrowsAsync<InvalidOperationException>(() => service.DoSomethingAsync(badInput, ct));
```

Always assert the specific exception type — never just "an exception was thrown".

### Try result assertions

- When a method returns `TriedEx<T>` or `TriedNullEx<T?>`, use the dedicated `Assert.TrySucceeded` and `Assert.TryFailed` helpers. 
- **Do NOT use `Assert.True(result.Success)` or `Assert.False(result.Success)` on Try result types**
- Do NOT access `.Value` without first asserting success.

#### Asserting success — `Assert.TrySucceeded`

```csharp
TriedEx<ThingId> result = await service.TryCreateAsync(input, userId, ct);
var thingId = Assert.TrySucceeded(result, "Expected service to create the thing successfully");
Assert.NotNull(thingId);
```

#### Asserting failure — `Assert.TryFailed`

```csharp
TriedEx<ThingId> result = await service.TryCreateAsync(badInput, userId, ct);
var error = Assert.TryFailed<ThingId, ArgumentException>(result, "Expected validation failure");
Assert.Contains("Name is required", error.Message);
```

#### Common mistakes

```csharp
// ❌ WRONG — do not use Assert.True/False on Try result types
Assert.True(result.Success);
Assert.False(result.Success);

// ❌ WRONG — do not access .Value without asserting success first
var value = result.Value;

// ✅ CORRECT — use the dedicated helpers
var value = Assert.TrySucceeded(result, "Expected success");
var error = Assert.TryFailed<T, SomeException>(result, "Expected failure");
```