using System;
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
/// Suggests Needlr-generated constructors only when an authored constructor is
/// mechanically equivalent to the generator's supported parameter, guard, and
/// field-assignment model.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenerateConstructorSuggestionAnalyzer : DiagnosticAnalyzer
{
    private const string ConstructorGuardAttributeName = "ConstructorGuardAttribute";
    private const string ConstructorGuardDefinitionAttributeName = "ConstructorGuardDefinitionAttribute";
    private const string ConstructorIgnoreAttributeName = "ConstructorIgnoreAttribute";
    private const string DeferToContainerAttributeName = "DeferToContainerAttribute";
    private const string RecordConstructorOverloadParameterAttributeName = "RecordConstructorOverloadParameterAttribute";

    /// <summary>
    /// Gets the diagnostics produced by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.GenerateConstructorSuggested);

    /// <summary>
    /// Registers symbol analysis for named types.
    /// </summary>
    /// <param name="context">The analyzer initialization context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeClassDeclaration,
            SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(
                classDeclaration,
                context.CancellationToken) is not
            INamedTypeSymbol typeSymbol)
        {
            return;
        }

        if (!IsSupportedType(typeSymbol) ||
            HasExistingConstructorGenerationConfiguration(typeSymbol) ||
            HasDeferToContainerAttribute(typeSymbol) ||
            RequiresParameterlessActivation(typeSymbol))
        {
            return;
        }

        if (typeSymbol.BaseType is not null &&
            typeSymbol.BaseType.SpecialType != SpecialType.System_Object &&
            !GeneratedConstructorEligibility.HasAccessibleParameterlessConstructor(typeSymbol.BaseType))
        {
            return;
        }

        var constructors = typeSymbol.InstanceConstructors
            .Where(constructor => !constructor.IsImplicitlyDeclared)
            .ToArray();
        if (constructors.Length != 1)
            return;

        var constructor = constructors[0];
        if (constructor.DeclaredAccessibility != Accessibility.Public ||
            constructor.GetAttributes().Length != 0 ||
            constructor.Parameters.Any(IsUnsupportedParameter))
        {
            return;
        }

        var constructorSyntax = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .SingleOrDefault(declaration =>
                SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetDeclaredSymbol(
                        declaration,
                        context.CancellationToken),
                    constructor));
        if (constructorSyntax is not null)
        {
            if (IsGeneratedSyntax(constructorSyntax) ||
                !MatchesOrdinaryConstructor(
                    context,
                    typeSymbol,
                    constructor,
                    constructorSyntax))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenerateConstructorSuggested,
                constructorSyntax.Identifier.GetLocation(),
                typeSymbol.Name));
            return;
        }

        if (classDeclaration.ParameterList is null ||
            typeSymbol.DeclaringSyntaxReferences.Length != 1 ||
            IsGeneratedSyntax(classDeclaration) ||
            !MatchesPrimaryConstructor(
                context,
                typeSymbol,
                constructor,
                classDeclaration))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.GenerateConstructorSuggested,
            classDeclaration.Identifier.GetLocation(),
            typeSymbol.Name));
    }

    private static bool IsSupportedType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.TypeKind == TypeKind.Class &&
            !typeSymbol.IsRecord &&
            !typeSymbol.IsFileLocal &&
            typeSymbol.ContainingType is null;
    }

    private static bool HasExistingConstructorGenerationConfiguration(
        INamedTypeSymbol typeSymbol)
    {
        if (GeneratedConstructorEligibility.HasGenerateConstructorAttribute(typeSymbol))
            return true;

        foreach (var member in typeSymbol.GetMembers())
        {
            foreach (var attribute in member.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                    continue;

                if (GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                        attributeClass,
                        ConstructorGuardAttributeName) ||
                    GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                        attributeClass,
                        ConstructorIgnoreAttributeName) ||
                    GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                        attributeClass,
                        RecordConstructorOverloadParameterAttributeName) ||
                    IsConstructorGuardAlias(attributeClass))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsConstructorGuardAlias(INamedTypeSymbol attributeClass)
    {
        return attributeClass.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } metaAttributeClass &&
            GeneratedConstructorEligibility.IsNeedlrGeneratorsAttribute(
                metaAttributeClass,
                ConstructorGuardDefinitionAttributeName));
    }

    private static bool HasDeferToContainerAttribute(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeClass &&
            attributeClass.Name == DeferToContainerAttributeName &&
            attributeClass.ContainingNamespace?.ToDisplayString() ==
                "NexusLabs.Needlr");
    }

    private static bool RequiresParameterlessActivation(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(interfaceSymbol =>
        {
            var namespaceName =
                interfaceSymbol.ContainingNamespace?.ToDisplayString();
            if (namespaceName == "NexusLabs.Needlr")
            {
                return interfaceSymbol.Name is
                    "IServiceCollectionPlugin" or
                    "IPostBuildServiceCollectionPlugin" or
                    "IWebApplicationBuilderPlugin" or
                    "IHostApplicationBuilderPlugin";
            }

            return namespaceName == "NexusLabs.Needlr.SignalR" &&
                interfaceSymbol.Name == "IHubRegistrationPlugin";
        });
    }

    private static bool IsUnsupportedParameter(IParameterSymbol parameter)
    {
        return parameter.RefKind != RefKind.None ||
            parameter.IsParams ||
            parameter.HasExplicitDefaultValue ||
            parameter.GetAttributes().Length != 0 ||
            ContainsPointerType(parameter.Type);
    }

    private static bool MatchesOrdinaryConstructor(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        IMethodSymbol constructor,
        ConstructorDeclarationSyntax constructorSyntax)
    {
        if (constructorSyntax.Initializer is not null ||
            constructorSyntax.Body is null ||
            constructorSyntax.ExpressionBody is not null)
        {
            return false;
        }

        var fields =
            GeneratedConstructorEligibility.GetEligibleConstructorFields(
                typeSymbol);
        if (fields.Count == 0 || fields.Count != constructor.Parameters.Length)
            return false;

        for (var i = 0; i < fields.Count; i++)
        {
            if (!MatchesParameter(fields[i], constructor.Parameters[i]))
                return false;
        }

        var semanticModel = context.SemanticModel;
        var statements = constructorSyntax.Body.Statements;
        var guards = new GeneratedConstructorGuardKind[fields.Count];
        var statementIndex = 0;

        for (var i = 0; i < fields.Count; i++)
        {
            if (statementIndex >= statements.Count ||
                !TryMatchGuardStatement(
                    semanticModel.Compilation,
                    semanticModel,
                    statements[statementIndex],
                    constructor.Parameters[i],
                    fields[i].Type,
                    out var guard))
            {
                continue;
            }

            guards[i] = guard;
            statementIndex++;
        }

        for (var i = 0; i < fields.Count; i++)
        {
            if (statementIndex >= statements.Count ||
                !MatchesAssignmentStatement(
                    semanticModel,
                    statements[statementIndex],
                    fields[i],
                    constructor.Parameters[i]))
            {
                return false;
            }

            statementIndex++;
        }

        return statementIndex == statements.Count &&
            GuardsCanBeGenerated(fields, guards);
    }

    private static bool MatchesPrimaryConstructor(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol typeSymbol,
        IMethodSymbol constructor,
        ClassDeclarationSyntax primaryDeclaration)
    {
        if (primaryDeclaration.ParameterList is null ||
            primaryDeclaration.BaseList?.Types
                .OfType<PrimaryConstructorBaseTypeSyntax>()
                .Any(baseType => baseType.ArgumentList.Arguments.Count > 0) ==
                    true)
        {
            return false;
        }

        var fields = GeneratedConstructorEligibility
            .GetOrderedInstanceFields(typeSymbol)
            .Where(field =>
                field.DeclaredAccessibility == Accessibility.Private &&
                field.IsReadOnly)
            .ToArray();
        if (fields.Length == 0 || fields.Length != constructor.Parameters.Length)
            return false;

        var initializers = new ExpressionSyntax[fields.Length];
        var guards = new GeneratedConstructorGuardKind[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            if (!MatchesParameter(fields[i], constructor.Parameters[i]) ||
                !TryGetFieldInitializer(
                    fields[i],
                    context.CancellationToken,
                    out var initializer) ||
                initializer.SyntaxTree != primaryDeclaration.SyntaxTree ||
                !TryMatchPrimaryInitializer(
                    context.SemanticModel.Compilation,
                    context.SemanticModel,
                    initializer,
                    constructor.Parameters[i],
                    fields[i].Type,
                    out guards[i]))
            {
                return false;
            }

            initializers[i] = initializer;
        }

        var guardedCount = guards.Count(
            guard => guard != GeneratedConstructorGuardKind.None);
        if (guardedCount > 0 && fields.Length > 1)
            return false;

        if (HasOtherInstanceInitializer(
                context,
                primaryDeclaration,
                fields) ||
            HasPrimaryParameterReferenceOutsideInitializers(
                context,
                primaryDeclaration,
                constructor.Parameters,
                initializers))
        {
            return false;
        }

        return GuardsCanBeGenerated(fields, guards);
    }

    private static bool MatchesParameter(
        IFieldSymbol field,
        IParameterSymbol parameter)
    {
        var parameterName =
            ConstructorGenerationDiscoveryHelper.GetParameterName(field.Name);
        if (parameterName.Length > 0 && parameterName[0] == '@')
            parameterName = parameterName.Substring(1);

        return parameter.Name == parameterName &&
            field.Type.ToDisplayString(
                ConstructorGenerationDiscoveryHelper.NullableAwareFormat) ==
            parameter.Type.ToDisplayString(
                ConstructorGenerationDiscoveryHelper.NullableAwareFormat);
    }

    private static bool TryMatchGuardStatement(
        Compilation compilation,
        SemanticModel semanticModel,
        StatementSyntax statement,
        IParameterSymbol parameter,
        ITypeSymbol fieldType,
        out GeneratedConstructorGuardKind guard)
    {
        guard = GeneratedConstructorGuardKind.None;
        if (statement is not ExpressionStatementSyntax
            {
                Expression: InvocationExpressionSyntax invocation,
            } ||
            invocation.ArgumentList.Arguments.Count != 1 ||
            !ReferencesSymbol(
                semanticModel,
                invocation.ArgumentList.Arguments[0].Expression,
                parameter))
        {
            return false;
        }

        if (semanticModel.GetSymbolInfo(invocation).Symbol is not
            IMethodSymbol { IsStatic: true } method)
        {
            return false;
        }

        var argumentNullException =
            compilation.GetTypeByMetadataName(
                "System.ArgumentNullException");
        var argumentException =
            compilation.GetTypeByMetadataName("System.ArgumentException");

        if (method.Name == "ThrowIfNull" &&
            SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                argumentNullException))
        {
            guard = GeneratedConstructorGuardKind.NotNull;
        }
        else if (method.Name == "ThrowIfNullOrEmpty" &&
            SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                argumentException))
        {
            guard = GeneratedConstructorGuardKind.NotNullOrEmpty;
        }
        else if (method.Name == "ThrowIfNullOrWhiteSpace" &&
            SymbolEqualityComparer.Default.Equals(
                method.ContainingType,
                argumentException))
        {
            guard = GeneratedConstructorGuardKind.NotNullOrWhiteSpace;
        }
        else
        {
            return false;
        }

        return IsGuardCompatible(fieldType, guard);
    }

    private static bool MatchesAssignmentStatement(
        SemanticModel semanticModel,
        StatementSyntax statement,
        IFieldSymbol field,
        IParameterSymbol parameter)
    {
        return statement is ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax assignment,
            } &&
            assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) &&
            ReferencesSymbol(semanticModel, assignment.Left, field) &&
            ReferencesSymbol(semanticModel, assignment.Right, parameter);
    }

    private static bool TryMatchPrimaryInitializer(
        Compilation compilation,
        SemanticModel semanticModel,
        ExpressionSyntax initializer,
        IParameterSymbol parameter,
        ITypeSymbol fieldType,
        out GeneratedConstructorGuardKind guard)
    {
        guard = GeneratedConstructorGuardKind.None;
        if (ReferencesSymbol(semanticModel, initializer, parameter))
            return true;

        if (initializer is not BinaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.CoalesceExpression,
                Right: ThrowExpressionSyntax
                {
                    Expression: ObjectCreationExpressionSyntax creation,
                },
            } coalesce ||
            !ReferencesSymbol(semanticModel, coalesce.Left, parameter) ||
            creation.Initializer is not null ||
            creation.ArgumentList?.Arguments.Count != 1)
        {
            return false;
        }

        var argumentNullException =
            compilation.GetTypeByMetadataName(
                "System.ArgumentNullException");
        if (!SymbolEqualityComparer.Default.Equals(
                semanticModel.GetTypeInfo(creation).Type,
                argumentNullException) ||
            creation.ArgumentList.Arguments[0].Expression is not
                InvocationExpressionSyntax
                {
                    Expression: IdentifierNameSyntax nameofIdentifier,
                    ArgumentList.Arguments.Count: 1,
                } nameofInvocation ||
            nameofIdentifier.Identifier.ValueText != "nameof" ||
            !ReferencesSymbol(
                semanticModel,
                nameofInvocation.ArgumentList.Arguments[0].Expression,
                parameter))
        {
            return false;
        }

        guard = GeneratedConstructorGuardKind.NotNull;
        return IsGuardCompatible(fieldType, guard);
    }

    private static bool HasOtherInstanceInitializer(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax declaration,
        IReadOnlyList<IFieldSymbol> captureFields)
    {
        var captures = new HashSet<IFieldSymbol>(
            captureFields,
            SymbolEqualityComparer.Default);

        foreach (var member in declaration.Members)
        {
            if (member is FieldDeclarationSyntax fieldDeclaration)
            {
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    if (variable.Initializer is null ||
                        context.SemanticModel.GetDeclaredSymbol(
                            variable,
                            context.CancellationToken) is not
                            IFieldSymbol field ||
                        field.IsStatic ||
                        captures.Contains(field))
                    {
                        continue;
                    }

                    return true;
                }
            }
            else if (member is PropertyDeclarationSyntax
                {
                    Initializer: not null,
                } propertyDeclaration &&
                context.SemanticModel.GetDeclaredSymbol(
                    propertyDeclaration,
                    context.CancellationToken) is
                    IPropertySymbol { IsStatic: false })
            {
                return true;
            }
            else if (member is EventFieldDeclarationSyntax
                eventFieldDeclaration)
            {
                foreach (var variable in
                    eventFieldDeclaration.Declaration.Variables)
                {
                    if (variable.Initializer is not null &&
                        context.SemanticModel.GetDeclaredSymbol(
                            variable,
                            context.CancellationToken) is
                            IEventSymbol { IsStatic: false })
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasPrimaryParameterReferenceOutsideInitializers(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax declaration,
        ImmutableArray<IParameterSymbol> parameters,
        IReadOnlyList<ExpressionSyntax> initializers)
    {
        foreach (var identifier in
            declaration.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (context.SemanticModel.GetSymbolInfo(
                    identifier,
                    context.CancellationToken).Symbol is not
                IParameterSymbol referencedParameter)
            {
                continue;
            }

            var parameterIndex = IndexOfParameter(
                parameters,
                referencedParameter);
            if (parameterIndex < 0)
                continue;

            var allowedInitializer = initializers[parameterIndex];
            if (!allowedInitializer.Span.Contains(identifier.Span))
                return true;
        }

        return false;
    }

    private static int IndexOfParameter(
        ImmutableArray<IParameterSymbol> parameters,
        IParameterSymbol candidate)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(parameters[i], candidate))
                return i;
        }

        return -1;
    }

    private static bool TryGetFieldInitializer(
        IFieldSymbol field,
        System.Threading.CancellationToken cancellationToken,
        out ExpressionSyntax initializer)
    {
        foreach (var syntaxReference in field.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax(cancellationToken) is
                VariableDeclaratorSyntax
                {
                    Initializer.Value: { } value,
                })
            {
                initializer = value;
                return true;
            }
        }

        initializer = null!;
        return false;
    }

    private static bool GuardsCanBeGenerated(
        IReadOnlyList<IFieldSymbol> fields,
        IReadOnlyList<GeneratedConstructorGuardKind> guards)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (!IsGuardCompatible(fields[i].Type, guards[i]))
                return false;
        }

        return true;
    }

    private static bool IsGuardCompatible(
        ITypeSymbol fieldType,
        GeneratedConstructorGuardKind guard)
    {
        return guard switch
        {
            GeneratedConstructorGuardKind.None => true,
            GeneratedConstructorGuardKind.NotNull =>
                ConstructorGuardAnalysisHelper.CanBeRuntimeNull(fieldType),
            GeneratedConstructorGuardKind.NotNullOrEmpty or
            GeneratedConstructorGuardKind.NotNullOrWhiteSpace =>
                fieldType.SpecialType == SpecialType.System_String,
            _ => false,
        };
    }

    private static bool ReferencesSymbol(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        ISymbol symbol)
    {
        return SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSymbolInfo(expression).Symbol,
            symbol);
    }

    private static bool ContainsPointerType(ITypeSymbol type)
    {
        return type switch
        {
            IPointerTypeSymbol => true,
            IFunctionPointerTypeSymbol => true,
            IArrayTypeSymbol arrayType =>
                ContainsPointerType(arrayType.ElementType),
            _ => false,
        };
    }

    private static bool IsGeneratedSyntax(SyntaxNode syntax)
    {
        var filePath = syntax.SyntaxTree.FilePath;
        if (filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(
                ".generated.cs",
                StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(
                ".designer.cs",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var leadingText = syntax.SyntaxTree
            .GetRoot()
            .GetLeadingTrivia()
            .ToFullString();
        return leadingText.IndexOf(
            "<auto-generated",
            StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
