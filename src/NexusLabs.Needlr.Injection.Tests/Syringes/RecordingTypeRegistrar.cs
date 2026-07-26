using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Type registrar that records the assemblies it was given and registers a single
/// resolvable service so tests can prove the configured registrar actually ran.
/// </summary>
[DoNotAutoRegister]
public sealed class RecordingTypeRegistrar : ITypeRegistrar
{
    private readonly List<Assembly> _observedAssemblies = [];

    /// <summary>
    /// Gets the assemblies observed across every registration call.
    /// </summary>
    public IReadOnlyList<Assembly> ObservedAssemblies => _observedAssemblies;

    /// <summary>
    /// Gets the type filterer observed during the last registration call.
    /// </summary>
    public ITypeFilterer? ObservedTypeFilterer { get; private set; }

    /// <inheritdoc />
    public void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies)
    {
        ObservedTypeFilterer = typeFilterer;
        _observedAssemblies.AddRange(assemblies);
        services.AddSingleton<ISyringeTestService, SyringeTestService>();
    }
}
