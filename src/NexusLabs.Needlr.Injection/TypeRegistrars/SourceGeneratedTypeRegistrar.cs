using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.TypeRegistrars;

/// <summary>
/// A type registrar that uses compile-time generated registration code instead of runtime reflection.
/// This registrar receives a delegate containing all the registration logic that was generated at compile time.
/// </summary>
/// <remarks>
/// This registrar is intended to be used with C# Source Generators that analyze the codebase at compile time
/// and generate the appropriate service registration code. This eliminates the need for runtime reflection
/// and assembly scanning, improving startup performance.
/// </remarks>
public sealed class SourceGeneratedTypeRegistrar : ITypeRegistrar
{
    private readonly Action<IServiceCollection> _registerAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceGeneratedTypeRegistrar"/> class.
    /// </summary>
    /// <param name="registerAction">
    /// The action that contains the generated registration code. This action should register all
    /// services that were discovered at compile time by the source generator.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registerAction"/> is null.</exception>
    public SourceGeneratedTypeRegistrar(Action<IServiceCollection> registerAction)
    {
        _registerAction = registerAction ?? throw new ArgumentNullException(nameof(registerAction));
    }

    /// <summary>
    /// Registers types using the compile-time generated registration code.
    /// </summary>
    /// <param name="services">The service collection to register types into.</param>
    /// <param name="typeFilterer">
    /// The type filterer (ignored in this implementation as filtering is done at compile time).
    /// </param>
    /// <param name="assemblies">
    /// The assemblies parameter (ignored in this implementation as all types are known at compile time).
    /// </param>
    /// <remarks>
    /// Unlike reflection-based registrars, this implementation ignores the <paramref name="assemblies"/> parameter
    /// because all types to be registered are already known at compile time and encoded in the
    /// <see cref="_registerAction"/> delegate.
    /// </remarks>
    public void RegisterTypesFromAssemblies(
        IServiceCollection services,
        ITypeFilterer typeFilterer,
        IReadOnlyList<Assembly> assemblies)
    {
        // Assemblies parameter ignored - all types known at compile time
        // TypeFilterer parameter ignored - filtering done at compile time
        _registerAction(services);
    }
}
