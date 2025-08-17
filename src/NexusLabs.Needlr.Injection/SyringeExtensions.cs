using Microsoft.Extensions.DependencyInjection;
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
    /// Configures the syringe to use the specified service collection populator factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="factory">The factory function for creating service collection populators.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingServiceCollectionPopulator((typeRegistrar, typeFilterer) => 
    ///         new ServiceCollectionPopulator(typeRegistrar, typeFilterer));
    /// </code>
    /// </example>
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
    /// Configures the syringe to add multiple post-plugin registration callbacks.
    /// These callbacks are executed after plugin registration but before the service provider is finalized.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="callbacks">The callbacks to add to the existing callback list.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .AddPostPluginRegistrationCallbacks(new[]
    ///     {
    ///         services => services.AddScoped&lt;IMyService, MyService&gt;(),
    ///         services => services.Configure&lt;MyOptions&gt;(options => options.Value = "test")
    ///     });
    /// </code>
    /// </example>
    public static Syringe AddPostPluginRegistrationCallbacks(
        this Syringe syringe, 
        IEnumerable<Action<IServiceCollection>> callbacks)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(callbacks);

        var existingCallbacks = syringe.PostPluginRegistrationCallbacks ?? [];
        List<Action<IServiceCollection>> newCallbacks = 
        [
            .. existingCallbacks, 
            .. callbacks
        ];
        
        return syringe with { PostPluginRegistrationCallbacks = newCallbacks };
    }

    /// <summary>
    /// Configures the syringe to add a post-plugin registration callback.
    /// This callback is executed after plugin registration but before the service provider is finalized.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="callback">The callback to add to the existing callback list.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .AddPostPluginRegistrationCallback(
    ///         services => services.Configure&lt;MyOptions&gt;(options => options.Value = "test")
    ///     );
    /// </code>
    /// </example>
    public static Syringe AddPostPluginRegistrationCallback(
        this Syringe syringe,
        Action<IServiceCollection> callback)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(callback);

        return syringe.AddPostPluginRegistrationCallbacks([callback]);
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

        return syringe.AddPostPluginRegistrationCallback(services =>
        {
            services.AddDecorator<TService, TDecorator>();
        });
    }
}