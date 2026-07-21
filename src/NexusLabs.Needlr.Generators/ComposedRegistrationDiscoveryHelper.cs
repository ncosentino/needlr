using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using NexusLabs.Needlr.Generators.Models;
using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Discovers <c>[RegisterClosedOverImplementationsOf]</c> markers and expands each into concrete closed
/// registrations — one per discovered concrete closed implementation of the designated open generic
/// interface — by closing the composition type over the same type argument(s) via Roslyn
/// <see cref="INamedTypeSymbol.Construct(ITypeSymbol[])"/> and resolving its constructor dependencies.
/// </summary>
internal static class ComposedRegistrationDiscoveryHelper
{
    private const string AttributeName = "RegisterClosedOverImplementationsOfAttribute";
    private const string AttributeNamespace = "NexusLabs.Needlr.Generators";
    private const string FromKeyedServicesAttributeFullName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";

    private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>
    /// A single discovered marker (before assembly/source metadata is attached by the caller).
    /// </summary>
    public readonly struct ComposedMarkerInfo
    {
        public ComposedMarkerInfo(
            INamedTypeSymbol compositionType,
            INamedTypeSymbol sourceOpenGenericInterface,
            INamedTypeSymbol? asServiceType,
            GeneratorLifetime lifetime)
        {
            CompositionType = compositionType;
            SourceOpenGenericInterface = sourceOpenGenericInterface;
            AsServiceType = asServiceType;
            Lifetime = lifetime;
        }

        public INamedTypeSymbol CompositionType { get; }
        public INamedTypeSymbol SourceOpenGenericInterface { get; }
        public INamedTypeSymbol? AsServiceType { get; }
        public GeneratorLifetime Lifetime { get; }
    }

    /// <summary>
    /// Reads all valid <c>[RegisterClosedOverImplementationsOf]</c> attributes from a type.
    /// Invalid shapes (non-open-generic source interface, missing facade) are skipped here and surfaced
    /// to the user by the companion analyzer.
    /// </summary>
    public static IReadOnlyList<ComposedMarkerInfo> GetComposedMarkers(INamedTypeSymbol typeSymbol)
    {
        var result = new List<ComposedMarkerInfo>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
                continue;

            if (attrClass.Name != AttributeName)
                continue;

            if (attrClass.ContainingNamespace?.ToDisplayString() != AttributeNamespace)
                continue;

            if (attribute.ConstructorArguments.Length < 1)
                continue;

            if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol sourceInterface)
                continue;

            // Only open generic interfaces drive discovery; the analyzer reports other shapes.
            if (!sourceInterface.IsUnboundGenericType || sourceInterface.TypeKind != TypeKind.Interface)
                continue;

