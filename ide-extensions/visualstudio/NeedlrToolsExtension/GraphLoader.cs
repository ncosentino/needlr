using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Watches for and loads Needlr graph files from the solution.
    /// </summary>
    public class GraphLoader : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private NeedlrGraph? _currentGraph;
        private bool _disposed;

        public event EventHandler<NeedlrGraph>? GraphLoaded;
        public event EventHandler? GraphCleared;

        public NeedlrGraph? CurrentGraph => _currentGraph;

        public async Task<NeedlrGraph?> FindAndLoadGraphAsync()
        {
            var solution = await Community.VisualStudio.Toolkit.VS.Solutions.GetCurrentSolutionAsync();
            if (solution == null || string.IsNullOrEmpty(solution.FullPath))
            {
                System.Diagnostics.Debug.WriteLine("Needlr: No solution loaded");
                return null;
            }

            var solutionDir = Path.GetDirectoryName(solution.FullPath);
            if (string.IsNullOrEmpty(solutionDir))
            {
                System.Diagnostics.Debug.WriteLine("Needlr: Could not determine solution directory");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"Needlr: Searching for graph in {solutionDir}");

            // Search for needlr-graph.json in obj folders
            var graphFiles = Directory.GetFiles(solutionDir, "needlr-graph.json", SearchOption.AllDirectories)
                .Where(f => f.Contains("obj"))
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Needlr: Found {graphFiles.Count} needlr-graph.json files");

            if (graphFiles.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Needlr: Loading graph from {graphFiles[0]}");
                return await LoadGraphFileAsync(graphFiles[0]);
            }

            // Fall back to searching for embedded graph in generated source files
            var sourceFiles = Directory.GetFiles(solutionDir, "NeedlrGraph.g.cs", SearchOption.AllDirectories)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Needlr: Found {sourceFiles.Count} NeedlrGraph.g.cs files");

            if (sourceFiles.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Needlr: Loading graph from source file {sourceFiles[0]}");
                return await LoadGraphFromSourceFileAsync(sourceFiles[0]);
            }

            System.Diagnostics.Debug.WriteLine("Needlr: No graph files found");
            return null;
        }

        public async Task<NeedlrGraph?> LoadGraphFileAsync(string filePath)
        {
            try
            {
                var content = await Task.Run(() => File.ReadAllText(filePath));
                var graph = JsonConvert.DeserializeObject<NeedlrGraph>(content);

                if (graph != null)
                {
                    _currentGraph = graph;
                    GraphLoaded?.Invoke(this, graph);
                    SetupFileWatcher(filePath);
                }

                return graph;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Needlr graph: {ex.Message}");
                return null;
            }
        }

        public async Task<NeedlrGraph?> LoadGraphFromSourceFileAsync(string filePath)
        {
            try
            {
                var content = await Task.Run(() => File.ReadAllText(filePath));

                // Extract JSON from the C# source file - look for GraphJsonContent or GraphJson
                // Try raw string literal format first (C# 11+)
                var match = System.Text.RegularExpressions.Regex.Match(
                    content,
                    @"GraphJson(?:Content)?\s*=\s*""""""([\s\S]*?)""""""",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (!match.Success)
                {
                    // Try verbatim string format @"..."
                    match = System.Text.RegularExpressions.Regex.Match(
                        content,
                        @"GraphJson(?:Content)?\s*=\s*@""((?:[^""]|"""")*)""",
                        System.Text.RegularExpressions.RegexOptions.Singleline);

                    if (!match.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not find GraphJson pattern in {filePath}");
                        return null;
                    }

                    // Unescape the verbatim string (doubled quotes become single)
                    var jsonString = match.Groups[1].Value.Replace("\"\"", "\"");
                    var graph = JsonConvert.DeserializeObject<NeedlrGraph>(jsonString);
                    
                    if (graph != null)
                    {
                        _currentGraph = graph;
                        GraphLoaded?.Invoke(this, graph);
                    }
                    
                    return graph;
                }
                else
                {
                    var graph = JsonConvert.DeserializeObject<NeedlrGraph>(match.Groups[1].Value);
                    
                    if (graph != null)
                    {
                        _currentGraph = graph;
                        GraphLoaded?.Invoke(this, graph);
                    }
                    
                    return graph;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract graph from source file: {ex.Message}");
                return null;
            }
        }

        private void SetupFileWatcher(string filePath)
        {
            _watcher?.Dispose();

            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                return;
            }

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _watcher.Changed += async (s, e) => await LoadGraphFileAsync(e.FullPath);
            _watcher.Deleted += (s, e) =>
            {
                _currentGraph = null;
                GraphCleared?.Invoke(this, EventArgs.Empty);
            };

            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _watcher?.Dispose();
                _disposed = true;
            }
        }
    }
}
