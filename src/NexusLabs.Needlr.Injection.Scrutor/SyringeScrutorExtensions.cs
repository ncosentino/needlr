namespace NexusLabs.Needlr.Injection.Scrutor;

/// <summary>
/// Extension methods for configuring <see cref="Syringe"/> instances with Scrutor-specific functionality.
/// </summary>
public static class SyringeScrutorExtensions
{
    /// <summary>
    /// Configures the syringe to use the Scrutor type registrar.
    /// </summary>
    /// <param name="syringe">The syringe to configure.</param>
    /// <returns>A new configured syringe instance.</returns>
    public static Syringe UsingScrutorTypeRegistrar(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingTypeRegistrar(new ScrutorTypeRegistrar());
    }
}