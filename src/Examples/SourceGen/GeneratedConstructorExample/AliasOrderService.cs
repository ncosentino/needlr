using System.Collections.Generic;

namespace GeneratedConstructorExample;

/// <summary>
/// Uses the <see cref="CollectionNotEmptyAttribute"/> alias instead of a
/// direct <c>[ConstructorGuard(typeof(CollectionNotEmptyGuard))]</c>. Both
/// forms normalize into the same internal guard model, so the generated
/// constructor call is identical to <see cref="OrderService"/>'s.
/// </summary>
public partial class AliasOrderService
{
    [CollectionNotEmpty]
    private readonly IReadOnlyCollection<string> _orders;

    /// <summary>The orders supplied to the generated constructor.</summary>
    public IReadOnlyCollection<string> Orders => _orders;
}
