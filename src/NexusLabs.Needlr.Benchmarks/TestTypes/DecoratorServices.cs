#pragma warning disable CS9113

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Benchmarks.TestTypes;

public interface IDecoratedService
{
    string Execute();
}

public sealed class DecoratedServiceImpl : IDecoratedService
{
    public string Execute() => "original";
}

[DecoratorFor<IDecoratedService>(Order = 1)]
public sealed class Decorator1(IDecoratedService inner) : IDecoratedService
{
    public string Execute() => $"D1({inner.Execute()})";
}

[DecoratorFor<IDecoratedService>(Order = 2)]
public sealed class Decorator2(IDecoratedService inner) : IDecoratedService
{
    public string Execute() => $"D2({inner.Execute()})";
}

[DecoratorFor<IDecoratedService>(Order = 3)]
public sealed class Decorator3(IDecoratedService inner) : IDecoratedService
{
    public string Execute() => $"D3({inner.Execute()})";
}

[DecoratorFor<IDecoratedService>(Order = 4)]
public sealed class Decorator4(IDecoratedService inner) : IDecoratedService
{
    public string Execute() => $"D4({inner.Execute()})";
}

[DecoratorFor<IDecoratedService>(Order = 5)]
public sealed class Decorator5(IDecoratedService inner) : IDecoratedService
{
    public string Execute() => $"D5({inner.Execute()})";
}
