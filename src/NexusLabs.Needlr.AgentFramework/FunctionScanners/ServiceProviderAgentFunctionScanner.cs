using Microsoft.Extensions.DependencyInjection;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.AgentFramework.FunctionScanners;

[RequiresUnreferencedCode("Service provider scanning uses reflection to discover types with [AgentFunction] methods.")]
[RequiresDynamicCode("Service provider scanning uses reflection APIs that may require dynamic code generation.")]
internal sealed class ServiceProviderAgentFunctionScanner(
    IServiceProvider _root) :
    IAgentFrameworkFunctionScanner
{
    public IReadOnlyList<Type> ScanForFunctionTypes()
    {
        var serviceCollection = _root.GetServiceCollection();
        return serviceCollection
            .Select(sd => sd.ServiceType)
            .Where(t => t is { IsClass: true } && !t.IsAbstract)
            .Where(HasAgentFunctions)
            .Distinct()
            .ToArray();
    }

    private static bool HasAgentFunctions(Type t) =>
        t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
         .Any(m => m.IsDefined(typeof(AgentFunctionAttribute), inherit: true));
}
