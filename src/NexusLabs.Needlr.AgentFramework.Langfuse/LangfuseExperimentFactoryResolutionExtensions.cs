namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Resolves built-in experiment capability interfaces implemented alongside
/// <see cref="ILangfuseClient"/>.
/// </summary>
internal static class LangfuseExperimentFactoryResolutionExtensions
{
    public static TFactory ResolveExperimentFactory<TFactory>(
        this object source,
        string message)
        where TFactory : class =>
        source as TFactory
        ?? throw new NotSupportedException(message);
}
