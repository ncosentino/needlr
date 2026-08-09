---
applyTo: "**/*.Tests/**/*.cs,**/*.Tests/*.cs"
---

# Test code

Follow the project's framework. Genesis defaults to TUnit; xUnit projects keep xUnit
syntax.

## Structure

- Do not use `#region`, divider comments, Arrange/Act/Assert comments, or ephemeral
  test IDs.
- Put fake implementations, builders, and other helper types in separate files.
- Put reusable setup/builders in shared test helpers rather than private methods copied
  across classes.
- Each test is independent, order-free, and uses unique data when state is shared.

## Async and cancellation

- Test methods are async when the subject is async.
- Do not use `.ConfigureAwait`, `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`,
  `Thread.Sleep`, or arbitrary `Task.Delay`.
- Coordinate with observable async signals. `TaskCompletionSource<T>` uses
  `RunContinuationsAsynchronously` and a generous deadlock timeout.
- Use the test context cancellation token. TUnit injects a
  `CancellationToken` parameter; xUnit uses `TestContext.Current.CancellationToken`.
- A custom token source is linked to the test token.

## Test doubles

- Use `NullLogger<T>.Instance` when logging is not asserted.
- Create Moq dependencies through one `MockRepository(MockBehavior.Strict)`; do not
  instantiate loose `Mock<T>` objects directly.
- Reset shared mocks before each test.
- Put per-test setups in the test. Shared setup contains only calls every test makes or
  DI-construction requirements, identified by a concise comment.
- Call `VerifyAll()` when strict mocks participate in the test.

## Assertions

- TUnit assertions are awaited and the test returns `Task`
  (`TUnitAssertions0002`).
- Never assert a constant/literal (`TUnitAssertions0005`); assert a value produced by
  the system under test.
- Assert exact expected values. Presence checks also assert collection count/state.
- Do not place assertions inside conditional branches except data-driven scenario
  selection.
- Boolean and relative assertions include a reason (`Because(...)` in TUnit or the
  message parameter in xUnit).
- Use dedicated comparison and exception assertions, not comparisons inside
  `IsTrue`/`Assert.True` or `Record.ExceptionAsync`.
- Verify explicit call counts when call count is the contract; verify `Times.Never`
  for prohibited calls.

## Tried result assertions

- xUnit uses `Assert.TrySucceeded` / `Assert.TryFailed`.
- TUnit projects reference `NexusLabs.TUnit.Assertions` and use awaited
  `Succeeded()` / `Failed().With<TException>()`.
- Assert the complete result before using its value/error. NLT0001 forbids separate
  member assertions.
- Do not add custom unwrap/value-or-throw helpers.
- TUnit projects do not add Coverlet packages or runsettings.
