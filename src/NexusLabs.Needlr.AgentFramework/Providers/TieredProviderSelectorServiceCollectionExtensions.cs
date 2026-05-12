using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Needlr.AgentFramework.Context;

namespace NexusLabs.Needlr.AgentFramework.Providers;

/// <summary>
/// Extension methods that register an
/// <see cref="ITieredProviderSelector{TQuery, TResult}"/> with optional consumer-supplied
/// <see cref="TieredProviderSelectorOptions"/> in the DI container.
/// </summary>
/// <remarks>
/// <para>
/// Wraps the boilerplate of resolving providers, the quota gate, the execution-context
/// accessor, and <see cref="TimeProvider"/> from the container so consumers do not have
/// to hand-roll the same factory in every host.
/// </para>
/// <para>
/// <b>Registration semantics: last-wins (override-friendly).</b> The extension uses
/// <c>AddSingleton</c> (not <c>TryAddSingleton</c>). If the same <c>(TQuery, TResult)</c>
/// pair is registered multiple times — for example, by two plugins —
/// <see cref="System.IServiceProvider.GetService"/> resolves the LAST descriptor added.
/// This is the intentional convention for consumer-supplied configuration: a
/// downstream plugin or test harness can override an upstream plugin's selector
/// registration without removing it first. (The <see cref="TimeProvider"/> dependency
/// is registered with <c>TryAddSingleton</c> because it IS framework infrastructure —
/// first-wins is correct for that.)
/// </para>
/// <para>
/// <b>Lifetime: Singleton.</b> The selector's per-instance skip cache is the whole point
/// of registering it as a long-lived service — registering as Scoped would reset the
/// cache per request, defeating the purpose. If you need a different lifetime,
/// hand-roll the registration.
/// </para>
/// <para>
/// <b>Configure delegate evaluation.</b> Both <c>configure</c> overloads invoke the
/// supplied delegate INSIDE the singleton factory at first resolution, with a real
/// <see cref="System.IServiceProvider"/> in scope. This means OnHit callbacks can
/// resolve other services (loggers, options monitors, telemetry sinks) from the
/// container — register them BEFORE calling
/// <see cref="ITieredProviderSelector{TQuery, TResult}"/>.GetRequiredService for the
/// first time. The delegate runs exactly once per Singleton lifetime; throw an
/// <see cref="System.InvalidOperationException"/> if it returns <see langword="null"/>.
/// </para>
/// </remarks>
public static class TieredProviderSelectorServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="ITieredProviderSelector{TQuery, TResult}"/> as a singleton
    /// using <see cref="TieredProviderSelectorOptions.Default"/> (PUE-only fall-through,
    /// no skip, no callback).
    /// </summary>
    /// <typeparam name="TQuery">Query type for the selector.</typeparam>
    /// <typeparam name="TResult">Result type for the selector.</typeparam>
    /// <param name="services">Service collection to add the registration to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddTieredProviderSelector<TQuery, TResult>(
        this IServiceCollection services)
    {
        return AddTieredProviderSelectorCore<TQuery, TResult>(services, optionsFactory: null);
    }

    /// <summary>
    /// Registers an <see cref="ITieredProviderSelector{TQuery, TResult}"/> as a singleton
    /// with consumer-supplied options derived from
    /// <see cref="TieredProviderSelectorOptions.Default"/>.
    /// </summary>
    /// <typeparam name="TQuery">Query type for the selector.</typeparam>
    /// <typeparam name="TResult">Result type for the selector.</typeparam>
    /// <param name="services">Service collection to add the registration to.</param>
    /// <param name="configure">
    /// Delegate that receives <see cref="TieredProviderSelectorOptions.Default"/> and
    /// returns the (possibly mutated) options to use. Runs lazily inside the singleton
    /// factory at first resolution. Must not return <see langword="null"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Use this overload when your <see cref="ProviderFailurePolicy.OnHit"/> callbacks
    /// (and any other policy fields) do not need access to other DI services. If you
    /// need an <c>ILogger</c>, <c>IOptionsMonitor</c>, or any other container-resolved
    /// service inside your callbacks, use the
    /// <see cref="AddTieredProviderSelector{TQuery, TResult}(IServiceCollection, Func{IServiceProvider, TieredProviderSelectorOptions, TieredProviderSelectorOptions})"/>
    /// overload instead.
    /// </remarks>
    public static IServiceCollection AddTieredProviderSelector<TQuery, TResult>(
        this IServiceCollection services,
        Func<TieredProviderSelectorOptions, TieredProviderSelectorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return AddTieredProviderSelectorCore<TQuery, TResult>(
            services,
            optionsFactory: _ => configure(TieredProviderSelectorOptions.Default));
    }

    /// <summary>
    /// Registers an <see cref="ITieredProviderSelector{TQuery, TResult}"/> as a singleton
    /// with consumer-supplied options derived from
    /// <see cref="TieredProviderSelectorOptions.Default"/>, with access to the
    /// <see cref="System.IServiceProvider"/> so policy callbacks can resolve other
    /// container services.
    /// </summary>
    /// <typeparam name="TQuery">Query type for the selector.</typeparam>
    /// <typeparam name="TResult">Result type for the selector.</typeparam>
    /// <param name="services">Service collection to add the registration to.</param>
    /// <param name="configure">
    /// Delegate that receives the <see cref="System.IServiceProvider"/> and
    /// <see cref="TieredProviderSelectorOptions.Default"/> and returns the (possibly
    /// mutated) options to use. Runs lazily inside the singleton factory at first
    /// resolution. Must not return <see langword="null"/>.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// services.AddTieredProviderSelector&lt;WebSearchQuery, IReadOnlyList&lt;WebSearchResult&gt;&gt;(
    ///     (sp, opts) =>
    ///     {
    ///         var logger = sp.GetRequiredService&lt;ILogger&lt;CopilotWebSearchProvider&gt;&gt;();
    ///         return opts with
    ///         {
    ///             FailurePolicies =
    ///             [
    ///                 .. opts.FailurePolicies,
    ///                 new ProviderFailurePolicy(
    ///                     Match: ex => ex is CopilotAuthException,
    ///                     SkipDuration: TimeSpan.FromMinutes(5),
    ///                     OnHit: ctx =>
    ///                     {
    ///                         logger.LogWarning(ctx.Exception,
    ///                             "Provider {Provider} skipped until {Until}",
    ///                             ctx.ProviderName, ctx.SkipUntil);
    ///                         return ValueTask.CompletedTask;
    ///                     }),
    ///             ],
    ///         };
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddTieredProviderSelector<TQuery, TResult>(
        this IServiceCollection services,
        Func<IServiceProvider, TieredProviderSelectorOptions, TieredProviderSelectorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return AddTieredProviderSelectorCore<TQuery, TResult>(
            services,
            optionsFactory: sp => configure(sp, TieredProviderSelectorOptions.Default));
    }

    private static IServiceCollection AddTieredProviderSelectorCore<TQuery, TResult>(
        IServiceCollection services,
        Func<IServiceProvider, TieredProviderSelectorOptions>? optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<ITieredProviderSelector<TQuery, TResult>>(sp =>
        {
            TieredProviderSelectorOptions resolved;
            if (optionsFactory is null)
            {
                resolved = TieredProviderSelectorOptions.Default;
            }
            else
            {
                resolved = optionsFactory(sp)
                    ?? throw new InvalidOperationException(
                        "The configure delegate passed to AddTieredProviderSelector returned null. " +
                        "Return TieredProviderSelectorOptions.Default if you want the framework defaults.");
            }

            return new TieredProviderSelector<TQuery, TResult>(
                sp.GetServices<ITieredProvider<TQuery, TResult>>(),
                sp.GetRequiredService<IQuotaGate>(),
                sp.GetRequiredService<IAgentExecutionContextAccessor>(),
                partitionSelector: null,
                options: resolved,
                timeProvider: sp.GetRequiredService<TimeProvider>());
        });

        return services;
    }
}
