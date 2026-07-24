using System;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Emits the generated public constructor for a partial class from a
/// <see cref="GeneratedConstructorModel"/>.
/// </summary>
internal static class GeneratedConstructorCodeGenerator
{
    private const string GeneratedConstructorFileSuffix = ".GeneratedConstructor.g.cs";

    /// <summary>
    /// Builds the stable <c>AddSource</c> hint name for a generated-constructor
    /// model, deterministically derived from the type's namespace, name, and generic
    /// arity -- with no mutable, encounter-order-dependent counter involved. Including
    /// arity is required because a namespace can legally declare both <c>Foo</c> and
    /// <c>Foo&lt;T&gt;</c> as distinct types; the arity suffix (mirroring how the CLR
    /// itself disambiguates same-named generic arities via a metadata name like
    /// <c>Foo`1</c>) keeps their hint names from colliding.
    /// </summary>
    internal static string BuildHintName(GeneratedConstructorModel model)
    {
        var baseName = model.ContainingNamespace.Length > 0
            ? $"{model.ContainingNamespace}.{model.ContainingTypeName}"
            : model.ContainingTypeName;

        var safeName = GeneratorHelpers.SanitizeIdentifier(baseName);

        return $"{safeName}_T{model.Arity}{GeneratedConstructorFileSuffix}";
    }

    internal static string GenerateConstructorSource(GeneratedConstructorModel model, string assemblyName, BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();

        breadcrumbs.WriteFileHeader(builder, assemblyName, $"Generated constructor for {model.ContainingTypeName}{model.TypeParameterList}");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (model.ContainingNamespace.Length > 0)
        {
            builder.AppendLine($"namespace {model.ContainingNamespace};");
            builder.AppendLine();
        }

        breadcrumbs.WriteInlineComment(builder, string.Empty, $"Constructor generated from {model.Fields.Length} eligible field(s)");
        builder.AppendLine($"partial class {model.ContainingTypeName}{model.TypeParameterList}");
        builder.AppendLine("{");

        var effectiveGuards = model.Fields
            .Select(field => ConstructorGuardCodeGenerator.ComposeEffectiveGuards(
                model.NullGuardMode == GeneratedConstructorNullGuardMode.NonNullableReferences &&
                    field.IsNonNullableReferenceType,
                field.ExplicitGuards))
            .ToArray();

        builder.AppendLine("    /// <summary>");
        builder.AppendLine($"    /// Initializes a new instance of the <see cref=\"{model.ContainingTypeName}{ToCrefTypeParameterList(model.TypeParameterList)}\"/> class.");
        builder.AppendLine("    /// </summary>");

        foreach (var field in model.Fields)
        {
            builder.AppendLine($"    /// <param name=\"{field.ParameterName}\">The value used to initialize <c>{field.FieldName}</c>.</param>");
        }

        ConstructorGuardCodeGenerator.WriteExceptionDocumentation(
            builder,
            model.Fields.Select(field => field.ParameterName).ToArray(),
            effectiveGuards,
            "    ");

        var parameterList = string.Join(", ", model.Fields.Select(f => $"{f.ParameterTypeName} {f.ParameterName}"));
        builder.AppendLine($"    public {model.ContainingTypeName}({parameterList})");
        builder.AppendLine("    {");

        var emittedAnyGuard = false;
        for (var i = 0; i < model.Fields.Length; i++)
        {
            var field = model.Fields[i];
            foreach (var guard in effectiveGuards[i])
            {
                var guardCall = ConstructorGuardCodeGenerator.BuildGuardCall(
                    guard,
                    field.ParameterName);
                builder.AppendLine($"        {guardCall}");
                emittedAnyGuard = true;
            }
        }

        if (emittedAnyGuard)
        {
            builder.AppendLine();
        }

        foreach (var field in model.Fields)
        {
            builder.AppendLine($"        {field.FieldName} = {field.ParameterName};");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string ToCrefTypeParameterList(string typeParameterList)
    {
        if (typeParameterList.Length == 0)
            return string.Empty;

        return typeParameterList.Replace('<', '{').Replace('>', '}');
    }

}
