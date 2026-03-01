using Microsoft.Extensions.AI;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Provides pre-built <see cref="AIFunction"/> instances for agent function types.
/// Implemented by the source generator to eliminate reflection-based function discovery.
/// </summary>
public interface IAIFunctionProvider
{
    /// <summary>
    /// Attempts to retrieve the pre-built <see cref="AIFunction"/> instances for a given function type.
    /// </summary>
    /// <param name="functionType">The function class type to look up.</param>
    /// <param name="serviceProvider">
    /// The service provider used to resolve the function class instance when needed.
    /// The provider handles instance creation to avoid reflection-based activation.
    /// </param>
    /// <param name="functions">
    /// When this method returns <c>true</c>, contains the pre-built functions for the type.
    /// </param>
    /// <returns>
    /// <c>true</c> if the provider has pre-built functions for <paramref name="functionType"/>; otherwise <c>false</c>.
    /// </returns>
    bool TryGetFunctions(
        Type functionType,
        IServiceProvider serviceProvider,
        [NotNullWhen(true)] out IReadOnlyList<AIFunction>? functions);
}
