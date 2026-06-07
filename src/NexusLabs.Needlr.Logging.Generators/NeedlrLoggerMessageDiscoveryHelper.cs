using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NexusLabs.Needlr.Logging.Generators.Models;

namespace NexusLabs.Needlr.Logging.Generators;

/// <summary>
/// Reads <c>[NeedlrLoggerMessage]</c> methods from Roslyn symbols and produces emission-ready
/// <see cref="DiscoveredLogMethod"/> values. Methods that are invalid (and therefore reported by the
/// analyzer) yield <see langword="null"/> so no broken code is generated.
/// </summary>
internal static class NeedlrLoggerMessageDiscoveryHelper
{
    private const string ILoggerMetadataName = "Microsoft.Extensions.Logging.ILogger";
    private const string ExceptionMetadataName = "System.Exception";

    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Attempts to build a <see cref="DiscoveredLogMethod"/> from a discovered attribute target.
    /// </summary>
    /// <param name="context">The attribute-syntax context produced by the incremental pipeline.</param>
    /// <returns>The discovered method, or <see langword="null"/> when the method is not eligible.</returns>
    public static DiscoveredLogMethod? TryCreate(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IMethodSymbol method)
        {
            return null;
        }

        if (!method.IsPartialDefinition ||
            method.PartialImplementationPart is not null ||
            !method.ReturnsVoid ||
            method.IsGenericMethod)
        {
            return null;
        }

        var compilation = context.SemanticModel.Compilation;
        var loggerType = compilation.GetTypeByMetadataName(ILoggerMetadataName);
        if (loggerType is null)
        {
            return null;
        }

        var exceptionType = compilation.GetTypeByMetadataName(ExceptionMetadataName);

        var loggerParameter = method.IsStatic
            ? method.Parameters.FirstOrDefault(parameter => IsLogger(parameter.Type, loggerType))
            : null;

        string loggerAccess;
        if (method.IsStatic)
        {
            if (loggerParameter is null)
            {
                return null;
            }

            loggerAccess = loggerParameter.Name;
        }
        else
        {
            var loggerMember = FindLoggerMember(method.ContainingType, loggerType);
            if (loggerMember is null)
            {
                return null;
            }

            loggerAccess = "this." + loggerMember;
        }

        var exceptionParameter = exceptionType is null
            ? null
            : method.Parameters.FirstOrDefault(parameter =>
                !SymbolEqualityComparer.Default.Equals(parameter, loggerParameter) &&
                IsException(parameter.Type, exceptionType));

        var parameters = ImmutableArray.CreateBuilder<LogParameterInfo>(method.Parameters.Length);
        foreach (var parameter in method.Parameters)
        {
            ParameterRole role;
            if (SymbolEqualityComparer.Default.Equals(parameter, loggerParameter))
            {
                role = ParameterRole.Logger;
            }
            else if (SymbolEqualityComparer.Default.Equals(parameter, exceptionParameter))
            {
                role = ParameterRole.Exception;
            }
            else
            {
                role = ParameterRole.Message;
            }

            parameters.Add(new LogParameterInfo(
                parameter.Name,
                parameter.Type.ToDisplayString(FullyQualifiedFormat),
                role));
        }

        var containingTypes = GetContainingTypes(method.ContainingType);
        if (containingTypes.IsDefaultOrEmpty)
        {
            return null;
        }

        var namespaceName = method.ContainingType.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : string.Empty;

        ReadAttribute(
            context.Attributes,
            method.Name,
            out var eventId,
            out var eventName,
            out var levelName,
            out var message,
            out var skipEnabledCheck);

