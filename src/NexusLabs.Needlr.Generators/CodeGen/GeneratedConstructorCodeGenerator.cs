using System;
using System.Collections.Generic;
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

        builder.AppendLine("    /// <summary>");
        builder.AppendLine($"    /// Initializes a new instance of the <see cref=\"{model.ContainingTypeName}{ToCrefTypeParameterList(model.TypeParameterList)}\"/> class.");
        builder.AppendLine("    /// </summary>");

        foreach (var field in model.Fields)
        {
            builder.AppendLine($"    /// <param name=\"{field.ParameterName}\">The value used to initialize <c>{field.FieldName}</c>.</param>");
        }

        var parameterList = string.Join(", ", model.Fields.Select(f => $"{f.ParameterTypeName} {f.ParameterName}"));
        builder.AppendLine($"    public {model.ContainingTypeName}({parameterList})");
        builder.AppendLine("    {");

        var emittedAnyGuard = false;
        foreach (var field in model.Fields)
        {
            foreach (var guardCall in ComposeGuardCalls(model.NullGuardMode, field))
            {
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

    private static List<string> ComposeGuardCalls(GeneratedConstructorNullGuardMode mode, EligibleConstructorField field)
    {
        var suppressDefault = field.ExplicitGuards.Any(g => g.Kind == GeneratedConstructorGuardKind.None);
        var seen = new HashSet<(GeneratedConstructorGuardKind Kind, string? Type, string? Method, string ForwardedArguments)>();
        var calls = new List<string>();

        if (mode == GeneratedConstructorNullGuardMode.NonNullableReferences && field.IsNonNullableReferenceType && !suppressDefault)
        {
            if (seen.Add((GeneratedConstructorGuardKind.NotNull, null, null, string.Empty)))
            {
                calls.Add(BuildGuardCall(GeneratedConstructorGuardKind.NotNull, field.ParameterName, null, null, Array.Empty<string>()));
            }
        }

        foreach (var guard in field.ExplicitGuards)
        {
            if (guard.Kind == GeneratedConstructorGuardKind.None)
                continue;

            var key = (guard.Kind, guard.CustomGuardTypeName, guard.CustomGuardMethodName, ForwardedArguments: string.Join("\u0001", guard.ForwardedArgumentLiterals));
            if (!seen.Add(key))
                continue;

            calls.Add(BuildGuardCall(guard.Kind, field.ParameterName, guard.CustomGuardTypeName, guard.CustomGuardMethodName, guard.ForwardedArgumentLiterals));
        }

        return calls;
    }

    private static string BuildGuardCall(
        GeneratedConstructorGuardKind kind,
        string parameterName,
        string? customGuardTypeName,
        string? customGuardMethodName,
        string[] forwardedArgumentLiterals)
    {
        return kind switch
        {
            GeneratedConstructorGuardKind.NotNull => $"global::System.ArgumentNullException.ThrowIfNull({parameterName});",
            GeneratedConstructorGuardKind.NotNullOrEmpty => $"global::System.ArgumentException.ThrowIfNullOrEmpty({parameterName});",
            GeneratedConstructorGuardKind.NotNullOrWhiteSpace => $"global::System.ArgumentException.ThrowIfNullOrWhiteSpace({parameterName});",
            GeneratedConstructorGuardKind.Custom => BuildCustomGuardCall(parameterName, customGuardTypeName, customGuardMethodName, forwardedArgumentLiterals),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Builds a custom guard method call, splicing any positional arguments forwarded
    /// from a parameterized alias attribute usage between the guarded value and the
    /// trailing <c>nameof</c> parameter name -- e.g.
    /// <c>MinCountGuard.Validate(value, 3, nameof(value));</c>. When
    /// <paramref name="forwardedArgumentLiterals"/> is empty (a built-in guard's
    /// call, a direct <c>[ConstructorGuard(typeof(...))]</c> usage, or a bare alias
    /// with no positional constructor arguments) the emitted call is byte-for-byte
    /// identical to the two-argument shape this generator has always emitted.
    /// </summary>
    private static string BuildCustomGuardCall(
        string parameterName,
        string? customGuardTypeName,
        string? customGuardMethodName,
        string[] forwardedArgumentLiterals)
    {
        var argumentList = forwardedArgumentLiterals.Length == 0
            ? parameterName
            : $"{parameterName}, {string.Join(", ", forwardedArgumentLiterals)}";

        return $"{customGuardTypeName}.{customGuardMethodName}({argumentList}, nameof({parameterName}));";
    }
}

