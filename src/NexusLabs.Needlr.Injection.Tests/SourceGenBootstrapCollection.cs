using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

/// <summary>
/// Serializes every test class that mutates the process-wide source-generation bootstrap state
/// so that registrations cannot leak across parallel or reordered execution.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SourceGenBootstrapCollection
{
    /// <summary>
    /// The collection name to apply with <see cref="CollectionAttribute"/>.
    /// </summary>
    public const string Name = "SourceGenBootstrapState";
}
