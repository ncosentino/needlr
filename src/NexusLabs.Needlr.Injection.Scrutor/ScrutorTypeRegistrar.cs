using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Scrutor;

/// <summary>
/// Registers types from assemblies using Scrutor.
/// </summary>
/// <remarks>
/// Scrutor does not traverse up the inheritance hierarchy of interfaces
/// to look for the <see cref="DoNotAutoRegisterAttribute"/>. As a result,
/// types that implement an interface with this attribute will still be 
/// registered unless the attribute is applied directly to the type itself.
/// </remarks>
public sealed class ScrutorTypeRegistrar : ITypeRegistrar
{
    /// <inheritdoc />
    public void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies)
    {
        services
            .Scan(x => x
            .FromAssemblies(assemblies)
            .AddClasses(classes =>
                classes
                    .WithoutAttribute<DoNotAutoRegisterAttribute>()
                    .Where(type => typeFilterer.IsInjectableScopedType(type)),
                publicOnly: false)
            .AsSelfWithInterfaces()
            .WithScopedLifetime());

        services
            .Scan(x => x
            .FromAssemblies(assemblies)
            .AddClasses(classes =>
                classes
                    .WithoutAttribute<DoNotAutoRegisterAttribute>()
                    .Where(type => typeFilterer.IsInjectableTransientType(type)),
                publicOnly: false)
            .AsSelfWithInterfaces()
            .WithTransientLifetime());

        services
            .Scan(x => x
            .FromAssemblies(assemblies)
            .AddClasses(classes =>
                classes
                    .WithoutAttribute<DoNotAutoRegisterAttribute>()
                    .Where(type => typeFilterer.IsInjectableSingletonType(type)),
                publicOnly: false)
            .AsSelfWithInterfaces()
            .WithSingletonLifetime());
    }
}
