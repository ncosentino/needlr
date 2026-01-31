namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// A custom attribute for marking special plugins.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SpecialPluginAttribute : Attribute
{
    public string Category { get; }

    public SpecialPluginAttribute(string category = "default")
    {
        Category = category;
    }
}

/// <summary>
/// Another custom attribute for testing multiple attributes on a plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PriorityPluginAttribute : Attribute
{
    public int Priority { get; }

    public PriorityPluginAttribute(int priority)
    {
        Priority = priority;
    }
}

/// <summary>
/// A plugin marked with SpecialPluginAttribute.
/// </summary>
[SpecialPlugin("test")]
public sealed class SpecialTestPlugin : ITestPlugin
{
    public string Name => nameof(SpecialTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin marked with both SpecialPluginAttribute and PriorityPluginAttribute.
/// </summary>
[SpecialPlugin("multi")]
[PriorityPlugin(1)]
public sealed class MultiAttributeTestPlugin : ITestPlugin
{
    public string Name => nameof(MultiAttributeTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin with only PriorityPluginAttribute.
/// </summary>
[PriorityPlugin(2)]
public sealed class PriorityOnlyTestPlugin : ITestPlugin
{
    public string Name => nameof(PriorityOnlyTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin with no attributes (for negative testing).
/// </summary>
public sealed class NoAttributeTestPlugin : ITestPlugin
{
    public string Name => nameof(NoAttributeTestPlugin);
    public void Execute() { }
}

/// <summary>
/// Base class with SpecialPluginAttribute for testing inherited attributes.
/// </summary>
[SpecialPlugin("inherited")]
public abstract class SpecialPluginBase : ITestPlugin
{
    public abstract string Name { get; }
    public abstract void Execute();
}

/// <summary>
/// Derived class that should inherit SpecialPluginAttribute from its base.
/// </summary>
public sealed class InheritedAttributeTestPlugin : SpecialPluginBase
{
    public override string Name => nameof(InheritedAttributeTestPlugin);
    public override void Execute() { }
}
