using NexusLabs.Needlr.Injection.TypeFilterers;

namespace NexusLabs.Needlr.Injection;

public static class ITypeFiltererExtensionMethods
{
    public static ITypeFilterer Except<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)));
    }

    public static ITypeFilterer UsingOnlyAsScoped<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) || t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)));
    }

    public static ITypeFilterer UsingOnlyAsTransient<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) || t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)));
    }

    public static ITypeFilterer UsingOnlyAsSingleton<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) && !t.IsAssignableTo(typeof(T)),
            (filter, t) => filter(t) || t.IsAssignableTo(typeof(T)));
    }
}