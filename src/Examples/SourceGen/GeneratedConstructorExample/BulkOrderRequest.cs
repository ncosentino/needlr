using System.Collections.Generic;

namespace GeneratedConstructorExample;

/// <summary>
/// Uses the parameterized <see cref="MinCountAttribute"/> alias. The generated
/// call forwards the alias usage's own positional argument:
/// <c>MinCountGuard.Validate(lineItems, 3, nameof(lineItems));</c>.
/// </summary>
public partial class BulkOrderRequest
{
    [MinCount(3)]
    private readonly IReadOnlyCollection<string> _lineItems;

    /// <summary>The line items supplied to the generated constructor.</summary>
    public IReadOnlyCollection<string> LineItems => _lineItems;
}
