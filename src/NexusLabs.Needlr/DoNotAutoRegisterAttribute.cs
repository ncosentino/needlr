namespace NexusLabs.Needlr;

/// <summary>
/// Prevents a class or interface from being automatically registered by Needlr's
/// dependency injection discovery pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute when a type should be discovered (scanned) but not registered
/// in the <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.
/// Common scenarios include:
/// </para>
/// <list type="bullet">
/// <item><description>Types with a custom or conditional registration strategy.</description></item>
/// <item><description>Abstract base classes that should not be resolved directly.</description></item>
/// <item><description>Types registered manually elsewhere in the composition root.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // This class is discovered during scanning but never added to the container
/// [DoNotAutoRegister]
/// public class InternalHelper : IHelper
/// {
///     // Registered manually or not at all
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DoNotAutoRegisterAttribute : Attribute
{
}