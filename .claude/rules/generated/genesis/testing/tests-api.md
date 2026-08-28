---
# AUTO-GENERATED from .github/instructions/genesis/testing/tests-api.instructions.md — do not edit
paths:
  - "**/*ApiTests*.cs"
---
# API integration tests

- Split one feature suite into a sealed partial base file plus one partial file per
  logical endpoint group.
- The base owns `ApiTestBase`, fixture wiring, shared strict mocks, and
  `OnModifyBuilder`.
- xUnit uses `IClassFixture<T>` and the base cancellation token. TUnit uses
  `[ClassDataSource(..., Shared = PerClass)]` and injected method cancellation tokens.
- Use `GetUnauthenticatedClient`, `GetUserClient`, and `GetAdminClient` for their
  intended auth scenarios; cover relevant 401/403 behavior.
- Create data through `DataSimulator` when a helper exists; do not seed API tests by
  calling repositories directly.
- Initialize/reset mocks in `OnModifyBuilder`. Add setups there only when every test
  invokes them; DI-construction property setups are the documented exception.
- Never use `CancellationToken.None` or direct `TestContext.Current` access.
- Assert the HTTP status and exact response/persistence/side-effect contract.
