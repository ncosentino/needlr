using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.PluginFactories;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances.
/// </summary>
/// <example>
/// Basic usage pattern:
/// <code>
/// var serviceProvider = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .UsingDefaultTypeFilterer()
///     .UsingDefaultAssemblyProvider()
///     .BuildServiceProvider();
/// </code>
/// 
/// Advanced configuration with callbacks:
/// <code>
/// var syringe = new Syringe()
///     .UsingTypeRegistrar(customRegistrar)
///     .UsingTypeFilterer(customFilterer)
///     .UsingServiceCollectionPopulator((tr, tf) => new ServiceCollectionPopulator(tr, tf))
///     .UsingAssemblyProvider(builder => builder
///         .MatchingAssemblies(x => x.Contains("MyApp"))
///         .UseLibTestEntrySorting()
///         .Build())
///     .UsingAdditionalAssemblies([Assembly.GetExecutingAssembly()])
///     .UsingPostPluginRegistrationCallback(services => services.AddScoped&lt;IMyService, MyService&gt;())
///     .UsingPostPluginRegistrationCallback(services => services.Configure&lt;MyOptions&gt;(opt => opt.Value = "test"));
/// </code>
/// </example>
public static class SyringeExtensions
{
    /// <summary>
    /// Configures the syringe to use the default type registrar.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingDefaultTypeRegistrar();
    /// </code>
    /// </example>
    public static Syringe UsingDefaultTypeRegistrar(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new DefaultTypeRegistrar());
    }

    /// <summary>
    /// Configures the syringe with all generated components for zero-reflection operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the recommended way to use source-generated dependency injection.
    /// It configures the type registrar, type filterer, plugin factory, and assembly
    /// provider to use pre-computed compile-time information, eliminating all runtime reflection.
    /// </para>
    /// <para>
    /// The assembly provider is automatically configured to include all assemblies
    /// that contain types in the TypeRegistry, enabling cross-assembly plugin discovery.
    /// </para>
    /// <para>
    /// To use this, your assembly must have:
    /// <list type="bullet">
    /// <item>A reference to <c>NexusLabs.Needlr.Generators</c></item>
    /// <item>The <c>[assembly: GenerateTypeRegistry(...)]</c> attribute</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="injectableTypeProvider">A function that returns the injectable types (typically TypeRegistry.GetInjectableTypes).</param>
    /// <param name="pluginTypeProvider">A function that returns the plugin types (typically TypeRegistry.GetPluginTypes).</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// // Zero-reflection configuration:
    /// using NexusLabs.Needlr.Generated;
    ///
    /// var syringe = new Syringe()
    ///     .UsingGeneratedComponents(
    ///         TypeRegistry.GetInjectableTypes,
    ///         TypeRegistry.GetPluginTypes);
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedComponents(
        this Syringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(injectableTypeProvider);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);

        return syringe
            .UsingGeneratedTypeRegistrar(injectableTypeProvider)
            .UsingGeneratedTypeFilterer(injectableTypeProvider)
            .UsingGeneratedPluginFactory(pluginTypeProvider)
            .UsingGeneratedAssemblyProvider(injectableTypeProvider, pluginTypeProvider);
    }

    /// <summary>
    /// Configures the syringe to use the generated assembly provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This provider derives the list of candidate assemblies from the TypeRegistry,
    /// extracting unique assemblies from both injectable types and plugin types.
    /// This enables cross-assembly plugin discovery when using source generation.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="injectableTypeProvider">A function that returns the injectable types.</param>
    /// <param name="pluginTypeProvider">A function that returns the plugin types.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingGeneratedAssemblyProvider(
    ///         TypeRegistry.GetInjectableTypes,
    ///         TypeRegistry.GetPluginTypes);
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedAssemblyProvider(
        this Syringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(injectableTypeProvider);
        ArgumentNullException.ThrowIfNull(pluginTypeProvider);

        return syringe.UsingAssemblyProvider(
            new GeneratedAssemblyProvider(injectableTypeProvider, pluginTypeProvider));
    }

    /// <summary>
    /// Configures the syringe to use the generated type registrar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This registrar uses compile-time generated type information instead of
    /// runtime reflection, providing better performance and AOT compatibility.
    /// </para>
    /// <para>
    /// <b>Note:</b> This overload uses reflection to locate the generated TypeRegistry.
    /// For zero-reflection scenarios, use <see cref="UsingGeneratedComponents"/> or
    /// <see cref="UsingGeneratedTypeRegistrar(Syringe, Func{IReadOnlyList{InjectableTypeInfo}})"/>
    /// with an explicit type provider.
    /// </para>
    /// <para>
    /// To use this, your assembly must have:
    /// <list type="bullet">
    /// <item>A reference to <c>NexusLabs.Needlr.Generators</c></item>
    /// <item>The <c>[assembly: GenerateTypeRegistry(...)]</c> attribute</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// // In AssemblyInfo.cs or any file:
    /// [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyCompany", "NexusLabs" })]
    ///
    /// // When building the service provider:
    /// var syringe = new Syringe().UsingGeneratedTypeRegistrar();
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedTypeRegistrar(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new GeneratedTypeRegistrar());
    }

    /// <summary>
    /// Configures the syringe to use the generated type registrar with a custom type provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload allows you to provide a custom function that returns the
    /// injectable types. This is useful for testing or when you need to customize
    /// the type discovery behavior.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="typeProvider">A function that returns the injectable types.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingGeneratedTypeRegistrar(NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes);
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedTypeRegistrar(
        this Syringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> typeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeProvider);
        return syringe.UsingTypeRegistrar(new GeneratedTypeRegistrar(typeProvider));
    }

    /// <summary>
    /// Configures the syringe to use the specified type registrar.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="typeRegistrar">The type registrar to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var customRegistrar = new MyCustomTypeRegistrar();
    /// var syringe = new Syringe().UsingTypeRegistrar(customRegistrar);
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingDefaultTypeFilterer();
    /// </code>
    /// </example>
    public static Syringe UsingDefaultTypeFilterer(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeFilterer(new DefaultTypeFilterer());
    }

    /// <summary>
    /// Configures the syringe to use the generated type filterer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This filterer uses compile-time generated lifetime information instead of
    /// runtime reflection for constructor analysis. When used with
    /// <see cref="UsingGeneratedTypeRegistrar(Syringe, Func{IReadOnlyList{InjectableTypeInfo}})"/>,
    /// it provides zero-reflection type filtering.
    /// </para>
    /// <para>
    /// For zero-reflection scenarios, explicitly provide the type provider to both
    /// the registrar and the filterer.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// // Zero-reflection configuration:
    /// var syringe = new Syringe()
    ///     .UsingGeneratedTypeRegistrar(TypeRegistry.GetInjectableTypes)
    ///     .UsingGeneratedTypeFilterer();
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedTypeFilterer(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeFilterer(new GeneratedTypeFilterer());
    }

    /// <summary>
    /// Configures the syringe to use the generated type filterer with a custom type provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload allows you to provide a custom function that returns the
    /// injectable types. The filterer builds a lookup table from this information,
    /// enabling reflection-free lifetime checks.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="typeProvider">A function that returns the injectable types.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingGeneratedTypeFilterer(TypeRegistry.GetInjectableTypes);
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedTypeFilterer(
        this Syringe syringe,
        Func<IReadOnlyList<InjectableTypeInfo>> typeProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeProvider);
        return syringe.UsingTypeFilterer(new GeneratedTypeFilterer(typeProvider));
    }

    /// <summary>
    /// Configures the syringe to use the specified type filterer.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="typeFilterer">The type filterer to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var customFilterer = new MyCustomTypeFilterer();
    /// var syringe = new Syringe().UsingTypeFilterer(customFilterer);
    /// </code>
    /// </example>
    public static Syringe UsingTypeFilterer(
        this Syringe syringe, 
        ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return syringe with { TypeFilterer = typeFilterer };
    }

    /// <summary>
    /// Configures the syringe to use the default reflection-based plugin factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingDefaultPluginFactory();
    /// </code>
    /// </example>
    public static Syringe UsingDefaultPluginFactory(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingPluginFactory(new PluginFactory());
    }

    /// <summary>
    /// Configures the syringe to use the generated plugin factory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This factory uses compile-time generated plugin information instead of
    /// runtime reflection, providing better performance and AOT compatibility.
    /// </para>
    /// <para>
    /// <b>Note:</b> This overload uses reflection to locate the generated TypeRegistry.
    /// For zero-reflection scenarios, use <see cref="UsingGeneratedComponents"/> or
    /// <see cref="UsingGeneratedPluginFactory(Syringe, Func{IReadOnlyList{PluginTypeInfo}})"/>
    /// with an explicit plugin provider.
    /// </para>
    /// <para>
    /// To use this, your assembly must have:
    /// <list type="bullet">
    /// <item>A reference to <c>NexusLabs.Needlr.Generators</c></item>
    /// <item>The <c>[assembly: GenerateTypeRegistry(...)]</c> attribute</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingGeneratedPluginFactory();
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedPluginFactory(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingPluginFactory(new GeneratedPluginFactory());
    }

    /// <summary>
    /// Configures the syringe to use the generated plugin factory with a custom plugin provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This overload allows you to provide a custom function that returns the
    /// plugin types. This is useful for testing or when you need to customize
    /// the plugin discovery behavior.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="pluginProvider">A function that returns the plugin types.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingGeneratedPluginFactory(NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes);
    /// </code>
    /// </example>
    public static Syringe UsingGeneratedPluginFactory(
        this Syringe syringe,
        Func<IReadOnlyList<PluginTypeInfo>> pluginProvider)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(pluginProvider);
        return syringe.UsingPluginFactory(new GeneratedPluginFactory(pluginProvider));
    }

    /// <summary>
    /// Configures the syringe to use the specified plugin factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="pluginFactory">The plugin factory to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var customFactory = new MyCustomPluginFactory();
    /// var syringe = new Syringe().UsingPluginFactory(customFactory);
    /// </code>
    /// </example>
    public static Syringe UsingPluginFactory(
        this Syringe syringe,
        IPluginFactory pluginFactory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(pluginFactory);

        return syringe with { PluginFactory = pluginFactory };
    }

    /// <summary>
    /// Configures the syringe to use the specified service collection populator factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="factory">The factory function for creating service collection populators.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingServiceCollectionPopulator((typeRegistrar, typeFilterer, pluginFactory) =>
    ///         new ServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory));
    /// </code>
    /// </example>
    public static Syringe UsingServiceCollectionPopulator(
        this Syringe syringe,
        Func<ITypeRegistrar, ITypeFilterer, IPluginFactory, IServiceCollectionPopulator> factory)
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
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingDefaultAssemblyProvider();
    /// </code>
    /// </example>
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
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingAssemblyProvider(builder => builder
    ///         .MatchingAssemblies(x => 
    ///             x.Contains("MyApp", StringComparison.OrdinalIgnoreCase) ||
    ///             x.Contains("MyLibrary", StringComparison.OrdinalIgnoreCase))
    ///         .UseLibTestEntrySorting()
    ///         .Build());
    /// </code>
    /// </example>
    public static Syringe UsingAssemblyProvider(this Syringe syringe, Func<IAssembyProviderBuilder, IAssemblyProvider> builderFunc)
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
    /// <example>
    /// <code>
    /// var assemblyProvider = new AssembyProviderBuilder()
    ///     .MatchingAssemblies(x => x.Contains("MyApp"))
    ///     .Build();
    /// var syringe = new Syringe().UsingAssemblyProvider(assemblyProvider);
    /// </code>
    /// </example>
    public static Syringe UsingAssemblyProvider(this Syringe syringe, IAssemblyProvider assemblyProvider)
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
    /// <example>
    /// <code>
    /// var additionalAssemblies = new[] { Assembly.GetExecutingAssembly(), Assembly.GetCallingAssembly() };
    /// var syringe = new Syringe().UsingAdditionalAssemblies(additionalAssemblies);
    /// </code>
    /// </example>
    public static Syringe UsingAdditionalAssemblies(this Syringe syringe, IReadOnlyList<Assembly> additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        return syringe with { AdditionalAssemblies = additionalAssemblies };
    }

    /// <summary>
    /// Configures the syringe to use post-plugin registration callbacks.
    /// These callbacks are executed after plugin registration but before the service provider is finalized.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="callbacks">The callbacks to execute during service provider building.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var callbacks = new List&lt;Action&lt;IServiceCollection&gt;&gt;
    /// {
    ///     services => services.AddScoped&lt;IMyService, MyService&gt;(),
    ///     services => services.Configure&lt;MyOptions&gt;(options => options.Value = "test")
    /// };
    /// var syringe = new Syringe().UsingPostPluginRegistrationCallbacks(callbacks);
    /// </code>
    /// </example>
    public static Syringe UsingPostPluginRegistrationCallbacks(
        this Syringe syringe, 
        IReadOnlyList<Action<IServiceCollection>> callbacks)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(callbacks);

        return syringe with { PostPluginRegistrationCallbacks = callbacks };
    }

    /// <summary>
    /// Configures the syringe to add a single post-plugin registration callback.
    /// This callback is executed after plugin registration but before the service provider is finalized.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="callback">The callback to execute during service provider building.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingPostPluginRegistrationCallback(services => services.AddScoped&lt;IMyService, MyService&gt;())
    ///     .UsingPostPluginRegistrationCallback(services => services.Configure&lt;MyOptions&gt;(options => options.Value = "test"));
    /// </code>
    /// </example>
    public static Syringe UsingPostPluginRegistrationCallback(
        this Syringe syringe, 
        Action<IServiceCollection> callback)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(callback);

        var existingCallbacks = syringe.PostPluginRegistrationCallbacks ?? [];
        var newCallbacks = new List<Action<IServiceCollection>>(existingCallbacks) { callback };
        
        return syringe with { PostPluginRegistrationCallbacks = newCallbacks };
    }

    /// <summary>
    /// Configures the syringe to add a decorator for the specified service type.
    /// This is a convenience method that adds a post-plugin registration callback to decorate the service.
    /// The decorator will preserve the original service's lifetime.
    /// Works with both interfaces and class types.
    /// </summary>
    /// <typeparam name="TService">The service type (interface or class) to decorate.</typeparam>
    /// <typeparam name="TDecorator">The decorator type that implements TService.</typeparam>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingDefaultTypeRegistrar()
    ///     .AddDecorator&lt;IMyService, MyServiceDecorator&gt;();
    /// </code>
    /// </example>
    public static Syringe AddDecorator<TService, TDecorator>(
        this Syringe syringe)
        where TDecorator : class, TService
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddDecorator<TService, TDecorator>();
        });
    }
}