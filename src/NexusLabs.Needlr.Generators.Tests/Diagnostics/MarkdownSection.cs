using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A Markdown section delimited by an ATX heading.
/// </summary>
/// <param name="Level">The heading level, or zero for content preceding the first heading.</param>
/// <param name="Title">The heading text.</param>
/// <param name="Lines">The raw lines belonging to the section, excluding the heading itself.</param>
/// <param name="Tables">The pipe tables declared in the section.</param>
/// <param name="MermaidBlocks">The Mermaid blocks declared in the section.</param>
internal sealed record MarkdownSection(
    int Level,
    string Title,
    IReadOnlyList<string> Lines,
    IReadOnlyList<MarkdownTable> Tables,
    IReadOnlyList<MermaidBlock> MermaidBlocks);
