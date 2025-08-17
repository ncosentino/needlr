namespace NexusLabs.Needlr.IntegrationTests;

public interface IMyManualService
{
    void DoSomething();
}

public interface IMyManualService2
{
    void DoSomething();
}

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

[DoNotAutoRegister]
public sealed class MyManualService : 
    IMyManualService, 
    IMyManualService2
{
    public void DoSomething()
    {
    }
}

[DoNotAutoRegister]
public sealed class MyManualDecorator(
    IMyManualService _wrapped) :
    IMyManualService
{
    public void DoSomething()
    {
        _wrapped.DoSomething();
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

public interface ITestServiceForDecoration
{
    string DoSomething();
}

[DoNotAutoRegister]
public sealed class TestServiceToBeDecorated : ITestServiceForDecoration
{
    public string DoSomething()
    {
        return "Original";
    }
}

[DoNotAutoRegister]
public sealed class TestServiceDecorator : ITestServiceForDecoration
{
    private readonly ITestServiceForDecoration _wrapped;

    public TestServiceDecorator(ITestServiceForDecoration wrapped)
    {
        _wrapped = wrapped;
    }

    public string DoSomething()
    {
        return $"Decorated: {_wrapped.DoSomething()}";
    }
}