---
applyTo: "**/*.Tests/**/*.cs,**/*.Tests/*.cs"
---

# Test boundaries

## Same-domain code stays real

- Do not write fake/stub/in-memory implementations of production abstractions owned by
  the same feature/domain as the subject.
- Do not mock same-domain services, repositories, orchestration, cache providers, or
  business logic.
- Resolve those dependencies through the real generated DI/test fixture.

Framework test doubles such as `FakeTimeProvider` and `NullLogger<T>` are allowed
because they replace framework infrastructure rather than domain behavior.

## Mock true boundaries only

Mock a dependency only when it crosses a system, vendor, process, or feature boundary,
for example:

- HTTP handlers/client factories;
- provider SDK/token acquisition;
- cross-feature client/contracts;
- schedulers or other third-party framework interfaces;
- auth/credential adapters.

If same-domain code cannot run real in a test, get explicit approval and document the
specific constraint at the setup.

## Extract the narrowest external operation

When one external call prevents testing a larger class, extract only that operation
behind a thin interface. Keep URL construction, parsing, policy, and error mapping in
the real subject under test.

Do not mock an entire class merely because one method reaches an external SDK.

## Data stores

- Data-access tests use the production database engine.
- Do not mock repositories above the data boundary.
- Do not substitute SQLite/in-memory EF or another database engine for production.
- Isolate tests with unique data rather than assuming an empty database.
