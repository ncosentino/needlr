using System.Reflection;

namespace NexusLabs.Needlr.Injection.TypeFilterers;

public sealed class DefaultTypeFilterer : ITypeFilterer
{
    public bool IsInjectableTransientType(Type type)
        => false;

    public bool IsInjectableSingletonType(Type type)
        => IsInjectableType(type);

    public bool IsInjectableType(
        Type type)
    {
        if (!TypeFiltering.IsConcreteType(type))
        {
            return false;
        }

        // ignore our own plugin types
        if (type.IsAssignableTo(typeof(IServiceCollectionPlugin)) ||
            type.IsAssignableTo(typeof(IPostBuildServiceCollectionPlugin)))
        {
            return false;
        }

        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 0)
            {
                return true;
            }

            if (parameters.Length == 1 && parameters[0].ParameterType == type)
            {
                return false;
            }

            if (parameters.All(p =>
                !p.ParameterType.IsAssignableTo(typeof(MulticastDelegate)) &&
                !p.ParameterType.IsValueType &&
                !p.ParameterType.Equals(typeof(string)) &&
                !p.ParameterType.IsPrimitive &&
                (p.ParameterType.IsClass ||
                p.ParameterType.IsInterface)))
            {
                return true;
            }
        }

        return false;
    }
}