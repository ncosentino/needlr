using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using NexusLabs.Needlr.AgentFramework.Generators.CodeGen;
using NexusLabs.Needlr.AgentFramework.Generators.Models;

namespace NexusLabs.Needlr.AgentFramework.Generators
{
    /// <summary>
    /// Source generator for [AsyncLocalScoped]-decorated interfaces.
    /// Emits an internal sealed class implementing the interface with proper
    /// AsyncLocal scoping and dispose semantics.
    /// </summary>
    [Generator]
    public class AsyncLocalScopedGenerator : IIncrementalGenerator
    {
        private const string AsyncLocalScopedAttributeName =
            "NexusLabs.Needlr.AgentFramework.AsyncLocalScopedAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var interfaces = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AsyncLocalScopedAttributeName,
                    predicate: static (s, _) => s is InterfaceDeclarationSyntax,
                    transform: static (ctx, ct) => ExtractInfo(ctx))
                .Where(static m => m.HasValue)
                .Select(static (m, _) => m!.Value);

            context.RegisterSourceOutput(interfaces, static (spc, info) =>
            {
                var source = AsyncLocalScopedCodeGenerator.Generate(info);
                var safeName = info.InterfaceFullName
                    .Replace("global::", "")
                    .Replace(".", "_")
                    .Replace("<", "_")
                    .Replace(">", "_");

                spc.AddSource(safeName + ".AsyncLocalScoped.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            });
        }

        private static AsyncLocalScopedInfo? ExtractInfo(GeneratorAttributeSyntaxContext ctx)
        {
            var typeSymbol = ctx.TargetSymbol as INamedTypeSymbol;
            if (typeSymbol == null || typeSymbol.TypeKind != TypeKind.Interface)
                return null;

            // Find the [AsyncLocalScoped] attribute and its Mutable property
            var attrData = ctx.Attributes.FirstOrDefault(a =>
                a.AttributeClass != null &&
                a.AttributeClass.ToDisplayString() == AsyncLocalScopedAttributeName);

            if (attrData == null)
                return null;

            bool isMutable = false;
            foreach (var named in attrData.NamedArguments)
            {
                if (named.Key == "Mutable" && named.Value.Value is bool b)
                    isMutable = b;
            }

            // Find the "Current" property to determine the value type
            var currentProp = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == "Current");

            if (currentProp == null)
                return null;

            // Get the non-nullable underlying type
            var valueType = currentProp.Type;
            if (valueType is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                valueType = namedType.TypeArguments[0];
            }
            else if (valueType.NullableAnnotation == NullableAnnotation.Annotated &&
                     valueType is INamedTypeSymbol annotatedType)
            {
                valueType = annotatedType;
            }

            var valueTypeFullName = valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Find the scope method (returns IDisposable)
            var scopeMethod = typeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.ReturnType != null &&
                    m.ReturnType.ToDisplayString().EndsWith("IDisposable"));

            if (scopeMethod == null)
                return null;

            bool hasScopeParameter = scopeMethod.Parameters.Length > 0;
            string scopeParameterTypeFullName = "";

            if (hasScopeParameter)
            {
                var paramType = scopeMethod.Parameters[0].Type;
                scopeParameterTypeFullName = paramType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            var interfaceFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var namespaceName = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true
                ? ""
                : typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";

            return new AsyncLocalScopedInfo(
                interfaceFullName: interfaceFullName,
                interfaceName: typeSymbol.Name,
                namespaceName: namespaceName,
                valueTypeFullName: valueTypeFullName,
                scopeMethodName: scopeMethod.Name,
                hasScopeParameter: hasScopeParameter,
                scopeParameterTypeFullName: scopeParameterTypeFullName,
                isMutable: isMutable);
        }
    }
}
