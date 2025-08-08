using Microsoft.Extensions.DependencyInjection;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Scrutor;

public sealed class ScrutorTypeRegistrar : ITypeRegistrar
{
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
