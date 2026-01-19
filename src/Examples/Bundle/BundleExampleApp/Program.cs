// ============================================================================
// Bundle Example Application
// ============================================================================
// This example demonstrates the NexusLabs.Needlr.Injection.Bundle package,
// which provides automatic fallback between source-generated and reflection-based
// dependency injection components.
//
// The Bundle package is ideal when you want:
// - Source-gen performance when available
// - Automatic reflection fallback for flexibility
// - Control over fallback behavior (logging, throwing, custom handling)
// ============================================================================

using BundleExamplePlugin;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Bundle;

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("Needlr Bundle Example - Demonstrating Auto-Configuration Features");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();

// ============================================================================
// Example 1: UsingAutoConfiguration()
// ============================================================================
// This is the simplest approach - Needlr automatically detects whether
// source-generated components are available and uses them, falling back
// to reflection if not.

Console.WriteLine("1. UsingAutoConfiguration()");
Console.WriteLine("-".PadRight(70, '-'));
Console.WriteLine("   Automatically detects source-gen vs reflection and configures accordingly.");
Console.WriteLine();

var provider1 = new Syringe()
    .UsingAutoConfiguration()
    .BuildServiceProvider();

var greeting1 = provider1.GetRequiredService<IGreetingService>();
Console.WriteLine($"   Result: {greeting1.Greet("Auto-Config User")}");
Console.WriteLine();

// ============================================================================
// Example 2: WithFallbackLogging()
// ============================================================================
// When reflection fallback occurs, this logs a warning message.
// Useful for monitoring/debugging which path is being used.

Console.WriteLine("2. WithFallbackLogging()");
Console.WriteLine("-".PadRight(70, '-'));
Console.WriteLine("   Logs a warning if reflection fallback is used.");
Console.WriteLine();

var provider2 = new Syringe()
    .WithFallbackLogging()
    .BuildServiceProvider();

var calculator = provider2.GetRequiredService<ICalculatorService>();
Console.WriteLine($"   Result: 5 + 3 = {calculator.Add(5, 3)}, 5 * 3 = {calculator.Multiply(5, 3)}");
Console.WriteLine();

// ============================================================================
// Example 3: WithFallbackBehavior() - Custom Handler
// ============================================================================
// Provide a custom handler that's called when reflection fallback occurs.
// You can log, collect metrics, or take any custom action.

Console.WriteLine("3. WithFallbackBehavior() - Custom Handler");
Console.WriteLine("-".PadRight(70, '-'));
Console.WriteLine("   Executes custom logic when reflection fallback occurs.");
Console.WriteLine();

var fallbackCount = 0;
var provider3 = new Syringe()
    .WithFallbackBehavior(context =>
    {
        fallbackCount++;
        Console.WriteLine($"   [Custom Handler] Fallback detected for: {context.ComponentName}");
        Console.WriteLine($"   [Custom Handler] Reason: {context.Reason}");
    })
    .BuildServiceProvider();

var time = provider3.GetRequiredService<ITimeService>();
Console.WriteLine($"   Result: Current time is {time.GetFormattedTime()}");
Console.WriteLine($"   Fallback count: {fallbackCount}");
Console.WriteLine();

// ============================================================================
// Example 4: Demonstrating Plugin Execution
// ============================================================================
// The Bundle approach works seamlessly with Needlr plugins.
// Plugins are discovered and executed regardless of source-gen or reflection.

Console.WriteLine("4. Plugin Execution");
Console.WriteLine("-".PadRight(70, '-'));
Console.WriteLine("   Plugins work seamlessly with the Bundle approach.");
Console.WriteLine();

var provider4 = new Syringe()
    .UsingAutoConfiguration()
    .BuildServiceProvider();

var welcomeMessage = provider4.GetRequiredService<WelcomeMessage>();
Console.WriteLine($"   Result: {welcomeMessage.Message}");
Console.WriteLine();

// ============================================================================
// Example 5: WithFastFailOnReflection()
// ============================================================================
// In AOT/trimming scenarios, you may want to ensure source-gen is ALWAYS used.
// This throws an exception if reflection would be needed.

Console.WriteLine("5. WithFastFailOnReflection()");
Console.WriteLine("-".PadRight(70, '-'));
Console.WriteLine("   Throws if source-gen is not available (for AOT scenarios).");
Console.WriteLine();

try
{
    // This will either succeed (if source-gen is available) or throw
    var provider5 = new Syringe()
        .WithFastFailOnReflection()
        .BuildServiceProvider();

    var greeting5 = provider5.GetRequiredService<IGreetingService>();
    Console.WriteLine($"   Result: {greeting5.Greet("Fast-Fail User")}");
    Console.WriteLine("   Source generation was available - no exception thrown.");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"   Expected exception: {ex.Message}");
    Console.WriteLine("   This is expected if source-gen bootstrap is not registered.");
}
Console.WriteLine();

// ============================================================================
// Summary
// ============================================================================

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("Summary of Bundle Extension Methods:");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();
Console.WriteLine("  UsingAutoConfiguration()   - Auto-detect and configure (recommended)");
Console.WriteLine("  WithFallbackLogging()      - Log warnings on reflection fallback");
Console.WriteLine("  WithFallbackBehavior(fn)   - Custom fallback handler");
Console.WriteLine("  WithFastFailOnReflection() - Throw if reflection would be used");
Console.WriteLine();
Console.WriteLine("The Bundle package is the recommended approach for most applications,");
Console.WriteLine("providing flexibility while enabling source-gen optimizations.");
Console.WriteLine();
