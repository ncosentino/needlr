namespace NeedlrCodeLens;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

/// <summary>
/// CodeLens that displays dependency count and lifetime for a Needlr service.
/// </summary>
#pragma warning disable VSEXTPREVIEW_CODELENS
internal class NeedlrDependencyCodeLens : InvokableCodeLens
{
    private readonly CodeElement _codeElement;
    private readonly GraphService _service;
    private readonly VisualStudioExtensibility _extensibility;

    public NeedlrDependencyCodeLens(
        CodeElement codeElement, 
        GraphService service,
        VisualStudioExtensibility extensibility)
    {
        _codeElement = codeElement;
        _service = service;
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

        var icon = lifetime switch
        {
            "Singleton" => "🔷",
            "Scoped" => "🔶",
            "Transient" => "⚪",
            _ => "🔗"
        };

        if (HasLifetimeMismatch())
        {
            icon = "⚠️";
        }

        var text = depCount > 0
            ? $"{icon} {depCount} dep{(depCount != 1 ? "s" : "")} | {lifetime}"
            : $"{icon} {lifetime}";

        return Task.FromResult(new CodeLensLabel
        {
            Text = text,
            Tooltip = BuildTooltip(),
        });
    }

    public override Task ExecuteAsync(
        CodeElementContext codeElementContext, 
        IClientContext clientContext, 
        CancellationToken cancelToken)
    {
        this.Invalidate();
        return Task.CompletedTask;
    }

    private string BuildTooltip()
    {
        var lines = new List<string>
        {
            $"Service: {_service.FullTypeName}",
            $"Lifetime: {_service.Lifetime}"
        };

        if (_service.Dependencies.Count > 0)
        {
            lines.Add($"Dependencies: {_service.Dependencies.Count}");
            foreach (var dep in _service.Dependencies.Take(5))
            {
                var resolved = dep.ResolvedTo != null 
                    ? $" → {GetSimpleTypeName(dep.ResolvedTo)}" 
                    : "";
                lines.Add($"  • {dep.TypeName}{resolved}");
            }
            if (_service.Dependencies.Count > 5)
            {
                lines.Add($"  ... and {_service.Dependencies.Count - 5} more");
            }
        }

        if (_service.Interfaces.Count > 0)
        {
            lines.Add($"Implements: {string.Join(", ", _service.Interfaces.Select(i => i.Name))}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private bool HasLifetimeMismatch()
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

    private static string GetSimpleTypeName(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName)) return fullTypeName;
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
    }
}
#pragma warning restore VSEXTPREVIEW_CODELENS
