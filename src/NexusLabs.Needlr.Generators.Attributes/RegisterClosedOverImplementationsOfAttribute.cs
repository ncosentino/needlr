using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks an open generic <em>composition</em> class so that the source generator registers a closed
/// instance of it for <strong>each</strong> discovered concrete implementation of a designated
/// open generic interface, exposed as a designated non-generic (or less-generic) facade service type.
/// </summary>
/// <remarks>
/// <para>
/// This is a source-generation only feature. It requires the <c>NexusLabs.Needlr.Generators</c>
/// package and has no effect when using reflection-based registration.
/// </para>
/// <para>
/// Use this attribute to keep a "compose-and-expose" pattern on the source-generation path instead of
/// hand-maintaining one registration line per type argument (which silently drifts) or falling back to
/// runtime reflection (which is AOT/trimming hostile). For every concrete closed implementation of the
/// designated open generic interface that the generator discovers, it closes this composition class over
/// the <em>same</em> type argument(s) and registers it as the service type named by <see cref="As"/>,
/// resolving the composition's constructor dependencies from the service provider.
/// </para>
/// <para>
/// The annotated class must:
/// <list type="bullet">
/// <item><description>Be an open generic class whose type-parameter arity matches the open generic interface.</description></item>
/// <item><description>Implement the service type specified by <see cref="As"/>.</description></item>
/// <item><description>Have a single public constructor whose parameters are all resolvable from the service provider.</description></item>
/// </list>
/// </para>
/// <para>
/// A discovered type argument that does not satisfy the composition class's generic constraints is
/// skipped with a build diagnostic (<c>NDLRGEN038</c>) rather than producing a runtime failure.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Auto-discovered today: concrete, closed, unattributed.
/// public interface IFooDefinition&lt;TData&gt; where TData : class
/// {
///     string Discriminator { get; }
/// }
///
/// public sealed class AlphaFoo : IFooDefinition&lt;AlphaData&gt; { /* ... */ }
/// public sealed class BetaFoo  : IFooDefinition&lt;BetaData&gt;  { /* ... */ }
///
/// // Non-generic facade consumed as IEnumerable&lt;IFoo&gt;.
/// public interface IFoo { string Discriminator { get; } }
///
/// // Reusable composition closed per discovered TData and exposed as IFoo.
/// [RegisterClosedOverImplementationsOf(typeof(IFooDefinition&lt;&gt;), As = typeof(IFoo))]
/// public sealed class FooCore&lt;TData&gt; : IFoo where TData : class
/// {
///     public FooCore(IFooDefinition&lt;TData&gt; definition, IFooStore&lt;TData&gt; store) { /* ... */ }
///     public string Discriminator =&gt; /* delegates to definition */ "";
/// }
///
/// // Generator emits, per discovered implementation:
/// // services.AddSingleton&lt;IFoo&gt;(sp =&gt; new FooCore&lt;AlphaData&gt;(
/// //     sp.GetRequiredService&lt;IFooDefinition&lt;AlphaData&gt;&gt;(),
/// //     sp.GetRequiredService&lt;IFooStore&lt;AlphaData&gt;&gt;()));
/// // services.AddSingleton&lt;IFoo&gt;(sp =&gt; new FooCore&lt;BetaData&gt;( ... ));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterClosedOverImplementationsOfAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterClosedOverImplementationsOfAttribute"/> class.
    /// </summary>
    /// <param name="sourceOpenGenericInterface">
    /// The open generic interface whose concrete closed implementations drive registration
    /// (e.g., <c>typeof(IFooDefinition&lt;&gt;)</c>). Must be an open generic interface.
    /// </param>
    public RegisterClosedOverImplementationsOfAttribute(Type sourceOpenGenericInterface)
    {
        SourceOpenGenericInterface = sourceOpenGenericInterface;
    }

    /// <summary>
    /// Gets the open generic interface whose concrete closed implementations drive registration.
    /// </summary>
    public Type SourceOpenGenericInterface { get; }

    /// <summary>
    /// Gets or sets the service type that each closed composition is registered as
    /// (e.g., <c>typeof(IFoo)</c>). Must be a type implemented by the annotated composition class.
    /// </summary>
    public Type? As { get; set; }

    /// <summary>
    /// Gets or sets the lifetime each closed composition registration is given.
    /// Defaults to <see cref="InjectableLifetime.Singleton"/>.
    /// </summary>
    public InjectableLifetime Lifetime { get; set; } = InjectableLifetime.Singleton;
}
