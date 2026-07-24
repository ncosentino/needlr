using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using NexusLabs.Needlr.Generators.Models;
using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Shared analyzer logic for constructor guards applied to generated-constructor
/// fields or generated record-overload properties.
/// </summary>
internal static class ConstructorGuardAnalysisHelper
{
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";
    private const string DefaultGuardMethodName = "Validate";
    private const int AttributeTargetsField = 0x0100;
    private const int AttributeTargetsProperty = 0x0080;

    /// <summary>
    /// Builds a normalized occurrence for a direct
    /// <c>ConstructorGuardAttribute</c>.
    /// </summary>
    internal static ConstructorGuardOccurrence BuildDirectGuardOccurrence(
        ISymbol member,
        ITypeSymbol memberType,
        string memberKind,
        AttributeData attribute,
        string? ineligibilityReason)
    {
        if (attribute.ConstructorArguments.Length == 0)
        {
            return CreateOccurrence(
                member,
                memberType,
                memberKind,
                attribute,
                ConstructorGuardOccurrenceKind.BuiltInNone,
                ineligibilityReason,
                null,
                null,
                false,
                false);
        }

        var first = attribute.ConstructorArguments[0];
        if (first.Kind == TypedConstantKind.Enum && first.Value is int enumValue)
        {
            var kind = enumValue == 0
                ? ConstructorGuardOccurrenceKind.BuiltInNone
                : ConstructorGuardOccurrenceKind.BuiltInPositive;
            return CreateOccurrence(
                member,
                memberType,
                memberKind,
                attribute,
                kind,
                ineligibilityReason,
                null,
                null,
                false,
                false);
        }

        if (first.Value is ITypeSymbol guardType)
        {
            var methodNameExplicit = attribute.ConstructorArguments.Length > 1 &&
                attribute.ConstructorArguments[1].Value is string;
            var methodName = methodNameExplicit
                ? (string)attribute.ConstructorArguments[1].Value!
                : DefaultGuardMethodName;
            return CreateOccurrence(
                member,
                memberType,
                memberKind,
                attribute,
                ConstructorGuardOccurrenceKind.CustomType,
                ineligibilityReason,
                guardType,
                methodName,
                methodNameExplicit,
                false);
        }

        return CreateOccurrence(
            member,
            memberType,
            memberKind,
            attribute,
            ConstructorGuardOccurrenceKind.BuiltInNone,
            ineligibilityReason,
            null,
            null,
            false,
            false);
    }

    /// <summary>
    /// Builds a normalized occurrence for a custom guard alias usage.
    /// </summary>
    internal static ConstructorGuardOccurrence BuildAliasOccurrence(
        ISymbol member,
        ITypeSymbol memberType,
        string memberKind,
        AttributeData attribute,
        string? ineligibilityReason,
        ITypeSymbol? guardType,
        string? methodName,
        bool methodNameExplicit,
        bool guardTypeUsageIsInSourceAlias)
    {
        return CreateOccurrence(
            member,
            memberType,
            memberKind,
            attribute,
            ConstructorGuardOccurrenceKind.Alias,
            ineligibilityReason,
            guardType,
            methodName,
            methodNameExplicit,
            guardTypeUsageIsInSourceAlias);
    }

    /// <summary>
    /// Resolves a <c>ConstructorGuardDefinitionAttribute</c> from an application-defined
    /// alias attribute type.
    /// </summary>
    internal static bool TryGetGuardDefinition(
        INamedTypeSymbol attributeClass,
        out ITypeSymbol? guardType,
        out string? methodName,
        out bool methodNameExplicit)
    {
        foreach (var metaAttribute in attributeClass.GetAttributes())
        {
            if (metaAttribute.AttributeClass is not { } metaClass ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                    metaClass,
                    ConstructorGuardDefinitionAttributeName))
            {
                continue;
            }

            if (metaAttribute.ConstructorArguments.Length == 0 ||
                metaAttribute.ConstructorArguments[0].Value is not ITypeSymbol resolvedGuardType)
            {
                continue;
            }

            guardType = resolvedGuardType;
            methodNameExplicit = metaAttribute.ConstructorArguments.Length > 1 &&
                metaAttribute.ConstructorArguments[1].Value is string;
            methodName = methodNameExplicit
                ? (string)metaAttribute.ConstructorArguments[1].Value!
                : DefaultGuardMethodName;
            return true;
        }

