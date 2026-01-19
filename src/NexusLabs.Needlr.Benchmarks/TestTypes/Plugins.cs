using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr.Benchmarks.TestTypes;

// ============================================================================
// IServiceCollectionPlugin implementations - 5 plugins
// ============================================================================

public sealed class ServiceCollectionPlugin1 : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<ManualService1>();
    }
}

public sealed class ServiceCollectionPlugin2 : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<ManualService2>();
    }
}

public sealed class ServiceCollectionPlugin3 : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddScoped<ManualService3>();
    }
}

public sealed class ServiceCollectionPlugin4 : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddTransient<ManualService4>();
    }
}

public sealed class ServiceCollectionPlugin5 : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<ManualService5>();
    }
}

// ============================================================================
// IPostBuildServiceCollectionPlugin implementations - 5 plugins
// ============================================================================

public sealed class PostBuildPlugin1 : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Post-build configuration
    }
}

public sealed class PostBuildPlugin2 : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Post-build configuration
    }
}

public sealed class PostBuildPlugin3 : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Post-build configuration
    }
}

public sealed class PostBuildPlugin4 : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Post-build configuration
    }
}

public sealed class PostBuildPlugin5 : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        // Post-build configuration
    }
}
