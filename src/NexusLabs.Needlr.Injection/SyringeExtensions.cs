using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.PluginFactories;
using NexusLabs.Needlr.Injection.TypeFilterers;
using NexusLabs.Needlr.Injection.TypeRegistrars;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances.
/// </summary>
/// <example>
/// Source-gen first (recommended for AOT/trimming):
/// <code>
/// // With module initializer bootstrap (automatic):
/// var serviceProvider = new Syringe().BuildServiceProvider();
/// 
/// // Or explicit generated components:
/// var serviceProvider = new Syringe()
///     .UsingGeneratedComponents(TypeRegistry.GetInjectableTypes, TypeRegistry.GetPluginTypes)
///     .BuildServiceProvider();
/// </code>
/// 
/// Reflection-based (for dynamic scenarios):
/// <code>
/// var serviceProvider = new Syringe()
///     .UsingReflection()
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
    /// Configures the syringe to use reflection-based type discovery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this when source generation is not available or when runtime assembly loading is required.
    /// This sets all components (type registrar, type filterer, plugin factory, assembly provider)
    /// to their reflection-based implementations.
    /// </para>
    /// <para>
    /// For AOT/trimming compatibility, use source-generated components instead (the default behavior
    /// when <c>[assembly: GenerateTypeRegistry(...)]</c> is present).
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance with all reflection-based components.</returns>
    /// <example>
    /// <code>
    /// var serviceProvider = new Syringe()
    ///     .UsingReflection()
    ///     .BuildServiceProvider();
    /// </code>
    /// </example>
    [RequiresUnreferencedCode("Enables reflection-based type discovery. Not compatible with AOT/trimming.")]
    public static Syringe UsingReflection(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe
            .UsingReflectionTypeRegistrar()
            .UsingReflectionTypeFilterer()
            .UsingReflectionPluginFactory()
            .UsingReflectionAssemblyProvider();
    }

    /// <summary>
    /// Configures the syringe to use the reflection-based type registrar.
    /// </summary>
    /// <remarks>
    /// This registrar uses runtime reflection to discover and register types.
    /// For AOT/trimming compatibility, use <see cref="UsingGeneratedTypeRegistrar(Syringe)"/> instead.
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingReflectionTypeRegistrar();
    /// </code>
    /// </example>
    public static Syringe UsingReflectionTypeRegistrar(
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
    /// Configures the syringe to use the reflection-based type filterer.
    /// </summary>
    /// <remarks>
    /// This filterer uses runtime reflection to analyze types.
    /// For AOT/trimming compatibility, use <see cref="UsingGeneratedTypeFilterer(Syringe)"/> instead.
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingReflectionTypeFilterer();
    /// </code>
    /// </example>
    public static Syringe UsingReflectionTypeFilterer(
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
    /// Configures the syringe to use the reflection-based plugin factory.
    /// </summary>
    /// <remarks>
    /// This factory uses runtime reflection to instantiate plugins.
    /// For AOT/trimming compatibility, use <see cref="UsingGeneratedPluginFactory"/> instead.
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingReflectionPluginFactory();
    /// </code>
    /// </example>
    public static Syringe UsingReflectionPluginFactory(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingPluginFactory(new PluginFactory());
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
    /// Configures the syringe to use the reflection-based assembly provider.
    /// </summary>
    /// <remarks>
    /// This provider uses runtime reflection to scan assemblies.
    /// For AOT/trimming compatibility, use <see cref="UsingGeneratedAssemblyProvider"/> instead.
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingReflectionAssemblyProvider();
    /// </code>
    /// </example>
    public static Syringe UsingReflectionAssemblyProvider(this Syringe syringe)
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
    ///     .UsingReflectionTypeRegistrar()
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

    /// <summary>
    /// Configures a handler to be invoked when reflection-based components are used as fallback
    /// because source-generated components are not available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, Needlr silently falls back to reflection when source generation is not configured.
    /// Use this method to detect and respond to reflection fallback scenarios.
    /// </para>
    /// <para>
    /// Built-in handlers are available in <see cref="ReflectionFallbackHandlers"/>:
    /// <list type="bullet">
    /// <item><see cref="ReflectionFallbackHandlers.ThrowException"/> - Throws an exception on fallback</item>
    /// <item><see cref="ReflectionFallbackHandlers.LogWarning"/> - Logs a warning to Console.Error</item>
    /// <item><see cref="ReflectionFallbackHandlers.Silent"/> - Does nothing (default behavior)</item>
    /// </list>
    /// </para>
    /// <para>
    /// See also: <seealso cref="WithFastFailOnReflection(Syringe)"/>
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="handler">The handler to invoke when reflection fallback occurs.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// // Fail fast if reflection is used (recommended for AOT apps):
    /// var syringe = new Syringe()
    ///     .WithReflectionFallbackHandler(ReflectionFallbackHandlers.ThrowException);
    /// 
    /// // Log warnings during development:
    /// var syringe = new Syringe()
    ///     .WithReflectionFallbackHandler(ReflectionFallbackHandlers.LogWarning);
    /// 
    /// // Custom handling:
    /// var syringe = new Syringe()
    ///     .WithReflectionFallbackHandler(ctx => 
    ///         _logger.LogWarning("Reflection fallback: {Component}", ctx.ComponentName));
    /// </code>
    /// </example>
    public static Syringe WithReflectionFallbackHandler(
        this Syringe syringe,
        Action<ReflectionFallbackContext> handler)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(handler);

        return syringe with { ReflectionFallbackHandler = handler };
    }

    /// <summary>
    /// Configures the syringe to throw an exception immediately if any reflection-based 
    /// component is used as a fallback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a convenience method equivalent to calling 
    /// <see cref="WithReflectionFallbackHandler"/> with <see cref="ReflectionFallbackHandlers.ThrowException"/>.
    /// </para>
    /// <para>
    /// Use this method in AOT/trimming scenarios where reflection is not available
    /// and you want to ensure that source-generated components are always used.
    /// If reflection fallback occurs, an exception will be thrown immediately,
    /// making it easy to identify missing source generation configuration.
    /// </para>
    /// </remarks>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance that will throw on reflection fallback.</returns>
    /// <example>
    /// <code>
    /// // Ensure no reflection is used (recommended for AOT apps):
    /// var syringe = new Syringe()
    ///     .UsingGeneratedComponents(TypeRegistry.GetInjectableTypes, TypeRegistry.GetPluginTypes)
    ///     .WithFastFailOnReflection();
    /// </code>
    /// </example>
    /// <seealso cref="WithReflectionFallbackHandler"/>
    /// <seealso cref="ReflectionFallbackHandlers.ThrowException"/>
    public static Syringe WithFastFailOnReflection(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.WithReflectionFallbackHandler(ReflectionFallbackHandlers.ThrowException);
    }
}