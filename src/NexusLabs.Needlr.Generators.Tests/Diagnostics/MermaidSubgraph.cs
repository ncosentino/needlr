using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A subgraph declared inside a Mermaid graph block.
/// </summary>
/// <param name="Id">The subgraph identifier.</param>
/// <param name="Label">The optional quoted subgraph label.</param>
/// <param name="NodeIds">Identifiers of the nodes declared inside the subgraph.</param>
internal sealed record MermaidSubgraph(string Id, string? Label, IReadOnlyList<string> NodeIds);
