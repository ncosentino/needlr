// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;

namespace NexusLabs.Needlr;

/// <summary>
/// Static registry for source-generated registrations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a decoupling layer between the core Needlr runtime and the
/// source-generated bootstrap code. The source generator emits a module initializer
/// that registers callbacks here, and the <c>ConfiguredSyringe</c> calls these
/// callbacks during <c>BuildServiceProvider</c>.
/// </para>
/// <para>
/// This allows <c>NexusLabs.Needlr.Injection</c> to have no dependency on
/// <c>NexusLabs.Needlr.Generators.Attributes</c>.
/// </para>
/// </remarks>
public static class SourceGenRegistry
{
    private static readonly object _gate = new();

    private static Action<object, object>? _optionsRegistrar;
    private static Action<object, object>? _extensionRegistrar;

    /// <summary>
    /// Registers the options registrar from source-generated code.
    /// </summary>
    /// <param name="registrar">
    /// Action that registers options. Parameters are (IServiceCollection, IConfiguration),
    /// typed as object to avoid package dependencies.
    /// </param>
    public static void RegisterOptionsRegistrar(Action<object, object> registrar)
    {
        lock (_gate)
        {
            var prior = _optionsRegistrar;
            if (prior == null)
            {
                _optionsRegistrar = registrar;
            }
            else
            {
                _optionsRegistrar = (services, config) =>
                {
                    prior(services, config);
                    registrar(services, config);
                };
            }
        }
    }

    /// <summary>
    /// Registers an extension registrar from extension packages.
    /// </summary>
    /// <param name="registrar">
    /// Action that registers extension services. Parameters are (IServiceCollection, IConfiguration),
    /// typed as object to avoid package dependencies.
    /// </param>
    public static void RegisterExtension(Action<object, object> registrar)
    {
        lock (_gate)
        {
            var prior = _extensionRegistrar;
            if (prior == null)
            {
                _extensionRegistrar = registrar;
            }
            else
            {
                _extensionRegistrar = (services, config) =>
                {
                    prior(services, config);
                    registrar(services, config);
                };
            }
        }
    }

    /// <summary>
    /// Gets the options registrar if one is registered.
    /// </summary>
    public static bool TryGetOptionsRegistrar(out Action<object, object>? registrar)
    {
        lock (_gate)
        {
            registrar = _optionsRegistrar;
            return registrar != null;
        }
    }

    /// <summary>
    /// Gets the extension registrar if one is registered.
    /// </summary>
    public static bool TryGetExtensionRegistrar(out Action<object, object>? registrar)
    {
        lock (_gate)
        {
            registrar = _extensionRegistrar;
            return registrar != null;
        }
    }

    /// <summary>
    /// Clears all registrations. For testing purposes only.
    /// </summary>
    internal static void Clear()
    {
        lock (_gate)
        {
            _optionsRegistrar = null;
            _extensionRegistrar = null;
        }
    }
}
