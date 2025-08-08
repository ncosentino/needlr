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
            t => !t.IsAssignableTo(typeof(T)),
            t => !t.IsAssignableTo(typeof(T)),
            t => !t.IsAssignableTo(typeof(T)));
    }
}