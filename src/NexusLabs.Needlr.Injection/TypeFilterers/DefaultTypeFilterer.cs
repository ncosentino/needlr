using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.TypeFilterers;

/// <summary>
/// Default type filterer that uses runtime reflection to analyze constructors.
/// </summary>
/// <remarks>
/// This filterer is not compatible with NativeAOT or trimming. For AOT scenarios,
/// use <see cref="GeneratedTypeFilterer"/> with the Needlr source generator instead.
/// </remarks>
[RequiresUnreferencedCode("DefaultTypeFilterer uses reflection to analyze constructors. Use GeneratedTypeFilterer for AOT scenarios.")]
public sealed class DefaultTypeFilterer : ITypeFilterer
{
    public bool IsInjectableScopedType(Type type)
        => false;

    public bool IsInjectableTransientType(Type type)
        => false;

    public bool IsInjectableSingletonType(
        Type type)
    {
        if (!TypeFiltering.IsConcreteType(type))
        {
            return false;
        }

        if (type.GetCustomAttribute<DoNotInjectAttribute>(inherit: true) is not null)
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