            INamedTypeSymbol? asServiceType = null;
            var lifetime = GeneratorLifetime.Singleton;

            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "As" && namedArg.Value.Value is INamedTypeSymbol asSymbol)
                {
                    asServiceType = asSymbol;
                }
                else if (namedArg.Key == "Lifetime" && namedArg.Value.Value is int lifetimeValue)
                {
                    lifetime = (GeneratorLifetime)lifetimeValue;
                }
            }

            result.Add(new ComposedMarkerInfo(typeSymbol, sourceInterface, asServiceType, lifetime));
        }

        return result;
    }

    /// <summary>
    /// Expands each marker into closed registrations using the discovered candidate implementation types,
    /// appending resolvable registrations to <paramref name="registrations"/> and constraint violations
    /// to <paramref name="violations"/>.
    /// </summary>
    public static void Expand(
        IReadOnlyList<DiscoveredComposedMarker> markers,
        IReadOnlyList<INamedTypeSymbol> candidateTypes,
        List<DiscoveredComposedRegistration> registrations,
        List<ComposedConstraintViolation> violations)
    {
        foreach (var marker in markers)
        {
            // The facade is required; absence is reported by the analyzer, skip emission here.
            if (marker.AsServiceType is null)
                continue;

            var facadeTypeName = marker.AsServiceType.ToDisplayString(FullyQualified);

            // Distinct closed implementations of the source interface, ordered for deterministic output.
            var closedInterfaces = FindClosedSourceInterfaces(marker.SourceOpenGenericInterface, candidateTypes)
                .OrderBy(i => i.ToDisplayString(FullyQualified), System.StringComparer.Ordinal)
                .ToList();

            foreach (var closedInterface in closedInterfaces)
            {
                var typeArguments = closedInterface.TypeArguments;

                // Only fully closed implementations (concrete type arguments) participate.
                if (typeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter))
                    continue;

                // Arity between the source interface and the composition must align to close the composition.
                if (typeArguments.Length != marker.CompositionType.TypeParameters.Length)
                    continue;

                if (!SatisfiesConstraints(marker.CompositionType, typeArguments))
                {
                    violations.Add(new ComposedConstraintViolation(
                        marker.CompositionType.ToDisplayString(FullyQualified),
                        string.Join(", ", typeArguments.Select(t => t.ToDisplayString(FullyQualified))),
                        marker.SourceOpenGenericInterface.ToDisplayString(FullyQualified),
                        marker.SourceFilePath));
                    continue;
                }

                var closedComposition = marker.CompositionType.Construct(typeArguments.ToArray());

                // A composition type using [GenerateConstructor]/field-triggered generation has
                // its effective constructor derived from the shared field model instead of
                // Roslyn's InstanceConstructors, which would otherwise only see the implicit
                // parameterless constructor visible before the sibling GeneratedConstructorGenerator
                // pass emits the real one within this compilation.
                if (GeneratedConstructorEligibility.IsEligibleForGeneratedConstructor(closedComposition))
                {
                    var generatedFields = GeneratedConstructorEligibility.GetEligibleConstructorFields(closedComposition);
                    var generatedArguments = generatedFields
                        .Select(f => BuildResolutionExpression(f.Type, serviceKey: null))
                        .ToList();

                    registrations.Add(new DiscoveredComposedRegistration(
                        facadeTypeName,
                        closedComposition.ToDisplayString(FullyQualified),
                        generatedArguments,
                        marker.Lifetime,
                        marker.AssemblyName,
                        marker.SourceFilePath));
                    continue;
                }

                var constructor = SelectConstructor(closedComposition);
                if (constructor is null)
                    continue;

                var arguments = constructor.Parameters
                    .Select(BuildResolutionExpression)
                    .ToList();

                registrations.Add(new DiscoveredComposedRegistration(
                    facadeTypeName,
                    closedComposition.ToDisplayString(FullyQualified),
                    arguments,
                    marker.Lifetime,
                    marker.AssemblyName,
                    marker.SourceFilePath));
            }
        }
    }

    private static List<INamedTypeSymbol> FindClosedSourceInterfaces(
        INamedTypeSymbol sourceOpenGenericInterface,
        IReadOnlyList<INamedTypeSymbol> candidateTypes)
    {
        var sourceDefinition = sourceOpenGenericInterface.OriginalDefinition;
        var result = new List<INamedTypeSymbol>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var candidate in candidateTypes)
        {
            if (candidate.IsAbstract || candidate.TypeKind != TypeKind.Class)
                continue;

            foreach (var iface in candidate.AllInterfaces)
            {
                if (!iface.IsGenericType)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, sourceDefinition))
                    continue;

                if (seen.Add(iface))
                    result.Add(iface);
            }
        }

        return result;
    }

    // A composition type is now handled above, before this method is reached, when it is
    // eligible for generated-constructor generation (see
    // GeneratedConstructorEligibility.IsEligibleForGeneratedConstructor in Expand). This
    // method remains the resolution path for a composition type with a hand-written
    // constructor.
    private static IMethodSymbol? SelectConstructor(INamedTypeSymbol closedComposition)
    {
        IMethodSymbol? best = null;

        foreach (var ctor in closedComposition.InstanceConstructors)
        {
            if (ctor.IsStatic)
                continue;

            if (ctor.DeclaredAccessibility != Accessibility.Public)
                continue;

            if (best is null || ctor.Parameters.Length > best.Parameters.Length)
                best = ctor;
        }

        return best;
    }

    private static string BuildResolutionExpression(IParameterSymbol parameter)
    {
        return BuildResolutionExpression(parameter.Type, GetFromKeyedServicesKey(parameter));
    }

    /// <summary>
    /// Builds a <c>sp.GetRequiredService&lt;T&gt;()</c> (or keyed-service) resolution
    /// expression for a type, independent of whether it came from a hand-written
    /// constructor's <see cref="IParameterSymbol"/> or a generated constructor's
    /// <see cref="IFieldSymbol"/>.
    /// </summary>
    private static string BuildResolutionExpression(ITypeSymbol type, string? serviceKey)
    {
        var typeName = type.ToDisplayString(FullyQualified);

        return serviceKey is null
            ? $"sp.GetRequiredService<{typeName}>()"
            : $"sp.GetRequiredKeyedService<{typeName}>(\"{GeneratorHelpers.EscapeStringLiteral(serviceKey)}\")";
    }

    private static string? GetFromKeyedServicesKey(IParameterSymbol parameter)
    {
        foreach (var attr in parameter.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != FromKeyedServicesAttributeFullName)
                continue;

            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string keyValue)
                return keyValue;
        }

        return null;
    }

    private static bool SatisfiesConstraints(
        INamedTypeSymbol composition,
        System.Collections.Immutable.ImmutableArray<ITypeSymbol> typeArguments)
    {
        var typeParameters = composition.TypeParameters;
        if (typeParameters.Length != typeArguments.Length)
            return false;

        for (var i = 0; i < typeParameters.Length; i++)
        {
            if (!SatisfiesConstraint(typeParameters[i], typeArguments[i]))
                return false;
        }

        return true;
    }

    private static bool SatisfiesConstraint(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol typeArgument)
    {
        if (typeParameter.HasReferenceTypeConstraint && !typeArgument.IsReferenceType)
            return false;

        if (typeParameter.HasValueTypeConstraint &&
            (!typeArgument.IsValueType || IsNullableValueType(typeArgument)))
            return false;

        if (typeParameter.HasNotNullConstraint && IsNullableValueType(typeArgument))
            return false;

        if (typeParameter.HasUnmanagedTypeConstraint && !typeArgument.IsUnmanagedType)
            return false;

        if (typeParameter.HasConstructorConstraint && !SatisfiesNewConstraint(typeArgument))
            return false;

        foreach (var constraintType in typeParameter.ConstraintTypes)
        {
            // Only non-generic constraint types are validated here: exact-match assignability is
            // variance-immune and reliable for them. Generic constraint types — whether self-referential
            // (where T : IComparable<T>), variant (where T : IProducer<Animal>), or invariant — are
            // deferred to the C# compiler so a variance/substitution subtlety can never skip a valid
            // registration. A bare type-parameter constraint (where T : U) is likewise deferred.
            if (constraintType is INamedTypeSymbol { IsGenericType: true } || ContainsTypeParameter(constraintType))
                continue;

            if (!IsAssignableTo(typeArgument, constraintType))
                return false;
        }

        return true;
    }

    private static bool IsAssignableTo(ITypeSymbol type, ITypeSymbol target)
    {
        if (SymbolEqualityComparer.Default.Equals(type, target))
            return true;

        if (target.TypeKind == TypeKind.Interface)
            return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, target));

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, target))
                return true;
        }

        return false;
    }

    private static bool SatisfiesNewConstraint(ITypeSymbol typeArgument)
    {
        if (typeArgument.IsValueType)
            return true;

        if (typeArgument is not INamedTypeSymbol named)
            return false;

        if (named.IsAbstract)
            return false;

        return named.InstanceConstructors.Any(c =>
            !c.IsStatic &&
            c.Parameters.Length == 0 &&
            c.DeclaredAccessibility == Accessibility.Public);
    }

    private static bool IsNullableValueType(ITypeSymbol typeArgument) =>
        typeArgument is INamedTypeSymbol named &&
        named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.TypeParameter)
            return true;

        return type is INamedTypeSymbol named &&
            named.TypeArguments.Any(ContainsTypeParameter);
    }
}
