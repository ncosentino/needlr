namespace NexusLabs.Needlr.Injection.Scrutor.Tests;

public interface IMyAutomaticService
{
    void DoSomething();
}

public interface IMyAutomaticService2
{
    void DoSomething();
}

public sealed class MyAutomaticService :
    IMyAutomaticService,
    IMyAutomaticService2
{
    public void DoSomething()
    {
    }
}

public interface IInterfaceWithMultipleImplementations
{
}

public sealed class ImplementationA : IInterfaceWithMultipleImplementations
{
}

public sealed class ImplementationB : IInterfaceWithMultipleImplementations
{
}
