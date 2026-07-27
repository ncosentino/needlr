using System.Reflection;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Assembly provider that returns a fixed assembly list.
/// </summary>
[DoNotAutoRegister]
public sealed class RecordingAssemblyProvider : IAssemblyProvider
{
    private readonly IReadOnlyList<Assembly> _assemblies;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingAssemblyProvider"/> class.
    /// </summary>
    /// <param name="assemblies">The assemblies to return from every call.</param>
    public RecordingAssemblyProvider(IReadOnlyList<Assembly> assemblies)
    {
        _assemblies = assemblies;
    }

    /// <summary>
    /// Gets the number of times candidate assemblies were requested.
    /// </summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<Assembly> GetCandidateAssemblies()
    {
        CallCount++;
        return _assemblies;
    }
}
