namespace NexusLabs.Needlr;

/// <summary>
/// Used to mark a class that will not be automatically registered via the 
/// Needlr dependency injection system.
/// </summary>
/// <remarks>
/// Use this when a type doesn't make sense to resolve automatically via 
/// dependency injection or it has an alternative registration mechanism
/// that you don't want to be handled by Needlr.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DoNotAutoRegisterAttribute : Attribute
{
}
