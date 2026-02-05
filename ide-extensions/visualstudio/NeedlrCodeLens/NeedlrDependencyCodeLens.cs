namespace NeedlrCodeLens;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.Shell;
using Microsoft.VisualStudio.Extensibility.UI;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

/// <summary>
/// CodeLens that displays dependency count and lifetime for a Needlr service.
/// Click for detailed dependency information in an inline popup.
/// </summary>
#pragma warning disable VSEXTPREVIEW_CODELENS
internal class NeedlrDependencyCodeLens : VisualCodeLens
{
    private readonly CodeElement _codeElement;
    private readonly GraphService _service;
    private readonly NeedlrGraph _graph;
    private readonly VisualStudioExtensibility _extensibility;

    public NeedlrDependencyCodeLens(
        CodeElement codeElement, 
        GraphService service,
        NeedlrGraph graph,
        VisualStudioExtensibility extensibility)
    {
        _codeElement = codeElement;
        _service = service;
        _graph = graph;
        _extensibility = extensibility;
    }

    public override void Dispose()
    {
    }

    public override Task<CodeLensLabel> GetLabelAsync(
        CodeElementContext codeElementContext, 
        CancellationToken token)
    {
        var depCount = _service.Dependencies.Count;
        var lifetime = _service.Lifetime;

        // Lifetime symbols (distinct shapes)
        var lifetimeSymbol = lifetime switch
        {
            "Singleton" => "◆",  // Diamond = one instance
            "Scoped" => "◈",     // Diamond with dot = per scope
            "Transient" => "◇",  // Empty diamond = new each time
            _ => "○"
        };

        // Warning indicator for captive dependencies
        var warning = HasCaptiveDependency() ? "⚠ " : "";

        // Compact label: ◆ Singleton | 3 deps
        var parts = new List<string> { $"{lifetimeSymbol} {lifetime}" };
        
        if (depCount > 0)
            parts.Add($"{depCount} dep{(depCount != 1 ? "s" : "")}");
        
        // Show assembly for plugins
        if (!string.IsNullOrEmpty(_service.AssemblyName) && 
            _service.AssemblyName != _graph.AssemblyName)
        {
            parts.Add($"[{_service.AssemblyName}]");
        }

        var text = warning + string.Join(" | ", parts);

        return Task.FromResult(new CodeLensLabel
        {
            Text = text,
            Tooltip = "Click for dependency details",
        });
    }

    public override Task<IRemoteUserControl> GetVisualizationAsync(
        CodeElementContext codeElementContext, 
        IClientContext clientContext, 
        CancellationToken token)
    {
        var viewModel = new NeedlrCodeLensViewModel(_service, _graph, _extensibility);
        return Task.FromResult<IRemoteUserControl>(new NeedlrCodeLensVisual(viewModel));
    }

    private bool HasCaptiveDependency()
    {
        if (_service.Lifetime == "Singleton")
        {
            return _service.Dependencies.Any(d => 
                d.ResolvedLifetime == "Scoped" || d.ResolvedLifetime == "Transient");
        }
        if (_service.Lifetime == "Scoped")
        {
            return _service.Dependencies.Any(d => d.ResolvedLifetime == "Transient");
        }
        return false;
    }
}

