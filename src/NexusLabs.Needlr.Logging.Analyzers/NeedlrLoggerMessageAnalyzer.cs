using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Logging.Analyzers;

/// <summary>
/// Validates usage of <c>[NeedlrLoggerMessage]</c> so misconfigurations surface as build diagnostics
/// rather than confusing generated-code errors.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NeedlrLoggerMessageAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeFullName = "NexusLabs.Needlr.Logging.NeedlrLoggerMessageAttribute";
    private const string ILoggerFullName = "Microsoft.Extensions.Logging.ILogger";
    private const string ExceptionFullName = "System.Exception";
    private const int MaxDefineParameters = 6;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MustBePartial,
            DiagnosticDescriptors.MustReturnVoid,
            DiagnosticDescriptors.MustNotBeGeneric,
            DiagnosticDescriptors.ContainingTypeMustBePartial,
            DiagnosticDescriptors.LoggerNotFound,
            DiagnosticDescriptors.TooManyParameters);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var attributeSymbol = compilationStart.Compilation.GetTypeByMetadataName(AttributeFullName);
            if (attributeSymbol is null)
            {
                return;
            }

            var loggerSymbol = compilationStart.Compilation.GetTypeByMetadataName(ILoggerFullName);
            var exceptionSymbol = compilationStart.Compilation.GetTypeByMetadataName(ExceptionFullName);

            compilationStart.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, attributeSymbol, loggerSymbol, exceptionSymbol),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol attributeSymbol,
        INamedTypeSymbol? loggerSymbol,
        INamedTypeSymbol? exceptionSymbol)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!HasAttribute(method, attributeSymbol))
        {
            return;
        }

        // For a partial method with both parts, analyze only the definition to avoid double-reporting.
        if (method.PartialDefinitionPart is not null)
        {
            return;
        }

        var location = method.Locations.Length > 0 ? method.Locations[0] : Location.None;

        if (!method.IsPartialDefinition && method.PartialDefinitionPart is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MustBePartial, location, method.Name));
        }

        if (!method.ReturnsVoid)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MustReturnVoid, location, method.Name));
        }

        if (method.IsGenericMethod)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MustNotBeGeneric, location, method.Name));
        }

        if (!IsContainingTypePartial(method))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContainingTypeMustBePartial,
                location,
                method.ContainingType?.Name ?? method.Name));
        }

        if (loggerSymbol is not null && !HasAccessibleLogger(method, loggerSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.LoggerNotFound, location, method.Name));
        }

        var messageParameterCount = CountMessageParameters(method, loggerSymbol, exceptionSymbol);
        if (messageParameterCount > MaxDefineParameters)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TooManyParameters,
                location,
                method.Name,
                messageParameterCount));
        }
    }

    private static bool HasAttribute(IMethodSymbol method, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsContainingTypePartial(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return true;
        }

        foreach (var reference in containingType.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is TypeDeclarationSyntax typeDeclaration &&
                typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAccessibleLogger(IMethodSymbol method, INamedTypeSymbol loggerSymbol)
    {
        if (method.IsStatic)
        {
            return method.Parameters.Any(parameter => IsLogger(parameter.Type, loggerSymbol));
        }

        for (var type = method.ContainingType; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                if (member.IsStatic)
                {
                    continue;
                }

                if (member is IFieldSymbol field && IsLogger(field.Type, loggerSymbol))
                {
                    return true;
                }

                if (member is IPropertySymbol { GetMethod: not null } property && IsLogger(property.Type, loggerSymbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountMessageParameters(
        IMethodSymbol method,
        INamedTypeSymbol? loggerSymbol,
        INamedTypeSymbol? exceptionSymbol)
    {
        var count = 0;
        var loggerConsumed = !method.IsStatic;
        var exceptionConsumed = false;

        foreach (var parameter in method.Parameters)
        {
            if (!loggerConsumed && loggerSymbol is not null && IsLogger(parameter.Type, loggerSymbol))
            {
                loggerConsumed = true;
                continue;
            }

            if (!exceptionConsumed && exceptionSymbol is not null && IsException(parameter.Type, exceptionSymbol))
            {
                exceptionConsumed = true;
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool IsLogger(ITypeSymbol type, INamedTypeSymbol loggerSymbol)
    {
        if (SymbolEqualityComparer.Default.Equals(type, loggerSymbol))
        {
            return true;
        }

        return type.AllInterfaces.Any(iface => SymbolEqualityComparer.Default.Equals(iface, loggerSymbol));
    }

    private static bool IsException(ITypeSymbol type, INamedTypeSymbol exceptionSymbol)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, exceptionSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
