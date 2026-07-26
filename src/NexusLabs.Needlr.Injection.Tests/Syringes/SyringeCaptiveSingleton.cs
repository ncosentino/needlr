namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Singleton that captures a scoped dependency, producing a lifetime mismatch that
/// <see cref="ConfiguredSyringe"/> verification is expected to detect.
/// </summary>
public sealed class SyringeCaptiveSingleton : ISyringeCaptiveSingleton
{
    private readonly ISyringeTestService _dependency;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyringeCaptiveSingleton"/> class.
    /// </summary>
    /// <param name="dependency">The shorter-lived dependency being captured.</param>
    public SyringeCaptiveSingleton(ISyringeTestService dependency)
    {
        _dependency = dependency;
    }

    /// <summary>
    /// Gets the captured dependency.
    /// </summary>
    public ISyringeTestService Dependency => _dependency;
}
