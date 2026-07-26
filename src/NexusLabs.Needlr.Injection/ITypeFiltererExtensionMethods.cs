using NexusLabs.Needlr.Injection.TypeFilterers;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Extension methods for <see cref="ITypeFilterer"/> providing fluent configuration of type lifetime rules.
/// </summary>
public static class ITypeFiltererExtensionMethods
{
    public static ITypeFilterer Except<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return Except(
            typeFilterer,
            static t => t.IsAssignableTo(typeof(T)));
    }

    public static ITypeFilterer Except(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        bool Filter(Predicate<Type> filter, Type t) => filter(t) && !predicate(t);

        return new TypeFilterDecorator(
            typeFilterer,
            Filter,
            Filter,
            Filter,
            exclusionPredicate: predicate);
    }

    public static ITypeFilterer UsingOnlyAsScoped<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return UsingOnlyAs(
            typeFilterer,
            static t => t.IsAssignableTo(typeof(T)),
            TypeFiltererLifetime.Scoped);
    }

    public static ITypeFilterer UsingOnlyAsScoped<T>(
        this ITypeFilterer typeFilterer,
        Predicate<Type> additionalPredicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(additionalPredicate);

        return UsingOnlyAs(
            typeFilterer,
            t => t.IsAssignableTo(typeof(T)) && additionalPredicate(t),
            TypeFiltererLifetime.Scoped);
    }

    public static ITypeFilterer UsingOnlyAsScoped(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return UsingOnlyAs(
            typeFilterer,
            predicate,
            TypeFiltererLifetime.Scoped);
    }

    public static ITypeFilterer UsingOnlyAsTransient<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return UsingOnlyAs(
            typeFilterer,
            static t => t.IsAssignableTo(typeof(T)),
            TypeFiltererLifetime.Transient);
    }

    public static ITypeFilterer UsingOnlyAsTransient<T>(
        this ITypeFilterer typeFilterer,
        Predicate<Type> additionalPredicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(additionalPredicate);

        return UsingOnlyAs(
            typeFilterer,
            t => t.IsAssignableTo(typeof(T)) && additionalPredicate(t),
            TypeFiltererLifetime.Transient);
    }

    public static ITypeFilterer UsingOnlyAsTransient(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return UsingOnlyAs(
            typeFilterer,
            predicate,
            TypeFiltererLifetime.Transient);
    }

    public static ITypeFilterer UsingOnlyAsSingleton<T>(
        this ITypeFilterer typeFilterer)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);

        return UsingOnlyAs(
            typeFilterer,
            static t => t.IsAssignableTo(typeof(T)),
            TypeFiltererLifetime.Singleton);
    }

    public static ITypeFilterer UsingOnlyAsSingleton<T>(
        this ITypeFilterer typeFilterer,
        Predicate<Type> additionalPredicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(additionalPredicate);

        return UsingOnlyAs(
            typeFilterer,
            t => t.IsAssignableTo(typeof(T)) && additionalPredicate(t),
            TypeFiltererLifetime.Singleton);
    }

    public static ITypeFilterer UsingOnlyAsSingleton(
        this ITypeFilterer typeFilterer,
        Predicate<Type> predicate)
    {
        ArgumentNullException.ThrowIfNull(typeFilterer);
        ArgumentNullException.ThrowIfNull(predicate);

        return UsingOnlyAs(
            typeFilterer,
            predicate,
            TypeFiltererLifetime.Singleton);
    }

    private static ITypeFilterer UsingOnlyAs(
        ITypeFilterer typeFilterer,
        Predicate<Type> lifetimeMatch,
        TypeFiltererLifetime lifetime)
    {
        // an excluded type must never be pulled back in by a lifetime override,
        // regardless of the order the exclusion and the override were chained in
        bool IsOverridden(Type t) => lifetimeMatch(t) && !typeFilterer.IsTypeExcluded(t);

        bool Include(Predicate<Type> filter, Type t) => filter(t) || IsOverridden(t);
        bool Exclude(Predicate<Type> filter, Type t) => filter(t) && !IsOverridden(t);

        return lifetime switch
        {
            TypeFiltererLifetime.Scoped => new TypeFilterDecorator(
                typeFilterer,
                Include,
                Exclude,
                Exclude),
            TypeFiltererLifetime.Transient => new TypeFilterDecorator(
                typeFilterer,
                Exclude,
                Include,
                Exclude),
            TypeFiltererLifetime.Singleton => new TypeFilterDecorator(
                typeFilterer,
                Exclude,
                Exclude,
                Include),
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                $"Unsupported lifetime '{lifetime}'."),
        };
    }
}
