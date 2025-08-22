using Microsoft.SemanticKernel;

using System.Reflection;

namespace NexusLabs.Needlr.SemanticKernel.PluginScanners;

internal sealed class AssemblySemanticKernelPluginScanner(
    IReadOnlyList<Assembly> _assemblies) :
    ISemanticKernelPluginScanner
{
    public IReadOnlyList<Type> ScanForPluginTypes()
    {
        var list = FromAssemblies(_assemblies)
            .Where(HasKernelFunctions)
            .Distinct()
            .ToArray();

        return list;
    }

    private static IEnumerable<Type> FromAssemblies(IEnumerable<Assembly> assemblies) =>
        assemblies
            .Where(a => !a.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Where(t => t.IsClass && (!t.IsAbstract || t.IsStatic()));

    private static bool HasKernelFunctions(Type t) =>
        t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
         .Any(m => m.IsDefined(typeof(KernelFunctionAttribute), inherit: true));

    private static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}