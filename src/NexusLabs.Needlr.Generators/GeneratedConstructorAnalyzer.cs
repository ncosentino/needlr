using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that validates <c>[GenerateConstructor]</c>, <c>[ConstructorGuard]</c>,
/// <c>[ConstructorIgnore]</c>, and <c>[ConstructorGuardDefinition]</c> usage:
/// NDLRGEN039-044 validate the shape of a type using generated-constructor
/// generation; NDLRGEN045-048 validate field-level guard attribute usage and
/// built-in guard compatibility; NDLRGEN049-052 validate custom guard type and
/// method resolution; NDLRGEN053-054 validate <c>[ConstructorGuardDefinition]</c>
/// alias declarations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedConstructorAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateConstructorAttributeName = "GenerateConstructorAttribute";
    private const string ConstructorGuardAttributeName = "ConstructorGuardAttribute";
    private const string ConstructorIgnoreAttributeName = "ConstructorIgnoreAttribute";
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";
    private const string DefaultGuardMethodName = "Validate";
    private const string FieldTypeIncompatibleReason = "its value parameter type is not compatible with the field's type";
    private const int AttributeTargetsField = 0x0100;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.GeneratedConstructorRequiresPartialType,
            DiagnosticDescriptors.GeneratedConstructorUnsupportedTypeShape,
            DiagnosticDescriptors.GeneratedConstructorConflictsWithExplicitConstructor,
            DiagnosticDescriptors.GeneratedConstructorBaseTypeRequiresParameterlessConstructor,
            DiagnosticDescriptors.GeneratedConstructorNoEligibleFields,
            DiagnosticDescriptors.GeneratedConstructorParameterNameCollision,
            DiagnosticDescriptors.ConstructorGuardAttributeHasNoEffect,
            DiagnosticDescriptors.ConstructorGuardAttributeOnIneligibleField,
            DiagnosticDescriptors.InvalidConstructorGuardEnumValue,
            DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
            DiagnosticDescriptors.ConstructorGuardTypeInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodNameInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodInvalid,
            DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
            DiagnosticDescriptors.ConstructorGuardDefinitionTargetInvalid,
            DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
            DiagnosticDescriptors.ConstructorGuardAliasUsageArgumentUnsupported,
            DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
            return;

        // Field-attribute occurrences are collected per actual declaration part: a
        // field only exists in the one partial-class part that declares it, so this
        // must run for every syntax-node invocation to see every field.
        var occurrences = CollectFieldGuardOccurrences(context, typeDeclaration, typeSymbol);
        var classHasTrigger = GeneratedConstructorEligibility.HasGenerateConstructorAttribute(typeSymbol) ||
            GeneratedConstructorEligibility.HasPositiveFieldGuardTrigger(typeSymbol);

        ReportFieldOccurrenceDiagnostics(context, typeSymbol, occurrences, classHasTrigger);

        // Symbol-level facts (class-shape eligibility, [GenerateConstructor]'s enum
        // argument, [ConstructorGuardDefinition] target validity) are identical no
        // matter which partial declaration part is currently being visited. This
        // analyzer is registered as a syntax-node action, so it runs once per partial
        // declaration part; without restricting these checks to a single canonical
        // part, a type split across N partial declarations would report each
        // class-level diagnostic N times.
        if (!GeneratedConstructorEligibility.IsCanonicalDeclaration(typeSymbol, typeDeclaration))
            return;

        AnalyzeGuardDefinitionTarget(context, typeSymbol);

        if (!classHasTrigger)
            return;

        AnalyzeGenerateConstructorEnumArgument(context, typeSymbol);
        AnalyzeTypeShape(context, typeDeclaration, typeSymbol);
    }

    // ------------------------------------------------------------------
    // Class-shape diagnostics: NDLRGEN039-044
    // ------------------------------------------------------------------

    private static void AnalyzeTypeShape(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDeclaration, INamedTypeSymbol typeSymbol)
    {
        var location = typeDeclaration.Identifier.GetLocation();

        if (typeSymbol.IsRecord)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorUnsupportedTypeShape, location, typeSymbol.Name, "a record type"));
        }
        else if (typeSymbol.ContainingType != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorUnsupportedTypeShape, location, typeSymbol.Name, "a nested type"));
        }

        if (!GeneratedConstructorEligibility.IsDeclaredPartial(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorRequiresPartialType, location, typeSymbol.Name));
        }

        if (GeneratedConstructorEligibility.HasExplicitInstanceConstructor(typeSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorConflictsWithExplicitConstructor, location, typeSymbol.Name));
        }

        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object &&
            !GeneratedConstructorEligibility.HasAccessibleParameterlessConstructor(typeSymbol.BaseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorBaseTypeRequiresParameterlessConstructor,
                location,
                typeSymbol.Name,
                typeSymbol.BaseType.Name));
        }

        var eligibleFields = GeneratedConstructorEligibility.GetEligibleConstructorFields(typeSymbol);
        if (eligibleFields.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratedConstructorNoEligibleFields, location, typeSymbol.Name));
            return;
        }

        var seenParameterNames = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var field in eligibleFields)
        {
            var parameterName = ConstructorGenerationDiscoveryHelper.GetParameterName(field.Name);
            if (!seenParameterNames.Add(parameterName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.GeneratedConstructorParameterNameCollision, location, typeSymbol.Name, parameterName));
            }
        }
    }

    private static void AnalyzeGenerateConstructorEnumArgument(SyntaxNodeAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrClass ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(attrClass, GenerateConstructorAttributeName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
                continue;

            ReportIfUndefinedEnum(context, attribute, attribute.ConstructorArguments[0]);
        }
    }

    private static void ReportIfUndefinedEnum(SyntaxNodeAnalysisContext context, AttributeData attribute, TypedConstant constant)
    {
        TryReportUndefinedEnum(context, GetAttributeLocation(context, attribute), constant);
    }

    /// <summary>
    /// Reports NDLRGEN047 when <paramref name="constant"/> is an enum-typed argument
    /// whose raw value does not match any member declared on the enum type, and returns
    /// <see langword="true"/> when it did so. Enum membership is checked against the
    /// real attribute-supplied enum type's fields rather than a mirrored local enum, so
    /// this stays correct if <c>ConstructorGuardKind</c> or <c>ConstructorNullGuardMode</c>
    /// gains new members.
    /// </summary>
    private static bool TryReportUndefinedEnum(SyntaxNodeAnalysisContext context, Location location, TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Enum || constant.Value is not int rawValue || constant.Type is not { } enumType)
            return false;

        var isDefined = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => f.HasConstantValue && f.ConstantValue is int memberValue && memberValue == rawValue);

        if (isDefined)
            return false;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidConstructorGuardEnumValue, location, rawValue, enumType.Name));
        return true;
    }

    // ------------------------------------------------------------------
    // Field-level occurrence collection and reporting: NDLRGEN045-052
    // ------------------------------------------------------------------

    private enum GuardOccurrenceKind
    {
        Ignore,
        BuiltInNone,
        BuiltInPositive,
        CustomType,
        Alias,
    }

    private readonly struct GuardOccurrence
    {
        public GuardOccurrence(
            IFieldSymbol field,
            AttributeData attribute,
            GuardOccurrenceKind kind,
            string? ineligibilityReason,
            ITypeSymbol? guardType = null,
            string? methodName = null,
            bool methodNameExplicit = false,
            bool guardTypeUsageIsInSourceAlias = false)
        {
            Field = field;
            Attribute = attribute;
            Kind = kind;
            IneligibilityReason = ineligibilityReason;
            GuardType = guardType;
            MethodName = methodName;
            MethodNameExplicit = methodNameExplicit;
            GuardTypeUsageIsInSourceAlias = guardTypeUsageIsInSourceAlias;
        }

        public IFieldSymbol Field { get; }
        public AttributeData Attribute { get; }
        public GuardOccurrenceKind Kind { get; }
        public string? IneligibilityReason { get; }
        public ITypeSymbol? GuardType { get; }
        public string? MethodName { get; }
        public bool MethodNameExplicit { get; }

        /// <summary>
        /// True when this custom-guard occurrence came from an alias attribute declared
        /// in the current compilation's source. Such a definition is independently
        /// validated at its own declaration site, so field-usage sites skip re-reporting
        /// the same underlying problem.
        /// </summary>
        public bool GuardTypeUsageIsInSourceAlias { get; }
    }

    private static List<GuardOccurrence> CollectFieldGuardOccurrences(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        INamedTypeSymbol typeSymbol)
    {
        var occurrences = new List<GuardOccurrence>();

        foreach (var fieldDeclaration in typeDeclaration.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                if (context.SemanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol fieldSymbol)
                    continue;

                var ineligibilityReason = GeneratedConstructorEligibility.GetFieldIneligibilityReason(fieldSymbol);

                foreach (var attribute in fieldSymbol.GetAttributes())
                {
                    if (attribute.AttributeClass is not { } attrClass)
                        continue;

                    if (GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(attrClass, ConstructorIgnoreAttributeName))
                    {
                        occurrences.Add(new GuardOccurrence(fieldSymbol, attribute, GuardOccurrenceKind.Ignore, ineligibilityReason));
                        continue;
                    }

                    if (GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(attrClass, ConstructorGuardAttributeName))
                    {
                        occurrences.Add(BuildConstructorGuardOccurrence(fieldSymbol, attribute, ineligibilityReason));
                        continue;
                    }

                    if (TryGetGuardDefinition(attrClass, out var guardType, out var methodName, out var methodNameExplicit))
                    {
                        var isInSourceAlias = attrClass.DeclaringSyntaxReferences.Length > 0;
                        occurrences.Add(new GuardOccurrence(
                            fieldSymbol, attribute, GuardOccurrenceKind.Alias, ineligibilityReason,
                            guardType, methodName, methodNameExplicit, isInSourceAlias));
                    }
                }
            }
        }

        return occurrences;
    }

    private static GuardOccurrence BuildConstructorGuardOccurrence(IFieldSymbol fieldSymbol, AttributeData attribute, string? ineligibilityReason)
    {
        if (attribute.ConstructorArguments.Length == 0)
            return new GuardOccurrence(fieldSymbol, attribute, GuardOccurrenceKind.BuiltInNone, ineligibilityReason);

        var first = attribute.ConstructorArguments[0];

        if (first.Kind == TypedConstantKind.Enum && first.Value is int enumValue)
        {
            var kind = enumValue == 0 ? GuardOccurrenceKind.BuiltInNone : GuardOccurrenceKind.BuiltInPositive;
            return new GuardOccurrence(fieldSymbol, attribute, kind, ineligibilityReason);
        }

        if (first.Value is ITypeSymbol guardType)
        {
            var methodNameExplicit = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is string;
            var methodName = methodNameExplicit ? (string)attribute.ConstructorArguments[1].Value! : DefaultGuardMethodName;
            return new GuardOccurrence(fieldSymbol, attribute, GuardOccurrenceKind.CustomType, ineligibilityReason, guardType, methodName, methodNameExplicit);
        }

        return new GuardOccurrence(fieldSymbol, attribute, GuardOccurrenceKind.BuiltInNone, ineligibilityReason);
    }

    private static bool TryGetGuardDefinition(INamedTypeSymbol attrClass, out ITypeSymbol? guardType, out string? methodName, out bool methodNameExplicit)
    {
        foreach (var metaAttribute in attrClass.GetAttributes())
        {
            if (metaAttribute.AttributeClass is not { } metaClass ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(metaClass, ConstructorGuardDefinitionAttributeName))
            {
                continue;
            }

            if (metaAttribute.ConstructorArguments.Length == 0 || metaAttribute.ConstructorArguments[0].Value is not ITypeSymbol resolvedGuardType)
                continue;

            guardType = resolvedGuardType;
            methodNameExplicit = metaAttribute.ConstructorArguments.Length > 1 && metaAttribute.ConstructorArguments[1].Value is string;
            methodName = methodNameExplicit ? (string)metaAttribute.ConstructorArguments[1].Value! : DefaultGuardMethodName;
            return true;
        }

        guardType = null;
        methodName = null;
        methodNameExplicit = false;
        return false;
    }

    private static void ReportFieldOccurrenceDiagnostics(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        List<GuardOccurrence> occurrences,
        bool classHasTrigger)
    {
        foreach (var occurrence in occurrences)
        {
            var location = GetAttributeLocation(context, occurrence.Attribute);

            if (occurrence.IneligibilityReason is { } reason)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardAttributeOnIneligibleField,
                    location,
                    occurrence.Field.Name,
                    reason));
                continue;
            }

            switch (occurrence.Kind)
            {
                case GuardOccurrenceKind.Ignore when !classHasTrigger:
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorGuardAttributeHasNoEffect, location, occurrence.Field.Name, "[ConstructorIgnore]"));
                    break;

                case GuardOccurrenceKind.BuiltInNone when !classHasTrigger:
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorGuardAttributeHasNoEffect, location, occurrence.Field.Name, "[ConstructorGuard(ConstructorGuardKind.None)]"));
                    break;

                case GuardOccurrenceKind.BuiltInPositive:
                    AnalyzeBuiltInGuard(context, occurrence, location);
                    break;

                case GuardOccurrenceKind.CustomType:
                    // A direct [ConstructorGuard(typeof(...))] usage's own constructor
                    // arguments are the guard type and an optional method name -- never
                    // values to forward -- so it always resolves against zero forwarded
                    // arguments.
                    AnalyzeCustomGuard(context, typeSymbol, occurrence, location, occurrence.GuardType, occurrence.MethodName, occurrence.MethodNameExplicit, ImmutableArray<ITypeSymbol>.Empty);
                    break;

                case GuardOccurrenceKind.Alias:
                    AnalyzeAliasGuardAtUsage(context, typeSymbol, occurrence, location);
                    break;
            }
        }
    }

    /// <summary>
    /// Analyzes a field-level alias attribute occurrence (one meta-annotated with
    /// <c>[ConstructorGuardDefinition]</c>). Every positional constructor argument of
    /// this alias usage (e.g. the <c>3</c> in <c>[MinCount(3)]</c>) is a candidate to
    /// forward onto the resolved guard method call, so it is always validated here
    /// first (NDLRGEN055) regardless of where the alias itself is declared. An alias
    /// declared in the current compilation's source has its guard type, method name,
    /// and general method shape validated once at its own declaration
    /// (NDLRGEN053/054), so this only reports the field-type or forwarded-argument
    /// incompatibility, or ambiguity, that can only be known at a usage site. An alias
    /// declared in a referenced assembly cannot be diagnosed at a declaration this
    /// compilation controls, so it receives the full field-usage validation applied to
    /// a direct custom guard.
    /// </summary>
    private static void AnalyzeAliasGuardAtUsage(SyntaxNodeAnalysisContext context, INamedTypeSymbol containingType, GuardOccurrence occurrence, Location location)
    {
        var guardType = occurrence.GuardType;

        if (!TryGetForwardedArgumentTypes(occurrence.Attribute, out var forwardedArgumentTypes, out var unsupportedReason))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardAliasUsageArgumentUnsupported, location, occurrence.Field.Name, unsupportedReason));
            return;
        }

        if (!occurrence.GuardTypeUsageIsInSourceAlias)
        {
            AnalyzeCustomGuard(context, containingType, occurrence, location, guardType, occurrence.MethodName, occurrence.MethodNameExplicit, forwardedArgumentTypes);
            return;
        }

        if (guardType is null || guardType.TypeKind == TypeKind.Error)
            return;

        var methodName = string.IsNullOrEmpty(occurrence.MethodName) ? DefaultGuardMethodName : occurrence.MethodName!;
        var resolution = TryResolveGuardMethod(
            context.Compilation, containingType, guardType, methodName, occurrence.Field.Type, forwardedArgumentTypes, out _, out var reason, out var failureKind);

        if (resolution == GuardMethodResolution.NotFound && failureKind == GuardResolutionFailureKind.ForwardedArgument)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible,
                location,
                methodName,
                guardType.ToDisplayString(),
                occurrence.Field.Name,
                occurrence.Field.Type.ToDisplayString(),
                reason));
        }
        else if (resolution == GuardMethodResolution.NotFound && reason == FieldTypeIncompatibleReason)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardMethodInvalid,
                location,
                methodName,
                guardType.ToDisplayString(),
                occurrence.Field.Name,
                occurrence.Field.Type.ToDisplayString(),
                reason));
        }
        else if (resolution == GuardMethodResolution.Ambiguous)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
                location,
                methodName,
                guardType.ToDisplayString(),
                occurrence.Field.Name,
                occurrence.Field.Type.ToDisplayString()));
        }
    }

    /// <summary>
    /// Validates every positional constructor argument of an alias attribute usage
    /// (e.g. <c>[MinCount(3)]</c>) and returns the compile-time type of each one, in
    /// declared order, to use for matching against the resolved guard method's middle
    /// parameters. Reports the same unsupported-shape rule
    /// <see cref="TypedConstantRenderer"/> enforces for code generation -- arrays and
    /// floating-point values are not forwarded -- and additionally rejects any named
    /// attribute argument or property, which this version never forwards regardless of
    /// its shape.
    /// </summary>
    private static bool TryGetForwardedArgumentTypes(AttributeData attribute, out ImmutableArray<ITypeSymbol> forwardedArgumentTypes, out string? unsupportedReason)
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

        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(attribute.ConstructorArguments.Length);
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

    // ------------------------------------------------------------------
    // Built-in guard compatibility: NDLRGEN047-048
    // ------------------------------------------------------------------

    private static void AnalyzeBuiltInGuard(SyntaxNodeAnalysisContext context, GuardOccurrence occurrence, Location location)
    {
        var first = occurrence.Attribute.ConstructorArguments[0];
        if (TryReportUndefinedEnum(context, location, first))
            return;

        if (first.Value is not int rawValue)
            return;

        var kind = (BuiltInGuardKindMirror)rawValue;
        var fieldType = occurrence.Field.Type;

        switch (kind)
        {
            case BuiltInGuardKindMirror.NotNull:
                if (!CanBeRuntimeNull(fieldType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
                        location,
                        "NotNull",
                        occurrence.Field.Name,
                        fieldType.ToDisplayString(),
                        "the field's type is a non-nullable value type, so a runtime null value is never possible"));
                }

                break;

            case BuiltInGuardKindMirror.NotNullOrEmpty:
            case BuiltInGuardKindMirror.NotNullOrWhiteSpace:
                if (fieldType.SpecialType != SpecialType.System_String)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorGuardIncompatibleWithFieldType,
                        location,
                        kind.ToString(),
                        occurrence.Field.Name,
                        fieldType.ToDisplayString(),
                        "this guard only applies to string-compatible fields"));
                }

                break;
        }
    }

    private enum BuiltInGuardKindMirror
    {
        None = 0,
        NotNull = 1,
        NotNullOrEmpty = 2,
        NotNullOrWhiteSpace = 3,
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

        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
    }

    // ------------------------------------------------------------------
    // Custom guard type/method resolution: NDLRGEN049-052, NDLRGEN056
    // ------------------------------------------------------------------

    private static void AnalyzeCustomGuard(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol containingType,
        GuardOccurrence occurrence,
        Location location,
        ITypeSymbol? guardType,
        string? methodName,
        bool methodNameExplicit,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes)
    {
        if (guardType is null || guardType.TypeKind == TypeKind.Error)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardTypeInvalid, location, occurrence.Field.Name, "the guard type could not be resolved"));
            return;
        }

        if (!context.Compilation.IsSymbolAccessibleWithin(guardType, containingType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardTypeInvalid,
                location,
                occurrence.Field.Name,
                $"'{guardType.ToDisplayString()}' is not accessible from '{containingType.ToDisplayString()}'"));
            return;
        }

        if (methodNameExplicit && string.IsNullOrWhiteSpace(methodName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConstructorGuardMethodNameInvalid, location, occurrence.Field.Name));
            return;
        }

        var effectiveMethodName = string.IsNullOrEmpty(methodName) ? DefaultGuardMethodName : methodName!;

        var resolution = TryResolveGuardMethod(
            context.Compilation, containingType, guardType, effectiveMethodName, occurrence.Field.Type, forwardedArgumentTypes, out _, out var reason, out var failureKind);

        switch (resolution)
        {
            case GuardMethodResolution.NotFound when failureKind == GuardResolutionFailureKind.ForwardedArgument:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardForwardedArgumentIncompatible,
                    location,
                    effectiveMethodName,
                    guardType.ToDisplayString(),
                    occurrence.Field.Name,
                    occurrence.Field.Type.ToDisplayString(),
                    reason));
                break;

            case GuardMethodResolution.NotFound:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardMethodInvalid,
                    location,
                    effectiveMethodName,
                    guardType.ToDisplayString(),
                    occurrence.Field.Name,
                    occurrence.Field.Type.ToDisplayString(),
                    reason ?? "no compatible method was found"));
                break;

            case GuardMethodResolution.Ambiguous:
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardMethodAmbiguous,
                    location,
                    effectiveMethodName,
                    guardType.ToDisplayString(),
                    occurrence.Field.Name,
                    occurrence.Field.Type.ToDisplayString()));
                break;
        }
    }

    private enum GuardMethodResolution
    {
        Found,
        NotFound,
        Ambiguous,
    }

    /// <summary>
    /// A classification of why a guard method candidate failed to resolve, used to pick
    /// between the value/general-shape diagnostic (NDLRGEN051) and the forwarded
    /// alias-argument diagnostic (NDLRGEN056) when reporting a <see cref="GuardMethodResolution.NotFound"/>
    /// result.
    /// </summary>
    private enum GuardResolutionFailureKind
    {
        /// <summary>No candidate was rejected, or resolution succeeded.</summary>
        None,

        /// <summary>
        /// The candidate was rejected for a reason unrelated to a specific forwarded
        /// argument: it doesn't exist, isn't accessible/static/void, uses ref/out/in, or
        /// its value parameter is incompatible with the field's type.
        /// </summary>
        General,

        /// <summary>
        /// The candidate was rejected specifically because of the arguments a
        /// parameterized alias usage forwards to it: its effective arity does not match
        /// the number of forwarded arguments, or a middle parameter's type is
        /// incompatible with its corresponding forwarded argument.
        /// </summary>
        ForwardedArgument,
    }

    /// <summary>
    /// Resolves the accessible static guard method named <paramref name="methodName"/> on
    /// <paramref name="guardType"/> that is compatible with <c>void Method(T value,
    /// ...forwarded arguments, string parameterName)</c>, where <c>T</c> is compatible
    /// with <paramref name="fieldType"/> (directly, or via a generic method's
    /// type-parameter unification), and each middle parameter is compatible with the
    /// correspondingly-positioned entry of <paramref name="forwardedArgumentTypes"/>.
    /// Pass <see langword="null"/> for <paramref name="fieldType"/> to validate only the
    /// method's durable general shape -- at least a value and a trailing string
    /// parameter name, in any accessible static void overload -- used to validate a
    /// <c>[ConstructorGuardDefinition]</c> at its own declaration, before any usage
    /// site's forwarded argument count is known.
    /// </summary>
    private static GuardMethodResolution TryResolveGuardMethod(
        Compilation compilation,
        INamedTypeSymbol withinType,
        ITypeSymbol guardType,
        string methodName,
        ITypeSymbol? fieldType,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        out IMethodSymbol? method,
        out string? reason,
        out GuardResolutionFailureKind failureKind)
    {
        method = null;
        reason = null;
        failureKind = GuardResolutionFailureKind.None;

        var candidates = guardType.GetMembers(methodName).OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .ToList();

        if (candidates.Count == 0)
        {
            reason = $"no method named '{methodName}' was found on '{guardType.ToDisplayString()}'";
            failureKind = GuardResolutionFailureKind.General;
            return GuardMethodResolution.NotFound;
        }

        // The effective arity (value + every forwarded argument + the trailing
        // parameter name) is only known once a usage site supplies its forwarded
        // argument count; at definition-site validation (fieldType is null) any
        // number of middle parameters is durable, since a usage site's own alias
        // constructor determines that count later.
        var expectedArity = fieldType is null ? (int?)null : forwardedArgumentTypes.Length + 2;
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

            if (expectedArity.HasValue && candidate.Parameters.Length != expectedArity.Value)
            {
                reason = $"it has {candidate.Parameters.Length - 2} parameter(s) between the value and the parameter name, but this alias usage forwards {forwardedArgumentTypes.Length} argument(s)";
                failureKind = GuardResolutionFailureKind.ForwardedArgument;
                continue;
            }

            if (candidate.Parameters[candidate.Parameters.Length - 1].Type.SpecialType != SpecialType.System_String)
            {
                reason = "its last parameter is not a string parameter name";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            var refKindParameter = candidate.Parameters.FirstOrDefault(p => p.RefKind != RefKind.None);
            if (refKindParameter is not null)
            {
                reason = $"its '{refKindParameter.Name}' parameter is passed by '{refKindParameter.RefKind.ToString().ToLowerInvariant()}', which a direct generated call cannot supply";
                failureKind = GuardResolutionFailureKind.General;
                continue;
            }

            if (fieldType is null)
            {
                // Definition-site validation: the value parameter's exact compatibility
                // and any middle parameters' forwarded-argument compatibility cannot be
                // checked without a concrete usage site, so only the method's general
                // shape is required here.
                matches.Add(candidate);
                continue;
            }

            var valueParameterType = candidate.Parameters[0].Type;
            var middleParameters = candidate.Parameters.Skip(1).Take(candidate.Parameters.Length - 2).ToList();

            bool isCompatible;
            string? candidateReason;
            GuardResolutionFailureKind candidateFailureKind;

            if (candidate.IsGenericMethod)
            {
                isCompatible = TryInferGenericParameterCompatibility(
                    candidate, valueParameterType, fieldType, middleParameters, forwardedArgumentTypes, compilation,
                    out candidateReason, out candidateFailureKind);
            }
            else
            {
                isCompatible = TryCheckNonGenericCompatibility(
                    fieldType, valueParameterType, middleParameters, forwardedArgumentTypes, compilation,
                    out candidateReason, out candidateFailureKind);
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

    private static bool IsAssignableTo(ITypeSymbol fieldType, ITypeSymbol parameterType, Compilation compilation)
    {
        if (SymbolEqualityComparer.Default.Equals(fieldType, parameterType))
            return true;

        var conversion = compilation.ClassifyConversion(fieldType, parameterType);
        return conversion.Exists && (conversion.IsIdentity || conversion.IsImplicit);
    }

    /// <summary>
    /// Checks a non-generic guard method candidate's value parameter against
    /// <paramref name="fieldType"/>, then each middle parameter against its
    /// correspondingly-positioned entry of <paramref name="forwardedArgumentTypes"/>,
    /// stopping at the first incompatibility found. The value-parameter check is
    /// classified as <see cref="GuardResolutionFailureKind.General"/> (NDLRGEN051); a
    /// middle-parameter check is classified as
    /// <see cref="GuardResolutionFailureKind.ForwardedArgument"/> (NDLRGEN056), since
    /// only a parameterized alias usage's own forwarded arguments can make a middle
    /// parameter incompatible.
    /// </summary>
    private static bool TryCheckNonGenericCompatibility(
        ITypeSymbol fieldType,
        ITypeSymbol valueParameterType,
        List<IParameterSymbol> middleParameters,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        Compilation compilation,
        out string? reason,
        out GuardResolutionFailureKind failureKind)
    {
        if (!IsAssignableTo(fieldType, valueParameterType, compilation))
        {
            reason = FieldTypeIncompatibleReason;
            failureKind = GuardResolutionFailureKind.General;
            return false;
        }

        for (var i = 0; i < middleParameters.Count; i++)
        {
            if (!IsAssignableTo(forwardedArgumentTypes[i], middleParameters[i].Type, compilation))
            {
                reason = $"its parameter '{middleParameters[i].Name}' of type '{middleParameters[i].Type.ToDisplayString()}' is not compatible with the forwarded argument of type '{forwardedArgumentTypes[i].ToDisplayString()}'";
                failureKind = GuardResolutionFailureKind.ForwardedArgument;
                return false;
            }
        }

        reason = null;
        failureKind = GuardResolutionFailureKind.None;
        return true;
    }

    /// <summary>
    /// True when <paramref name="genericMethod"/>'s value-parameter type
    /// (<paramref name="parameterType"/>) unifies with <paramref name="fieldType"/> (or
    /// one of its base types/interfaces), every one of <paramref name="middleParameters"/>
    /// unifies with its correspondingly-positioned entry of
    /// <paramref name="forwardedArgumentTypes"/> using that same substitution -- so a
    /// type parameter shared between the value and a forwarded argument must resolve to
    /// the same type argument at both positions -- such that EVERY one of the method's
    /// own type parameters receives a binding, and every bound type argument satisfies
    /// its type parameter's declared constraints (see <see cref="FindConstraintViolation"/>).
    /// A failure that occurs while unifying a middle parameter, or that is only
    /// reachable via a type parameter that never appears in the value parameter's type,
    /// is classified as <see cref="GuardResolutionFailureKind.ForwardedArgument"/>
    /// (NDLRGEN056); every other failure is <see cref="GuardResolutionFailureKind.General"/>
    /// (NDLRGEN051).
    /// </summary>
    private static bool TryInferGenericParameterCompatibility(
        IMethodSymbol genericMethod,
        ITypeSymbol parameterType,
        ITypeSymbol fieldType,
        List<IParameterSymbol> middleParameters,
        ImmutableArray<ITypeSymbol> forwardedArgumentTypes,
        Compilation compilation,
        out string? reason,
        out GuardResolutionFailureKind failureKind)
    {
        var methodTypeParameters = new HashSet<ITypeSymbol>(genericMethod.TypeParameters, SymbolEqualityComparer.Default);
        string? lastReason = null;
        var lastFailureKind = GuardResolutionFailureKind.General;

        foreach (var candidateType in GetTypeAndSupertypes(fieldType))
        {
            var substitution = new Dictionary<ITypeSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
            if (!TryUnify(parameterType, candidateType, methodTypeParameters, substitution))
                continue;

            var forwardedMismatch = false;
            for (var i = 0; i < middleParameters.Count; i++)
            {
                if (!TryUnify(middleParameters[i].Type, forwardedArgumentTypes[i], methodTypeParameters, substitution))
                {
                    lastReason ??= $"its parameter '{middleParameters[i].Name}' cannot accept the forwarded argument of type '{forwardedArgumentTypes[i].ToDisplayString()}'";
                    lastFailureKind = GuardResolutionFailureKind.ForwardedArgument;
                    forwardedMismatch = true;
                    break;
                }
            }

            if (forwardedMismatch)
                continue;

            var unboundParameter = genericMethod.TypeParameters.FirstOrDefault(tp => !substitution.ContainsKey(tp));
            if (unboundParameter is not null)
            {
                var onlyForwarded = IsReachableOnlyThroughForwardedParameters(unboundParameter, parameterType, middleParameters);
                lastReason ??= onlyForwarded
                    ? $"its type parameter '{unboundParameter.Name}' cannot be inferred from the field's type or the forwarded arguments"
                    : $"its type parameter '{unboundParameter.Name}' cannot be inferred from the field's type";
                lastFailureKind = onlyForwarded ? GuardResolutionFailureKind.ForwardedArgument : GuardResolutionFailureKind.General;
                continue;
            }

            var constraintViolation = FindConstraintViolation(genericMethod.TypeParameters, substitution, compilation, out var violatingTypeParameter);
            if (constraintViolation is not null)
            {
                var onlyForwarded = violatingTypeParameter is not null &&
                    IsReachableOnlyThroughForwardedParameters(violatingTypeParameter, parameterType, middleParameters);
                lastReason ??= constraintViolation;
                lastFailureKind = onlyForwarded ? GuardResolutionFailureKind.ForwardedArgument : GuardResolutionFailureKind.General;
                continue;
            }

            reason = null;
            failureKind = GuardResolutionFailureKind.None;
            return true;
        }

        reason = lastReason ?? FieldTypeIncompatibleReason;
        failureKind = lastReason is null ? GuardResolutionFailureKind.General : lastFailureKind;
        return false;
    }

    /// <summary>
    /// True when <paramref name="typeParameter"/> appears anywhere within
    /// <paramref name="type"/> -- as the type itself, as one of a named type's type
    /// arguments (recursively), or as an array's element type. Used to decide whether a
    /// generic guard method's unresolved or constraint-violating type parameter is
    /// reachable from the value parameter's own type (a general-shape/value concern,
    /// NDLRGEN051) or only from a forwarded alias argument's parameter (NDLRGEN056).
    /// </summary>
    private static bool ContainsTypeParameter(ITypeSymbol type, ITypeParameterSymbol typeParameter)
    {
        if (SymbolEqualityComparer.Default.Equals(type, typeParameter))
            return true;

        if (type is INamedTypeSymbol namedType)
            return namedType.TypeArguments.Any(typeArgument => ContainsTypeParameter(typeArgument, typeParameter));

        if (type is IArrayTypeSymbol arrayType)
            return ContainsTypeParameter(arrayType.ElementType, typeParameter);

        return false;
    }

    /// <summary>
    /// True when <paramref name="typeParameter"/> is reachable from at least one of
    /// <paramref name="middleParameters"/>' types but not from
    /// <paramref name="valueParameterType"/> -- meaning a resolution failure tied to
    /// this type parameter is specifically a forwarded-alias-argument concern
    /// (NDLRGEN056) rather than a general value/shape concern (NDLRGEN051). A type
    /// parameter reachable from neither (e.g. an extra type parameter on a guard method
    /// that forwards no arguments at all) is never classified as forwarded, since there
    /// is then no forwarded argument it could plausibly be blamed on.
    /// </summary>
    private static bool IsReachableOnlyThroughForwardedParameters(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol valueParameterType,
        List<IParameterSymbol> middleParameters)
    {
        if (ContainsTypeParameter(valueParameterType, typeParameter))
            return false;

        return middleParameters.Any(parameter => ContainsTypeParameter(parameter.Type, typeParameter));
    }

    /// <summary>
    /// Returns a description of the first type parameter in <paramref name="typeParameters"/>
    /// whose inferred type argument (looked up in <paramref name="substitution"/>) does not
    /// satisfy that type parameter's declared constraints, or <see langword="null"/> when
    /// every bound type argument is compatible. Reference/value/unmanaged/constructor
    /// constraints are checked directly against the inferred type's own shape; a named
    /// constraint type (an interface or base-class constraint) is checked via
    /// <c>Compilation.ClassifyConversion</c>, the same real conversion-classification
    /// API the compiler itself uses to decide whether an implicit conversion exists --
    /// rather than a separate hand-rolled compatibility rule. <paramref name="violatingTypeParameter"/>
    /// is set to the offending type parameter's own symbol, so a caller can determine
    /// whether the violation is reachable from the value parameter's type or only from a
    /// forwarded argument's parameter.
    /// </summary>
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

            if (typeParameter.HasReferenceTypeConstraint && !argumentType.IsReferenceType)
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires a reference type, but '{argumentType.ToDisplayString()}' is a value type";
            }

            if (typeParameter.HasValueTypeConstraint && !argumentType.IsValueType)
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires a non-nullable value type, but '{argumentType.ToDisplayString()}' is not";
            }

            if (typeParameter.HasUnmanagedTypeConstraint && !argumentType.IsUnmanagedType)
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires an unmanaged type, but '{argumentType.ToDisplayString()}' is not unmanaged";
            }

            if (typeParameter.HasConstructorConstraint &&
                argumentType is INamedTypeSymbol namedArgumentType &&
                namedArgumentType.TypeKind != TypeKind.Struct &&
                !GeneratedConstructorEligibility.HasAccessibleParameterlessConstructor(namedArgumentType))
            {
                violatingTypeParameter = typeParameter;
                return $"its type parameter '{typeParameter.Name}' requires a public parameterless constructor, which '{argumentType.ToDisplayString()}' does not have";
            }

            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(argumentType, constraintType))
                    continue;

                var conversion = compilation.ClassifyConversion(argumentType, constraintType);
                if (conversion.Exists && (conversion.IsIdentity || conversion.IsImplicit))
                    continue;

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
        while (current != null)
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
        if (parameterType is ITypeParameterSymbol typeParameter && methodTypeParameters.Contains(typeParameter))
        {
            if (substitution.TryGetValue(typeParameter, out var bound))
                return SymbolEqualityComparer.Default.Equals(bound, candidateType);

            substitution[typeParameter] = candidateType;
            return true;
        }

        if (parameterType is INamedTypeSymbol namedParameter && candidateType is INamedTypeSymbol namedCandidate)
        {
            if (!SymbolEqualityComparer.Default.Equals(namedParameter.OriginalDefinition, namedCandidate.OriginalDefinition))
                return false;

            if (namedParameter.TypeArguments.Length != namedCandidate.TypeArguments.Length)
                return false;

            for (var i = 0; i < namedParameter.TypeArguments.Length; i++)
            {
                if (!TryUnify(namedParameter.TypeArguments[i], namedCandidate.TypeArguments[i], methodTypeParameters, substitution))
                    return false;
            }

            return true;
        }

        if (parameterType is IArrayTypeSymbol arrayParameter && candidateType is IArrayTypeSymbol arrayCandidate)
        {
            return TryUnify(arrayParameter.ElementType, arrayCandidate.ElementType, methodTypeParameters, substitution);
        }

        return SymbolEqualityComparer.Default.Equals(parameterType, candidateType);
    }

    // ------------------------------------------------------------------
    // [ConstructorGuardDefinition] declaration validation: NDLRGEN053-054
    // ------------------------------------------------------------------

    private static void AnalyzeGuardDefinitionTarget(SyntaxNodeAnalysisContext context, INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attrClass ||
                !GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(attrClass, ConstructorGuardDefinitionAttributeName))
            {
                continue;
            }

            var location = GetAttributeLocation(context, attribute);
            var targetInvalidReason = GetGuardDefinitionTargetInvalidReason(typeSymbol);
            if (targetInvalidReason is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionTargetInvalid, location, typeSymbol.Name, targetInvalidReason));
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not ITypeSymbol guardType ||
                guardType.TypeKind == TypeKind.Error)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard, location, typeSymbol.Name, "the guard type could not be resolved"));
                continue;
            }

            if (!context.Compilation.IsSymbolAccessibleWithin(guardType, typeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
                    location,
                    typeSymbol.Name,
                    $"'{guardType.ToDisplayString()}' is not accessible from '{typeSymbol.ToDisplayString()}'"));
                continue;
            }

            var methodNameExplicit = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is string;
            var methodName = methodNameExplicit ? (string)attribute.ConstructorArguments[1].Value! : DefaultGuardMethodName;

            if (methodNameExplicit && string.IsNullOrWhiteSpace(methodName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard, location, typeSymbol.Name, "the guard method name must not be empty or consist only of white space"));
                continue;
            }

            var resolution = TryResolveGuardMethod(context.Compilation, typeSymbol, guardType, methodName, fieldType: null, ImmutableArray<ITypeSymbol>.Empty, out _, out var reason, out _);
            if (resolution == GuardMethodResolution.NotFound)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorGuardDefinitionUnresolvedGuard,
                    location,
                    typeSymbol.Name,
                    reason ?? "no compatible guard method was found"));
            }
        }
    }

    private static string? GetGuardDefinitionTargetInvalidReason(INamedTypeSymbol attrClass)
    {
        if (!InheritsFromSystemAttribute(attrClass))
            return "not derived from System.Attribute";

        var usageAttribute = attrClass.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute");

        if (usageAttribute is not null &&
            usageAttribute.ConstructorArguments.Length > 0 &&
            usageAttribute.ConstructorArguments[0].Value is int validOn &&
            (validOn & AttributeTargetsField) == 0)
        {
            return "not usable on fields ([AttributeUsage] does not include AttributeTargets.Field)";
        }

        return null;
    }

    private static bool InheritsFromSystemAttribute(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.ToDisplayString() == "System.Attribute")
                return true;

            current = current.BaseType;
        }

        return false;
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private static Location GetAttributeLocation(SyntaxNodeAnalysisContext context, AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? Location.None;
    }
}
