using System.Reflection;
using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NexusLabs.Needlr.Generators.CodeGen;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Runtime tests for generated source-bootstrap behavior across assembly boundaries.
/// </summary>
public sealed class BootstrapCodeGeneratorRuntimeTests
{
    [Fact]
    public void ReferencedModuleConstructor_RunsBeforeHostRegistration()
    {
        var probeKey = $"Needlr.BootstrapProbe.{Guid.NewGuid():N}";
        var hostRegistrationKey = $"Needlr.HostRegistrationProbe.{Guid.NewGuid():N}";
        var referencedSource = $$"""
            #nullable enable
            using System;
            using System.Runtime.CompilerServices;

            namespace ReferencedLib
            {
                internal static class ReferencedModuleInitializer
                {
                    [ModuleInitializer]
                    internal static void Initialize()
                    {
                        AppContext.SetData("{{probeKey}}", true);
                    }
                }
            }

            namespace ReferencedLib.Generated
            {
                public static class TypeRegistry
                {
                }
            }
            """;

        var referencedAssembly = CompileAssembly(
            "ReferencedLib",
            [referencedSource],
            []);
        var referencedMetadata = MetadataReference.CreateFromImage(referencedAssembly);

        var bootstrapSource = BootstrapCodeGenerator.GenerateModuleInitializerBootstrapSource(
            "HostApp",
            ["ReferencedLib"],
            new BreadcrumbWriter(BreadcrumbLevel.None),
            hasFactories: false,
            hasOptions: false,
            hasProviders: false);
        var hostSupportSource = $$"""
            #nullable enable
            using System;
            using System.Collections.Generic;

            namespace Microsoft.Extensions.Configuration
            {
                public interface IConfiguration
                {
                }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }
            }

            namespace NexusLabs.Needlr.Generators
            {
                public sealed class InjectableTypeInfo
                {
                }

                public sealed class PluginTypeInfo
                {
                }

                public static class NeedlrSourceGenBootstrap
                {
                    public static void Register(
                        Func<IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
                        Func<IReadOnlyList<PluginTypeInfo>> pluginTypeProvider,
                        Action<object> decoratorApplier,
                        Action<object, object>? optionsRegistrar)
                    {
                        AppContext.SetData(
                            "{{hostRegistrationKey}}",
                            AppContext.GetData("{{probeKey}}"));
                    }
                }
            }

            namespace HostApp.Generated
            {
                using Microsoft.Extensions.DependencyInjection;
                using NexusLabs.Needlr.Generators;

                internal static class TypeRegistry
                {
                    internal static IReadOnlyList<InjectableTypeInfo> GetInjectableTypes() =>
                        Array.Empty<InjectableTypeInfo>();

                    internal static IReadOnlyList<PluginTypeInfo> GetPluginTypes() =>
                        Array.Empty<PluginTypeInfo>();

                    internal static void ApplyDecorators(IServiceCollection services)
                    {
                    }
                }
            }

            namespace HostApp
            {
                public static class EntryPoint
                {
                    public static void Touch()
                    {
                    }
                }
            }
            """;
        var hostAssembly = CompileAssembly(
            "HostApp",
            [bootstrapSource, hostSupportSource],
            [referencedMetadata]);

        var loadContext = new AssemblyLoadContext(
            $"Needlr.BootstrapRuntimeTest.{Guid.NewGuid():N}",
            isCollectible: true);
        loadContext.Resolving += (_, assemblyName) =>
        {
            if (!string.Equals(assemblyName.Name, "ReferencedLib", StringComparison.Ordinal))
                return null;

            using var referencedAssemblyStream = new MemoryStream(referencedAssembly);
            return loadContext.LoadFromStream(referencedAssemblyStream);
        };

        try
        {
            using var hostAssemblyStream = new MemoryStream(hostAssembly);
            var loadedHostAssembly = loadContext.LoadFromStream(hostAssemblyStream);
            var entryPointType = loadedHostAssembly.GetType("HostApp.EntryPoint");
            Assert.NotNull(entryPointType);

            var touchMethod = entryPointType.GetMethod(
                "Touch",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(touchMethod);

            _ = touchMethod.Invoke(null, null);

            Assert.Equal(true, AppContext.GetData(probeKey));
            Assert.Equal(true, AppContext.GetData(hostRegistrationKey));
        }
        finally
        {
            AppContext.SetData(probeKey, null);
            AppContext.SetData(hostRegistrationKey, null);
            loadContext.Unload();
        }
    }

    private static byte[] CompileAssembly(
        string assemblyName,
        IReadOnlyList<string> sources,
        IReadOnlyList<MetadataReference> additionalReferences)
    {
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source))
            .ToArray();
        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(additionalReferences)
            .ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        Assert.True(
            emitResult.Success,
            $"Expected {assemblyName} to compile: {string.Join("; ", emitResult.Diagnostics)}");

        return assemblyStream.ToArray();
    }
}
