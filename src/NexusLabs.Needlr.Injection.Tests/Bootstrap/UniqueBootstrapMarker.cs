namespace NexusLabs.Needlr.Injection.Tests.Bootstrap;

/// <summary>
/// Open generic marker used to fabricate distinct closed types for concurrent
/// source-generation bootstrap registration tests.
/// </summary>
/// <typeparam name="T">An arbitrary type argument used only to make the closed type unique.</typeparam>
public sealed class UniqueBootstrapMarker<T>
{
}
