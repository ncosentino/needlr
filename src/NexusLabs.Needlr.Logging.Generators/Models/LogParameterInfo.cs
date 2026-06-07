namespace NexusLabs.Needlr.Logging.Generators.Models;

/// <summary>
/// A single parameter of a discovered <c>[NeedlrLoggerMessage]</c> method.
/// </summary>
internal readonly struct LogParameterInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogParameterInfo"/> struct.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="type">The fully-qualified parameter type (with <c>global::</c> prefix and nullable annotations).</param>
    /// <param name="role">How the parameter participates in the generated body.</param>
    public LogParameterInfo(string name, string type, ParameterRole role)
    {
        Name = name;
        Type = type;
        Role = role;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the fully-qualified parameter type.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the role the parameter plays in the generated logging body.
    /// </summary>
    public ParameterRole Role { get; }
}
