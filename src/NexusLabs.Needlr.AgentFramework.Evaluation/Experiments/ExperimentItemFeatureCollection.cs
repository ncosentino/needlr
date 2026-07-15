using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides exact-type access to adapter-owned features for one experiment item scope.
/// </summary>
/// <remarks>
/// Feature objects remain valid for the item lifetime across repeated scope activation and
/// deactivation. Per-activation state must be resolved through the feature rather than captured
/// when the feature is registered.
/// </remarks>
public sealed class ExperimentItemFeatureCollection
{
    private readonly IReadOnlyDictionary<Type, object> _features;

    internal ExperimentItemFeatureCollection(IReadOnlyDictionary<Type, object> features)
    {
        _features = new Dictionary<Type, object>(features);
    }

    /// <summary>
    /// Attempts to get the feature registered for the exact requested type.
    /// </summary>
    /// <typeparam name="TFeature">The adapter-owned feature type.</typeparam>
    /// <param name="feature">The registered feature when found.</param>
    /// <returns><see langword="true"/> when the exact feature type is registered.</returns>
    public bool TryGet<TFeature>([NotNullWhen(true)] out TFeature? feature)
        where TFeature : class
    {
        if (_features.TryGetValue(typeof(TFeature), out var value))
        {
            feature = (TFeature)value;
            return true;
        }

        feature = null;
        return false;
    }

    /// <summary>
    /// Gets the feature registered for the exact requested type.
    /// </summary>
    /// <typeparam name="TFeature">The adapter-owned feature type.</typeparam>
    /// <returns>The registered feature.</returns>
    /// <exception cref="KeyNotFoundException">The exact feature type is not registered.</exception>
    public TFeature GetRequired<TFeature>()
        where TFeature : class =>
        TryGet<TFeature>(out var feature)
            ? feature
            : throw new KeyNotFoundException(
                $"Experiment item feature '{typeof(TFeature).FullName}' is not registered.");
}
