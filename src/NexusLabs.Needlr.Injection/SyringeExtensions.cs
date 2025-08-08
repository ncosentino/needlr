using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances.
/// </summary>
public static class SyringeExtensions
{
    /// <summary>
    /// Configures the syringe to use the default type registrar.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingDefaultTypeRegistrar(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new DefaultTypeRegistrar());
    }

    /// <summary>
    /// Configures the syringe to use the specified type registrar.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="typeRegistrar">The type registrar to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingTypeRegistrar(
        this Syringe syringe,
        ITypeRegistrar typeRegistrar)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeRegistrar);

        return syringe with { TypeRegistrar = typeRegistrar };
    }

    /// <summary>
    /// Configures the syringe to use the default type filterer.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingDefaultTypeFilterer(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeFilterer(new DefaultTypeFilterer());
    }

    /// <summary>
    /// Configures the syringe to use the specified type filterer.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="typeFilterer">The type filterer to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingTypeFilterer(
        this Syringe syringe,
        ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return syringe with { TypeFilterer = typeFilterer };
    }

    /// <summary>
    /// Configures the syringe to use the specified service collection populator factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="factory">The factory function for creating service collection populators.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingServiceCollectionPopulator(
        this Syringe syringe,
        Func<ITypeRegistrar, ITypeFilterer, IServiceCollectionPopulator> factory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(factory);

        return syringe with { ServiceCollectionPopulatorFactory = factory };
    }

    /// <summary>
    /// Configures the syringe to use the default assembly provider.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingDefaultAssemblyProvider(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingAssemblyProvider(new AssembyProviderBuilder().Build());
    }

    /// <summary>
    /// Configures the syringe to use an assembly provider built from the specified builder function.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="builderFunc">The function to configure the assembly provider builder.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingAssemblyProvider(
        this Syringe syringe,
        Func<IAssembyProviderBuilder, IAssemblyProvider> builderFunc)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(builderFunc);

        var builder = new AssembyProviderBuilder();
        var assemblyProvider = builderFunc(builder);
        return syringe with { AssemblyProvider = assemblyProvider };
    }

    /// <summary>
    /// Configures the syringe to use the specified assembly provider.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="assemblyProvider">The assembly provider to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingAssemblyProvider(
        this Syringe syringe, 
        IAssemblyProvider assemblyProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(assemblyProvider);

        return syringe with { AssemblyProvider = assemblyProvider };
    }

    /// <summary>
    /// Configures the syringe to use additional assemblies.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="additionalAssemblies">The additional assemblies to include.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingAdditionalAssemblies(
        this Syringe syringe, 
        IReadOnlyList<Assembly> additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        return syringe with { AdditionalAssemblies = additionalAssemblies };
    }
}