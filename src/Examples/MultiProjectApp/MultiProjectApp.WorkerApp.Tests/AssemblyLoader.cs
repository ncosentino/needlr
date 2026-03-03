using System.Runtime.CompilerServices;

namespace MultiProjectApp.WorkerApp.Tests;

/// <summary>
/// Forces <c>MultiProjectApp.WorkerApp</c> to load before any test runs.
/// </summary>
/// <remarks>
/// <para>
/// .NET loads assemblies lazily: a <c>&lt;ProjectReference&gt;</c> puts a DLL in the output
/// directory but does NOT cause it to load at runtime. An assembly only loads when the JIT
/// compiles code that directly references one of its types.
/// </para>
/// <para>
/// In this test project, no test method directly references a <c>MultiProjectApp.WorkerApp</c>
/// type (or the <c>MultiProjectApp.Features.Notifications</c> types it transitively pulls in).
/// The tests exercise those services only through the DI container built by
/// <c>Syringe.UsingSourceGen()</c>. Without this class, <c>WorkerApp.dll</c> would never
/// load, its <c>Generated.TypeRegistry</c> module initializer would never fire, and Needlr
/// would silently skip those registrations.
/// </para>
/// <para>
/// The <see cref="ModuleInitializerAttribute"/> guarantees that <see cref="Initialize"/> runs
/// when <em>this</em> test executable loads — before any test is discovered or run.
/// </para>
/// <para>
/// This pattern is the correct solution when a test project needs types from a referenced
/// assembly to be registered with Needlr's source-gen infrastructure, but does not naturally
/// reference any of that assembly's types in test code.
/// </para>
/// </remarks>
internal static class AssemblyLoader
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Force-load MultiProjectApp.WorkerApp to trigger its Generated.TypeRegistry module
        // initializer. WorkerApp directly references Notifications, so loading WorkerApp also
        // loads Notifications — both TypeRegistries fire, and their types become available to
        // UsingSourceGen() when tests build the service provider.
        _ = typeof(MultiProjectApp.WorkerApp.Generated.TypeRegistry).Assembly;
    }
}
