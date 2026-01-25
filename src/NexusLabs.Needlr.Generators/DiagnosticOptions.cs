using System;
using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Configuration options for diagnostic file generation.
/// </summary>
internal sealed class DiagnosticOptions
{
    /// <summary>
    /// Whether diagnostics are enabled.
    /// </summary>
    public bool Enabled { get; }
    
    /// <summary>
    /// Directory path where diagnostic files will be written.
    /// </summary>
    public string OutputPath { get; }
    
    /// <summary>
    /// Optional filter - only include types whose fully qualified names are in this set.
    /// If empty, all types are included.
    /// </summary>
    public HashSet<string> TypeFilter { get; }

    public DiagnosticOptions(bool enabled, string outputPath, HashSet<string> typeFilter)
    {
        Enabled = enabled;
        OutputPath = outputPath ?? string.Empty;
        TypeFilter = typeFilter ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Parses diagnostic options from MSBuild property values.
    /// </summary>
    public static DiagnosticOptions Parse(string? enabledValue, string? outputPath, string? filterValue)
    {
        var enabled = ParseBool(enabledValue);
        var path = string.IsNullOrWhiteSpace(outputPath) ? string.Empty : outputPath!.Trim();
        var filter = ParseFilter(filterValue);
        
        return new DiagnosticOptions(enabled, path, filter);
    }

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value!.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal);
    }

    private static HashSet<string> ParseFilter(string? value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        
        if (string.IsNullOrWhiteSpace(value))
            return result;

        var parts = value!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
