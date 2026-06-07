using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using NexusLabs.Needlr.Logging.Generators.CodeGen;
using NexusLabs.Needlr.Logging.Generators.Models;

namespace NexusLabs.Needlr.Logging.Generators;

/// <summary>
/// Source generator that implements <c>[NeedlrLoggerMessage]</c> partial methods with cancellation-aware
/// logging built on top of the public <c>LoggerMessage.Define</c> fast path.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NeedlrLoggerMessageGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "NexusLabs.Needlr.Logging.NeedlrLoggerMessageAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (attributeContext, _) => NeedlrLoggerMessageDiscoveryHelper.TryCreate(attributeContext))
            .Where(static method => method.HasValue)
            .Select(static (method, _) => method!.Value);

        context.RegisterSourceOutput(methods.Collect(), static (production, items) => Emit(production, items));
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<DiscoveredLogMethod> methods)
    {
        if (methods.IsDefaultOrEmpty)
        {
            return;
        }

        var groups = new Dictionary<string, List<DiscoveredLogMethod>>();
        var order = new List<string>();
        foreach (var method in methods)
        {
            if (!groups.TryGetValue(method.ContainingTypeKey, out var list))
            {
                list = new List<DiscoveredLogMethod>();
                groups[method.ContainingTypeKey] = list;
                order.Add(method.ContainingTypeKey);
            }

            list.Add(method);
        }

        var fileIndex = 0;
        foreach (var key in order)
        {
            var list = groups[key];
            var named = new List<(DiscoveredLogMethod Method, string FieldName)>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                named.Add((list[i], $"__{list[i].MethodName}Callback_{i}"));
            }

            var source = NeedlrLoggerMessageCodeGenerator.GenerateForType(
                list[0].Namespace,
                list[0].ContainingTypes,
                named);

            context.AddSource(MakeHintName(key, fileIndex), SourceText.From(source, Encoding.UTF8));
            fileIndex++;
        }
    }

    private static string MakeHintName(string containingTypeKey, int index)
    {
        var builder = new StringBuilder(containingTypeKey.Length);
        foreach (var character in containingTypeKey)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return $"{builder}_{index}.NeedlrLoggerMessage.g.cs";
    }
}
