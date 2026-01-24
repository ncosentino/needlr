using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.AssemblyOrdering;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Core extension methods for configuring <see cref="Syringe"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This class contains the core configuration methods that work with any implementation.
/// For implementation-specific extensions, reference one of:
/// </para>
/// <list type="bullet">
/// <item><c>NexusLabs.Needlr.Injection.SourceGen</c> - for AOT-compatible source-generated components</item>
/// <item><c>NexusLabs.Needlr.Injection.Reflection</c> - for runtime reflection-based components</item>
/// <item><c>NexusLabs.Needlr.Injection.Bundle</c> - for both with automatic fallback</item>
/// </list>
/// </remarks>
public static class SyringeExtensions
{
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
    /// Configures the syringe to use the specified plugin factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="pluginFactory">The plugin factory to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingPluginFactory(
        this Syringe syringe,
        IPluginFactory pluginFactory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(pluginFactory);

        return syringe with { PluginFactory = pluginFactory };
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
    /// Configures the syringe to use the specified service collection populator factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="factory">The factory function for creating service collection populators.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingServiceCollectionPopulator(
        this Syringe syringe,
        Func<ITypeRegistrar, ITypeFilterer, IPluginFactory, IServiceCollectionPopulator> factory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(factory);

        return syringe with { ServiceCollectionPopulatorFactory = factory };
    }

    /// <summary>
    /// Configures the syringe to use the specified service provider builder factory.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="factory">The factory function for creating service provider builders.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingServiceProviderBuilderFactory(
        this Syringe syringe,
        Func<IServiceCollectionPopulator, IAssemblyProvider, IReadOnlyList<Assembly>, IServiceProviderBuilder> factory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(factory);

        return syringe with { ServiceProviderBuilderFactory = factory };
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

    /// <summary>
    /// Configures the syringe to use post-plugin registration callbacks.
    /// These callbacks are executed after plugin registration but before the service provider is finalized.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="callbacks">The callbacks to execute during service provider building.</param>
    /// <returns>A new configured syringe instance.</returns>
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
    /// Configures assembly ordering using expression-based rules.
    /// Assemblies are sorted into tiers based on the first matching rule.
    /// Unmatched assemblies are placed last.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="configure">Action to configure the ordering rules.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// new Syringe()
    ///     .UsingReflection()  // or .UsingSourceGen()
    ///     .OrderAssemblies(order => order
    ///         .By(a => a.Name.EndsWith(".Core"))
    ///         .ThenBy(a => a.Name.Contains("Services"))
    ///         .ThenBy(a => a.Name.Contains("Tests")))
    ///     .BuildServiceProvider();
    /// </code>
    /// </example>
    public static Syringe OrderAssemblies(
        this Syringe syringe,
        Action<AssemblyOrderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new AssemblyOrderBuilder();
        configure(builder);
        return syringe with { AssemblyOrder = builder };
    }

    /// <summary>
    /// Configures assembly ordering using a pre-built order builder.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="orderBuilder">The pre-configured order builder.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// new Syringe()
    ///     .UsingSourceGen()
    ///     .OrderAssemblies(AssemblyOrder.LibTestEntry())
    ///     .BuildServiceProvider();
    /// </code>
    /// </example>
    public static Syringe OrderAssemblies(
        this Syringe syringe,
        AssemblyOrderBuilder orderBuilder)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(orderBuilder);

        return syringe with { AssemblyOrder = orderBuilder };
    }

    /// <summary>
    /// Configures assembly ordering: libraries first, then executables, tests last.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UseLibTestEntryOrdering(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.OrderAssemblies(AssemblyOrder.LibTestEntry());
    }

    /// <summary>
    /// Configures assembly ordering: non-test assemblies first, tests last.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UseTestsLastOrdering(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.OrderAssemblies(AssemblyOrder.TestsLast());
    }

    /// <summary>
    /// Configures verification options for the syringe.
    /// Verification runs automatically during <see cref="Syringe.BuildServiceProvider"/>.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="options">The verification options to use.</param>
    /// <returns>A new configured syringe instance.</returns>
    /// <example>
    /// <code>
    /// // Strict mode - throw on any issue
    /// new Syringe()
    ///     .UsingSourceGen()
    ///     .WithVerification(VerificationOptions.Strict)
    ///     .BuildServiceProvider();
    /// 
    /// // Disable verification
    /// new Syringe()
    ///     .UsingSourceGen()
    ///     .WithVerification(VerificationOptions.Disabled)
    ///     .BuildServiceProvider();
    /// 
    /// // Custom configuration
    /// new Syringe()
    ///     .UsingSourceGen()
    ///     .WithVerification(new VerificationOptions
    ///     {
    ///         LifestyleMismatchBehavior = VerificationBehavior.Throw,
    ///         IssueReporter = issue => logger.LogWarning(issue.Message)
    ///     })
    ///     .BuildServiceProvider();
    /// </code>
    /// </example>
    public static Syringe WithVerification(
        this Syringe syringe,
        VerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(options);

        return syringe with { VerificationOptions = options };
    }

    /// <summary>
    /// Configures verification options using a builder action.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <param name="configure">An action to configure the verification options.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe WithVerification(
        this Syringe syringe,
        Action<VerificationOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new VerificationOptionsBuilder();
        configure(builder);
        return syringe with { VerificationOptions = builder.Build() };
    }

    /// <summary>
    /// Builds a service provider with default configuration.
    /// </summary>
    /// <param name="syringe">The syringe to build from.</param>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    public static IServiceProvider BuildServiceProvider(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.BuildServiceProvider(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }
}
