namespace NexusLabs.Needlr.Injection;

public static class TypeFiltering
{
    /// <summary>
    /// Determines if a type is a concrete type suitable for registration.
    /// This filters out abstract classes, interfaces, nested types, delegates, exceptions, attributes, etc.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a concrete type that can be instantiated.</returns>
    public static bool IsConcreteType(Type type) =>
        type.IsClass &&
        !type.IsAbstract &&
        !type.IsGenericTypeDefinition &&
        !type.IsFunctionPointer &&
        !type.IsValueType &&
        !type.IsInterface &&
        !type.IsCOMObject &&
        !type.IsEnum &&
        !type.IsNested &&
        !type.IsAssignableTo(typeof(MulticastDelegate)) &&
        !type.IsAssignableTo(typeof(Exception)) &&
        !type.IsAssignableTo(typeof(Attribute)) &&
        !IsRecord(type) &&
        type.Name[0] != '<' && type.Name[^1] != '>' //t.GetCustomAttribute<CompilerGeneratedAttribute>() is null
        ;

    /// <summary>
    /// Determines if a type is a record type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a record.</returns>
    public static bool IsRecord(Type type)
    {
        return type.IsClass &&
               type.GetMethod("<Clone>$") is not null;
    }
}
