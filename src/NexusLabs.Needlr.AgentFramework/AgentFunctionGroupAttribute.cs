namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Assigns a class to a named function group so that it can be wired to agents by group name
/// rather than by explicit type reference.
/// </summary>
/// <remarks>
/// Apply this attribute to classes that contain methods decorated with
/// <see cref="AgentFunctionAttribute"/>. Register groups using
/// <c>AddAgentFunctionGroupsFromAssemblies()</c> or <c>AddAgentFunctionGroupsFromGenerated()</c>,
/// then reference them in <see cref="AgentFactoryOptions.FunctionGroups"/> at agent creation time.
/// <para>
/// A class may belong to multiple groups by applying this attribute more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [AgentFunctionGroup("research")]
/// public class GeographyFunctions { ... }
///
/// [AgentFunctionGroup("research")]
/// [AgentFunctionGroup("general")]
/// public class FactFunctions { ... }
///
/// // At agent creation:
/// agentFactory.CreateAgent(opts => opts.FunctionGroups = ["research"]);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AgentFunctionGroupAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="AgentFunctionGroupAttribute"/>.
    /// </summary>
    /// <param name="groupName">The name of the group this class belongs to.</param>
    public AgentFunctionGroupAttribute(string groupName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        GroupName = groupName;
    }

    /// <summary>
    /// Gets the name of the group this class belongs to.
    /// </summary>
    public string GroupName { get; }
}
