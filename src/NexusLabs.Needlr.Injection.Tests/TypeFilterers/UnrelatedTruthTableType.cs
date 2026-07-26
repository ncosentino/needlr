using NexusLabs.Needlr;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

/// <summary>
/// A type that is not assignable to <see cref="ITruthTableTarget"/>.
/// </summary>
[DoNotAutoRegister]
public sealed class UnrelatedTruthTableType
{
}
