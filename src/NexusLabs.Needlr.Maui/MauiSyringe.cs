using Microsoft.Maui.Hosting;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Maui;

/// <summary>
/// Provides a fluent API for populating a .NET MAUI application's service collection with
/// Needlr-discovered services. Wraps a <see cref="ConfiguredSyringe"/> with MAUI-specific behavior.
/// </summary>
/// <remarks>
/// <para>
/// A MAUI head project owns a single dependency-injection container created by
/// <c>MauiApp.CreateBuilder()</c>. <see cref="MauiSyringe"/> applies every Needlr-discovered
/// registration to that container's <see cref="MauiAppBuilder.Services"/>, so MAUI resolves your
/// <c>App</c>, pages, and view models — and every Needlr service — from one provider with no
/// per-type manual registration.
/// </para>
/// <para>
/// Obtain a <see cref="MauiSyringe"/> by calling <c>ForMaui()</c> on a configured syringe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = MauiApp.CreateBuilder();
/// builder.UseMauiApp&lt;App&gt;();
///
/// new Syringe()
///     .UsingSourceGen()
///     .ForMaui()
///     .PopulateInto(builder);
///
/// return builder.Build();
/// </code>
/// </example>
public sealed record MauiSyringe
{
    internal ConfiguredSyringe BaseSyringe { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MauiSyringe"/> class.
    /// </summary>
    /// <param name="baseSyringe">The configured syringe to wrap.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="baseSyringe"/> is <see langword="null"/>.</exception>
    public MauiSyringe(ConfiguredSyringe baseSyringe)
    {
        System.ArgumentNullException.ThrowIfNull(baseSyringe);
        BaseSyringe = baseSyringe;
    }

    /// <summary>
    /// Applies the Needlr-discovered registrations to the supplied <see cref="MauiAppBuilder"/>'s
    /// service collection, using <see cref="MauiAppBuilder.Configuration"/> for options binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder's <see cref="MauiAppBuilder.Configuration"/> is used to bind any
    /// <c>[Options]</c>- and <c>[HttpClientOptions]</c>-decorated types, matching the behavior of
    /// the ASP.NET and host integrations. Services already present on the builder (for example MAUI's
    /// own registrations, or your own <c>builder.Services.Add...</c> calls) are preserved.
    /// </para>
    /// </remarks>
    /// <param name="builder">The MAUI application builder to populate.</param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public MauiAppBuilder PopulateInto(MauiAppBuilder builder)
    {
        System.ArgumentNullException.ThrowIfNull(builder);

        var typeRegistrar = BaseSyringe.GetOrCreateTypeRegistrar();
        var typeFilterer = BaseSyringe.GetOrCreateTypeFilterer();
        var pluginFactory = BaseSyringe.GetOrCreatePluginFactory();
        var serviceCollectionPopulator = BaseSyringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
        var assemblyProvider = BaseSyringe.GetOrCreateAssemblyProvider();
        var additionalAssemblies = BaseSyringe.GetAdditionalAssemblies();

        var serviceProviderBuilder = BaseSyringe.GetOrCreateServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        var candidateAssemblies = serviceProviderBuilder.GetCandidateAssemblies();

        serviceCollectionPopulator.RegisterToServiceCollection(
            builder.Services,
            builder.Configuration,
            candidateAssemblies);

        // User-registered post-plugin callbacks run first, then the source-generated
        // options/extension registrars — the same ordering ConfiguredSyringe and the ASP.NET
        // integration use. Without these, [Options]-decorated types bound by the generator would
        // never have their IConfigureOptions<T> registered.
        foreach (var callback in BaseSyringe.GetPostPluginRegistrationCallbacks())
        {
            callback(builder.Services);
        }

        if (SourceGenRegistry.TryGetOptionsRegistrar(out var optionsRegistrar) && optionsRegistrar is not null)
        {
            optionsRegistrar(builder.Services, builder.Configuration);
        }

        if (SourceGenRegistry.TryGetExtensionRegistrar(out var extensionRegistrar) && extensionRegistrar is not null)
        {
            extensionRegistrar(builder.Services, builder.Configuration);
        }

        return builder;
    }
}
