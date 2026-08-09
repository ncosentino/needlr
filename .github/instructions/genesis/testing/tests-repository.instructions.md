---
applyTo: "**/*.Tests/**/*Repository*Tests*.cs,**/*.Tests/*Repository*Tests*.cs"
---

# Repository tests

- Resolve the repository from the fixture-built service provider; never instantiate
  it directly.
- Use the template's real production database engine. `MySqlContainerFixture` expects
  a locally available MySQL service; it is not a repository mock or in-memory
  substitute.
- Build/reuse the service provider through `TestFixtureBuilder` and the generated
  plugin pipeline.
- Reusable seed/entity helpers live in a static test helper shared by test classes.
- Every test uses unique identifiers so parallel runs do not collide.
- Do not put inline SQL in tests except to create an otherwise unreachable invalid
  database state. Verify cross-domain side effects through first-class repositories.
- Repository mocks use the shared strict `MockRepository`; reset each mock in the
  xUnit constructor or TUnit `[Before(Test)]` hook.
- Timestamp assertions allow at most 100 ms tolerance.
- Keep repository behavior assertions exact: persisted values, counts, transaction
  effects, and missing/error contracts.
