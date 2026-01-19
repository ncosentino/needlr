using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

namespace BundleExamplePlugin;

/// <summary>
/// Plugin that registers additional services during DI configuration.
/// </summary>
public sealed class ExamplePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register a custom message that can be injected
        options.Services.AddSingleton<WelcomeMessage>(new WelcomeMessage("Welcome to the Bundle Example!"));
        
        Console.WriteLine("  [Plugin] ExamplePlugin.Configure() called - registered WelcomeMessage");
    }
}

/// <summary>
/// A simple message holder registered by the plugin.
/// </summary>
public sealed class WelcomeMessage(string message)
{
    public string Message { get; } = message;
}
