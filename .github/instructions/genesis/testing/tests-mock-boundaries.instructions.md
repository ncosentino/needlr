---
applyTo: "**/*.Tests/**/*.cs,**/*.Tests/*.cs"
---

# Mock Boundary Rules

These rules govern what may and may not be mocked or faked in tests. They apply to ALL test types
(repository, service, API integration). Violations are treated as defects.

## Rule 1 — No handwritten fakes or stubs

Handwritten fake/stub/in-memory implementations of same-domain production abstractions are
**FORBIDDEN**. Do not create or use replacements such as `FakeRepository`, `FakeService`,
`InMemoryCache`, or similar test-only implementations for dependencies owned by the same
feature/domain as the SUT.

Framework-provided test doubles (e.g., `TestTimeProvider`, `NullLogger<T>`) are allowed because
they replace framework infrastructure, not domain behavior.

## Rule 2 — Never mock same-domain types

Mocking (via Moq or any mocking library) a type that belongs to the **same domain** as the SUT is
**FORBIDDEN**. Same-domain dependencies must be real, resolved from the DI container via
`TestFixtureBuilder` or `ApiTestBase`.

### What is "same-domain"?

A dependency is **same-domain** if it is owned by the same feature root as the SUT and represents
that feature's business logic, repository logic, orchestration, or domain behavior. For example,
if the SUT is `NexusAI.Features.VideoTranscription.VideoTranscriptionService`, then
`IVideoTranscriptionPipelineService`, `IAzureVideoIndexerClient`, and
`VideoTranscriptionCacheProvider` are all same-domain — they live in the same feature and implement
that feature's logic.

### What is a "boundary dependency"?

Dependencies that cross a system, vendor, or process boundary are **boundary dependencies** and
**may** be mocked:

| Category | Examples |
|----------|----------|
| HTTP | `IHttpClientFactory`, `HttpMessageHandler` |
| SDK / cross-feature contracts | `IContentClient`, `IMediaStorageManager`, `IJobProgressReporter` |
| Third-party frameworks | `ISchedulerFactory` (Quartz), `IScheduler` (Quartz) |
| Framework infrastructure | `ITimeProvider`, `ILogger<T>`, `IOptions<T>` |
| Auth / token providers | Thin interfaces wrapping external credential acquisition |

Cross-feature boundaries are also mockable: if Feature A talks to Feature B via a MassTransit
contract or an SDK client interface, that interface is a boundary and may be mocked in Feature A's
tests.

### Exception process

If a same-domain type genuinely cannot be used as a real dependency in tests, you MUST:
1. Get explicit approval from the user before mocking it
2. Add a comment in the test explaining why the exception was necessary

## Rule 3 — Extract external calls, don't mock large classes

When a class cannot be functionally tested because a **small part** makes an external call (e.g.,
Azure SDK credential acquisition, third-party token exchange, direct socket I/O), extract **only
that external call** behind a thin interface. This maximizes real code coverage while minimizing
mock surface area.

Apply the same design heuristic used in N+1 prevention: **extract the narrowest boundary
necessary**, not the whole surrounding class.

```csharp
// ❌ WRONG — mocking the entire client because it calls Azure Identity internally
Mock<IAzureVideoIndexerClient> _mockViClient;
// All URL construction, response parsing, error handling goes UNTESTED

// ✅ CORRECT — extract only the external call behind a thin interface
internal interface IArmTokenProvider
{
    Task<AccessToken> GetArmTokenAsync(CancellationToken cancellationToken);
}

// AzureVideoIndexerClient now takes IArmTokenProvider + IHttpClientFactory
// Tests mock only IArmTokenProvider (credential acquisition) and IHttpClientFactory (HTTP)
// ALL client logic (URL construction, JSON parsing, error mapping) is tested REAL
```

### When to apply this pattern

- The class under test has a method that directly calls an external SDK (Azure Identity, AWS SDK,
  third-party auth library) that cannot be intercepted via `IHttpClientFactory`
- The external call is a small fraction of the class's overall logic
- Mocking the entire class would leave significant business logic untested

### When NOT to apply this pattern

- The dependency is already a thin boundary adapter with no business logic — mock it directly
- The class IS the boundary adapter (e.g., a pure HTTP client wrapper) — mock `IHttpClientFactory`
  or `HttpMessageHandler` instead
- The external call can already be intercepted via existing mock points (e.g., HTTP calls go
  through `IHttpClientFactory`)

## Rule 4 — Never fake or substitute the data store

Tests that involve data access MUST run against a **real database instance** — either a locally-running instance or a container, depending on what the repository uses.

- **NEVER mock a repository** to test the business logic above it. Mocking at the repository boundary means the real queries are never exercised, which produces false confidence.
- **NEVER use an in-memory or fake database substitute** (e.g., SQLite in-memory, in-memory EF Core, hand-rolled stubs). These do not behave identically to the production database engine.
- **NEVER use a different database engine than production**. Engine-specific behavior — locking, collation, JSON handling, index usage — differs in ways that only surface in production.
- The test database may contain data from other tests. Always use unique identifiers per test to avoid conflicts.
