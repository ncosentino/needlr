using System.Diagnostics.CodeAnalysis;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace NexusLabs.Needlr.FluentValidation;

/// <summary>
/// Extension methods for registering FluentValidation validators as options validators.
/// </summary>
public static class FluentValidationServiceCollectionExtensions
{
    /// <summary>
    /// Adds a FluentValidation validator as an options validator for the specified options type.
    /// </summary>
    /// <typeparam name="TOptions">The options type to validate.</typeparam>
    /// <typeparam name="TValidator">The FluentValidation validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers:
    /// <list type="bullet">
    /// <item>The validator as a singleton</item>
    /// <item>An <see cref="IValidateOptions{TOptions}"/> adapter that uses the validator</item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// services.AddFluentValidationOptionsAdapter&lt;DatabaseOptions, DatabaseOptionsValidator&gt;();
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddFluentValidationOptionsAdapter<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TValidator>(
        this IServiceCollection services)
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        services.TryAddSingleton<TValidator>();
        services.TryAddSingleton<IValidator<TOptions>>(sp => sp.GetRequiredService<TValidator>());
        services.AddSingleton<IValidateOptions<TOptions>>(sp =>
            new FluentValidationOptionsAdapter<TOptions>(sp.GetRequiredService<IValidator<TOptions>>()));

        return services;
    }

    /// <summary>
    /// Adds a FluentValidation validator as an options validator for named options.
    /// </summary>
    /// <typeparam name="TOptions">The options type to validate.</typeparam>
    /// <typeparam name="TValidator">The FluentValidation validator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The name of the options instance to validate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentValidationOptionsAdapter<TOptions, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TValidator>(
        this IServiceCollection services,
        string name)
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        services.TryAddSingleton<TValidator>();
        services.TryAddSingleton<IValidator<TOptions>>(sp => sp.GetRequiredService<TValidator>());
        services.AddSingleton<IValidateOptions<TOptions>>(sp =>
            new FluentValidationOptionsAdapter<TOptions>(sp.GetRequiredService<IValidator<TOptions>>(), name));

        return services;
    }

    /// <summary>
    /// Adds a FluentValidation validator instance as an options validator.
    /// </summary>
    /// <typeparam name="TOptions">The options type to validate.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="validator">The validator instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFluentValidationOptionsAdapter<TOptions>(
        this IServiceCollection services,
        IValidator<TOptions> validator)
        where TOptions : class
    {
        services.AddSingleton<IValidateOptions<TOptions>>(
            new FluentValidationOptionsAdapter<TOptions>(validator));

        return services;
    }
}
