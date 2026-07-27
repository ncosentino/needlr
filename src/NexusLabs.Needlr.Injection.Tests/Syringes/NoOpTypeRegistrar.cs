using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// Type registrar that performs no registrations, used when a test needs full control
/// over the contents of the service collection.
/// </summary>
[DoNotAutoRegister]
public sealed class NoOpTypeRegistrar : ITypeRegistrar
{
    /// <inheritdoc />
    public void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies)
    {
    }
}
