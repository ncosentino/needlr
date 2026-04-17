---
applyTo: "**/*.Tests/**/*Repository*Tests*.cs,**/*.Tests/*Repository*Tests*.cs"
---

# Repository Test Rules

These rules apply specifically to repository test files.

## Never instantiate a SUT directly

NEVER use `new MyRepository(...)`. Always resolve the SUT from the DI container:

```csharp
public sealed class MyRepositoryTests : IClassFixture<MySqlContainerFixture>
{
    private static ITestFixture? _testFixture;

    private static IServiceProvider? _serviceProvider;

    public MyRepositoryTests(MySqlContainerFixture mySqlFixture)
    {
        _testFixture ??= new TestFixtureBuilder()
            .UsingMySqlContainerFixture(mySqlFixture)
            .Build();

        _serviceProvider ??= _testFixture.GetOrCreateServiceProvider();
    }

    [Fact]
    public async Task CreateAsync_ValidInput_Persists()
    {
        var repo = _serviceProvider!.GetRequiredService<MyRepository>();
        // ...
    }
}
```

## MySqlContainerFixture — local MySQL, not Testcontainers

`MySqlContainerFixture` connects to a **locally-running MySQL instance** (localhost/127.0.0.1). There is no Testcontainers dependency — a MySQL server must already be running before these tests execute.

This is intentional. Repository tests must run against a real database — faking or substituting the database engine produces false confidence. See the data store testing rules in the general test instructions.

## Shared test helpers

Helpers like "create a valid entity" or "seed prerequisite rows" belong in a **static helper class** (e.g., `MyFeatureTestHelpers`) in the test project — never as `private static` methods inside a single test class. All test classes in the project reuse these helpers.

## Unique identifiers per test

Every test MUST use unique values (Guid-based slugs, randomized user IDs) to prevent unique constraint violations when tests run in parallel or sequentially within the same class.

## No inline SQL in test files

Do not write raw SQL in test files. The only exception is within repository tests themselves to simulate error scenarios that are impossible to produce through the normal code path (e.g., inserting a row that violates a constraint that should never exist in production).

If a repository writes to another domain's table as a side effect, verify it via a first-class repository method — not raw SQL.

## Mock init pattern

In repository tests specifically, `mock.Reset()` is called inside the **constructor** — not in a separate method:

```csharp
private static MockRepository? _mockRepository;
private static Mock<IMyDependency>? _mockDependency;

public MyRepositoryTests(MySqlContainerFixture mySqlFixture)
{
    _testFixture ??= new TestFixtureBuilder()
        .UsingMySqlContainerFixture(mySqlFixture)
        .Build();

    _mockRepository ??= new MockRepository(MockBehavior.Strict);
    _mockDependency ??= _mockRepository.Create<IMyDependency>();
    _mockDependency.Reset();
}
```

## Timestamp tolerance

When asserting timestamps, allow 100ms tolerance.

## Reference

- `documentation/architecture/patterns/testing.md`
- `documentation/testing-guide.md`
