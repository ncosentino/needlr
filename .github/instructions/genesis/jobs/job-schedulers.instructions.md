---
applyTo: "**/*JobScheduler.cs"
---

# Job Scheduler Rules

A `*JobScheduler` is a dedicated class that encapsulates the logic for scheduling a specific Quartz job. It knows which job type to schedule, how to build its data map, and how to construct the trigger.

## Interface

Every `*JobScheduler` MUST have a corresponding `I*JobScheduler` interface:

```csharp
public interface IScheduledPostJobScheduler
{
    Task<TriedEx<OneShotJobScheduleResult>> TryScheduleAsync(
        UserId ownerUserId,
        ContentId contentId,
        DateTimeOffset targetDateTime,
        CancellationToken cancellationToken);
}
```

Carter modules, unit-of-works, and other callers inject the interface — never the concrete class.

## Implementation

```csharp
internal sealed partial class ScheduledPostJobScheduler(
    ILogger<ScheduledPostJobScheduler> _logger,
    OneShotJobScheduler _oneShotJobScheduler) :
    IScheduledPostJobScheduler
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Scheduling post for content {ContentId} at {TargetDateTime}")]
    private partial void LogScheduling(ContentId contentId, DateTimeOffset targetDateTime);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Scheduled post for content {ContentId} with execution ID {ExecutionId}")]
    private partial void LogScheduled(ContentId contentId, JobExecutionId executionId);

    public async Task<TriedEx<OneShotJobScheduleResult>> TryScheduleAsync(
        UserId ownerUserId,
        ContentId contentId,
        DateTimeOffset targetDateTime,
        CancellationToken cancellationToken) => await
    Try.GetAsync<OneShotJobScheduleResult>(_logger, async () =>
    {
        LogScheduling(contentId, targetDateTime);

        var jobData = new JobDataMap
        {
            [ScheduledPostJob.OwnerUserIdKey] = ownerUserId.Value,
            [ScheduledPostJob.ContentIdKey] = contentId.Value
        };

        var trigger = TriggerBuilder
            .Create()
            .StartAt(targetDateTime)
            .WithIdentity($"ScheduledPost_{contentId.Value}_{Guid.NewGuid()}", "ScheduledPosts")
            .Build();

        var scheduleResult = await _oneShotJobScheduler
            .ScheduleJobAsync<ScheduledPostJob>(ownerUserId, trigger, jobData, cancellationToken);

        if (!scheduleResult.Success)
        {
            return scheduleResult.Error;
        }

        LogScheduled(contentId, scheduleResult.Value.JobExecutionId);
        return new TriedEx<OneShotJobScheduleResult>(scheduleResult.Value);
    });
}
```

## Rules

### Thin orchestrators

The scheduler's only job is to build the data map, construct the trigger, and call `OneShotJobScheduler`. No business logic. No repository calls. No `UnitOfWork` calls.

### Job data map keys

Always use `const` fields defined on the job class for data map keys — never inline string literals:

```csharp
// In ScheduledPostJob.cs:
internal const string ContentIdKey = "ContentId";

// In ScheduledPostJobScheduler.cs:
[ScheduledPostJob.ContentIdKey] = contentId.Value,
```

### Auto-discovery

`*JobScheduler` classes are auto-discovered and registered as singletons by Needlr. Do NOT manually register them in a plugin's `Configure()`.

### Logging

Use `[LoggerMessage]` source-generated logging — never `_logger.LogInformation("...", params)` directly.

### Placement

The scheduler lives in the **same vertical slice folder** as the job it schedules. If the job is `Scheduling/ScheduledPostJob.cs`, the scheduler is `Scheduling/ScheduledPostJobScheduler.cs`.
