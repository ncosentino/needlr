using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr;

namespace MultiProjectApp.Features.Reporting;

/// <summary>Generates simple text reports.</summary>
public interface IReportService
{
    string Generate(string title, IEnumerable<string> items);
}

/// <summary>Stub implementation of <see cref="IReportService"/>.</summary>
public sealed class ConsoleReportService : IReportService
{
    public string Generate(string title, IEnumerable<string> items)
    {
        var lines = items.ToList();
        return $"=== {title} ===\n{string.Join("\n", lines.Select((l, i) => $"  {i + 1}. {l}"))}\n({lines.Count} items)";
    }
}

/// <summary>
/// Helper class intentionally decorated with [DoNotAutoRegister] — this is the correct
/// usage pattern: preventing a non-plugin helper from being auto-registered as a DI service.
/// Compare with the Bootstrap project which shows the INCORRECT (and now warned) usage.
/// </summary>
[DoNotAutoRegister]
public sealed class ReportingInternals
{
    public static string FormatHeader(string title) => $"=== {title} ===";
}

/// <summary>Registers reporting services into the DI container.</summary>
public sealed class ReportingPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IReportService, ConsoleReportService>();
    }
}