/// <summary>
/// ViewModel for the CodeLens popup visualization.
/// </summary>
[DataContract]
internal class NeedlrCodeLensViewModel
{
    public NeedlrCodeLensViewModel(GraphService service, NeedlrGraph graph, VisualStudioExtensibility extensibility)
    {
        TypeName = service.TypeName;
        FullTypeName = service.FullTypeName;
        Lifetime = service.Lifetime;
        AssemblyName = service.AssemblyName ?? "";
        
        LifetimeSymbol = service.Lifetime switch
        {
            "Singleton" => "◆",
            "Scoped" => "◈",
            "Transient" => "◇",
            _ => "○"
        };
        
        // Dependencies with location info for navigation
        Dependencies = service.Dependencies.Select(d => 
        {
            var resolvedService = graph.Services.FirstOrDefault(s => 
                s.FullTypeName == d.ResolvedTo || s.FullTypeName == d.FullTypeName);
            return new DependencyItem
            {
                TypeName = d.TypeName,
                FullTypeName = d.FullTypeName,
                ResolvedTo = d.ResolvedTo != null ? GetSimpleTypeName(d.ResolvedTo) : null,
                Lifetime = d.ResolvedLifetime ?? "",
                IsCaptive = IsCaptiveDependency(service.Lifetime, d.ResolvedLifetime),
                IsCaptiveVisibility = IsCaptiveDependency(service.Lifetime, d.ResolvedLifetime) ? "Visible" : "Collapsed",
                FilePath = resolvedService?.Location?.FilePath,
                Line = resolvedService?.Location?.Line ?? 0,
                NavigateCommand = new NavigateToTypeCommand(resolvedService?.Location, d.ResolvedTo ?? d.FullTypeName, extensibility)
            };
        }).ToList();
        
        DependencyCount = Dependencies.Count;
        MaxDepth = CalculateDepth(service, graph, new HashSet<string>());
        
        // Used by with location info
        var usedBy = graph.Services.Where(s => 
            s.Dependencies.Any(d => 
                d.ResolvedTo == service.FullTypeName || 
                d.FullTypeName == service.FullTypeName ||
                service.Interfaces.Any(i => i.FullName == d.FullTypeName))).ToList();
        
        UsedBy = usedBy.Select(s => new UsedByItem
        {
            TypeName = s.TypeName,
            FullTypeName = s.FullTypeName,
            Lifetime = s.Lifetime,
            FilePath = s.Location?.FilePath,
            Line = s.Location?.Line ?? 0,
            NavigateCommand = new NavigateToTypeCommand(s.Location, s.FullTypeName, extensibility)
        }).ToList();
        
        UsedByCount = UsedBy.Count;
        
        // Interfaces
        Interfaces = service.Interfaces.Select(i => i.Name).ToList();
        InterfacesDisplay = Interfaces.Count > 0 ? string.Join(", ", Interfaces) : "none";
        
        // Decorators
        Decorators = service.Decorators.OrderBy(d => d.Order).Select(d => d.TypeName).ToList();
        
        // Warnings
        HasCaptiveDependency = Dependencies.Any(d => d.IsCaptive);
        HasCaptiveDependencyVisibility = HasCaptiveDependency ? "Visible" : "Collapsed";
    }

    [DataMember] public string TypeName { get; set; }
    [DataMember] public string FullTypeName { get; set; }
    [DataMember] public string Lifetime { get; set; }
    [DataMember] public string LifetimeSymbol { get; set; }
    [DataMember] public string AssemblyName { get; set; }
    [DataMember] public int DependencyCount { get; set; }
    [DataMember] public int MaxDepth { get; set; }
    [DataMember] public List<DependencyItem> Dependencies { get; set; }
    [DataMember] public int UsedByCount { get; set; }
    [DataMember] public List<UsedByItem> UsedBy { get; set; }
    [DataMember] public List<string> Interfaces { get; set; }
    [DataMember] public string InterfacesDisplay { get; set; }
    [DataMember] public List<string> Decorators { get; set; }
    [DataMember] public bool HasCaptiveDependency { get; set; }
    [DataMember] public string HasCaptiveDependencyVisibility { get; set; }
    
    private static bool IsCaptiveDependency(string parentLifetime, string? depLifetime)
    {
        if (string.IsNullOrEmpty(depLifetime)) return false;
        if (parentLifetime == "Singleton")
            return depLifetime == "Scoped" || depLifetime == "Transient";
        if (parentLifetime == "Scoped")
            return depLifetime == "Transient";
        return false;
    }
    
