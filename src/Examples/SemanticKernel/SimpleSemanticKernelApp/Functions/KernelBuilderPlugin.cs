using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.SemanticKernel;
/// <summary>
/// This isn't necessary, but it illustrates that you can manually
/// register services with the kernel builder if you want to if
/// you leverage the [DoNotAutoRegister] attribute on the types
/// being registered.
/// </summary>
internal sealed class KernelBuilderPlugin : IKernelBuilderPlugin
{
    public void Configure(KernelBuilderPluginOptions options)
    {
        options.KernelBuilder.Services.AddSingleton<CountriesProvider>();
    }
}


