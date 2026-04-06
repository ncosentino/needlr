// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal static class AgentDiscoveryHelper
{
    private const string AgentFunctionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionAttribute";
    private const string AgentFunctionGroupAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionGroupAttribute";

    public static AgentFunctionTypeInfo? GetAgentFunctionTypeInfo(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel
            .GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

        if (typeSymbol is null)
            return null;

        return TryGetTypeInfo(typeSymbol);
    }

    public static AgentFunctionTypeInfo? TryGetTypeInfo(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeKind != TypeKind.Class)
            return null;

        if (!IsAccessibleFromGeneratedCode(typeSymbol))
            return null;

        if (!typeSymbol.IsStatic && typeSymbol.IsAbstract)
            return null;

        bool hasAgentFunction = false;
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (method.MethodKind != MethodKind.Ordinary)
                continue;

            if (method.DeclaredAccessibility != Accessibility.Public)
                continue;

            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName)
                {
                    hasAgentFunction = true;
                    break;
                }
            }

            if (hasAgentFunction)
                break;
        }

        if (!hasAgentFunction)
            return null;

        var methodInfos = ImmutableArray.CreateBuilder<AgentFunctionMethodInfo>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (method.MethodKind != MethodKind.Ordinary)
                continue;

            if (method.DeclaredAccessibility != Accessibility.Public)
                continue;

            bool isAgentFunction = false;
            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName)
                {
                    isAgentFunction = true;
                    break;
                }
            }

            if (!isAgentFunction)
                continue;

            var returnType = method.ReturnType;
            bool isVoid = returnType.SpecialType == SpecialType.System_Void;
            bool isTask = returnType is INamedTypeSymbol nt &&
                nt.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
                (nt.MetadataName == "Task" || nt.MetadataName == "ValueTask");
            bool isTaskOfT = returnType is INamedTypeSymbol nt2 &&
                nt2.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
                (nt2.MetadataName == "Task`1" || nt2.MetadataName == "ValueTask`1") &&
                nt2.TypeArguments.Length == 1;
            bool isAsync = isTask || isTaskOfT;
            bool isVoidLike = isVoid || isTask;
            string? returnValueTypeFQN = isVoidLike ? null : isTaskOfT
                ? GetFullyQualifiedName(((INamedTypeSymbol)returnType).TypeArguments[0])
                : GetFullyQualifiedName(returnType);

            string? methodDesc = GetDescriptionFromAttributes(method.GetAttributes());

            var parameters = ImmutableArray.CreateBuilder<AgentFunctionParameterInfo>();
            foreach (var param in method.Parameters)
            {
                bool isCancellationToken = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";
                bool isNullable = param.NullableAnnotation == NullableAnnotation.Annotated ||
                    (param.Type is INamedTypeSymbol pnt && pnt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);
                bool hasDefault = param.HasExplicitDefaultValue;
                string? paramDesc = GetDescriptionFromAttributes(param.GetAttributes());
                string jsonSchemaType = GetJsonSchemaType(param.Type, out string? itemJsonSchemaType);
                string typeFullName = GetFullyQualifiedName(param.Type);

                parameters.Add(new AgentFunctionParameterInfo(
                    param.Name, typeFullName, jsonSchemaType, itemJsonSchemaType,
                    isCancellationToken, isNullable, hasDefault, paramDesc));
            }

            methodInfos.Add(new AgentFunctionMethodInfo(
                method.Name, isAsync, isVoidLike, returnValueTypeFQN,
                parameters.ToImmutable(), methodDesc ?? ""));
        }

        return new AgentFunctionTypeInfo(
            GetFullyQualifiedName(typeSymbol),
            typeSymbol.ContainingAssembly?.Name ?? "Unknown",
            typeSymbol.IsStatic,
            methodInfos.ToImmutable());
    }

    public static ImmutableArray<AgentFunctionGroupEntry> GetAgentFunctionGroupEntries(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel
            .GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;

        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<AgentFunctionGroupEntry>.Empty;

        var entries = ImmutableArray.CreateBuilder<AgentFunctionGroupEntry>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != AgentFunctionGroupAttributeName)
                continue;

            if (attr.ConstructorArguments.Length != 1)
                continue;

            var groupName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(groupName))
                continue;

            entries.Add(new AgentFunctionGroupEntry(GetFullyQualifiedName(typeSymbol), groupName!));
        }

        return entries.ToImmutable();
    }

    public static NeedlrAiAgentTypeInfo? GetNeedlrAiAgentTypeInfo(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return null;

        var classDeclaration = context.TargetNode as ClassDeclarationSyntax;
        var isPartial = classDeclaration?.Modifiers.Any(m => m.ValueText == "partial") ?? false;

        var namespaceName = typeSymbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : typeSymbol.ContainingNamespace?.ToDisplayString();

        var functionGroupNames = ImmutableArray<string>.Empty;
        var explicitFunctionTypeFQNs = ImmutableArray<string>.Empty;
        var hasExplicitFunctionTypes = false;

        var agentAttr = context.Attributes.FirstOrDefault();
        if (agentAttr is not null)
        {
            var groupsArg = agentAttr.NamedArguments.FirstOrDefault(a => a.Key == "FunctionGroups");
            if (groupsArg.Key is not null && groupsArg.Value.Kind == TypedConstantKind.Array)
            {
                functionGroupNames = groupsArg.Value.Values
                    .Select(v => v.Value as string)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToImmutableArray();
            }

            var typesArg = agentAttr.NamedArguments.FirstOrDefault(a => a.Key == "FunctionTypes");
            if (typesArg.Key is not null && typesArg.Value.Kind == TypedConstantKind.Array)
            {
                hasExplicitFunctionTypes = true;
                explicitFunctionTypeFQNs = typesArg.Value.Values
                    .Where(v => v.Kind == TypedConstantKind.Type && v.Value is INamedTypeSymbol)
                    .Select(v => GetFullyQualifiedName((INamedTypeSymbol)v.Value!))
                    .ToImmutableArray();
            }
        }

        return new NeedlrAiAgentTypeInfo(
            GetFullyQualifiedName(typeSymbol),
            typeSymbol.Name,
            namespaceName,
            isPartial,
            functionGroupNames,
            explicitFunctionTypeFQNs,
            hasExplicitFunctionTypes);
    }

    public static ImmutableArray<HandoffEntry> GetHandoffEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<HandoffEntry>.Empty;

        var initialTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<HandoffEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol targetTypeSymbol)
                continue;

            var targetTypeName = GetFullyQualifiedName(targetTypeSymbol);
            var reason = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;

            entries.Add(new HandoffEntry(initialTypeName, typeSymbol.Name, targetTypeName, reason));
        }

        return entries.ToImmutable();
    }

    public static ImmutableArray<GroupChatEntry> GetGroupChatEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<GroupChatEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<GroupChatEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var groupName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(groupName))
                continue;

            entries.Add(new GroupChatEntry(agentTypeName, groupName!));
        }

        return entries.ToImmutable();
    }

    public static ImmutableArray<SequenceEntry> GetSequenceEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<SequenceEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<SequenceEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 2)
                continue;

            var pipelineName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(pipelineName))
                continue;

            if (attr.ConstructorArguments[1].Value is not int order)
                continue;

            entries.Add(new SequenceEntry(agentTypeName, pipelineName!, order));
        }

        return entries.ToImmutable();
    }

    public static ImmutableArray<TerminationConditionEntry> GetTerminationConditionEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<TerminationConditionEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<TerminationConditionEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var typeArg = attr.ConstructorArguments[0];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol condTypeSymbol)
                continue;

            var condTypeFQN = GetFullyQualifiedName(condTypeSymbol);
            var ctorArgLiterals = ImmutableArray<string>.Empty;

            if (attr.ConstructorArguments.Length > 1)
            {
                var paramsArg = attr.ConstructorArguments[1];
                if (paramsArg.Kind == TypedConstantKind.Array)
                {
                    ctorArgLiterals = paramsArg.Values
                        .Select(SerializeTypedConstant)
                        .Where(s => s is not null)
                        .Select(s => s!)
                        .ToImmutableArray();
                }
            }

            entries.Add(new TerminationConditionEntry(agentTypeName, condTypeFQN, ctorArgLiterals));
        }

        return entries.ToImmutable();
    }

    public static string? SerializeTypedConstant(TypedConstant constant)
    {
        return constant.Kind switch
        {
            TypedConstantKind.Primitive when constant.Value is string s =>
                "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
            TypedConstantKind.Primitive when constant.Value is int i => i.ToString(),
            TypedConstantKind.Primitive when constant.Value is long l => l.ToString() + "L",
            TypedConstantKind.Primitive when constant.Value is bool b => b ? "true" : "false",
            TypedConstantKind.Primitive when constant.Value is null => "null",
            TypedConstantKind.Type when constant.Value is ITypeSymbol ts =>
                $"typeof({GetFullyQualifiedName(ts)})",
            _ => null,
        };
    }

    public static string? GetDescriptionFromAttributes(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.ComponentModel.DescriptionAttribute" &&
                attr.ConstructorArguments.Length == 1 &&
                attr.ConstructorArguments[0].Value is string desc)
                return desc;
        }
        return null;
    }

    public static string GetJsonSchemaType(ITypeSymbol type, out string? itemType)
    {
        itemType = null;
        if (type is INamedTypeSymbol nullable && nullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];

        switch (type.SpecialType)
        {
            case SpecialType.System_String: return "string";
            case SpecialType.System_Boolean: return "boolean";
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64: return "integer";
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal: return "number";
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            itemType = GetJsonSchemaType(arrayType.ElementType, out _);
            return "array";
        }

        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1)
        {
            var baseName = named.ConstructedFrom.ToDisplayString();
            if (baseName == "System.Collections.Generic.IEnumerable<T>" ||
                baseName == "System.Collections.Generic.List<T>" ||
                baseName == "System.Collections.Generic.IReadOnlyList<T>" ||
                baseName == "System.Collections.Generic.ICollection<T>" ||
                baseName == "System.Collections.Generic.IList<T>")
            {
                itemType = GetJsonSchemaType(named.TypeArguments[0], out _);
                return "array";
            }
        }

        return "";
    }

    public static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.DeclaredAccessibility == Accessibility.Private ||
            typeSymbol.DeclaredAccessibility == Accessibility.Protected)
            return false;

        var current = typeSymbol.ContainingType;
        while (current != null)
        {
            if (current.DeclaredAccessibility == Accessibility.Private)
                return false;
            current = current.ContainingType;
        }

        return true;
    }

    public static string GetFullyQualifiedName(ITypeSymbol typeSymbol) =>
        "global::" + typeSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(
                SymbolDisplayGlobalNamespaceStyle.Omitted));

    public static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Generated";

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else if (c == '.' || c == '-' || c == ' ')
                sb.Append(c == '.' ? '.' : '_');
        }

        var result = sb.ToString();
        var segments = result.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsDigit(segments[i][0]))
                segments[i] = "_" + segments[i];
        }

        return string.Join(".", segments.Where(s => s.Length > 0));
    }

    public static string GetShortName(string fqn)
    {
        var stripped = fqn.StartsWith("global::", System.StringComparison.Ordinal) ? fqn.Substring(8) : fqn;
        var lastDot = stripped.LastIndexOf('.');
        return lastDot >= 0 ? stripped.Substring(lastDot + 1) : stripped;
    }

    public static string StripAgentSuffix(string className)
    {
        const string suffix = "Agent";
        return className.EndsWith(suffix, System.StringComparison.Ordinal) && className.Length > suffix.Length
            ? className.Substring(0, className.Length - suffix.Length)
            : className;
    }

    public static string GroupNameToPascalCase(string groupName)
    {
        var sb = new StringBuilder();
        var capitalizeNext = true;
        foreach (var c in groupName)
        {
            if (c == '-' || c == '_' || c == ' ')
            {
                capitalizeNext = true;
            }
            else if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
        }
        return sb.ToString();
    }
}
