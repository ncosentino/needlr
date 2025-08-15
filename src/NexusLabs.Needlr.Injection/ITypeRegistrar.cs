using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Provides functionality for registering types in a dependency injection container.
/// </summary>
[DoNotAutoRegister]
public interface ITypeRegistrar
{
    /// <summary>
    /// Registers types from the specified assemblies into the service collection.
    /// </summary>
    /// <param name="services">The service collection to register types into.</param>
    /// <param name="typeFilterer">The type filterer used to determine which types should be registered and their lifetimes.</param>
    /// <param name="assemblies">The assemblies to scan for types to register.</param>
    void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies);
}
