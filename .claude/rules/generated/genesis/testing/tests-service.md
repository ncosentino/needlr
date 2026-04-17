---
# AUTO-GENERATED from .github/instructions/genesis/testing/tests-service.instructions.md — do not edit
paths:
  - "**/*.Tests/**/*Service*Tests*.cs"
  - "**/*.Tests/*Service*Tests*.cs"
---
# Service Test Rules

These rules apply specifically to service test files.

## Testing with a real database

Service tests inject `MySqlContainerFixture` for the same reason as repository tests — even when the primary subject is business logic, the underlying repository calls must run against a real database to give meaningful results. Faking or mocking the database layer produces false confidence. See the data store testing rules in the general test instructions.

## Never instantiate a SUT directly

NEVER use `new MyService(...)`. Always resolve the SUT from the DI container:

```csharp
public sealed class MyServiceTests : IClassFixture<MySqlContainerFixture>
{
    private static ITestFixture? _testFixture;
    private static IServiceProvider? _serviceProvider;
    private static MockRepository? _mockRepository;
    private static Mock<IMyDependency>? _mockDependency;
	
	// NOTE: this is xUnit specific, but conceptually we want a test-context
	// cancellation token for usage across all tests.
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
	
    public MyServiceTests(MySqlContainerFixture mySqlFixture)
    {
        _mockRepository ??= new MockRepository(MockBehavior.Strict);
		
        _mockDependency ??= _mockRepository.Create<IMyDependency>();
        _mockDependency.Reset();

		// NOTE: you may not be in a code base with a dedicated test
		// fixture builder, but this is a pattern used to configure
		// dependency injection as-close-as-possible to production
		// and then you can override test-specific needs
        _testFixture ??= new TestFixtureBuilder()
            .UsingMySqlContainerFixture(mySqlFixture)
            .UsingDependency(_mockDependency.Object)
            .Build();
			
		// NOTE: the service provider field is static assuming that it is
		// expensive to build it. if a test fixture caches it, then it is
		// acceptable to keep it simply as an instance field.
		_serviceProvider ??= _testFixture.GetOrCreateServiceProvider();
    }

    [Fact]
    public async Task DoSomethingAsync_ValidInput_Succeeds()
    {
        var service = _serviceProvider.GetRequiredService<IMyService>();
        
		// ... do the test
		
		_mockRepository.VerifyAll();
    }
}
```

## Shared test helpers

- Helpers like "create a valid entity" or "build a create input" belong in a **static helper class** (e.g., `MyFeatureTestHelpers`) in the test project — never as `private static` methods inside a single test class.
- All test classes in the project reuse these helpers. Strive to factor out reusable setup and assertions to a shared spot to keep tests as simple to read as possible by not duplicating setup and validation code.

## Unique identifiers per test

- Every test MUST use unique values (Guid-based slugs, randomized user IDs) to prevent state bleed when tests run in parallel or sequentially within the same class.
- Tests within a class will run sequentially, but they MUST be runnable in any order. It is FORBIDDEN for a test to depend on the state of another test or require particular order.
- Tests across classes MUST be entirely isolated so that ANY test class can run in parallel with another.

## Mock init pattern

In service tests, specifically when using a static test fixture or service provider, `mock.Reset()` is called inside the **constructor** — not in a separate method:

```csharp
private static MockRepository? _mockRepository;
private static Mock<IMyDependency>? _mockDependency;

public MyServiceTests(MySqlContainerFixture mySqlFixture)
{
    _mockRepository ??= new MockRepository(MockBehavior.Strict);
    _mockDependency ??= _mockRepository.Create<IMyDependency>();
    _mockDependency.Reset();

    _testFixture ??= new TestFixtureBuilder()
        .UsingMySqlContainerFixture(mySqlFixture)
        .UsingDependency(_mockDependency.Object)
        .Build();
}
```
