namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Marks a method as an agent function that can be auto-discovered by Needlr
/// and registered as an <see cref="Microsoft.Extensions.AI.AIFunction"/> tool
/// for Microsoft Agent Framework agents.
/// </summary>
/// <remarks>
/// Apply this attribute to public methods on a class or static class.
/// Needlr's scanners and source generator will discover all classes that
/// contain at least one method decorated with <c>[AgentFunction]</c> and
/// register them with the agent factory.
///
/// Use <see cref="System.ComponentModel.DescriptionAttribute"/> to provide
/// LLM-friendly descriptions for methods and parameters.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AgentFunctionAttribute : Attribute;
