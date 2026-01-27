namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should only be registered as the specified interface type,
/// rather than all interfaces it implements.
/// </summary>
/// <typeparam name="TInterface">
/// The interface type to register as. Must be an interface implemented by the decorated class.
/// </typeparam>
/// <remarks>
/// <para>
/// By default, Needlr registers a class as all non-system interfaces it implements.
/// Use this attribute when you want explicit control over which interface(s) are publicly 
/// resolvable from the container.
/// </para>
/// <para>
/// When this attribute is present, the class will ONLY be registered as the specified interface(s)
/// and as itself (the concrete type). Other implemented interfaces will not be registered.
/// </para>
/// <para>
/// Multiple [RegisterAs&lt;T&gt;] attributes can be applied to register the class as multiple
/// specific interfaces while still excluding others.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public interface IReader { string Read(); }
/// public interface IWriter { void Write(string data); }
/// public interface ILogger { void Log(string message); }
/// 
/// // Only registered as IReader - not as IWriter or ILogger
/// [RegisterAs&lt;IReader&gt;]
/// public class FileService : IReader, IWriter, ILogger
/// {
///     public string Read() => "data";
///     public void Write(string data) { }
///     public void Log(string message) { }
/// }
/// 
/// // Register as multiple specific interfaces
/// [RegisterAs&lt;IReader&gt;]
/// [RegisterAs&lt;IWriter&gt;]
/// public class DualService : IReader, IWriter, ILogger
/// {
///     // Registered as IReader and IWriter, but NOT as ILogger
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class RegisterAsAttribute<TInterface> : Attribute
    where TInterface : class
{
    /// <summary>
    /// Gets the interface type that the class should be registered as.
    /// </summary>
    public Type InterfaceType => typeof(TInterface);
}
