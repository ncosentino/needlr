namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// First interface for RegisterAs testing.
/// </summary>
public interface IRegisterAsReader
{
    string Read();
}

/// <summary>
/// Second interface for RegisterAs testing.
/// </summary>
public interface IRegisterAsWriter
{
    void Write(string data);
}

/// <summary>
/// Third interface that will NOT be registered when using [RegisterAs].
/// </summary>
public interface IRegisterAsLogger
{
    void Log(string message);
}

/// <summary>
/// Service that implements three interfaces but is only registered as one.
/// Using [RegisterAs&lt;IRegisterAsReader&gt;] means only IRegisterAsReader is resolvable,
/// not IRegisterAsWriter or IRegisterAsLogger.
/// </summary>
[RegisterAs<IRegisterAsReader>]
public sealed class SingleRegisterAsService : IRegisterAsReader, IRegisterAsWriter, IRegisterAsLogger
{
    public string Read() => "Read";
    public void Write(string data) { }
    public void Log(string message) { }
}

/// <summary>
/// Service registered as multiple specific interfaces (but not all).
/// [RegisterAs&lt;IRegisterAsReader&gt;] and [RegisterAs&lt;IRegisterAsWriter&gt;] means
/// both are resolvable, but NOT IRegisterAsLogger.
/// </summary>
[RegisterAs<IRegisterAsReader>]
[RegisterAs<IRegisterAsWriter>]
public sealed class MultipleRegisterAsService : IRegisterAsReader, IRegisterAsWriter, IRegisterAsLogger
{
    public string Read() => "MultiRead";
    public void Write(string data) { }
    public void Log(string message) { }
}

/// <summary>
/// Service with NO [RegisterAs] attribute - all interfaces should be registered.
/// Used as a control case.
/// </summary>
public sealed class NoRegisterAsService : IRegisterAsReader, IRegisterAsWriter
{
    public string Read() => "DefaultRead";
    public void Write(string data) { }
}

/// <summary>
/// Base interface for testing RegisterAs with interface hierarchies.
/// </summary>
public interface IRegisterAsBaseService
{
    string GetBase();
}

/// <summary>
/// Child interface that extends base.
/// </summary>
public interface IRegisterAsChildService : IRegisterAsBaseService
{
    string GetChild();
}

/// <summary>
/// Service implementing child interface, registered only as base.
/// The [RegisterAs&lt;IRegisterAsBaseService&gt;] means we register as base,
/// even though the class implements the child interface.
/// </summary>
[RegisterAs<IRegisterAsBaseService>]
public sealed class RegisterAsBaseOnlyService : IRegisterAsChildService
{
    public string GetBase() => "Base";
    public string GetChild() => "Child";
}
