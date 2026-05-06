// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

            string? returnJsonSchemaType = null;
            string? returnObjectSchemaJson = null;
            if (!isVoidLike)
            {
                var unwrappedReturnType = isTaskOfT
                    ? ((INamedTypeSymbol)returnType).TypeArguments[0]
                    : returnType;
                returnJsonSchemaType = GetJsonSchemaType(unwrappedReturnType, out _);
                if (returnJsonSchemaType == "object")
                {
                    returnObjectSchemaJson = BuildObjectSchemaJson(unwrappedReturnType);
                }
            }

            string? methodDesc = GetDescriptionFromAttributes(method.GetAttributes());

            var parameters = ImmutableArray.CreateBuilder<AgentFunctionParameterInfo>();
            foreach (var param in method.Parameters)
            {
                bool isCancellationToken = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";
                bool isNullable = param.NullableAnnotation == NullableAnnotation.Annotated ||
                    (param.Type is INamedTypeSymbol pnt && pnt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);
                bool hasDefault = param.HasExplicitDefaultValue;
                string? defaultLiteral = hasDefault
                    ? ConvertToCSharpLiteral(param.ExplicitDefaultValue, param.Type)
                    : null;
                bool isEnum = param.Type.TypeKind == TypeKind.Enum ||
                    (param.Type is INamedTypeSymbol enumNullable &&
                     enumNullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
                     enumNullable.TypeArguments.Length == 1 &&
                     enumNullable.TypeArguments[0].TypeKind == TypeKind.Enum);
                string? paramDesc = GetDescriptionFromAttributes(param.GetAttributes());
                string jsonSchemaType = GetJsonSchemaType(param.Type, out string? itemJsonSchemaType);
                string? jsonSchemaFormat = GetJsonSchemaFormat(param.Type);
                string typeFullName = GetFullyQualifiedName(param.Type);

                // Build object schema for complex array items (e.g., FaqEntry[] → properties of FaqEntry)
                string? itemObjectSchemaJson = null;
                IReadOnlyList<ObjectPropertyInfo>? itemObjectProperties = null;
                if (jsonSchemaType == "array" && itemJsonSchemaType == "object")
                {
                    ITypeSymbol? elementType = null;
                    if (param.Type is IArrayTypeSymbol arrSym)
                        elementType = arrSym.ElementType;
                    else if (param.Type is INamedTypeSymbol namedSym && namedSym.IsGenericType && namedSym.TypeArguments.Length == 1)
                        elementType = namedSym.TypeArguments[0];

                    if (elementType != null)
                    {
                        itemObjectSchemaJson = BuildObjectSchemaJson(elementType);
                        itemObjectProperties = BuildObjectPropertyInfos(elementType);
                    }
                }

                // Build object schema for top-level complex DTO parameters (e.g., MyDto dto).
                // Same machinery as array items but applied to the parameter type itself —
                // gives the LLM a proper {"type":"object","properties":{…},"required":[…]}
                // schema and lets the wrapper extract per-property via TryGetProperty + helper
                // calls instead of the broken as-cast that silently returns default(MyDto).
                string? objectSchemaJson = null;
                IReadOnlyList<ObjectPropertyInfo>? objectProperties = null;
                if (jsonSchemaType == "object")
                {
                    var dtoType = param.Type;
                    if (dtoType is INamedTypeSymbol dtoNullable && dtoNullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
                        dtoType = dtoNullable.TypeArguments[0];
                    objectSchemaJson = BuildObjectSchemaJson(dtoType);
                    objectProperties = BuildObjectPropertyInfos(dtoType);
                }

                parameters.Add(new AgentFunctionParameterInfo(
                    param.Name, typeFullName, jsonSchemaType, jsonSchemaFormat, itemJsonSchemaType,
                    itemObjectSchemaJson, itemObjectProperties,
                    objectSchemaJson, objectProperties,
                    isCancellationToken, isNullable, hasDefault, defaultLiteral, isEnum, paramDesc));
            }

            methodInfos.Add(new AgentFunctionMethodInfo(
                method.Name, isAsync, isVoidLike, returnValueTypeFQN,
                returnJsonSchemaType, returnObjectSchemaJson,
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

            var order = 0;
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "Order" && named.Value.Value is int orderValue)
                {
                    order = orderValue;
                    break;
                }
            }

            entries.Add(new GroupChatEntry(agentTypeName, groupName!, order));
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

    public static ImmutableArray<ProgressSinksEntry> GetProgressSinksEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<ProgressSinksEntry>.Empty;

        var agentTypeName = GetFullyQualifiedName(typeSymbol);

        foreach (var attr in context.Attributes)
        {
            // params Type[] is a single constructor arg of TypedConstantKind.Array
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var firstArg = attr.ConstructorArguments[0];
            if (firstArg.Kind != TypedConstantKind.Array)
                continue;

            var sinkFQNs = ImmutableArray.CreateBuilder<string>();
            foreach (var element in firstArg.Values)
            {
                if (element.Kind == TypedConstantKind.Type && element.Value is INamedTypeSymbol sinkType)
                {
                    sinkFQNs.Add(GetFullyQualifiedName(sinkType));
                }
            }

            if (sinkFQNs.Count > 0)
            {
                return ImmutableArray.Create(
                    new ProgressSinksEntry(agentTypeName, typeSymbol.Name, sinkFQNs.ToImmutable()));
            }
        }

        return ImmutableArray<ProgressSinksEntry>.Empty;
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
            case SpecialType.System_DateTime: return "string";
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            itemType = GetJsonSchemaType(arrayType.ElementType, out _);
            if (string.IsNullOrEmpty(itemType))
                itemType = "object";
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
                if (string.IsNullOrEmpty(itemType))
                    itemType = "object";
                return "array";
            }
        }

        // Stringified value types — no SpecialType assignment in Roslyn for these.
        // Identified by their full display name. The matching JSON Schema "format" comes
        // from GetJsonSchemaFormat.
        var displayName = type.ToDisplayString();
        if (displayName == "System.Guid" ||
            displayName == "System.DateTimeOffset" ||
            displayName == "System.TimeSpan")
            return "string";

        // Enum types map to string (LLMs pass enum values as strings)
        if (type.TypeKind == TypeKind.Enum)
            return "string";

        // Complex types (classes, records, structs) → "object"
        if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct)
            return "object";

        return "";
    }

    /// <summary>
    /// Returns the JSON Schema <c>format</c> hint for stringified value types: <c>"uuid"</c>
    /// for <see cref="System.Guid"/>, <c>"date-time"</c> for <see cref="System.DateTime"/> and
    /// <see cref="System.DateTimeOffset"/>, <c>"duration"</c> for <see cref="System.TimeSpan"/>.
    /// Returns <see langword="null"/> for any other type.
    /// </summary>
    public static string? GetJsonSchemaFormat(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nullable && nullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];

        if (type.SpecialType == SpecialType.System_DateTime)
            return "date-time";

        var displayName = type.ToDisplayString();
        return displayName switch
        {
            "System.Guid" => "uuid",
            "System.DateTimeOffset" => "date-time",
            "System.TimeSpan" => "duration",
            _ => null,
        };
    }

    /// <summary>
    /// Builds a JSON schema string for a complex object type's properties.
    /// Returns <see langword="null"/> if the type has no public properties or is not a complex type.
    /// </summary>
    public static string? BuildObjectSchemaJson(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nullable && nullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];

        // Get public instance properties (including inherited)
        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                        !p.IsStatic && !p.IsIndexer &&
                        p.GetMethod != null)
            .ToList();

        if (properties.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"object\",\"properties\":{");

        var required = new List<string>();
        var first = true;
        foreach (var prop in properties)
        {
            if (!first) sb.Append(",");
            first = false;

            // Use camelCase for JSON property names (standard convention)
            var propName = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
            var propSchemaType = GetJsonSchemaType(prop.Type, out _);
            if (string.IsNullOrEmpty(propSchemaType))
                propSchemaType = "string"; // fallback
            var propSchemaFormat = GetJsonSchemaFormat(prop.Type);

            // Check for [Description] attribute on the property
            string? propDesc = null;
            foreach (var attr in prop.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "DescriptionAttribute")
                {
                    propDesc = attr.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    break;
                }
            }

            sb.Append($"\"{propName}\":{{\"type\":\"{propSchemaType}\"");
            if (!string.IsNullOrEmpty(propSchemaFormat))
            {
                sb.Append($",\"format\":\"{propSchemaFormat}\"");
            }
            if (!string.IsNullOrEmpty(propDesc))
            {
                var escapedDesc = propDesc!.Replace("\\", "\\\\").Replace("\"", "\\\"");
                sb.Append($",\"description\":\"{escapedDesc}\"");
            }
            sb.Append("}");

            // Non-nullable value types and non-nullable reference types are required
            if (!prop.Type.IsValueType && prop.NullableAnnotation != NullableAnnotation.Annotated)
                required.Add(propName);
            else if (prop.Type.IsValueType && prop.NullableAnnotation != NullableAnnotation.Annotated
                     && prop.Type is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType != SpecialType.System_Nullable_T)
                required.Add(propName);
        }

        sb.Append("}");
        if (required.Count > 0)
        {
            sb.Append(",\"required\":[");
            sb.Append(string.Join(",", required.Select(r => "\"" + r + "\"")));
            sb.Append("]");
        }
        sb.Append("}");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a list of <see cref="ObjectPropertyInfo"/> for manual property extraction
    /// from JsonElement. Used in generated code for AOT-safe deserialization of
    /// complex array element types.
    /// </summary>
    public static IReadOnlyList<ObjectPropertyInfo>? BuildObjectPropertyInfos(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nullable && nullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];

        var properties = type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                        !p.IsStatic && !p.IsIndexer &&
                        p.GetMethod != null && p.SetMethod != null)
            .ToList();

        if (properties.Count == 0)
            return null;

        var result = new List<ObjectPropertyInfo>();
        foreach (var prop in properties)
        {
            var jsonName = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1);
            var schemaType = GetJsonSchemaType(prop.Type, out _);
            if (string.IsNullOrEmpty(schemaType))
                schemaType = "string";
            var schemaFormat = GetJsonSchemaFormat(prop.Type);
            var csharpTypeFullName = GetFullyQualifiedName(prop.Type);
            var isNullable = prop.NullableAnnotation == NullableAnnotation.Annotated ||
                (prop.Type is INamedTypeSymbol pnt && pnt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);
            var initDefaultLiteral = TryGetPropertyInitializerLiteral(prop);
            result.Add(new ObjectPropertyInfo(
                prop.Name, jsonName, csharpTypeFullName, schemaType, schemaFormat,
                isNullable, initDefaultLiteral));
        }

        return result;
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
            SymbolDisplayFormat.FullyQualifiedFormat
                .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
                .WithMiscellaneousOptions(
                    SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                        & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes));

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

    /// <summary>
    /// Converts a Roslyn-supplied parameter default value (boxed as <see cref="object"/>) into a
    /// C# literal expression suitable for direct emission into generated source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles the three cases that produce non-trivial output:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <paramref name="value"/> is <see langword="null"/> for a non-nullable value type
    /// (e.g., <c>Guid id = default</c>) — emits <c>default(typeFullName)</c> rather than
    /// <c>"null"</c> which would not compile against the variable's declared type.
    /// </description></item>
    /// <item><description>
    /// <paramref name="parameterType"/> is an <see langword="enum"/> — Roslyn surfaces the
    /// underlying primitive value (e.g., <c>2</c> for <c>Mode.Append</c>); this method
    /// resolves the matching enum field by constant value and emits the typed literal
    /// (<c>global::MyApp.Mode.Append</c>). When no field matches (flags combination), emits
    /// a typed cast (<c>(global::MyApp.Mode)2</c>) which is still C#-legal.
    /// </description></item>
    /// <item><description>
    /// Primitive types — emits the standard C# literal form (e.g., <c>5L</c> for long,
    /// <c>9.99m</c> for decimal, escaped string literals).
    /// </description></item>
    /// </list>
    /// <para>
    /// Falls back to <c>default(typeFullName)</c> for any value the helper cannot render
    /// unambiguously.
    /// </para>
    /// </remarks>
    public static string ConvertToCSharpLiteral(object? value, ITypeSymbol parameterType)
    {
        var typeFullName = GetFullyQualifiedName(parameterType);

        var underlyingType = parameterType is INamedTypeSymbol nullableNamed &&
            nullableNamed.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
            nullableNamed.TypeArguments.Length == 1
                ? nullableNamed.TypeArguments[0]
                : parameterType;

        if (value is null)
        {
            bool isNullableValueType = parameterType is INamedTypeSymbol nvt &&
                nvt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T;
            bool isAnnotatedReference = !parameterType.IsValueType &&
                parameterType.NullableAnnotation == NullableAnnotation.Annotated;

            if (parameterType.IsValueType && !isNullableValueType)
                return "default(" + typeFullName + ")";

            if (!parameterType.IsValueType && !isAnnotatedReference)
                return "default(" + typeFullName + ")";

            return "null";
        }

        if (underlyingType.TypeKind == TypeKind.Enum)
        {
            var underlyingTypeFullName = GetFullyQualifiedName(underlyingType);
            foreach (var member in underlyingType.GetMembers().OfType<IFieldSymbol>())
            {
                if (member.HasConstantValue && Equals(member.ConstantValue, value))
                    return underlyingTypeFullName + "." + member.Name;
            }

            var castValue = ConvertPrimitiveLiteral(value, underlyingTypeFullName);
            return "(" + underlyingTypeFullName + ")(" + castValue + ")";
        }

        return ConvertPrimitiveLiteral(value, typeFullName);
    }

    private static string ConvertPrimitiveLiteral(object value, string typeFullName)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            byte n => n.ToString(CultureInfo.InvariantCulture),
            sbyte n => n.ToString(CultureInfo.InvariantCulture),
            short n => n.ToString(CultureInfo.InvariantCulture),
            ushort n => n.ToString(CultureInfo.InvariantCulture),
            int n => n.ToString(CultureInfo.InvariantCulture),
            uint n => n.ToString(CultureInfo.InvariantCulture) + "U",
            long n => n.ToString(CultureInfo.InvariantCulture) + "L",
            ulong n => n.ToString(CultureInfo.InvariantCulture) + "UL",
            float n when !float.IsNaN(n) && !float.IsInfinity(n) => n.ToString("R", CultureInfo.InvariantCulture) + "f",
            double n when !double.IsNaN(n) && !double.IsInfinity(n) => n.ToString("R", CultureInfo.InvariantCulture) + "d",
            decimal n => n.ToString(CultureInfo.InvariantCulture) + "m",
            _ => "default(" + typeFullName + ")"
        };
    }

    /// <summary>
    /// Reads the property's source declaration to extract a simple-literal initializer (e.g.,
    /// <c>= "default"</c>, <c>= 5</c>, <c>= true</c>, <c>= null</c>) and returns it as a C#
    /// literal string suitable for direct emission. Returns <see langword="null"/> when the
    /// property has no initializer or the initializer is not a simple literal expression
    /// (e.g., a method call, an array creation, an interpolated string). Non-literal
    /// initializers are intentionally skipped — they are not safe to splice into generated
    /// code without semantic analysis.
    /// </summary>
    public static string? TryGetPropertyInitializerLiteral(IPropertySymbol prop)
    {
        foreach (var syntaxRef in prop.DeclaringSyntaxReferences)
        {
            var node = syntaxRef.GetSyntax();
            if (node is PropertyDeclarationSyntax propDecl &&
                propDecl.Initializer is { } init &&
                init.Value is LiteralExpressionSyntax literal)
            {
                return literal.ToString();
            }
        }
        return null;
    }
}
