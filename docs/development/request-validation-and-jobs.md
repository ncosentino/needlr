# Request validation and job scheduling

Needlr does not define HTTP request-validation or job-scheduling abstractions. Those
behaviors remain application concerns and should not be added to the core dependency
injection packages.

## FluentValidation integration

`NexusLabs.Needlr.FluentValidation` adapts `IValidator<TOptions>` to
`IValidateOptions<TOptions>`. It supports startup and named-options validation; it is
not an endpoint pipeline and does not decide how an application represents validation
failures.

Consumer validators are ordinary services and can be discovered by Needlr when they
match the selected registration conventions.

## Scheduling boundary

Needlr currently has no Quartz or general scheduler integration. A future integration
must live in a dedicated package and own only registration/composition behavior.
Trigger semantics, persistence, retries, and business workflows remain with the
scheduler or consuming application.
