---
# AUTO-GENERATED from .github/instructions/genesis/jobs/job-schedulers.instructions.md — do not edit
paths:
  - "**/*JobScheduler.cs"
---
# Job schedulers

- Every scheduler has an `I*JobScheduler` interface; callers inject the interface.
- A scheduler only builds the job data map/trigger and delegates to
  `OneShotJobScheduler`. It contains no business logic, repository calls, or
  unit-of-work execution.
- Job data-map keys are constants owned by the job class, never inline strings.
- `TryScheduleAsync` requires a cancellation token and returns
  `TriedEx<OneShotJobScheduleResult>`.
- Use generated logging for stable job/business identifiers.
- Needlr discovers scheduler implementations; do not register them manually.
- Place the scheduler beside the job in the same vertical slice.
