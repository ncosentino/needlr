---
applyTo: "**/*ApiTests*.cs"
---

# API Integration Test Rules

These rules apply specifically to API integration test files (`*ApiTests*.cs`).

## File structure — partial classes

API tests for a feature are split across multiple files using `partial class`:

- **`MyFeatureApiTests.Base.cs`** — the base partial class with fixture setup and shared mocks
- **`MyFeatureApiTests.[EndpointGroup].cs`** — one partial file per logical endpoint group

```csharp
// MyFeatureApiTests.Base.cs
public sealed partial class MyFeatureApiTests(
    MySqlContainerFixture mySqlFixture) :
    ApiTestBase(mySqlFixture),
    IClassFixture<MySqlContainerFixture>
{
    private static MockRepository? _mockRepository;
    private static Mock<IMyExternalDependency>? _mockDependency;

    protected override TestFixtureBuilder OnModifyBuilder(TestFixtureBuilder builder)
    {
        _mockRepository ??= new MockRepository(MockBehavior.Strict);

        _mockDependency ??= _mockRepository.Create<IMyExternalDependency>();
        _mockDependency.Reset();

        return builder.UsingDependency(_mockDependency.Object);
    }
}
```

```csharp
// MyFeatureApiTests.Create.cs
public sealed partial class MyFeatureApiTests
{
    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var client = GetUserClient();
        // ...
    }
}
```

## HTTP clients

Always use the typed client methods — never inject `HttpClient` directly:

| Method | When to use |
|--------|-------------|
| `GetUnauthenticatedClient()` | Testing 401 scenarios |
| `GetUserClient()` | Normal authenticated user |
| `GetAdminClient()` | Admin-only endpoints |

Always test all relevant auth scenarios for each endpoint (401 for unauthenticated, 403 for wrong role if applicable).

## Test data

Use `DataSimulator` for test data creation where a method exists. Do not manually insert test data by calling repositories directly in API test files.

## Mocks

In API tests specifically, mocks are initialized inside `OnModifyBuilder` rather than the constructor:

```csharp
protected override TestFixtureBuilder OnModifyBuilder(TestFixtureBuilder builder)
{
    _mockRepository ??= new MockRepository(MockBehavior.Strict);

    _mockDependency ??= _mockRepository.Create<IMyExternalDependency>();
    _mockDependency.Reset();

    return builder.UsingDependency(_mockDependency.Object);
}
```

**NEVER add mock setups inside `OnModifyBuilder`** unless the setup is GUARANTEED to be invoked in EVERY single test. Exception: property accessors read during DI service construction may be set up after `Reset()` — mark them with a comment indicating they are DI-construction requirements.

## CancellationToken

Use the `CancellationToken` property from `ApiTestBase` — do NOT use `TestContext.Current.CancellationToken` or `CancellationToken.None`.

## Reference

- `documentation/architecture/patterns/testing.md`
