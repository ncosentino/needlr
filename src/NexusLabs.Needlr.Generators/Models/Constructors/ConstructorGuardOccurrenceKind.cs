namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// The normalized shape of one constructor guard or exclusion attribute occurrence.
/// </summary>
internal enum ConstructorGuardOccurrenceKind
{
    Ignore,
    BuiltInNone,
    BuiltInPositive,
    CustomType,
    Alias,
}
