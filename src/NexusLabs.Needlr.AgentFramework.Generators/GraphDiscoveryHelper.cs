// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework.Generators;

internal static class GraphDiscoveryHelper
{
    public static ImmutableArray<GraphEdgeEntry> GetGraphEdgeEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !AgentDiscoveryHelper.IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<GraphEdgeEntry>.Empty;

        var sourceTypeName = AgentDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<GraphEdgeEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 2)
                continue;

            var graphName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(graphName))
                continue;

            var typeArg = attr.ConstructorArguments[1];
            if (typeArg.Kind != TypedConstantKind.Type || typeArg.Value is not INamedTypeSymbol targetTypeSymbol)
                continue;

            var targetTypeName = AgentDiscoveryHelper.GetFullyQualifiedName(targetTypeSymbol);

            string? condition = null;
            var isRequired = true;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "Condition" && named.Value.Value is string conditionValue)
                {
                    condition = conditionValue;
                }
                else if (named.Key == "IsRequired" && named.Value.Value is bool isReqValue)
                {
                    isRequired = isReqValue;
                }
            }

            entries.Add(new GraphEdgeEntry(
                sourceTypeName,
                typeSymbol.Name,
                graphName!,
                targetTypeName,
                condition,
                isRequired));
        }

        return entries.ToImmutable();
    }

    public static ImmutableArray<GraphEntryPointEntry> GetGraphEntryPointEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !AgentDiscoveryHelper.IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<GraphEntryPointEntry>.Empty;

        var agentTypeName = AgentDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<GraphEntryPointEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var graphName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(graphName))
                continue;

            var routingMode = 0;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "RoutingMode" && named.Value.Value is int routeVal)
                {
                    routingMode = routeVal;
                }
            }

            entries.Add(new GraphEntryPointEntry(
                agentTypeName,
                typeSymbol.Name,
                graphName!,
                routingMode));
        }

        return entries.ToImmutable();
    }

    public static ImmutableArray<GraphNodeEntry> GetGraphNodeEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !AgentDiscoveryHelper.IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<GraphNodeEntry>.Empty;

        var agentTypeName = AgentDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<GraphNodeEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var graphName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(graphName))
                continue;

            var joinMode = 0;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "JoinMode" && named.Value.Value is int joinVal)
                {
                    joinMode = joinVal;
                }
            }

            entries.Add(new GraphNodeEntry(
                agentTypeName,
                typeSymbol.Name,
                graphName!,
                joinMode));
        }

        return entries.ToImmutable();
    }

    public static ImmutableArray<GraphReducerEntry> GetGraphReducerEntries(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var typeSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (typeSymbol is null || !AgentDiscoveryHelper.IsAccessibleFromGeneratedCode(typeSymbol))
            return ImmutableArray<GraphReducerEntry>.Empty;

        var agentTypeName = AgentDiscoveryHelper.GetFullyQualifiedName(typeSymbol);
        var entries = ImmutableArray.CreateBuilder<GraphReducerEntry>();

        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length < 1)
                continue;

            var graphName = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(graphName))
                continue;

            string? reducerMethod = null;

            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == "ReducerMethod" && named.Value.Value is string methodVal)
                {
                    reducerMethod = methodVal;
                }
            }

            if (!string.IsNullOrWhiteSpace(reducerMethod))
            {
                entries.Add(new GraphReducerEntry(
                    agentTypeName,
                    typeSymbol.Name,
                    graphName!,
                    reducerMethod!));
            }
        }

        return entries.ToImmutable();
    }
}
