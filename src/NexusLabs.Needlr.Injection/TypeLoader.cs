using System.Reflection;

namespace NexusLabs.Needlr.Injection;

public static class TypeLoader
{
    public static IReadOnlyList<Type> GetInjectableTypesImplementing<T>(
        IReadOnlyList<Assembly> assemblies,
        Predicate<Type> typeFilter)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(typeFilter);

        List<Type> resultTypes = [];
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(type =>
                        typeFilter.Invoke(type) &&
                        type.IsAssignableTo(typeof(T)))
                    .ToList();

                resultTypes.AddRange(types);
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loadedTypes = ex.Types.Where(t => t != null).Cast<Type>();
                var validTypes = loadedTypes
                    .Where(type =>
                        typeFilter.Invoke(type) &&
                        type.IsAssignableTo(typeof(T)))
                    .ToList();

                resultTypes.AddRange(validTypes);
            }
        }

        return resultTypes.Distinct().ToArray();
    }
}
