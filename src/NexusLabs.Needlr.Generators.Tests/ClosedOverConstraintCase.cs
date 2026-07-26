using System.Collections.Generic;

using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// The observable generator output for a single <c>[RegisterClosedOverImplementationsOf]</c>
/// constraint scenario: the emitted registration code plus the diagnostics the generator reported.
/// </summary>
internal sealed record ClosedOverConstraintCase(
    string GeneratedCode,
    IReadOnlyList<Diagnostic> Diagnostics);
