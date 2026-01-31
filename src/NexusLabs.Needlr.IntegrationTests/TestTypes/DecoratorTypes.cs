namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Interface for testing decorator pattern auto-registration behavior.
/// </summary>
public interface IDecoratorTestService
{
    string GetValue();
}

/// <summary>
/// The "inner" implementation that should be registered as IDecoratorTestService.
/// </summary>
public sealed class DecoratorTestServiceImpl : IDecoratorTestService
{
    public string GetValue() => "Original";
}

/// <summary>
/// A decorator that implements IDecoratorTestService AND takes IDecoratorTestService
/// as a constructor parameter. This should be registered as itself only, NOT as
/// IDecoratorTestService, to avoid circular dependency issues.
/// </summary>
public sealed class DecoratorTestServiceDecorator : IDecoratorTestService
{
    private readonly IDecoratorTestService _inner;

    public DecoratorTestServiceDecorator(IDecoratorTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Decorated({_inner.GetValue()})";
}

/// <summary>
/// A second decorator to test multiple decorators don't cause issues.
/// </summary>
public sealed class DecoratorTestServiceSecondDecorator : IDecoratorTestService
{
    private readonly IDecoratorTestService _inner;

    public DecoratorTestServiceSecondDecorator(IDecoratorTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"SecondDecorated({_inner.GetValue()})";
}

/// <summary>
/// A class that takes an interface in the constructor but does NOT implement it.
/// This is NOT a decorator and should be registered normally with all its interfaces.
/// </summary>
public interface INonDecoratorTestService
{
    string Process();
}

public interface INonDecoratorDependency
{
    string GetData();
}

public sealed class NonDecoratorDependencyImpl : INonDecoratorDependency
{
    public string GetData() => "Data";
}

public sealed class NonDecoratorTestService : INonDecoratorTestService
{
    private readonly INonDecoratorDependency _dependency;

    public NonDecoratorTestService(INonDecoratorDependency dependency)
    {
        _dependency = dependency;
    }

    public string Process() => $"Processed: {_dependency.GetData()}";
}
