using System;

namespace NexusLabs.Needlr.Avalonia;

/// <summary>
/// Generates a parameterless constructor for Avalonia design-time XAML preview support.
/// </summary>
/// <remarks>
/// <para>
/// Apply this to a <c>partial</c> class that has a constructor with injectable
/// parameters (the DI constructor). The source generator emits a parameterless
/// constructor that:
/// </para>
/// <list type="bullet">
/// <item>Suppresses CS8618 (non-nullable fields not initialized) via <c>#pragma</c></item>
/// <item>Throws <see cref="InvalidOperationException"/> if called at runtime
///   outside Avalonia's design mode</item>
/// <item>Calls <c>InitializeComponent()</c> for the XAML previewer</item>
/// </list>
/// <para>
/// At runtime, Needlr's richest-constructor selection ensures the DI constructor
/// is used. The generated parameterless constructor exists solely for the Avalonia
/// XAML designer/previewer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateAvaloniaDesignTimeConstructor]
/// public sealed partial class MainWindow : Window
/// {
///     private readonly ShellViewModel _viewModel;
///
///     public MainWindow(ShellViewModel viewModel)
///     {
///         _viewModel = viewModel;
///         InitializeComponent();
///         DataContext = viewModel;
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateAvaloniaDesignTimeConstructorAttribute : Attribute
{
}
