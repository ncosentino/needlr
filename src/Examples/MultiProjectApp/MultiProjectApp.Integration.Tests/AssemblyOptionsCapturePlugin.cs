using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

namespace MultiProjectApp.Integration.Tests;

/// <summary>
/// Captures the candidate assemblies supplied to service collection plugins for integration tests.
/// </summary>
internal sealed class AssemblyOptionsCapturePlugin : IServiceCollectionPlugin
{
    /// <inheritdoc />
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IReadOnlyList<Assembly>>(options.Assemblies);
    }
}
