namespace BundleExamplePlugin;

/// <summary>
/// Interface for a greeting service.
/// </summary>
public interface IGreetingService
{
    string Greet(string name);
}

/// <summary>
/// A simple greeting service implementation.
/// </summary>
public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}

/// <summary>
/// Interface for a calculator service.
/// </summary>
public interface ICalculatorService
{
    int Add(int a, int b);
    int Multiply(int a, int b);
}

/// <summary>
/// A simple calculator service implementation.
/// </summary>
public sealed class CalculatorService : ICalculatorService
{
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}

/// <summary>
/// Interface for a time service.
/// </summary>
public interface ITimeService
{
    DateTime GetCurrentTime();
    string GetFormattedTime();
}

/// <summary>
/// A time service that provides current time information.
/// </summary>
public sealed class TimeService : ITimeService
{
    public DateTime GetCurrentTime() => DateTime.Now;
    public string GetFormattedTime() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
