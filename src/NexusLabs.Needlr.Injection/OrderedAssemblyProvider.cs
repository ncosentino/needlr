using System.Reflection;

using NexusLabs.Needlr.Injection.AssemblyOrdering;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// An assembly provider that wraps another provider and applies ordering to the results.
/// </summary>
[DoNotAutoRegister]
internal sealed class OrderedAssemblyProvider : IAssemblyProvider
{
    private readonly IAssemblyProvider _inner;
    private readonly AssemblyOrderBuilder _orderBuilder;
    private readonly Lazy<IReadOnlyList<Assembly>> _lazyAssemblies;

    public OrderedAssemblyProvider(
        IAssemblyProvider inner,
        AssemblyOrderBuilder orderBuilder)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(orderBuilder);

        _inner = inner;
        _orderBuilder = orderBuilder;
        _lazyAssemblies = new Lazy<IReadOnlyList<Assembly>>(() =>
        {
            var assemblies = _inner.GetCandidateAssemblies();
            return _orderBuilder.Sort(assemblies);
        });
    }

    public IReadOnlyList<Assembly> GetCandidateAssemblies() => _lazyAssemblies.Value;
}
