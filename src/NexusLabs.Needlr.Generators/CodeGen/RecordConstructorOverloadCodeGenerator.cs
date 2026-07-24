using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Emits one public forwarding constructor overload for a positional record model.
/// </summary>
internal static class RecordConstructorOverloadCodeGenerator
{
    private const string GeneratedFileSuffix =
        ".RecordConstructorOverload.g.cs";

    /// <summary>
    /// Builds the deterministic generated-source hint name.
    /// </summary>
    internal static string BuildHintName(RecordConstructorOverloadModel model)
    {
        var baseName = model.ContainingNamespace.Length > 0
            ? $"{model.ContainingNamespace}.{model.ContainingTypeName}"
            : model.ContainingTypeName;
        return $"{GeneratorHelpers.SanitizeIdentifier(baseName)}_T{model.Arity}{GeneratedFileSuffix}";
    }

    /// <summary>
    /// Emits the complete partial record declaration and forwarding constructor.
    /// </summary>
    internal static string GenerateSource(
        RecordConstructorOverloadModel model,
        string assemblyName,
        BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();

        breadcrumbs.WriteFileHeader(
            builder,
            assemblyName,
            $"Generated record constructor overload for {model.ContainingTypeName}{model.TypeParameterList}");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        if (model.ContainingNamespace.Length > 0)
        {
            builder.AppendLine($"namespace {model.ContainingNamespace};");
            builder.AppendLine();
        }

        breadcrumbs.WriteInlineComment(
            builder,
            string.Empty,
            $"Constructor overload generated from {model.PropertyParameters.Length} marked property parameter(s)");
        builder.AppendLine(
            $"partial record class {model.EscapedContainingTypeName}{model.TypeParameterList}");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine(
            $"    /// Initializes a new instance of the <see cref=\"{model.EscapedContainingTypeName}{ToCrefTypeParameterList(model.TypeParameterList)}\"/> record by forwarding its positional parameters and assigning its marked properties.");
        builder.AppendLine("    /// </summary>");

        foreach (var parameter in model.PrimaryParameters)
        {
            builder.AppendLine(
                $"    /// <param name=\"{parameter.Name}\">{parameter.Documentation}</param>");
        }

        foreach (var parameter in model.PropertyParameters)
        {
            builder.AppendLine(
                $"    /// <param name=\"{parameter.PropertyName}\">{parameter.Documentation}</param>");
        }

        ConstructorGuardCodeGenerator.WriteExceptionDocumentation(
            builder,
            model.PropertyParameters
                .Select(parameter => parameter.PropertyName)
                .ToArray(),
            model.PropertyParameters
                .Select(parameter => parameter.EffectiveGuards)
                .ToArray(),
            "    ");

        var parameters = model.PrimaryParameters
            .Select(parameter =>
                $"{parameter.DeclarationModifier}{parameter.TypeName} {parameter.EscapedName}")
            .Concat(model.PropertyParameters.Select(parameter =>
                $"{parameter.TypeName} {parameter.EscapedPropertyName}"));
        builder.AppendLine(
            $"    public {model.EscapedContainingTypeName}({string.Join(", ", parameters)})");

        var forwardedArguments = string.Join(
            ", ",
            model.PrimaryParameters.Select(parameter =>
                parameter.ArgumentModifier + parameter.EscapedName));
        builder.AppendLine($"        : this({forwardedArguments})");
        builder.AppendLine("    {");

        var emittedGuard = false;
        foreach (var parameter in model.PropertyParameters)
        {
            foreach (var guard in parameter.EffectiveGuards)
            {
                builder.AppendLine(
                    $"        {ConstructorGuardCodeGenerator.BuildGuardCall(guard, parameter.EscapedPropertyName)}");
                emittedGuard = true;
            }
        }

        if (emittedGuard)
            builder.AppendLine();

        foreach (var parameter in model.PropertyParameters)
        {
            builder.AppendLine(
                $"        this.{parameter.EscapedPropertyName} = {parameter.EscapedPropertyName};");
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
