using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// A structurally parsed Markdown pipe table.
/// </summary>
/// <param name="Columns">The header cell values.</param>
/// <param name="Rows">The data rows, each holding one value per column.</param>
internal sealed record MarkdownTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    /// <summary>
    /// Splits a Markdown table line into trimmed cell values.
    /// </summary>
    /// <param name="line">The raw table line including leading and trailing pipes.</param>
    /// <returns>The cell values.</returns>
    public static IReadOnlyList<string> SplitCells(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1);
        }

        if (trimmed.EndsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
            {
                current.Append("\\|");
                i++;
                continue;
            }

            if (c == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    /// <summary>
    /// Gets the value of a column for a row.
    /// </summary>
    /// <param name="rowIndex">The zero-based data row index.</param>
    /// <param name="columnName">The header name of the column.</param>
    /// <returns>The cell value.</returns>
    public string Cell(int rowIndex, string columnName)
    {
        var columnIndex = Columns
            .Select((name, index) => (name, index))
            .First(x => string.Equals(x.name, columnName, StringComparison.Ordinal))
            .index;
        return Rows[rowIndex][columnIndex];
    }

    /// <summary>
    /// Gets every value of a column across all rows.
    /// </summary>
    /// <param name="columnName">The header name of the column.</param>
    /// <returns>The column values in row order.</returns>
    public IReadOnlyList<string> Column(string columnName)
    {
        return Enumerable
            .Range(0, Rows.Count)
            .Select(rowIndex => Cell(rowIndex, columnName))
            .ToList();
    }
}
