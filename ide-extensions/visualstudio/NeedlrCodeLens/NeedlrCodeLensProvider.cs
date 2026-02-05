namespace NeedlrCodeLens;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

/// <summary>
/// CodeLens provider that shows dependency information above Needlr service classes.
/// Displays: "🔷 3 deps | Singleton" above each registered service.
/// </summary>
#pragma warning disable VSEXTPREVIEW_CODELENS
[VisualStudioContribution]
internal class NeedlrCodeLensProvider : ExtensionPart, ICodeLensProvider
{
    public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
    {
        AppliesTo = new[]
        {
            DocumentFilter.FromDocumentType(DocumentType.KnownValues.Code),
        },
    };

#pragma warning disable CEE0027
    public CodeLensProviderConfiguration CodeLensProviderConfiguration =>
        new("Needlr Dependencies")
        {
            Priority = 200,
        };
#pragma warning restore CEE0027

    public async Task<CodeLens?> TryCreateCodeLensAsync(
        CodeElement codeElement, 
        CodeElementContext codeElementContext, 
        CancellationToken token)
    {
        Debug.WriteLine($"[NeedlrCodeLens] TryCreateCodeLensAsync called for: {codeElement.Kind} - {codeElement.UniqueIdentifier ?? codeElement.Description}");

        // Only show CodeLens for type declarations (classes, structs)
        if (codeElement.Kind != CodeElementKind.KnownValues.Type)
        {
            Debug.WriteLine($"[NeedlrCodeLens] Skipping - not a type (Kind={codeElement.Kind})");
            return null;
        }

        // Get the type identifier - could be fully qualified or simple name
        var identifier = codeElement.UniqueIdentifier ?? codeElement.Description;
        if (string.IsNullOrEmpty(identifier))
        {
            Debug.WriteLine("[NeedlrCodeLens] Skipping - no identifier");
            return null;
        }

        Debug.WriteLine($"[NeedlrCodeLens] Type identifier: {identifier}");

        // Log all available properties
        foreach (var prop in codeElementContext.Properties)
        {
            Debug.WriteLine($"[NeedlrCodeLens] Property: {prop.Key} = {prop.Value}");
        }

        // Try to get file path from Properties bag
        string? filePath = null;
        if (codeElementContext.Properties.TryGetValue("DocumentMoniker", out var monikerPath))
        {
            filePath = monikerPath;
            Debug.WriteLine($"[NeedlrCodeLens] Got file path from DocumentMoniker: {filePath}");
        }
        else if (codeElementContext.Properties.TryGetValue("FilePath", out var path))
        {
            filePath = path;
            Debug.WriteLine($"[NeedlrCodeLens] Got file path from FilePath: {filePath}");
        }

        // Try to find the Needlr graph
        NeedlrGraph? graph;
        if (!string.IsNullOrEmpty(filePath))
        {
            graph = await GraphLoader.GetGraphForFileAsync(filePath);
        }
        else
        {
            // Fall back to project GUID search
            graph = await GraphLoader.GetGraphAsync(codeElement.ProjectGuid);
        }
        
        if (graph == null)
        {
            Debug.WriteLine("[NeedlrCodeLens] No graph found");
            return null;
        }

        Debug.WriteLine($"[NeedlrCodeLens] Graph loaded with {graph.Services.Count} services");

        // Find service by type name - extract simple type name from identifier
        var typeName = ExtractTypeName(identifier);
        Debug.WriteLine($"[NeedlrCodeLens] Looking for service: {typeName}");

        var service = graph.Services.FirstOrDefault(s => 
            s.TypeName == typeName || 
            s.FullTypeName == identifier ||
            s.FullTypeName.EndsWith("." + typeName));
        
        if (service == null)
        {
            Debug.WriteLine($"[NeedlrCodeLens] Service not found for: {typeName}");
            return null;
        }

        Debug.WriteLine($"[NeedlrCodeLens] Found service: {service.FullTypeName} ({service.Lifetime})");
        return new NeedlrDependencyCodeLens(codeElement, service, graph, this.Extensibility);
    }

    private static string ExtractTypeName(string identifier)
    {
        // Extract simple type name from fully qualified name
        var lastDot = identifier.LastIndexOf('.');
        return lastDot >= 0 ? identifier.Substring(lastDot + 1) : identifier;
    }
}
#pragma warning restore VSEXTPREVIEW_CODELENS
