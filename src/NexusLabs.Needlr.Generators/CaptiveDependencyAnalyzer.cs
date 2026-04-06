// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Detects disposable captive dependencies and reports diagnostics.
/// A captive dependency occurs when a longer-lived service depends on a shorter-lived disposable.
/// </summary>
internal static class CaptiveDependencyAnalyzer
{
    /// <summary>
    /// Detects disposable captive dependencies using inferred lifetimes from DiscoveryResult.
    /// Reports NDLRGEN022 when a longer-lived service depends on a shorter-lived disposable.
    /// </summary>
    internal static void ReportDisposableCaptiveDependencies(SourceProductionContext spc, DiscoveryResult discoveryResult)
    {
        // Build lookup from type name to DiscoveredType for O(1) lifetime lookups
        var typeLookup = new Dictionary<string, DiscoveredType>();
        foreach (var type in discoveryResult.InjectableTypes)
        {
            typeLookup[type.TypeName] = type;
            // Also map by interfaces so we can look up dependencies by interface
            foreach (var iface in type.InterfaceNames)
            {
                // Only add if not already present (first registration wins for interface resolution)
                if (!typeLookup.ContainsKey(iface))
                {
                    typeLookup[iface] = type;
                }
            }
        }

        // Check each injectable type for captive dependencies
        foreach (var type in discoveryResult.InjectableTypes)
        {
            CheckForCaptiveDependencies(spc, type, typeLookup);
        }
    }

    /// <summary>
    /// Checks a single type for captive dependency issues.
    /// </summary>
    private static void CheckForCaptiveDependencies(
        SourceProductionContext spc,
        DiscoveredType type,
        Dictionary<string, DiscoveredType> typeLookup)
    {
        // Skip types with transient lifetime - they can't capture shorter-lived dependencies
        if (type.Lifetime == GeneratorLifetime.Transient)
            return;

        foreach (var param in type.ConstructorParameters)
        {
            // Skip factory patterns that create new instances on demand
            if (IsFactoryPattern(param.TypeName))
                continue;

            // Try to find the dependency in our discovered types
            if (!typeLookup.TryGetValue(param.TypeName, out var dependency))
                continue;

            // Check if the dependency is shorter-lived
            if (!IsShorterLifetime(type.Lifetime, dependency.Lifetime))
                continue;

            // Check if the dependency is disposable
            if (!dependency.IsDisposable)
                continue;

            // Report the captive dependency
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DisposableCaptiveDependency,
                Location.None,
                type.TypeName,
                GetLifetimeName(type.Lifetime),
                dependency.TypeName,
                GetLifetimeName(dependency.Lifetime)));
        }
    }

    /// <summary>
    /// Checks if a type name represents a factory pattern that creates new instances on demand.
    /// </summary>
    private static bool IsFactoryPattern(string typeName)
    {
        // Func<T> - factory delegate
        if (typeName.StartsWith("System.Func<", StringComparison.Ordinal))
            return true;

        // Lazy<T> - deferred creation
        if (typeName.StartsWith("System.Lazy<", StringComparison.Ordinal))
            return true;

        // IServiceScopeFactory - creates new scopes
        if (typeName == "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory")
            return true;

        // IServiceProvider - resolves services dynamically
        if (typeName == "System.IServiceProvider")
            return true;

        return false;
    }

    /// <summary>
    /// Checks if dependency lifetime is shorter than consumer lifetime.
    /// </summary>
    private static bool IsShorterLifetime(GeneratorLifetime consumer, GeneratorLifetime dependency)
    {
        // Singleton > Scoped > Transient (in terms of lifetime duration)
        // A shorter lifetime means the dependency will be disposed sooner
        return (consumer, dependency) switch
        {
            (GeneratorLifetime.Singleton, GeneratorLifetime.Scoped) => true,
            (GeneratorLifetime.Singleton, GeneratorLifetime.Transient) => true,
            (GeneratorLifetime.Scoped, GeneratorLifetime.Transient) => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the human-readable name for a lifetime.
    /// </summary>
    internal static string GetLifetimeName(GeneratorLifetime lifetime) => lifetime switch
    {
        GeneratorLifetime.Singleton => "Singleton",
        GeneratorLifetime.Scoped => "Scoped",
        GeneratorLifetime.Transient => "Transient",
        _ => lifetime.ToString()
    };
}
