using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeedlrToolsExtension
{
    /// <summary>
    /// Loads and merges Needlr graph files from all projects in the solution.
    /// </summary>
    public class GraphLoader : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
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

            System.Diagnostics.Debug.WriteLine($"Needlr: Searching for graphs in {solutionDir}");

            // Load and merge all graphs from multiple projects
            return await LoadAndMergeAllGraphsAsync(solutionDir);
        }

        private async Task<NeedlrGraph?> LoadAndMergeAllGraphsAsync(string solutionDir)
        {
            var allServices = new Dictionary<string, GraphService>();
            var allDiagnostics = new List<GraphDiagnostic>();
            string? primaryAssemblyName = null;
            string? primaryProjectPath = null;

            // Clear existing watchers
            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }
            _watchers.Clear();

            // Find all NeedlrGraph.g.cs files (each project with [GenerateTypeRegistry] has one)
            var sourceFiles = Directory.GetFiles(solutionDir, "NeedlrGraph.g.cs", SearchOption.AllDirectories).ToList();
            System.Diagnostics.Debug.WriteLine($"Needlr: Found {sourceFiles.Count} NeedlrGraph.g.cs files");

            foreach (var sourceFile in sourceFiles)
            {
                var graph = await LoadGraphFromSourceFileInternalAsync(sourceFile);
                if (graph == null) continue;

                System.Diagnostics.Debug.WriteLine($"Needlr: Loaded graph from {sourceFile} with {graph.Services.Count} services");

                // Use first graph's metadata as primary
                if (primaryAssemblyName == null)
                {
                    primaryAssemblyName = graph.AssemblyName;
                    primaryProjectPath = graph.ProjectPath;
                }

                // Merge services - prefer entries with source locations
                foreach (var service in graph.Services)
                {
                    if (!allServices.TryGetValue(service.FullTypeName, out var existing))
                    {
                        allServices[service.FullTypeName] = service;
                    }
                    else if (HasBetterLocation(service, existing))
                    {
                        allServices[service.FullTypeName] = service;
                    }
                }

                // Merge diagnostics
                allDiagnostics.AddRange(graph.Diagnostics);

                // Watch this file for changes
                SetupFileWatcher(sourceFile);
            }

            // Also check for needlr-graph.json files
            var jsonFiles = Directory.GetFiles(solutionDir, "needlr-graph.json", SearchOption.AllDirectories)
                .Where(f => f.Contains("obj"))
                .ToList();

            foreach (var jsonFile in jsonFiles)
            {
                var graph = await LoadGraphFileInternalAsync(jsonFile);
                if (graph == null) continue;

                System.Diagnostics.Debug.WriteLine($"Needlr: Loaded graph from {jsonFile} with {graph.Services.Count} services");

                if (primaryAssemblyName == null)
                {
                    primaryAssemblyName = graph.AssemblyName;
                    primaryProjectPath = graph.ProjectPath;
                }

                foreach (var service in graph.Services)
                {
                    if (!allServices.TryGetValue(service.FullTypeName, out var existing))
                    {
                        allServices[service.FullTypeName] = service;
                    }
                    else if (HasBetterLocation(service, existing))
                    {
                        allServices[service.FullTypeName] = service;
                    }
                }

                allDiagnostics.AddRange(graph.Diagnostics);
                SetupFileWatcher(jsonFile);
            }

            if (allServices.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Needlr: No services found in any graph");
                return null;
            }

            // Create merged graph
            var mergedGraph = new NeedlrGraph
            {
                SchemaVersion = "1.0",
                GeneratedAt = DateTime.UtcNow.ToString("O"),
                AssemblyName = primaryAssemblyName ?? "Merged",
                ProjectPath = primaryProjectPath,
                Services = allServices.Values.ToList(),
                Diagnostics = allDiagnostics,
                Statistics = CalculateStatistics(allServices.Values)
            };

            _currentGraph = mergedGraph;
            GraphLoaded?.Invoke(this, mergedGraph);

            System.Diagnostics.Debug.WriteLine($"Needlr: Merged graph has {mergedGraph.Services.Count} services");
            return mergedGraph;
        }

        private static bool HasBetterLocation(GraphService newService, GraphService existing)
        {
            var newHasLocation = !string.IsNullOrEmpty(newService.Location?.FilePath) && newService.Location.Line > 0;
            var existingHasLocation = !string.IsNullOrEmpty(existing.Location?.FilePath) && existing.Location.Line > 0;
            return newHasLocation && !existingHasLocation;
        }

        private static GraphStatistics CalculateStatistics(IEnumerable<GraphService> services)
        {
            var list = services.ToList();
            return new GraphStatistics
            {
                TotalServices = list.Count,
                Singletons = list.Count(s => s.Lifetime == "Singleton"),
                Scoped = list.Count(s => s.Lifetime == "Scoped"),
                Transient = list.Count(s => s.Lifetime == "Transient"),
                Decorators = list.Sum(s => s.Decorators.Count),
                Interceptors = list.Sum(s => s.Interceptors.Count),
                Factories = list.Count(s => s.Metadata.HasFactory),
                Options = list.Count(s => s.Metadata.HasOptions),
                HostedServices = list.Count(s => s.Metadata.IsHostedService),
                Plugins = list.Count(s => s.Metadata.IsPlugin)
            };
        }

        private async Task<NeedlrGraph?> LoadGraphFileInternalAsync(string filePath)
        {
            try
            {
                var content = await Task.Run(() => File.ReadAllText(filePath));
                return JsonConvert.DeserializeObject<NeedlrGraph>(content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Needlr graph: {ex.Message}");
                return null;
            }
        }

        private async Task<NeedlrGraph?> LoadGraphFromSourceFileInternalAsync(string filePath)
        {
            try
            {
                var content = await Task.Run(() => File.ReadAllText(filePath));

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

                    var jsonString = match.Groups[1].Value.Replace("\"\"", "\"");
                    return JsonConvert.DeserializeObject<NeedlrGraph>(jsonString);
                }

                return JsonConvert.DeserializeObject<NeedlrGraph>(match.Groups[1].Value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract graph from source file: {ex.Message}");
                return null;
            }
        }

        public async Task<NeedlrGraph?> LoadGraphFileAsync(string filePath)
        {
            var graph = await LoadGraphFileInternalAsync(filePath);
            if (graph != null)
            {
                _currentGraph = graph;
                GraphLoaded?.Invoke(this, graph);
            }
            return graph;
        }

        public async Task<NeedlrGraph?> LoadGraphFromSourceFileAsync(string filePath)
        {
            var graph = await LoadGraphFromSourceFileInternalAsync(filePath);
            if (graph != null)
            {
                _currentGraph = graph;
                GraphLoaded?.Invoke(this, graph);
            }
            return graph;
        }

        private void SetupFileWatcher(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                return;
            }

            var watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            watcher.Changed += async (s, e) =>
            {
                // Reload all graphs when any changes
                var solutionDir = Path.GetDirectoryName(directory);
                while (!string.IsNullOrEmpty(solutionDir) && !Directory.GetFiles(solutionDir, "*.sln*").Any())
                {
                    solutionDir = Path.GetDirectoryName(solutionDir);
                }
                
                if (!string.IsNullOrEmpty(solutionDir))
                {
                    await LoadAndMergeAllGraphsAsync(solutionDir);
                }
            };

            watcher.Deleted += (s, e) =>
            {
                _currentGraph = null;
                GraphCleared?.Invoke(this, EventArgs.Empty);
            };

            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var watcher in _watchers)
                {
                    watcher.Dispose();
                }
                _watchers.Clear();
                _disposed = true;
            }
        }
    }
}
