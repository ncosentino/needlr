namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Marker interface for job-like types that should be registered as transient.
/// Simulates IJob from Quartz.
/// </summary>
public interface ITestJob
{
    void Execute();
}

/// <summary>
/// A singleton service that also implements ITestJob.
/// Default lifetime is singleton (parameterless ctor), but should be transient when ITestJob is overridden.
/// </summary>
public sealed class SingletonJobService : ITestJob
{
    public void Execute() { }
}

/// <summary>
/// Another job implementation with singleton default.
/// </summary>
public sealed class AnotherSingletonJob : ITestJob
{
    public void Execute() { }
}

/// <summary>
/// A regular singleton service that does NOT implement ITestJob.
/// Should remain singleton even when ITestJob types are overridden to transient.
/// </summary>
public sealed class RegularSingletonService
{
    public void DoWork() { }
}
