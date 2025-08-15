using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

/// <summary>
/// This is a plugin that will get automatically registered 
/// and invoked by the Needlr framework. We do not need to add
/// any attributes to this class, as it implements the 
/// <see cref="IServiceCollectionPlugin"/> interface.
/// </summary>
internal sealed class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<MyService>();
        options.Services.AddSingleton<IMyService, MyDecorator>(s => 
            new MyDecorator(s.GetRequiredService<MyService>()));
    }
}
