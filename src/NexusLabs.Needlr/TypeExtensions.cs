namespace NexusLabs.Needlr;

public static class TypeExtensions
{
    public static bool IsStatic(this Type type)
    {
        return type.IsAbstract && type.IsSealed;
    }
}