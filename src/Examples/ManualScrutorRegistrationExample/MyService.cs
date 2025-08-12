using NexusLabs.Needlr;

/// <summary>
/// This is a service that will be registered manually,
/// so we need to tell the Needlr framework not to 
/// register it automatically.
/// </summary>
[DoNotAutoRegister]
public sealed class MyService : IMyService
{
    public void DoSomething()
    {
        Console.WriteLine("Hello, from Dev Leader!");
    }
}