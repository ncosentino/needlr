namespace NeedlrCodeLens;

using System;
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
        // Only show CodeLens for type declarations (classes, structs)
        if (codeElement.Kind != CodeElementKind.KnownValues.Type)
        {
            return null;
        }

        // Get the type identifier - could be fully qualified or simple name
        var identifier = codeElement.UniqueIdentifier ?? codeElement.Description;
        if (string.IsNullOrEmpty(identifier))
        {
            return null;
        }

        // Get the project GUID to help narrow down which graph to use
        var projectGuid = codeElement.ProjectGuid;

        // Try to find the Needlr graph
        var graph = await GraphLoader.GetGraphAsync(projectGuid);
        
        if (graph == null)
        {
            return null;
        }

        // Find service by type name - extract simple type name from identifier
        var typeName = ExtractTypeName(identifier);
        var service = graph.Services.FirstOrDefault(s => 
            s.TypeName == typeName || 
            s.FullTypeName == identifier ||
            s.FullTypeName.EndsWith("." + typeName));
        
        if (service == null)
        {
            return null;
        }

        return new NeedlrDependencyCodeLens(codeElement, service, this.Extensibility);
    }

    private static string ExtractTypeName(string identifier)
    {
        // Extract simple type name from fully qualified name
        var lastDot = identifier.LastIndexOf('.');
        return lastDot >= 0 ? identifier.Substring(lastDot + 1) : identifier;
    }
}
#pragma warning restore VSEXTPREVIEW_CODELENS
