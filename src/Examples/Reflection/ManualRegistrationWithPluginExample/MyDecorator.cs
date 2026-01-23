using NexusLabs.Needlr;

/// <summary>
/// This is a decorator for the <see cref="IMyService"/> interface.
/// 
/// This example uses [DoNotAutoRegister] with manual plugin registration.
/// For simpler automatic wiring, use [DecoratorFor&lt;IMyService&gt;]:
/// 
/// <code>
/// [DecoratorFor&lt;IMyService&gt;(Order = 1)]
/// public sealed class MyDecorator(IMyService wrapped) : IMyService { ... }
/// </code>
/// </summary>
/// <param name="_wrapped">This will get provided by the dependency injection framework.</param>
[DoNotAutoRegister]
public sealed class MyDecorator(
    IMyService _wrapped) :
    IMyService
{
    public void DoSomething()
    {
        Console.WriteLine("---BEFORE---");
        _wrapped.DoSomething();
        Console.WriteLine("---AFTER---");
    }
}
