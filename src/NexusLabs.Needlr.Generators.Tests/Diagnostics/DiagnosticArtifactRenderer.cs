using System;
using System.Collections.Generic;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// Renders diagnostic Markdown artifacts directly from discovery models.
/// </summary>
internal static class DiagnosticArtifactRenderer
{
    public const string Timestamp = "2024-01-01 00:00:00";

    public const string AssemblyName = "TestAssembly";

    public static HashSet<string> NoFilter()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    public static HashSet<string> Filter(params string[] terms)
    {
        return new HashSet<string>(terms, StringComparer.Ordinal);
    }

    public static Dictionary<string, List<DiagnosticTypeInfo>> NoReferencedAssemblies()
    {
        return new Dictionary<string, List<DiagnosticTypeInfo>>(StringComparer.Ordinal);
    }

    public static Dictionary<string, List<DiagnosticTypeInfo>> ReferencedAssembly(
        string assemblyName,
        params DiagnosticTypeInfo[] types)
    {
        return new Dictionary<string, List<DiagnosticTypeInfo>>(StringComparer.Ordinal)
        {
            [assemblyName] = new List<DiagnosticTypeInfo>(types),
        };
    }

    public static string DependencyGraph(DiscoveryResult discovery, HashSet<string> filter)
    {
        return DependencyGraph(discovery, filter, NoReferencedAssemblies());
    }

    public static string DependencyGraph(
        DiscoveryResult discovery,
        HashSet<string> filter,
        Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
    {
        return DiagnosticsGenerator.GenerateDependencyGraphMarkdown(
            discovery,
            AssemblyName,
            Timestamp,
            filter,
            Array.Empty<string>(),
            referencedAssemblyTypes);
    }

    public static string LifetimeSummary(DiscoveryResult discovery, HashSet<string> filter)
    {
        return LifetimeSummary(discovery, filter, NoReferencedAssemblies());
    }

    public static string LifetimeSummary(
        DiscoveryResult discovery,
        HashSet<string> filter,
        Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
    {
        return DiagnosticsGenerator.GenerateLifetimeSummaryMarkdown(
            discovery,
            AssemblyName,
            Timestamp,
            filter,
            referencedAssemblyTypes);
    }

    public static string RegistrationIndex(DiscoveryResult discovery, HashSet<string> filter)
    {
        return RegistrationIndex(discovery, filter, NoReferencedAssemblies());
    }

    public static string RegistrationIndex(
        DiscoveryResult discovery,
        HashSet<string> filter,
        Dictionary<string, List<DiagnosticTypeInfo>> referencedAssemblyTypes)
    {
        return DiagnosticsGenerator.GenerateRegistrationIndexMarkdown(
            discovery,
            AssemblyName,
            null,
            Timestamp,
            filter,
            referencedAssemblyTypes);
    }

    public static string OptionsSummary(DiscoveryResult discovery, HashSet<string> filter)
    {
        return DiagnosticsGenerator.GenerateOptionsSummaryMarkdown(
            discovery,
            AssemblyName,
            Timestamp,
            filter);
    }
}
