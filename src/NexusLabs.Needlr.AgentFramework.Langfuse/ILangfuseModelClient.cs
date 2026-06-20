namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Registers model price definitions with Langfuse so it can compute cost for generations whose
/// model it does not price by default.
/// </summary>
public interface ILangfuseModelClient
{
    /// <summary>
    /// Gets a value indicating whether model registration is performed. <see langword="false"/>
    /// when Langfuse is not configured, in which case <see cref="EnsureModelPriceAsync"/> is a no-op.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Ensures a model definition with the given <see cref="LangfuseModelPrice.ModelName"/> exists,
    /// creating it only when absent. Safe to call on every run.
    /// </summary>
    /// <param name="price">The model price to ensure.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the model definition exists in Langfuse.</returns>
    Task EnsureModelPriceAsync(LangfuseModelPrice price, CancellationToken cancellationToken = default);
}
