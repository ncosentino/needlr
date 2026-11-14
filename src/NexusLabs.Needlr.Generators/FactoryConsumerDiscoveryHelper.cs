using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NexusLabs.Needlr.Generators.CodeGen;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

internal static class FactoryConsumerDiscoveryHelper
{
    internal static Compilation BindFactoryConsumers(
        Compilation compilation,
        IReadOnlyList<DiscoveredFactory> factories,
        string assemblyName,
        BreadcrumbWriter breadcrumbs,
        string? projectDirectory)
    {
        var interfaceFactories = factories.Where(factory => factory.GenerateInterface).ToArray();
        if (interfaceFactories.Length == 0)
            return compilation;

        // AddSource does not update the input compilation. Reuse the real emitter in
        // a private compilation so Roslyn binds factory names, aliases and namespaces
        // without relaxing discovery's rejection of unrelated error symbols.
        var source = FactoryCodeGenerator.GenerateFactoryInterfacesSource(
            interfaceFactories,
            assemblyName,
            breadcrumbs,
            projectDirectory);
        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        var factoryTree = CSharpSyntaxTree.ParseText(
            GeneratedSourceText.Create(source),
            parseOptions);

        return compilation.AddSyntaxTrees(factoryTree);
    }
}
