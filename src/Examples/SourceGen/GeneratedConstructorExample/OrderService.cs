using System.Collections.Generic;

using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 6 -- a direct custom guard type.
/// <c>[ConstructorGuard(typeof(CollectionNotEmptyGuard))]</c> alone triggers
/// constructor generation and resolves the conventional
/// <c>static void Validate(T, string)</c> method on
/// <see cref="CollectionNotEmptyGuard"/> at compile time.
/// </summary>
public partial class OrderService
{
    [ConstructorGuard(typeof(CollectionNotEmptyGuard))]
    private readonly IReadOnlyCollection<string> _orders;

    /// <summary>The orders supplied to the generated constructor.</summary>
    public IReadOnlyCollection<string> Orders => _orders;
}
