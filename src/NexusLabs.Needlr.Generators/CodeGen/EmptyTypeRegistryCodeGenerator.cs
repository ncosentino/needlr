// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Emits a minimal <c>TypeRegistry</c> and module-initializer bootstrap for an assembly that
/// declares <c>[GenerateTypeRegistry]</c> but has nothing to register.
/// </summary>
/// <remarks>
/// The emitted code depends only on the attributes package (<c>InjectableTypeInfo</c>,
/// <c>PluginTypeInfo</c>, and <c>NeedlrSourceGenBootstrap</c>) — never on the injection packages.
/// A project must reference the attributes package to use <c>[GenerateTypeRegistry]</c> at all, so
/// this registry always compiles, even for a project that references no Needlr injection packages
/// (a documentation, contracts, or abstractions library). Emitting it keeps a type-less participant
/// force-loadable by consumers (<c>typeof(&lt;Assembly&gt;.Generated.TypeRegistry)</c>) without the
/// <c>CS0234</c> failure that omitting it would cause.
/// </remarks>
internal static class EmptyTypeRegistryCodeGenerator
{
    /// <summary>
    /// Emits the empty <c>TypeRegistry</c> exposing empty injectable and plugin providers.
    /// </summary>
    /// <param name="assemblyName">The assembly the registry is generated for.</param>
    /// <param name="breadcrumbs">The breadcrumb writer supplied by the orchestration method.</param>
    /// <returns>The generated C# source for the empty <c>TypeRegistry</c>.</returns>
    internal static string GenerateTypeRegistrySource(string assemblyName, BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Type Registry (empty)");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Compile-time generated registry for an assembly that declares [GenerateTypeRegistry]");
        builder.AppendLine("/// but contains no registerable types. Exposes empty providers so that consumers which");
        builder.AppendLine("/// force-load this type compile, without depending on the Needlr injection packages.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class TypeRegistry");
        builder.AppendLine("{");
        builder.AppendLine("    private static readonly global::NexusLabs.Needlr.Generators.InjectableTypeInfo[] _types = [];");
        builder.AppendLine("    private static readonly global::NexusLabs.Needlr.Generators.PluginTypeInfo[] _plugins = [];");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>Gets all injectable types discovered at compile time (none).</summary>");
        builder.AppendLine("    /// <returns>An empty read-only list.</returns>");
        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NexusLabs.Needlr.Generators.InjectableTypeInfo> GetInjectableTypes() => _types;");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>Gets all plugin types discovered at compile time (none).</summary>");
        builder.AppendLine("    /// <returns>An empty read-only list.</returns>");
        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NexusLabs.Needlr.Generators.PluginTypeInfo> GetPluginTypes() => _plugins;");
        builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Emits the module-initializer bootstrap that registers the empty providers with
    /// <c>NeedlrSourceGenBootstrap</c>.
    /// </summary>
    /// <param name="assemblyName">The assembly the bootstrap is generated for.</param>
    /// <param name="breadcrumbs">The breadcrumb writer supplied by the orchestration method.</param>
    /// <returns>The generated C# source for the module-initializer bootstrap.</returns>
    /// <remarks>
    /// Uses the two-argument <c>Register</c> overload (no decorator applier, no options registrar),
    /// which takes no <c>IServiceCollection</c>/<c>IConfiguration</c> parameters and therefore keeps
    /// the emitted code free of any Microsoft.Extensions.DependencyInjection dependency.
    /// </remarks>
    internal static string GenerateBootstrapSource(string assemblyName, BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Source-Gen Bootstrap (empty)");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();
        builder.AppendLine("internal static class NeedlrSourceGenModuleInitializer");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("    internal static void Initialize()");
        builder.AppendLine("    {");
        builder.AppendLine("        global::NexusLabs.Needlr.Generators.NeedlrSourceGenBootstrap.Register(");
        builder.AppendLine($"            global::{safeAssemblyName}.Generated.TypeRegistry.GetInjectableTypes,");
        builder.AppendLine($"            global::{safeAssemblyName}.Generated.TypeRegistry.GetPluginTypes);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }
}
