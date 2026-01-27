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
/// // Generated: IMyServiceFactory with Create(string connectionString) returning MyService
/// // Generated: Func&lt;string, MyService&gt;
/// // Consumer can inject either and create instances with runtime params
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateFactoryAttribute : Attribute
{
    /// <summary>
    /// Controls what factory artifacts are generated.
    /// Default: <see cref="FactoryGenerationMode.All"/>
    /// </summary>
    public FactoryGenerationMode Mode { get; set; } = FactoryGenerationMode.All;
}

/// <summary>
/// Generates a factory for this type that returns the specified interface type,
/// allowing runtime parameters to be specified while auto-injecting the rest from the service provider.
/// </summary>
/// <typeparam name="TInterface">
/// The interface type that the factory's <c>Create()</c> method will return.
/// Must be an interface implemented by the decorated class.
/// </typeparam>
/// <remarks>
/// <para>
/// This is a source-generation only feature. Reflection mode ignores this attribute.
/// </para>
/// <para>
/// Use this generic variant when you need the factory to return an interface type for:
/// <list type="bullet">
/// <item><description>Mocking the factory's return value in tests</description></item>
/// <item><description>Abstracting the concrete implementation from consumers</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateFactory&lt;IMyService&gt;]
/// public class MyService : IMyService
/// {
///     public MyService(IDependency dep, string connectionString) { }
/// }
/// 
/// // Generated: IMyServiceFactory with Create(string connectionString) returning IMyService
/// // Generated: Func&lt;string, IMyService&gt;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateFactoryAttribute<TInterface> : Attribute
    where TInterface : class
{
    /// <summary>
    /// Controls what factory artifacts are generated.
    /// Default: <see cref="FactoryGenerationMode.All"/>
    /// </summary>
    public FactoryGenerationMode Mode { get; set; } = FactoryGenerationMode.All;
}
