namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Classifies why a custom constructor guard method did not resolve.
/// </summary>
internal enum GuardResolutionFailureKind
{
    None,
    General,
    ForwardedArgument,
}
