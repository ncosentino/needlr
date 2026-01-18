using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel.PluginScanners;

[RequiresUnreferencedCode("Service provider scanning uses reflection to discover types with [KernelFunction] methods.")]
[RequiresDynamicCode("Service provider scanning uses reflection APIs that may require dynamic code generation.")]
internal sealed class ServiceProviderSemanticKernelPluginScanner(
    IServiceProvider _root) : 
    ISemanticKernelPluginScanner
{
    public IReadOnlyList<Type> ScanForPluginTypes()
    {
        var serviceCollection = _root.GetServiceCollection();
        var list = ScanForPluginTypes(serviceCollection)
            .Where(t => serviceCollection.Any(sd => sd.ServiceType == t))
            .ToArray();
        return list;
    }

    private static IReadOnlyList<Type> ScanForPluginTypes(IServiceCollection serviceCollection)
    {
        var list = serviceCollection
            .Select(sd => sd.ServiceType)
            .Where(t => t is { IsClass: true } && !t.IsAbstract)
            .Where(HasKernelFunctions)
            .Distinct()
            .ToArray();

        return list;
    }

    private static bool HasKernelFunctions(Type t) =>
        t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Any(m => m.IsDefined(typeof(KernelFunctionAttribute), inherit: true));
}
