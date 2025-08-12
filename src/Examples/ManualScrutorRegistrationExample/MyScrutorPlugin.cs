using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

/// <summary>
/// This is a plugin that will get automatically registered 
/// and invoked by the Needlr framework. We do not need to add
/// any attributes to this class, as it implements the 
/// <see cref="IServiceCollectionPlugin"/> interface.
/// This plugin uses Scrutor decoration extensions to register
/// the decorator pattern.
/// </summary>
internal sealed class MyScrutorPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        // Register the base service first
        options.Services.AddSingleton<IMyService, MyService>();
        
        // Use Scrutor to decorate the service
        options.Services.Decorate<IMyService, MyDecorator>();
    }
}