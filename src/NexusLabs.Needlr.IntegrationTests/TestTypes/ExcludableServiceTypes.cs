namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Marker interface for types that should be excluded via Except&lt;T&gt;().
/// </summary>
public interface IExcludableService
{
}

/// <summary>
/// A service that implements IExcludableService and should be excluded
/// when Except&lt;IExcludableService&gt;() is used.
/// </summary>
public sealed class ExcludableServiceA : IExcludableService
{
}

/// <summary>
/// Another service implementing IExcludableService for testing.
/// </summary>
public sealed class ExcludableServiceB : IExcludableService
{
}

/// <summary>
/// A service that does NOT implement IExcludableService.
/// Should NOT be excluded by Except&lt;IExcludableService&gt;().
/// </summary>
public interface INonExcludableService
{
}

/// <summary>
/// Implementation that should remain registered after Except&lt;IExcludableService&gt;().
/// </summary>
public sealed class RegularServiceImpl : INonExcludableService
{
}

/// <summary>
/// A service that implements BOTH IExcludableService and INonExcludableService.
/// Should be excluded because it implements IExcludableService.
/// </summary>
public sealed class MixedExcludableService : IExcludableService, INonExcludableService
{
}
