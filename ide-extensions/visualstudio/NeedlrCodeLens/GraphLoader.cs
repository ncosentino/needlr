namespace NeedlrCodeLens;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// Loads and merges Needlr graph files from multiple projects.
/// Each project with [GenerateTypeRegistry] and NeedlrExportGraph=true generates its own graph.
/// We merge them to get complete source location information for all types.
/// </summary>
internal static class GraphLoader
{
    private static NeedlrGraph? _mergedGraph;
    private static readonly HashSet<string> _loadedGraphFiles = new();
    
    /// <summary>
    /// Get merged graph for a project. Loads all available graphs and merges them.
    /// </summary>
    public static async Task<NeedlrGraph?> GetGraphAsync(Guid projectGuid)
    {
        Debug.WriteLine($"[NeedlrCodeLens] GetGraphAsync called for project: {projectGuid}");

        if (_mergedGraph != null)
        {
            Debug.WriteLine($"[NeedlrCodeLens] Returning cached merged graph with {_mergedGraph.Services.Count} services");
            return _mergedGraph;
        }

        // Search for all Needlr graphs and merge them
        _mergedGraph = await LoadAndMergeAllGraphsAsync();
        return _mergedGraph;
    }

    /// <summary>
    /// Force refresh the cached graph.
    /// </summary>
    public static void InvalidateCache()
    {
        _mergedGraph = null;
        _loadedGraphFiles.Clear();
    }

    /// <summary>
    /// Get merged graph for a file path. Searches from the file's directory up to find graphs.
    /// </summary>
    public static async Task<NeedlrGraph?> GetGraphForFileAsync(string filePath)
    {
        Debug.WriteLine($"[NeedlrCodeLens] GetGraphForFileAsync called for: {filePath}");
        
        if (_mergedGraph != null)
        {
            return _mergedGraph;
        }

        // Find the solution/repo root by walking up
        var directory = Path.GetDirectoryName(filePath);
        string? solutionRoot = null;
        
        while (!string.IsNullOrEmpty(directory))
        {
            // Check for common solution indicators
            if (Directory.GetFiles(directory, "*.slnx").Length > 0 ||
                Directory.GetFiles(directory, "*.sln").Length > 0 ||
                Directory.Exists(Path.Combine(directory, ".git")))
            {
                solutionRoot = directory;
                break;
            }
            var parent = Directory.GetParent(directory);
            directory = parent?.FullName;
        }

        if (solutionRoot != null)
        {
            _mergedGraph = await LoadAndMergeGraphsFromDirectoryAsync(solutionRoot);
        }
        
        // Fallback to hardcoded path for development
        if (_mergedGraph == null)
        {
            var needlrPath = @"C:\dev\nexus-labs\needlr";
            if (Directory.Exists(needlrPath))
            {
                _mergedGraph = await LoadAndMergeGraphsFromDirectoryAsync(needlrPath);
            }
        }

        return _mergedGraph;
    }

    private static async Task<NeedlrGraph?> LoadAndMergeAllGraphsAsync()
    {
        var searchPaths = new[]
        {
            @"C:\dev\nexus-labs\needlr",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\source\repos",
            @"C:\dev"
        };

        foreach (var path in searchPaths)
        {
            if (Directory.Exists(path))
            {
                var graph = await LoadAndMergeGraphsFromDirectoryAsync(path);
                if (graph != null && graph.Services.Count > 0)
                {
                    return graph;
                }
            }
        }

        return null;
    }

    private static async Task<NeedlrGraph?> LoadAndMergeGraphsFromDirectoryAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            // Find ALL NeedlrGraph.g.cs files
            var graphFiles = Directory.GetFiles(directory, "NeedlrGraph.g.cs", SearchOption.AllDirectories);
            Debug.WriteLine($"[NeedlrCodeLens] Found {graphFiles.Length} graph files in {directory}");

            if (graphFiles.Length == 0)
            {
                return null;
            }

            // Load all graphs
            var graphs = new List<NeedlrGraph>();
            foreach (var graphFile in graphFiles)
            {
                if (_loadedGraphFiles.Contains(graphFile))
                    continue;
                    
                Debug.WriteLine($"[NeedlrCodeLens] Loading graph from: {graphFile}");
                var graph = await LoadGraphFromSourceFileAsync(graphFile);
                if (graph != null)
                {
                    Debug.WriteLine($"[NeedlrCodeLens] Loaded {graph.AssemblyName} with {graph.Services.Count} services");
                    graphs.Add(graph);
                    _loadedGraphFiles.Add(graphFile);
                }
            }

            if (graphs.Count == 0)
            {
                return null;
            }

            // Merge all graphs - services from each graph bring their own source locations
            var merged = MergeGraphs(graphs);
            Debug.WriteLine($"[NeedlrCodeLens] Merged graph has {merged.Services.Count} services from {graphs.Count} projects");
            return merged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NeedlrCodeLens] Error loading graphs from {directory}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Merge multiple graphs into one. Later graphs with source locations override earlier ones.
    /// </summary>
    private static NeedlrGraph MergeGraphs(List<NeedlrGraph> graphs)
    {
        var merged = new NeedlrGraph
        {
            SchemaVersion = "1.0",
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            AssemblyName = "Merged"
        };

        // Use dictionary to dedupe by full type name, preferring entries with source locations
        var servicesByType = new Dictionary<string, GraphService>();

        foreach (var graph in graphs)
        {
            foreach (var service in graph.Services)
            {
                var key = service.FullTypeName;
                
                // Add or replace if this one has a source location and existing doesn't
                if (!servicesByType.TryGetValue(key, out var existing))
                {
                    servicesByType[key] = service;
                }
                else if (service.Location?.FilePath != null && existing.Location?.FilePath == null)
                {
                    // This service has source location, existing doesn't - prefer this one
                    servicesByType[key] = service;
                }
            }
        }

        merged.Services = servicesByType.Values.ToList();
        return merged;
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
                var graph = JsonConvert.DeserializeObject<NeedlrGraph>(jsonString);
                return graph;
            }
            else
            {
                Debug.WriteLine($"[NeedlrCodeLens] Could not find GraphJson in {filePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NeedlrCodeLens] Error loading graph from {filePath}: {ex.Message}");
        }

        return null;
    }
}
