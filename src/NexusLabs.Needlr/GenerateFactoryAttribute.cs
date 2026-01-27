namespace NexusLabs.Needlr;

/// <summary>
/// Generates a factory for this type, allowing runtime parameters to be 
/// specified while auto-injecting the rest from the service provider.
/// </summary>
/// <remarks>
/// <para>
/// This is a source-generation only feature. Reflection mode ignores this attribute.
/// </para>
/// <para>
/// When applied to a class with mixed injectable and non-injectable constructor parameters,
/// the generator will create:
/// <list type="bullet">
/// <item><description>A <c>Func&lt;TRuntime..., TService&gt;</c> that takes only the non-injectable parameters</description></item>
/// <item><description>An <c>I{TypeName}Factory</c> interface with a <c>Create()</c> method</description></item>
/// </list>
/// </para>
/// <para>
/// The type itself is NOT registered in the container - only the factory is.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateFactory]
/// public class MyService : IMyService
/// {
///     public MyService(IDependency dep, string connectionString) { }
/// }
/// 
/// // Generated: IMyServiceFactory with Create(string connectionString)
/// // Generated: Func&lt;string, MyService&gt;
/// // Consumer can inject either and create instances with runtime params
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateFactoryAttribute : Attribute
{
    /// <summary>
    /// Controls what factory artifacts are generated.
    /// Default: <see cref="FactoryGenerationMode.All"/>
    /// </summary>
    public FactoryGenerationMode Mode { get; set; } = FactoryGenerationMode.All;
}
