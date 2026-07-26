using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A structurally parsed Markdown diagnostic artifact.
/// </summary>
/// <param name="Sections">The sections of the document in declaration order.</param>
internal sealed record MarkdownDocument(IReadOnlyList<MarkdownSection> Sections)
{
    /// <summary>
    /// Parses generated Markdown into sections, tables, and Mermaid blocks.
    /// </summary>
    /// <param name="content">The Markdown content.</param>
    /// <returns>The parsed document.</returns>
    public static MarkdownDocument Parse(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sections = new List<MarkdownSection>();
        var currentLevel = 0;
        var currentTitle = string.Empty;
        var currentLines = new List<string>();
        var insideFence = false;

        void CloseSection()
        {
            sections.Add(BuildSection(currentLevel, currentTitle, currentLines));
        }

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                currentLines.Add(line);
                continue;
            }

            if (!insideFence && line.StartsWith("#", StringComparison.Ordinal))
            {
                CloseSection();
                var level = line.TakeWhile(c => c == '#').Count();
                currentLevel = level;
                currentTitle = line.Substring(level).Trim();
                currentLines = new List<string>();
                continue;
            }

            currentLines.Add(line);
        }

        CloseSection();
        return new MarkdownDocument(sections);
    }

    /// <summary>
    /// Gets the section with the given heading text.
    /// </summary>
    /// <param name="title">The heading text.</param>
    /// <returns>The matching section.</returns>
    public MarkdownSection Section(string title)
    {
        return Sections.Single(s => string.Equals(s.Title, title, StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines whether a section with the given heading text exists.
    /// </summary>
    /// <param name="title">The heading text.</param>
    /// <returns><see langword="true"/> when the section exists.</returns>
    public bool HasSection(string title)
    {
        return Sections.Any(s => string.Equals(s.Title, title, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the single section whose heading starts with the given prefix.
    /// </summary>
    /// <param name="prefix">The heading prefix.</param>
    /// <returns>The matching section.</returns>
    public MarkdownSection SectionStartingWith(string prefix)
    {
        return Sections.Single(s => s.Title.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Determines whether a section whose heading starts with the given prefix exists.
    /// </summary>
    /// <param name="prefix">The heading prefix.</param>
    /// <returns><see langword="true"/> when such a section exists.</returns>
    public bool HasSectionStartingWith(string prefix)
    {
        return Sections.Any(s => s.Title.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the heading texts of all sections that have a heading.
    /// </summary>
    /// <returns>The section titles in document order.</returns>
    public IReadOnlyList<string> SectionTitles()
    {
        return Sections.Where(s => s.Level > 0).Select(s => s.Title).ToList();
    }

    /// <summary>
    /// Gets every Mermaid block in the document.
    /// </summary>
    /// <returns>The Mermaid blocks in document order.</returns>
    public IReadOnlyList<MermaidBlock> AllMermaidBlocks()
    {
        return Sections.SelectMany(s => s.MermaidBlocks).ToList();
    }

    private static MarkdownSection BuildSection(int level, string title, IReadOnlyList<string> lines)
    {
        var tables = new List<MarkdownTable>();
        var mermaidBlocks = new List<MermaidBlock>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("```mermaid", StringComparison.Ordinal))
            {
                var body = new List<string>();
                i++;
                while (i < lines.Count && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    body.Add(lines[i]);
                    i++;
                }

                mermaidBlocks.Add(MermaidBlock.Parse(body));
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                i++;
                while (i < lines.Count && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    i++;
                }

                continue;
            }

            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 >= lines.Count || !IsSeparatorRow(lines[i + 1]))
            {
                continue;
            }

            var columns = MarkdownTable.SplitCells(line);
            var rows = new List<IReadOnlyList<string>>();
            i += 2;
            while (i < lines.Count && lines[i].Trim().StartsWith("|", StringComparison.Ordinal))
            {
                rows.Add(MarkdownTable.SplitCells(lines[i]));
                i++;
            }

            i--;
            tables.Add(new MarkdownTable(columns, rows));
        }

        return new MarkdownSection(level, title, lines, tables, mermaidBlocks);
    }

    private static bool IsSeparatorRow(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("|", StringComparison.Ordinal))
        {
            return false;
        }

        return MarkdownTable
            .SplitCells(trimmed)
            .All(cell => cell.Length > 0 && cell.All(c => c == '-' || c == ':'));
    }
}
