using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A structurally parsed Mermaid graph block extracted from generated Markdown.
/// </summary>
/// <param name="Direction">The graph direction declaration (for example <c>graph TD</c>).</param>
/// <param name="Nodes">All node declarations in the block.</param>
/// <param name="Edges">All edges in the block.</param>
/// <param name="Subgraphs">All subgraphs in the block.</param>
/// <param name="UnrecognizedLines">Lines that could not be parsed as Mermaid constructs.</param>
internal sealed record MermaidBlock(
    string Direction,
    IReadOnlyList<MermaidNode> Nodes,
    IReadOnlyList<MermaidEdge> Edges,
    IReadOnlyList<MermaidSubgraph> Subgraphs,
    IReadOnlyList<string> UnrecognizedLines)
{
    private static readonly Regex DirectionPattern = new(@"^(?:graph|flowchart)\s+(?<direction>[A-Za-z]+)$");
    private static readonly Regex SubgraphPattern = new(@"^subgraph\s+(?<id>[^\[\s]+)(?:\[""(?<label>.*)""\])?$");
    private static readonly Regex EdgePattern = new(@"^(?<from>[A-Za-z0-9_]+)\s*(?<arrow>-\.->|-{2,3}>|-\.-)\s*(?:\|(?<label>[^|]*)\|\s*)?(?<to>[A-Za-z0-9_]+)$");
    private static readonly Regex NodePattern = new(@"^(?<id>[A-Za-z0-9_]+)(?<shape>[\[\(\{]+)""(?<label>.*)""[\]\)\}]+$");

    /// <summary>
    /// Parses the body lines of a Mermaid block.
    /// </summary>
    /// <param name="lines">The lines between the opening and closing code fences.</param>
    /// <returns>The parsed block.</returns>
    public static MermaidBlock Parse(IReadOnlyList<string> lines)
    {
        var direction = string.Empty;
        var nodes = new List<MermaidNode>();
        var edges = new List<MermaidEdge>();
        var subgraphs = new List<MermaidSubgraph>();
        var unrecognized = new List<string>();
        var openSubgraphs = new Stack<(string Id, string? Label, List<string> NodeIds)>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var directionMatch = DirectionPattern.Match(line);
            if (directionMatch.Success)
            {
                direction = line;
                continue;
            }

            var subgraphMatch = SubgraphPattern.Match(line);
            if (subgraphMatch.Success)
            {
                var label = subgraphMatch.Groups["label"].Success
                    ? subgraphMatch.Groups["label"].Value
                    : null;
                openSubgraphs.Push((subgraphMatch.Groups["id"].Value, label, new List<string>()));
                continue;
            }

            if (string.Equals(line, "end", StringComparison.Ordinal))
            {
                if (openSubgraphs.Count == 0)
                {
                    unrecognized.Add(line);
                    continue;
                }

                var (id, label, nodeIds) = openSubgraphs.Pop();
                subgraphs.Add(new MermaidSubgraph(id, label, nodeIds));
                continue;
            }

            var edgeMatch = EdgePattern.Match(line);
            if (edgeMatch.Success)
            {
                edges.Add(new MermaidEdge(
                    edgeMatch.Groups["from"].Value,
                    edgeMatch.Groups["to"].Value,
                    edgeMatch.Groups["arrow"].Value,
                    edgeMatch.Groups["label"].Success ? edgeMatch.Groups["label"].Value : null));
                continue;
            }

            var nodeMatch = NodePattern.Match(line);
            if (nodeMatch.Success)
            {
                var nodeId = nodeMatch.Groups["id"].Value;
                nodes.Add(new MermaidNode(nodeId, nodeMatch.Groups["label"].Value, nodeMatch.Groups["shape"].Value));
                if (openSubgraphs.Count > 0)
                {
                    openSubgraphs.Peek().NodeIds.Add(nodeId);
                }

                continue;
            }

            unrecognized.Add(line);
        }

        while (openSubgraphs.Count > 0)
        {
            var (id, label, nodeIds) = openSubgraphs.Pop();
            subgraphs.Add(new MermaidSubgraph(id, label, nodeIds));
            unrecognized.Add($"subgraph {id} was never closed");
        }

        return new MermaidBlock(direction, nodes, edges, subgraphs, unrecognized);
    }

    /// <summary>
    /// Determines whether the block contains an edge between two nodes.
    /// </summary>
    /// <param name="from">The source node identifier.</param>
    /// <param name="to">The target node identifier.</param>
    /// <returns><see langword="true"/> when the directed edge exists.</returns>
    public bool HasEdge(string from, string to)
    {
        foreach (var edge in Edges)
        {
            if (string.Equals(edge.From, from, StringComparison.Ordinal) &&
                string.Equals(edge.To, to, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the identifiers of all declared nodes.
    /// </summary>
    /// <returns>The set of declared node identifiers.</returns>
    public HashSet<string> NodeIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            ids.Add(node.Id);
        }

        return ids;
    }
}