        guardType = null;
        methodName = null;
        methodNameExplicit = false;
        return false;
    }

    /// <summary>
    /// Reports the built-in or custom guard diagnostics for one positive occurrence.
    /// </summary>
    internal static void AnalyzePositiveGuardOccurrence(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType,
        ConstructorGuardOccurrence occurrence,
        Location location)
    {
        switch (occurrence.Kind)
        {
            case ConstructorGuardOccurrenceKind.BuiltInPositive:
                AnalyzeBuiltInGuard(context, occurrence, location);
                break;
            case ConstructorGuardOccurrenceKind.CustomType:
                AnalyzeCustomGuard(
                    context,
                    containingType,
                    occurrence,
                    location,
                    occurrence.GuardType,
                    occurrence.MethodName,
                    occurrence.MethodNameExplicit,
                    ImmutableArray<ITypeSymbol>.Empty);
                break;
            case ConstructorGuardOccurrenceKind.Alias:
                AnalyzeAliasGuardAtUsage(
                    context,
                    containingType,
                    occurrence,
                    location);
                break;
        }
    }

    /// <summary>
    /// Returns whether a positive guard occurrence can be emitted as a valid direct
    /// call. Used by generators to fail closed when analyzer diagnostics identify an
    /// invalid guard declaration.
    /// </summary>
    internal static bool IsPositiveGuardOccurrenceValidForGeneration(
        Compilation compilation,
        INamedTypeSymbol containingType,
        ConstructorGuardOccurrence occurrence)
    {
        switch (occurrence.Kind)
        {
            case ConstructorGuardOccurrenceKind.BuiltInPositive:
                if (occurrence.Attribute.ConstructorArguments.Length == 0)
                    return false;

                var constant = occurrence.Attribute.ConstructorArguments[0];
                if (!IsDefinedEnumValue(constant) ||
                    constant.Value is not int rawValue)
                {
                    return false;
                }

                var kind = (BuiltInConstructorGuardKindMirror)rawValue;
                return kind switch
                {
                    BuiltInConstructorGuardKindMirror.NotNull =>
                        CanBeRuntimeNull(occurrence.MemberType),
                    BuiltInConstructorGuardKindMirror.NotNullOrEmpty or
                    BuiltInConstructorGuardKindMirror.NotNullOrWhiteSpace =>
                        occurrence.MemberType.SpecialType ==
                            SpecialType.System_String,
                    _ => false,
                };
            case ConstructorGuardOccurrenceKind.CustomType:
                return IsCustomGuardValidForGeneration(
                    compilation,
                    containingType,
                    occurrence,
                    ImmutableArray<ITypeSymbol>.Empty);
            case ConstructorGuardOccurrenceKind.Alias:
                if (!TryGetForwardedArgumentTypes(
                    occurrence.Attribute,
                    out var forwardedArgumentTypes,
                    out _))
                {
                    return false;
                }

                return IsCustomGuardValidForGeneration(
                    compilation,
                    containingType,
                    occurrence,
                    forwardedArgumentTypes);
            default:
                return true;
        }
    }

    /// <summary>
    /// Reports NDLRGEN047 when an enum-valued guard argument is undefined.
    /// </summary>
    internal static bool TryReportUndefinedEnum(
        SyntaxNodeAnalysisContext context,
        Location location,
        TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Enum ||
            constant.Value is not int rawValue ||
            constant.Type is not { } enumType)
        {
            return false;
        }

        if (IsDefinedEnumValue(constant))
            return false;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidConstructorGuardEnumValue,
            location,
            rawValue,
            enumType.Name));
        return true;
    }

    /// <summary>
    /// Resolves the accessible static guard method compatible with a guarded member and
    /// any positional arguments forwarded from an alias usage.
    /// </summary>
    internal static GuardMethodResolution TryResolveGuardMethod(
        Compilation compilation,
        INamedTypeSymbol withinType,
        ITypeSymbol guardType,
        string methodName,
        ITypeSymbol? memberType,
        string memberKind,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        out IMethodSymbol? method,
        out string? reason,
        out GuardResolutionFailureKind failureKind)
    {
        method = null;
        reason = null;
        failureKind = GuardResolutionFailureKind.None;

        var candidates = guardType.GetMembers(methodName)
            .OfType<IMethodSymbol>()
            .Where(candidate => candidate.MethodKind == MethodKind.Ordinary)
            .ToList();
        if (candidates.Count == 0)
        {
            reason = $"no method named '{methodName}' was found on '{guardType.ToDisplayString()}'";
            failureKind = GuardResolutionFailureKind.General;
            return GuardMethodResolution.NotFound;
        }

        var expectedArity = memberType is null
            ? (int?)null
            : forwardedArgumentTypes.Length + 2;
        var matches = new List<IMethodSymbol>();

        foreach (var candidate in candidates)
        {
            if (!compilation.IsSymbolAccessibleWithin(candidate, withinType))
            {
                reason = "it is not accessible";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            if (!candidate.IsStatic)
            {
                reason = "it is not static";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            if (!candidate.ReturnsVoid)
            {
                reason = "it does not return void";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            if (candidate.Parameters.Length < 2)
            {
                reason = "it does not have at least a value parameter and a trailing string parameter name";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            if (expectedArity.HasValue &&
                candidate.Parameters.Length != expectedArity.Value)
            {
                reason = $"it has {candidate.Parameters.Length - 2} parameter(s) between the value and the parameter name, but this alias usage forwards {forwardedArgumentTypes.Length} argument(s)";
                failureKind = GuardResolutionFailureKind.ForwardedArgument;
                continue;
            }

            if (candidate.Parameters[candidate.Parameters.Length - 1].Type.SpecialType !=
                SpecialType.System_String)
            {
                reason = "its last parameter is not a string parameter name";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            var refKindParameter = candidate.Parameters.FirstOrDefault(
                parameter => parameter.RefKind != RefKind.None);
            if (refKindParameter is not null)
            {
                reason = $"its '{refKindParameter.Name}' parameter is passed by '{refKindParameter.RefKind.ToString().ToLowerInvariant()}', which a direct generated call cannot supply";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            if (memberType is null)
            {
                matches.Add(candidate);
                continue;
            }

            var valueParameterType = candidate.Parameters[0].Type;
            var middleParameters = candidate.Parameters
                .Skip(1)
                .Take(candidate.Parameters.Length - 2)
                .ToList();

            bool isCompatible;
            string? candidateReason;
            GuardResolutionFailureKind candidateFailureKind;

            if (candidate.IsGenericMethod)
            {
                isCompatible = TryInferGenericParameterCompatibility(
                    candidate,
                    valueParameterType,
                    memberType,
                    memberKind,
                    middleParameters,
                    forwardedArgumentTypes,
                    compilation,
                    out candidateReason,
                    out candidateFailureKind);
            }
            else
            {
                isCompatible = TryCheckNonGenericCompatibility(
                    memberType,
                    memberKind,
                    valueParameterType,
                    middleParameters,
                    forwardedArgumentTypes,
                    compilation,
                    out candidateReason,
                    out candidateFailureKind);
            }

            if (!isCompatible)
            {
                reason = candidateReason;
                failureKind = candidateFailureKind;
                continue;
            }

            matches.Add(candidate);
        }

        if (matches.Count == 1)
        {
            method = matches[0];
            failureKind = GuardResolutionFailureKind.None;
            return GuardMethodResolution.Found;
        }

        if (matches.Count > 1)
        {
            failureKind = GuardResolutionFailureKind.None;
            return GuardMethodResolution.Ambiguous;
        }

        if (reason is null)
        {
            reason = "no accessible static method compatible with (value, ...forwarded arguments, string parameterName) was found";
            failureKind = GuardResolutionFailureKind.General;
        }

        return GuardMethodResolution.NotFound;
    }

    /// <summary>
    /// Returns why an alias definition cannot target constructor-guard members.
    /// </summary>
    internal static string? GetGuardDefinitionTargetInvalidReason(
        INamedTypeSymbol attributeClass)
    {
        if (!InheritsFromSystemAttribute(attributeClass))
            return "not derived from System.Attribute";

        var usageAttribute = attributeClass.GetAttributes()
            .FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() ==
                "System.AttributeUsageAttribute");
        if (usageAttribute is not null &&
            usageAttribute.ConstructorArguments.Length > 0 &&
            usageAttribute.ConstructorArguments[0].Value is int validOn &&
            (validOn & (AttributeTargetsField | AttributeTargetsProperty)) == 0)
        {
            return "not usable on fields or properties ([AttributeUsage] includes neither AttributeTargets.Field nor AttributeTargets.Property)";
        }

        return null;
    }

    /// <summary>
    /// Gets the source location of an attribute occurrence.
    /// </summary>
    internal static Location GetAttributeLocation(
        SyntaxNodeAnalysisContext context,
        AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?
            .GetSyntax(context.CancellationToken)
            .GetLocation() ?? Location.None;
    }

    private static ConstructorGuardOccurrence CreateOccurrence(
        ISymbol member,
        ITypeSymbol memberType,
        string memberKind,
        AttributeData attribute,
        ConstructorGuardOccurrenceKind kind,
        string? ineligibilityReason,
        ITypeSymbol? guardType,
        string? methodName,
        bool methodNameExplicit,
        bool guardTypeUsageIsInSourceAlias)
    {
        return new ConstructorGuardOccurrence(
            member,
            memberType,
            memberKind,
            attribute,
            kind,
            ineligibilityReason,
            guardType,
            methodName,
            methodNameExplicit,
            guardTypeUsageIsInSourceAlias);
    }

    private static bool IsCustomGuardValidForGeneration(
        Compilation compilation,
        INamedTypeSymbol containingType,
        ConstructorGuardOccurrence occurrence,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes)
    {
        if (occurrence.GuardType is null ||
            occurrence.GuardType.TypeKind == TypeKind.Error ||
            !compilation.IsSymbolAccessibleWithin(
                occurrence.GuardType,
                containingType) ||
            occurrence.MethodNameExplicit &&
                string.IsNullOrWhiteSpace(occurrence.MethodName))
        {
            return false;
        }

        var methodName = string.IsNullOrEmpty(occurrence.MethodName)
            ? DefaultGuardMethodName
            : occurrence.MethodName!;
        return TryResolveGuardMethod(
            compilation,
            containingType,
            occurrence.GuardType,
            methodName,
            occurrence.MemberType,
            occurrence.MemberKind,
            forwardedArgumentTypes,
            out _,
            out _,
            out _) == GuardMethodResolution.Found;
    }

    private static bool IsDefinedEnumValue(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Enum ||
            constant.Value is not int rawValue ||
            constant.Type is not { } enumType)
        {
            return false;
        }

        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(field =>
                field.HasConstantValue &&
                field.ConstantValue is int memberValue &&
                memberValue == rawValue);
    }

    private static void AnalyzeAliasGuardAtUsage(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType,
        ConstructorGuardOccurrence occurrence,
        Location location)
    {
        if (!TryGetForwardedArgumentTypes(
            occurrence.Attribute,
            out var forwardedArgumentTypes,
            out var unsupportedReason))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardAliasUsageArgumentUnsupported,
                location,
                occurrence.Member.Name,
                unsupportedReason));
            return;
        }

        if (!occurrence.GuardTypeUsageIsInSourceAlias)
        {
            AnalyzeCustomGuard(
                context,
                containingType,
                occurrence,
                location,
                occurrence.GuardType,
                occurrence.MethodName,
                occurrence.MethodNameExplicit,
                forwardedArgumentTypes);
            return;
        }

        if (occurrence.GuardType is null ||
            occurrence.GuardType.TypeKind == TypeKind.Error)
        {
            return;
        }

        var methodName = string.IsNullOrEmpty(occurrence.MethodName)
            ? DefaultGuardMethodName
            : occurrence.MethodName!;
        var resolution = TryResolveGuardMethod(
            context.Compilation,
            containingType,
            occurrence.GuardType,
            methodName,
            occurrence.MemberType,
            occurrence.MemberKind,
            forwardedArgumentTypes,
            out _,
            out var reason,
            out var failureKind);

        if (resolution == GuardMethodResolution.NotFound &&
            failureKind == GuardResolutionFailureKind.ForwardedArgument)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible,
                location,
                methodName,
                occurrence.GuardType.ToDisplayString(),
                occurrence.Member.Name,
                occurrence.MemberType.ToDisplayString(),
                reason));
        }
        else if (resolution == GuardMethodResolution.NotFound &&
            reason == GetMemberTypeIncompatibleReason(occurrence.MemberKind))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardMethodInvalid,
                location,
                methodName,
                occurrence.GuardType.ToDisplayString(),
                occurrence.Member.Name,
                occurrence.MemberType.ToDisplayString(),
                reason));
        }
        else if (resolution == GuardMethodResolution.Ambiguous)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
                location,
                methodName,
                occurrence.GuardType.ToDisplayString(),
                occurrence.Member.Name,
                occurrence.MemberType.ToDisplayString()));
        }
    }

    private static void AnalyzeBuiltInGuard(
        SyntaxNodeAnalysisContext context,
        ConstructorGuardOccurrence occurrence,
        Location location)
    {
        var first = occurrence.Attribute.ConstructorArguments[0];
        if (TryReportUndefinedEnum(context, location, first))
            return;

        if (first.Value is not int rawValue)
            return;

        var kind = (BuiltInConstructorGuardKindMirror)rawValue;
        switch (kind)
        {
            case BuiltInConstructorGuardKindMirror.NotNull:
                if (!CanBeRuntimeNull(occurrence.MemberType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
                        location,
                        "NotNull",
                        occurrence.Member.Name,
                        occurrence.MemberType.ToDisplayString(),
                        $"the {occurrence.MemberKind}'s type is a non-nullable value type, so a runtime null value is never possible"));
                }

                break;
            case BuiltInConstructorGuardKindMirror.NotNullOrEmpty:
            case BuiltInConstructorGuardKindMirror.NotNullOrWhiteSpace:
                if (occurrence.MemberType.SpecialType != SpecialType.System_String)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
                        location,
                        kind.ToString(),
                        occurrence.Member.Name,
                        occurrence.MemberType.ToDisplayString(),
                        $"this guard only applies to string-compatible {GetMemberKindPlural(occurrence.MemberKind)}"));
                }

                break;
        }
    }

    private static void AnalyzeCustomGuard(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType,
        ConstructorGuardOccurrence occurrence,
        Location location,
        ITypeSymbol? guardType,
        string? methodName,
        bool methodNameExplicit,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes)
    {
        if (guardType is null || guardType.TypeKind == TypeKind.Error)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardTypeInvalid,
                location,
                occurrence.Member.Name,
                "the guard type could not be resolved"));
            return;
        }

        if (!context.Compilation.IsSymbolAccessibleWithin(guardType, containingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardTypeInvalid,
                location,
                occurrence.Member.Name,
                $"'{guardType.ToDisplayString()}' is not accessible from '{containingType.ToDisplayString()}'"));
            return;
        }

        if (methodNameExplicit && string.IsNullOrWhiteSpace(methodName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardMethodNameInvalid,
                location,
                occurrence.Member.Name));
            return;
        }

        var effectiveMethodName = string.IsNullOrEmpty(methodName)
            ? DefaultGuardMethodName
            : methodName!;
        var resolution = TryResolveGuardMethod(
            context.Compilation,
            containingType,
            guardType,
            effectiveMethodName,
            occurrence.MemberType,
            occurrence.MemberKind,
            forwardedArgumentTypes,
            out _,
            out var reason,
            out var failureKind);

        switch (resolution)
        {
            case GuardMethodResolution.NotFound
                when failureKind == GuardResolutionFailureKind.ForwardedArgument:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible,
                    location,
                    effectiveMethodName,
                    guardType.ToDisplayString(),
                    occurrence.Member.Name,
                    occurrence.MemberType.ToDisplayString(),
                    reason));
                break;
            case GuardMethodResolution.NotFound:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardMethodInvalid,
                    location,
                    effectiveMethodName,
                    guardType.ToDisplayString(),
                    occurrence.Member.Name,
                    occurrence.MemberType.ToDisplayString(),
                    reason ?? "no compatible method was found"));
                break;
            case GuardMethodResolution.Ambiguous:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
                    location,
                    effectiveMethodName,
                    guardType.ToDisplayString(),
                    occurrence.Member.Name,
                    occurrence.MemberType.ToDisplayString()));
                break;
        }
    }

    private static bool TryGetForwardedArgumentTypes(
        AttributeData attribute,
        out ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        out string? unsupportedReason)
    {
        if (attribute.NamedArguments.Length > 0)
        {
            forwardedArgumentTypes = ImmutableArray<ITypeSymbol>.Empty;
            unsupportedReason = $"named argument '{attribute.NamedArguments[0].Key}' is not forwarded to the guard method in this version";
            return false;
        }

        if (attribute.ConstructorArguments.Length == 0)
        {
            forwardedArgumentTypes = ImmutableArray<ITypeSymbol>.Empty;
            unsupportedReason = null;
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(
            attribute.ConstructorArguments.Length);
        for (var i = 0; i < attribute.ConstructorArguments.Length; i++)
        {
            var constant = attribute.ConstructorArguments[i];
            if (!TypedConstantRenderer.TryRender(constant, out _))
            {
                forwardedArgumentTypes = ImmutableArray<ITypeSymbol>.Empty;
                unsupportedReason = $"positional argument {i + 1} is {DescribeUnsupportedConstant(constant)}, which is not forwarded to the guard method in this version";
                return false;
            }

            builder.Add(constant.Type!);
        }

        forwardedArgumentTypes = builder.MoveToImmutable();
        unsupportedReason = null;
        return true;
    }

    private static string DescribeUnsupportedConstant(TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Array)
            return "an array";

        if (constant.Value is float or double)
            return "a floating-point value";

        return "an unsupported value";
    }

    private static bool CanBeRuntimeNull(ITypeSymbol type)
    {
        if (type.IsReferenceType)
            return true;

        if (type is ITypeParameterSymbol typeParameter)
        {
            return !typeParameter.HasValueTypeConstraint &&
                !typeParameter.HasUnmanagedTypeConstraint;
        }

        return type is INamedTypeSymbol
        {
            OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
        };
    }

    private static bool IsAssignableTo(
        ITypeSymbol sourceType,
        ITypeSymbol parameterType,
        Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, parameterType))
            return true;

        var conversion = compilation.ClassifyConversion(sourceType, parameterType);
        return conversion.Exists && (conversion.IsIdentity || conversion.IsImplicit);
    }

    private static bool TryCheckNonGenericCompatibility(
        ITypeSymbol memberType,
        string memberKind,
        ITypeSymbol valueParameterType,
        List<IParameterSymbol> middleParameters,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        Compilation compilation,
        out string? reason,
        out GuardResolutionFailureKind failureKind)
    {
        if (!IsAssignableTo(memberType, valueParameterType, compilation))
        {
            reason = GetMemberTypeIncompatibleReason(memberKind);
            failureKind = GuardResolutionFailureKind.General;
            return false;
        }

        for (var i = 0; i < middleParameters.Count; i++)
        {
            if (IsAssignableTo(
                forwardedArgumentTypes[i],
                middleParameters[i].Type,
                compilation))
            {
                continue;
            }

            reason = $"its parameter '{middleParameters[i].Name}' of type '{middleParameters[i].Type.ToDisplayString()}' is not compatible with the forwarded argument of type '{forwardedArgumentTypes[i].ToDisplayString()}'";
            failureKind = GuardResolutionFailureKind.ForwardedArgument;
            return false;
        }

        reason = null;
        failureKind = GuardResolutionFailureKind.None;
        return true;
    }

    private static bool TryInferGenericParameterCompatibility(
        IMethodSymbol genericMethod,
        ITypeSymbol parameterType,
        ITypeSymbol memberType,
        string memberKind,
        List<IParameterSymbol> middleParameters,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        Compilation compilation,
        out string? reason,
        out GuardResolutionFailureKind failureKind)
    {
        var methodTypeParameters = new HashSet<ITypeSymbol>(
            genericMethod.TypeParameters,
            SymbolEqualityComparer.Default);
        string? lastReason = null;
        var lastFailureKind = GuardResolutionFailureKind.General;

        foreach (var candidateType in GetTypeAndSupertypes(memberType))
        {
            var substitution = new Dictionary<ITypeSymbol, ITypeSymbol>(
                SymbolEqualityComparer.Default);
            if (!TryUnify(
                parameterType,
                candidateType,
                methodTypeParameters,
                substitution))
            {
                continue;
            }

            var forwardedMismatch = false;
            for (var i = 0; i < middleParameters.Count; i++)
            {
                if (TryUnify(
                    middleParameters[i].Type,
                    forwardedArgumentTypes[i],
                    methodTypeParameters,
                    substitution))
                {
                    continue;
                }

                lastReason ??= $"its parameter '{middleParameters[i].Name}' cannot accept the forwarded argument of type '{forwardedArgumentTypes[i].ToDisplayString()}'";
                lastFailureKind = GuardResolutionFailureKind.ForwardedArgument;
                forwardedMismatch = true;
                break;
            }

            if (forwardedMismatch)
                continue;

            var unboundParameter = genericMethod.TypeParameters.FirstOrDefault(
                typeParameter => !substitution.ContainsKey(typeParameter));
            if (unboundParameter is not null)
            {
                var onlyForwarded = IsReachableOnlyThroughForwardedParameters(
                    unboundParameter,
                    parameterType,
                    middleParameters);
                lastReason ??= onlyForwarded
                    ? $"its type parameter '{unboundParameter.Name}' cannot be inferred from the {memberKind}'s type or the forwarded arguments"
                    : $"its type parameter '{unboundParameter.Name}' cannot be inferred from the {memberKind}'s type";
                lastFailureKind = onlyForwarded
                    ? GuardResolutionFailureKind.ForwardedArgument
                    : GuardResolutionFailureKind.General;
                continue;
            }

            var constraintViolation = FindConstraintViolation(
                genericMethod.TypeParameters,
                substitution,
                compilation,
                out var violatingTypeParameter);
            if (constraintViolation is not null)
            {
                var onlyForwarded = violatingTypeParameter is not null &&
                    IsReachableOnlyThroughForwardedParameters(
                        violatingTypeParameter,
                        parameterType,
                        middleParameters);
                lastReason ??= constraintViolation;
                lastFailureKind = onlyForwarded
                    ? GuardResolutionFailureKind.ForwardedArgument
                    : GuardResolutionFailureKind.General;
                continue;
            }

            reason = null;
            failureKind = GuardResolutionFailureKind.None;
            return true;
        }

        reason = lastReason ?? GetMemberTypeIncompatibleReason(memberKind);
        failureKind = lastReason is null
            ? GuardResolutionFailureKind.General
            : lastFailureKind;
        return false;
    }

    private static string GetMemberTypeIncompatibleReason(string memberKind)
    {
        return $"its value parameter type is not compatible with the {memberKind}'s type";
    }

    private static string GetMemberKindPlural(string memberKind)
    {
        return memberKind == "property" ? "properties" : memberKind + "s";
    }

    private static bool ContainsTypeParameter(
        ITypeSymbol type,
        ITypeParameterSymbol typeParameter)
    {
        if (SymbolEqualityComparer.Default.Equals(type, typeParameter))
            return true;

        if (type is INamedTypeSymbol namedType)
        {
            return namedType.TypeArguments.Any(
                typeArgument => ContainsTypeParameter(typeArgument, typeParameter));
        }

        if (type is IArrayTypeSymbol arrayType)
            return ContainsTypeParameter(arrayType.ElementType, typeParameter);

        return false;
    }

    private static bool IsReachableOnlyThroughForwardedParameters(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol valueParameterType,
        List<IParameterSymbol> middleParameters)
    {
        if (ContainsTypeParameter(valueParameterType, typeParameter))
            return false;

        return middleParameters.Any(
            parameter => ContainsTypeParameter(parameter.Type, typeParameter));
    }

    private static string? FindConstraintViolation(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        Dictionary<ITypeSymbol, ITypeSymbol> substitution,
        Compilation compilation,
        out ITypeParameterSymbol? violatingTypeParameter)
    {
        foreach (var typeParameter in typeParameters)
        {
            if (!substitution.TryGetValue(typeParameter, out var argumentType))
                continue;

            if (typeParameter.HasReferenceTypeConstraint &&
                !argumentType.IsReferenceType)
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires a reference type, but '{argumentType.ToDisplayString()}' is a value type";
            }

            if (typeParameter.HasValueTypeConstraint &&
                !argumentType.IsValueType)
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires a non-nullable value type, but '{argumentType.ToDisplayString()}' is not";
            }

            if (typeParameter.HasUnmanagedTypeConstraint &&
                !argumentType.IsUnmanagedType)
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires an unmanaged type, but '{argumentType.ToDisplayString()}' is not unmanaged";
            }

            if (typeParameter.HasConstructorConstraint &&
                argumentType is INamedTypeSymbol namedArgumentType &&
                namedArgumentType.TypeKind != TypeKind.Struct &&
                !GeneratedConstructorEligibility.HasAccessibleParameterlessConstructor(
                    namedArgumentType))
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires a public parameterless constructor, which '{argumentType.ToDisplayString()}' does not have";
            }

            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(
                    argumentType,
                    constraintType))
                {
                    continue;
                }

                var conversion = compilation.ClassifyConversion(
                    argumentType,
                    constraintType);
                if (conversion.Exists &&
                    (conversion.IsIdentity || conversion.IsImplicit))
                {
                    continue;
                }

                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires '{constraintType.ToDisplayString()}', which '{argumentType.ToDisplayString()}' does not satisfy";
            }
        }

        violatingTypeParameter = null;
        return null;
    }

    private static IEnumerable<ITypeSymbol> GetTypeAndSupertypes(ITypeSymbol type)
    {
        yield return type;

        var current = (type as INamedTypeSymbol)?.BaseType;
        while (current is not null)
        {
            yield return current;
            current = current.BaseType;
        }

        foreach (var iface in type.AllInterfaces)
        {
            yield return iface;
        }
    }

    private static bool TryUnify(
        ITypeSymbol parameterType,
        ITypeSymbol candidateType,
        HashSet<ITypeSymbol> methodTypeParameters,
        Dictionary<ITypeSymbol, ITypeSymbol> substitution)
    {
        if (parameterType is ITypeParameterSymbol typeParameter &&
            methodTypeParameters.Contains(typeParameter))
        {
            if (substitution.TryGetValue(typeParameter, out var bound))
                return SymbolEqualityComparer.Default.Equals(bound, candidateType);

            substitution[typeParameter] = candidateType;
            return true;
        }

        if (parameterType is INamedTypeSymbol namedParameter &&
            candidateType is INamedTypeSymbol namedCandidate)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                namedParameter.OriginalDefinition,
                namedCandidate.OriginalDefinition))
            {
                return false;
            }

            if (namedParameter.TypeArguments.Length !=
                namedCandidate.TypeArguments.Length)
            {
                return false;
            }

            for (var i = 0; i < namedParameter.TypeArguments.Length; i++)
            {
                if (!TryUnify(
                    namedParameter.TypeArguments[i],
                    namedCandidate.TypeArguments[i],
                    methodTypeParameters,
                    substitution))
                {
                    return false;
                }
            }

            return true;
        }

        if (parameterType is IArrayTypeSymbol arrayParameter &&
            candidateType is IArrayTypeSymbol arrayCandidate)
        {
            return TryUnify(
                arrayParameter.ElementType,
                arrayCandidate.ElementType,
                methodTypeParameters,
                substitution);
        }

        return SymbolEqualityComparer.Default.Equals(parameterType, candidateType);
    }

    private static bool InheritsFromSystemAttribute(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.ToDisplayString() == "System.Attribute")
                return true;

            current = current.BaseType;
        }

        return false;
    }
}