    private static int CalculateDepth(GraphService service, NeedlrGraph graph, HashSet<string> visited)
    {
        if (visited.Contains(service.FullTypeName))
            return 0;
            
        visited.Add(service.FullTypeName);
        
        var maxChildDepth = 0;
        foreach (var dep in service.Dependencies)
        {
            var resolvedService = graph.Services.FirstOrDefault(s => 
                s.FullTypeName == dep.ResolvedTo || s.FullTypeName == dep.FullTypeName);
            if (resolvedService != null)
            {
                var childDepth = CalculateDepth(resolvedService, graph, new HashSet<string>(visited));
                maxChildDepth = Math.Max(maxChildDepth, childDepth);
            }
        }
        
        return maxChildDepth + 1;
    }
    
    private static string GetSimpleTypeName(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName)) return fullTypeName;
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
    }
}

[DataContract]
internal class DependencyItem
{
    [DataMember] public string TypeName { get; set; } = "";
    [DataMember] public string FullTypeName { get; set; } = "";
    [DataMember] public string? ResolvedTo { get; set; }
    [DataMember] public string Lifetime { get; set; } = "";
    [DataMember] public bool IsCaptive { get; set; }
    [DataMember] public string IsCaptiveVisibility { get; set; } = "Collapsed";
    [DataMember] public string? FilePath { get; set; }
    [DataMember] public int Line { get; set; }
    [DataMember] public IAsyncCommand? NavigateCommand { get; set; }
}

[DataContract]
internal class UsedByItem
{
    [DataMember] public string TypeName { get; set; } = "";
    [DataMember] public string FullTypeName { get; set; } = "";
    [DataMember] public string Lifetime { get; set; } = "";
    [DataMember] public string? FilePath { get; set; }
    [DataMember] public int Line { get; set; }
    [DataMember] public IAsyncCommand? NavigateCommand { get; set; }
}

/// <summary>
/// Command to navigate to a type's source location.
/// </summary>
internal class NavigateToTypeCommand : AsyncCommand
{
    public NavigateToTypeCommand(GraphLocation? location, string? fullTypeName, VisualStudioExtensibility extensibility)
        : base(async (parameter, cancellationToken) =>
        {
            if (string.IsNullOrEmpty(location?.FilePath)) 
            {
                await extensibility.Shell().ShowPromptAsync(
                    "Source not available (type from compiled assembly)",
                    PromptOptions.OK,
                    cancellationToken);
                return;
            }
            
            try
            {
                // Verify file exists
                if (!System.IO.File.Exists(location.FilePath))
                {
                    await extensibility.Shell().ShowPromptAsync(
                        $"File not found: {location.FilePath}",
                        PromptOptions.OK,
                        cancellationToken);
                    return;
                }
                
                var fileUri = new Uri(location.FilePath, UriKind.Absolute);
                var documents = extensibility.Documents();
                
                // Open with selection at the target line
                if (location.Line > 0)
                {
                    // Line numbers in the graph are 1-based, Range uses 0-based
                    var line = location.Line - 1;
                    var range = new Microsoft.VisualStudio.RpcContracts.Utilities.Range(
                        startLine: line, 
                        startColumn: 0, 
                        endLine: line, 
                        endColumn: 0);
                    
                    var options = new Microsoft.VisualStudio.RpcContracts.OpenDocument.OpenDocumentOptions(
                        selection: range,
                        ensureVisible: range);
                    
                    await documents.OpenDocumentAsync(fileUri, options, cancellationToken);
                }
                else
                {
                    await documents.OpenDocumentAsync(fileUri, cancellationToken);
                }
                
                System.Diagnostics.Debug.WriteLine($"[NeedlrCodeLens] Opened {location.FilePath}:{location.Line}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NeedlrCodeLens] Navigation error: {ex.Message}");
                await extensibility.Shell().ShowPromptAsync(
                    $"Failed to navigate: {ex.Message}",
                    PromptOptions.OK,
                    cancellationToken);
            }
        })
    {
    }
}

/// <summary>
/// Remote user control for CodeLens popup visualization.
/// </summary>
internal class NeedlrCodeLensVisual : RemoteUserControl
{
    public NeedlrCodeLensVisual(NeedlrCodeLensViewModel viewModel)
        : base(viewModel)
    {
    }
}
#pragma warning restore VSEXTPREVIEW_CODELENS
