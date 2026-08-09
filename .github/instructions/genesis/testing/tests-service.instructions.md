---
applyTo: "**/*.Tests/**/*Service*Tests*.cs,**/*.Tests/*Service*Tests*.cs"
---

# Service tests

- Resolve the subject from the generated DI/test fixture. Do not construct the service
  directly.
- When service behavior reaches persistence, use the production database engine and
  real repository registrations.
- Override only true external/cross-feature boundaries through
  `TestFixtureBuilder.UsingDependency`.
- Keep expensive fixture/service-provider state shared at class scope when the
  repository's fixture supports it.

## Framework setup

For TUnit:

- use a per-class data source for shared infrastructure;
- reset shared mocks in `[Before(Test)]`;
- accept the injected test `CancellationToken` parameter.

For xUnit:

- use `IClassFixture<T>`/constructor injection;
- reset shared mocks in the constructor because xUnit creates a new test instance;
- capture `TestContext.Current.CancellationToken` once per instance.

## Isolation

- Use unique identifiers and data for every test.
- Tests do not depend on order or another test's state.
- Shared builders/entity factories live in a test-helper type and are reused across
  classes.
- Per-test mock setups stay in the test method.
- Verify strict mock expectations before the test completes.
