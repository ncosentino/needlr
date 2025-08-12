using NexusLabs.Needlr;

/// <summary>
/// This is a decorator for the <see cref="IMyService"/> interface.
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