namespace NeedlrCodeLens;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// Loads Needlr graph files from the project's obj folder.
/// </summary>
internal static class GraphLoader
{
    // Cache graphs by assembly name to avoid repeated file system lookups
    private static readonly ConcurrentDictionary<string, NeedlrGraph?> _graphCache = new();
    private static NeedlrGraph? _lastLoadedGraph;
    
    /// <summary>
    /// Get graph for a project GUID. Since we can't map GUIDs directly to files,
    /// we try to find any available Needlr graph. For multi-project solutions,
    /// we load all graphs and match by assembly/type name.
    /// </summary>
    public static async Task<NeedlrGraph?> GetGraphAsync(Guid projectGuid)
    {
        // For now, return the cached graph or search common locations
        if (_lastLoadedGraph != null)
        {
            return _lastLoadedGraph;
        }

        // Search for Needlr graphs in common VS solution locations
        var searchPaths = GetPossibleSearchPaths();
        
        foreach (var searchPath in searchPaths)
        {
            var graph = await TryLoadGraphFromDirectoryAsync(searchPath);
            if (graph != null)
            {
                _lastLoadedGraph = graph;
                return graph;
            }
        }

        return null;
    }

    /// <summary>
    /// Force refresh the cached graph.
    /// </summary>
    public static void InvalidateCache()
    {
        _graphCache.Clear();
        _lastLoadedGraph = null;
    }

    private static IEnumerable<string> GetPossibleSearchPaths()
    {
        // Try current directory and parent directories
        var currentDir = Environment.CurrentDirectory;
        yield return currentDir;

        // Try user's common solution locations
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(userProfile, "source", "repos");
        yield return Path.Combine(userProfile, "Projects");

        // Try common VS paths
        var vsPath = Environment.GetEnvironmentVariable("VSAPPIDDIR");
        if (!string.IsNullOrEmpty(vsPath))
        {
            yield return Path.GetDirectoryName(vsPath) ?? vsPath;
        }
    }

    private static async Task<NeedlrGraph?> TryLoadGraphFromDirectoryAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            // Search for NeedlrGraph.g.cs files
            var graphFiles = Directory.GetFiles(directory, "NeedlrGraph.g.cs", SearchOption.AllDirectories);
            foreach (var graphFile in graphFiles)
            {
                var graph = await LoadGraphFromSourceFileAsync(graphFile);
                if (graph != null)
                {
                    return graph;
                }
            }
        }
        catch
        {
            // Ignore file system errors
        }

        return null;
    }

    public static async Task<NeedlrGraph?> GetGraphForFileAsync(string filePath)
    {
        // Find the obj folder relative to the file and look for NeedlrGraph.g.cs
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            var objDir = Path.Combine(directory, "obj");
            if (Directory.Exists(objDir))
            {
                try
                {
                    var graphFiles = Directory.GetFiles(objDir, "NeedlrGraph.g.cs", SearchOption.AllDirectories);
                    if (graphFiles.Length > 0)
                    {
                        return await LoadGraphFromSourceFileAsync(graphFiles[0]);
                    }
                }
                catch
                {
                    // Ignore file system errors
                }
            }

            var parent = Directory.GetParent(directory);
            directory = parent?.FullName;
        }

        return null;
    }

    private static async Task<NeedlrGraph?> LoadGraphFromSourceFileAsync(string filePath)
    {
        try
        {
            var content = await Task.Run(() => File.ReadAllText(filePath));

            // Extract JSON from the C# source file (verbatim string @"...")
            var match = Regex.Match(
                content,
                @"GraphJson(?:Content)?\s*=\s*@""((?:[^""]|"""")*)""",
                RegexOptions.Singleline);

            if (match.Success)
            {
                var jsonString = match.Groups[1].Value.Replace("\"\"", "\"");
                return JsonConvert.DeserializeObject<NeedlrGraph>(jsonString);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }
}
