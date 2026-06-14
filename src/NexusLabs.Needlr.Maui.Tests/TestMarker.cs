namespace NexusLabs.Needlr.Maui.Tests;

/// <summary>
/// A marker used to verify that <c>PopulateInto</c> preserves registrations already present on the
/// builder. It is excluded from auto-registration so the only registration is the one the test adds
/// manually, allowing an instance-identity assertion.
/// </summary>
[DoNotAutoRegister]
public sealed class TestMarker
{
}
