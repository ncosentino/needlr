using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 4 -- the <see cref="ConstructorGuardKind.NotNullOrEmpty"/> built-in
/// string guard, distinct from <see cref="ConstructorGuardKind.NotNullOrWhiteSpace"/>
/// demonstrated by <see cref="TenantService"/>: an empty string fails
/// <c>NotNullOrEmpty</c>, but only whitespace-only or empty strings fail
/// <c>NotNullOrWhiteSpace</c>.
/// </summary>
[GenerateConstructor]
public partial class ProductCatalogEntry
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrEmpty)]
    private readonly string _productCode;

    /// <summary>The product code supplied to the generated constructor.</summary>
    public string ProductCode => _productCode;
}
