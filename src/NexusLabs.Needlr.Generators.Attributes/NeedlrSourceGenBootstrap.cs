using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Runtime bootstrap registry for source-generated Needlr components.
/// </summary>
/// <remarks>
/// The source generator emits a module initializer in the host assembly that calls
/// <see cref="Register"/> with the generated TypeRegistry providers.
/// Needlr runtime can then discover generated registries without any runtime reflection.
/// </remarks>
public static class NeedlrSourceGenBootstrap
{
    private sealed class Registration
    {
        public Registration(
            Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
            Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
        {
            InjectableTypeProvider = injectableTypeProvider;
            PluginTypeProvider = pluginTypeProvider;
        }

        public Func<IReadOnlyList<InjectableTypeInfo>> InjectableTypeProvider { get; }
        public Func<IReadOnlyList<PluginTypeInfo>> PluginTypeProvider { get; }
    }

    private static readonly object _gate = new object();
    private static readonly List<Registration> _registrations = new List<Registration>();

    private static readonly AsyncLocal<Registration?> _asyncLocalOverride = new AsyncLocal<Registration?>();

    private static Registration? _cachedCombined;

    /// <summary>
    /// Registers the generated type and plugin providers for this application.
    /// </summary>
    public static void Register(
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        if (injectableTypeProvider is null) throw new ArgumentNullException(nameof(injectableTypeProvider));
        if (pluginTypeProvider is null) throw new ArgumentNullException(nameof(pluginTypeProvider));

        lock (_gate)
        {
            _registrations.Add(new Registration(injectableTypeProvider, pluginTypeProvider));
            _cachedCombined = null;
        }
    }

    /// <summary>
    /// Gets the registered providers (if any).
    /// </summary>
    public static bool TryGetProviders(
        out Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        out Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        var local = _asyncLocalOverride.Value;
        if (local is not null)
        {
            injectableTypeProvider = local.InjectableTypeProvider;
            pluginTypeProvider = local.PluginTypeProvider;
            return true;
        }

        lock (_gate)
        {
            if (_registrations.Count == 0)
            {
                injectableTypeProvider = null!;
                pluginTypeProvider = null!;
                return false;
            }

            if (_cachedCombined is null)
            {
                _cachedCombined = Combine(_registrations);
            }

            injectableTypeProvider = _cachedCombined.InjectableTypeProvider;
            pluginTypeProvider = _cachedCombined.PluginTypeProvider;
            return true;
        }
    }

    internal static IDisposable BeginTestScope(
        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
    {
        if (injectableTypeProvider is null) throw new ArgumentNullException(nameof(injectableTypeProvider));
        if (pluginTypeProvider is null) throw new ArgumentNullException(nameof(pluginTypeProvider));

        var prior = _asyncLocalOverride.Value;
        _asyncLocalOverride.Value = new Registration(injectableTypeProvider, pluginTypeProvider);
        return new Scope(prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Registration? _prior;

        public Scope(Registration? prior)
        {
            _prior = prior;
        }

        public void Dispose()
        {
            _asyncLocalOverride.Value = _prior;
        }
    }

    private static Registration Combine(IReadOnlyList<Registration> registrations)
    {
        // Snapshot the current registrations to avoid capturing a mutable List.
        var injectableProviders = registrations.Select(r => r.InjectableTypeProvider).ToArray();
        var pluginProviders = registrations.Select(r => r.PluginTypeProvider).ToArray();

        IReadOnlyList<InjectableTypeInfo> GetInjectableTypes()
        {
            var result = new List<InjectableTypeInfo>();
            var seen = new HashSet<Type>();

            foreach (var provider in injectableProviders)
            {
                foreach (var info in provider())
                {
                    if (seen.Add(info.Type))
                    {
                        result.Add(info);
                    }
                }
            }

            return result;
        }

        IReadOnlyList<PluginTypeInfo> GetPluginTypes()
        {
            var result = new List<PluginTypeInfo>();
            var seen = new HashSet<Type>();

            foreach (var provider in pluginProviders)
            {
                foreach (var info in provider())
                {
                    if (seen.Add(info.PluginType))
                    {
                        result.Add(info);
                    }
                }
            }

            return result;
        }

        return new Registration(GetInjectableTypes, GetPluginTypes);
    }
}
