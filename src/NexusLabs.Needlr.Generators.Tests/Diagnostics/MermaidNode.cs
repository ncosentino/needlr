namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A node declaration inside a Mermaid graph block.
/// </summary>
/// <param name="Id">The Mermaid node identifier.</param>
/// <param name="Label">The quoted display label of the node.</param>
/// <param name="Shape">The raw shape delimiter that opens the node declaration (for example <c>[[</c>).</param>
internal sealed record MermaidNode(string Id, string Label, string Shape);
