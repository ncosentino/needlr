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

    public static ITypeFilterer Except(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !predicate(t),
            (filter, t) => filter(t) && !predicate(t),
            (filter, t) => filter(t) && !predicate(t));
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

    public static ITypeFilterer UsingOnlyAsScoped<T>(
        this ITypeFilterer typeFilterer,
        Predicate<Type> additionalPredicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(additionalPredicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) || (t.IsAssignableTo(typeof(T)) && additionalPredicate(t)),
            (filter, t) => filter(t) && !(t.IsAssignableTo(typeof(T)) && additionalPredicate(t)),
            (filter, t) => filter(t) && !(t.IsAssignableTo(typeof(T)) && additionalPredicate(t)));
    }

    public static ITypeFilterer UsingOnlyAsScoped(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) || predicate(t),
            (filter, t) => filter(t) && !predicate(t),
            (filter, t) => filter(t) && !predicate(t));
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

    public static ITypeFilterer UsingOnlyAsTransient<T>(
        this ITypeFilterer typeFilterer,
        Predicate<Type> additionalPredicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(additionalPredicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !(t.IsAssignableTo(typeof(T)) && additionalPredicate(t)),
            (filter, t) => filter(t) || (t.IsAssignableTo(typeof(T)) && additionalPredicate(t)),
            (filter, t) => filter(t) && !(t.IsAssignableTo(typeof(T)) && additionalPredicate(t)));
    }

    public static ITypeFilterer UsingOnlyAsTransient(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !predicate(t),
            (filter, t) => filter(t) || predicate(t),
            (filter, t) => filter(t) && !predicate(t));
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

    public static ITypeFilterer UsingOnlyAsSingleton<T>(
        this ITypeFilterer typeFilterer,
        Predicate<Type> additionalPredicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(additionalPredicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !(t.IsAssignableTo(typeof(T)) && additionalPredicate(t)),
            (filter, t) => filter(t) && !(t.IsAssignableTo(typeof(T)) && additionalPredicate(t)),
            (filter, t) => filter(t) || (t.IsAssignableTo(typeof(T)) && additionalPredicate(t)));
    }

    public static ITypeFilterer UsingOnlyAsSingleton(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return new TypeFilterDecorator(
            typeFilterer,
            (filter, t) => filter(t) && !predicate(t),
            (filter, t) => filter(t) && !predicate(t),
            (filter, t) => filter(t) || predicate(t));
    }
}