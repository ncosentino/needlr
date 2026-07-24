using System.Collections.Generic;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Composes normalized constructor guards, emits their direct calls, and writes XML
/// exception documentation for built-in guards.
/// </summary>
internal static class ConstructorGuardCodeGenerator
{
    /// <summary>
    /// Composes an optional default null guard with explicit guards, preserving order
    /// and removing duplicate effective calls.
    /// </summary>
    internal static ConstructorGuardModel[] ComposeEffectiveGuards(
        bool includeDefaultNotNull,
        ConstructorGuardModel[] explicitGuards)
    {
        var suppressDefault = explicitGuards.Any(
            guard => guard.Kind == GeneratedConstructorGuardKind.None);
        var seen = new HashSet<(
            GeneratedConstructorGuardKind Kind,
            string? Type,
            string? Method,
            string ForwardedArguments)>();
        var guards = new List<ConstructorGuardModel>();

        if (includeDefaultNotNull && !suppressDefault &&
            seen.Add((GeneratedConstructorGuardKind.NotNull, null, null, string.Empty)))
        {
            guards.Add(new ConstructorGuardModel(
                GeneratedConstructorGuardKind.NotNull,
                null,
                null,
                null));
        }

        foreach (var guard in explicitGuards)
        {
            if (guard.Kind == GeneratedConstructorGuardKind.None)
                continue;

            var key = (
                guard.Kind,
                guard.CustomGuardTypeName,
                guard.CustomGuardMethodName,
                ForwardedArguments: string.Join("\u0001", guard.ForwardedArgumentLiterals));
            if (!seen.Add(key))
                continue;

            guards.Add(guard);
        }

        return guards.ToArray();
    }

    /// <summary>
    /// Returns whether an effective built-in guard rejects a runtime null value.
    /// </summary>
    internal static bool HasBuiltInNullRejectingGuard(
        IReadOnlyList<ConstructorGuardModel> effectiveGuards)
    {
        return effectiveGuards.Any(guard =>
            guard.Kind == GeneratedConstructorGuardKind.NotNull ||
            guard.Kind == GeneratedConstructorGuardKind.NotNullOrEmpty ||
            guard.Kind == GeneratedConstructorGuardKind.NotNullOrWhiteSpace);
    }

    /// <summary>
    /// Builds the direct C# call for one normalized guard.
    /// </summary>
    internal static string BuildGuardCall(
        ConstructorGuardModel guard,
        string parameterName)
    {
        return guard.Kind switch
        {
            GeneratedConstructorGuardKind.NotNull =>
                $"global::System.ArgumentNullException.ThrowIfNull({parameterName});",
            GeneratedConstructorGuardKind.NotNullOrEmpty =>
                $"global::System.ArgumentException.ThrowIfNullOrEmpty({parameterName});",
            GeneratedConstructorGuardKind.NotNullOrWhiteSpace =>
                $"global::System.ArgumentException.ThrowIfNullOrWhiteSpace({parameterName});",
            GeneratedConstructorGuardKind.Custom =>
                BuildCustomGuardCall(guard, parameterName),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Writes coalesced XML exception documentation for built-in guards.
    /// </summary>
    internal static void WriteExceptionDocumentation(
        StringBuilder builder,
        IReadOnlyList<string> parameterNames,
        IReadOnlyList<ConstructorGuardModel[]> effectiveGuards,
        string indentation)
    {
        var nullParameters = new List<string>();
        var emptyParameters = new List<string>();
        var whiteSpaceParameters = new List<string>();

        for (var i = 0; i < parameterNames.Count; i++)
        {
            var documentsNullFailure = false;
            var documentsEmptyFailure = false;
            var documentsWhiteSpaceFailure = false;

            foreach (var guard in effectiveGuards[i])
            {
                switch (guard.Kind)
                {
                    case GeneratedConstructorGuardKind.NotNull:
                        documentsNullFailure = true;
                        break;
                    case GeneratedConstructorGuardKind.NotNullOrEmpty:
                        documentsNullFailure = true;
                        documentsEmptyFailure = true;
                        break;
                    case GeneratedConstructorGuardKind.NotNullOrWhiteSpace:
                        documentsNullFailure = true;
                        documentsWhiteSpaceFailure = true;
                        break;
                }
            }

            if (documentsNullFailure)
                nullParameters.Add(parameterNames[i]);

            if (documentsWhiteSpaceFailure)
                whiteSpaceParameters.Add(parameterNames[i]);
            else if (documentsEmptyFailure)
                emptyParameters.Add(parameterNames[i]);
        }

        if (nullParameters.Count > 0)
        {
            var description = BuildParameterCondition(
                nullParameters,
                "is <see langword=\"null\"/>.");
            WriteExceptionElement(
                builder,
                indentation,
                "global::System.ArgumentNullException",
                description);
        }

        if (emptyParameters.Count == 0 && whiteSpaceParameters.Count == 0)
            return;

        var descriptions = new List<string>();
        if (emptyParameters.Count > 0)
        {
            descriptions.Add(BuildParameterCondition(
                emptyParameters,
                "is empty"));
        }

        if (whiteSpaceParameters.Count > 0)
        {
            descriptions.Add(BuildParameterCondition(
                whiteSpaceParameters,
                "is empty or consists only of white-space characters"));
        }

        WriteExceptionElement(
            builder,
            indentation,
            "global::System.ArgumentException",
            string.Join("; or ", descriptions) + ".");
    }

    private static string BuildCustomGuardCall(
        ConstructorGuardModel guard,
        string parameterName)
    {
        var argumentList = guard.ForwardedArgumentLiterals.Length == 0
            ? parameterName
            : $"{parameterName}, {string.Join(", ", guard.ForwardedArgumentLiterals)}";

        return $"{guard.CustomGuardTypeName}.{guard.CustomGuardMethodName}({argumentList}, nameof({parameterName}));";
    }

    private static string BuildParameterCondition(
        IReadOnlyList<string> parameterNames,
        string condition)
    {
        return $"{FormatParameterReferences(parameterNames)} {condition}";
    }

    private static string FormatParameterReferences(IReadOnlyList<string> parameterNames)
    {
        if (parameterNames.Count == 1)
            return $"<paramref name=\"{parameterNames[0]}\"/>";

        if (parameterNames.Count == 2)
        {
            return $"<paramref name=\"{parameterNames[0]}\"/> or <paramref name=\"{parameterNames[1]}\"/>";
        }

        var references = parameterNames
            .Select(parameterName => $"<paramref name=\"{parameterName}\"/>")
            .ToArray();
        return string.Join(", ", references.Take(references.Length - 1)) +
            $", or {references[references.Length - 1]}";
    }

    private static void WriteExceptionElement(
        StringBuilder builder,
        string indentation,
        string exceptionTypeName,
        string description)
    {
        builder.AppendLine(
            $"{indentation}/// <exception cref=\"{exceptionTypeName}\">");
        builder.AppendLine($"{indentation}/// {description}");
        builder.AppendLine($"{indentation}/// </exception>");
    }
}