        return new DiscoveredLogMethod(
            namespaceName,
            containingTypes,
            method.Name,
            GetModifiers(method),
            parameters.ToImmutable(),
            loggerAccess,
            eventId,
            eventName,
            levelName,
            message,
            skipEnabledCheck);
    }

    private static string GetModifiers(IMethodSymbol method)
    {
        var syntax = method.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (syntax is null)
        {
            return method.IsStatic ? "static" : string.Empty;
        }

        var kept = syntax.Modifiers
            .Where(modifier => !modifier.IsKind(SyntaxKind.PartialKeyword))
            .Select(modifier => modifier.Text);

        return string.Join(" ", kept);
    }

    private static ImmutableArray<ContainingTypeInfo> GetContainingTypes(INamedTypeSymbol? type)
    {
        var chain = new List<ContainingTypeInfo>();
        for (var current = type; current is not null; current = current.ContainingType)
        {
            chain.Add(new ContainingTypeInfo(
                GetTypeKeyword(current),
                current.Name,
                GetTypeParameterList(current)));
        }

        chain.Reverse();
        return chain.ToImmutableArray();
    }

    private static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Struct)
        {
            return type.IsRecord ? "record struct" : "struct";
        }

        return type.IsRecord ? "record" : "class";
    }

    private static string GetTypeParameterList(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
        {
            return string.Empty;
        }

        return "<" + string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)) + ">";
    }

    private static string? FindLoggerMember(INamedTypeSymbol containingType, INamedTypeSymbol loggerType)
    {
        for (var type = containingType; type is not null; type = type.BaseType)
        {
            var fromOwnType = SymbolEqualityComparer.Default.Equals(type, containingType);

            foreach (var member in type.GetMembers())
            {
                if (member.IsStatic)
                {
                    continue;
                }

                if (!fromOwnType && member.DeclaredAccessibility == Accessibility.Private)
                {
                    continue;
                }

                switch (member)
                {
                    case IFieldSymbol { IsImplicitlyDeclared: false } field when IsLogger(field.Type, loggerType):
                        return field.Name;
                    case IPropertySymbol { GetMethod: not null } property when IsLogger(property.Type, loggerType):
                        return property.Name;
                }
            }
        }

        return null;
    }

    private static void ReadAttribute(
        ImmutableArray<AttributeData> attributes,
        string methodName,
        out int eventId,
        out string eventName,
        out string levelName,
        out string message,
        out bool skipEnabledCheck)
    {
        eventId = 0;
        string? explicitEventName = null;
        // Mirror NeedlrLoggerMessageAttribute.Level default (LogLevel.Information == 2).
        var level = 2;
        message = string.Empty;
        skipEnabledCheck = false;

        if (attributes.Length > 0)
        {
            var attribute = attributes[0];

            if (attribute.AttributeConstructor is { } constructor)
            {
                for (var i = 0; i < attribute.ConstructorArguments.Length && i < constructor.Parameters.Length; i++)
                {
                    var value = attribute.ConstructorArguments[i].Value;
                    switch (constructor.Parameters[i].Name)
                    {
                        case "eventId":
                            eventId = ToInt32(value, eventId);
                            break;
                        case "level":
                            level = ToInt32(value, level);
                            break;
                        case "message":
                            message = value as string ?? message;
                            break;
                    }
                }
            }

            foreach (var namedArgument in attribute.NamedArguments)
            {
                var value = namedArgument.Value.Value;
                switch (namedArgument.Key)
                {
                    case "EventId":
                        eventId = ToInt32(value, eventId);
                        break;
                    case "EventName":
                        explicitEventName = value as string;
                        break;
                    case "Level":
                        level = ToInt32(value, level);
                        break;
                    case "Message":
                        message = value as string ?? message;
                        break;
                    case "SkipEnabledCheck":
                        skipEnabledCheck = value is bool boolean && boolean;
                        break;
                }
            }
        }

        eventName = string.IsNullOrEmpty(explicitEventName) ? methodName : explicitEventName!;
        levelName = ToLevelName(level);
    }

    private static int ToInt32(object? value, int fallback) =>
        value is null ? fallback : Convert.ToInt32(value);

    private static string ToLevelName(int level) => level switch
    {
        0 => "Trace",
        1 => "Debug",
        2 => "Information",
        3 => "Warning",
        4 => "Error",
        5 => "Critical",
        6 => "None",
        _ => "Information",
    };

    private static bool IsLogger(ITypeSymbol type, INamedTypeSymbol loggerType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, loggerType))
        {
            return true;
        }

        return type.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, loggerType));
    }

    private static bool IsException(ITypeSymbol type, INamedTypeSymbol exceptionType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, exceptionType))
            {
                return true;
            }
        }

        return false;
    }
}
