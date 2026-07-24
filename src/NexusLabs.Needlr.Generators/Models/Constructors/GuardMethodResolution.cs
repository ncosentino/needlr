namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// The result of resolving a direct custom constructor guard method.
/// </summary>
internal enum GuardMethodResolution
{
    Found,
    NotFound,
    Ambiguous,
}
