namespace NexusLabs.Needlr;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DoNotInjectAttribute : Attribute
{
}
