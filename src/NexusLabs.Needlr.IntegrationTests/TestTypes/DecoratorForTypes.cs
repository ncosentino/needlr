namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Interface for testing [DecoratorFor&lt;T&gt;] attribute-based decorator discovery.
/// </summary>
public interface IDecoratorForTestService
{
    string GetValue();
}

/// <summary>
/// Base service implementation that will be decorated via [DecoratorFor&lt;T&gt;] attributes.
/// </summary>
public sealed class DecoratorForTestServiceImpl : IDecoratorForTestService
{
    public string GetValue() => "Original";
}

/// <summary>
/// First decorator using [DecoratorFor&lt;T&gt;] attribute with Order = 1.
/// Lower order decorators are applied first (closer to the original service).
/// </summary>
[DecoratorFor<IDecoratorForTestService>(Order = 1)]
public sealed class DecoratorForFirstDecorator : IDecoratorForTestService
{
    private readonly IDecoratorForTestService _inner;

    public DecoratorForFirstDecorator(IDecoratorForTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"First({_inner.GetValue()})";
}

/// <summary>
/// Second decorator using [DecoratorFor&lt;T&gt;] attribute with Order = 2.
/// Higher order decorators wrap lower order ones.
/// Expected chain: SecondDecorator -> FirstDecorator -> Original
/// </summary>
[DecoratorFor<IDecoratorForTestService>(Order = 2)]
public sealed class DecoratorForSecondDecorator : IDecoratorForTestService
{
    private readonly IDecoratorForTestService _inner;

    public DecoratorForSecondDecorator(IDecoratorForTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Second({_inner.GetValue()})";
}

/// <summary>
/// Third decorator with Order = 0 (applied first, closest to the original service).
/// </summary>
[DecoratorFor<IDecoratorForTestService>(Order = 0)]
public sealed class DecoratorForZeroOrderDecorator : IDecoratorForTestService
{
    private readonly IDecoratorForTestService _inner;

    public DecoratorForZeroOrderDecorator(IDecoratorForTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Zero({_inner.GetValue()})";
}

/// <summary>
/// Interface for testing decorator alongside manually registered decorator.
/// Note: This tests manual decorator wiring, not [DecoratorFor] attribute.
/// </summary>
public interface IManualAndAttributeDecoratorService
{
    string GetValue();
}

/// <summary>
/// Base service for manual + attribute decorator testing.
/// </summary>
[DoNotAutoRegister]
public sealed class ManualAndAttributeDecoratorServiceImpl : IManualAndAttributeDecoratorService
{
    public string GetValue() => "Base";
}

/// <summary>
/// Manually applied decorator (not using DecoratorFor attribute).
/// </summary>
[DoNotAutoRegister]
public sealed class ManualDecorator : IManualAndAttributeDecoratorService
{
    private readonly IManualAndAttributeDecoratorService _inner;

    public ManualDecorator(IManualAndAttributeDecoratorService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Manual({_inner.GetValue()})";
}

/// <summary>
/// Manually applied attribute-style decorator.
/// </summary>
[DoNotAutoRegister]
public sealed class AttributeDecorator : IManualAndAttributeDecoratorService
{
    private readonly IManualAndAttributeDecoratorService _inner;

    public AttributeDecorator(IManualAndAttributeDecoratorService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Attribute({_inner.GetValue()})";
}
