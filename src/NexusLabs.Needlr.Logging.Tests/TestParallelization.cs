using Xunit;

// The tests mutate process-wide NeedlrCancellationLogging state and environment variables,
// so they must not run in parallel with one another.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
