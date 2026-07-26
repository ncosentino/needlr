namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A directed edge inside a Mermaid graph block.
/// </summary>
/// <param name="From">The source node identifier.</param>
/// <param name="To">The target node identifier.</param>
/// <param name="Arrow">The raw arrow token (for example <c>--&gt;</c> or <c>-.-&gt;</c>).</param>
/// <param name="Label">The optional pipe-delimited edge label, or <see langword="null"/> when unlabeled.</param>
internal sealed record MermaidEdge(string From, string To, string Arrow, string? Label);
