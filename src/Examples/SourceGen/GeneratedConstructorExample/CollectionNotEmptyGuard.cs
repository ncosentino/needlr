using System;
using System.Collections.Generic;

namespace GeneratedConstructorExample;

/// <summary>
/// A custom constructor guard type. The generator resolves and calls
/// <see cref="Validate{T}"/> directly at compile time as an ordinary static
/// method call -- never through reflection.
/// </summary>
public static class CollectionNotEmptyGuard
{
    /// <summary>Throws when <paramref name="value"/> is <see langword="null"/> or empty.</summary>
    /// <param name="value">The collection to validate.</param>
    /// <param name="parameterName">The constructor parameter name to report.</param>
    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Count == 0)
        {
            throw new ArgumentException("Collection must not be empty.", parameterName);
        }
    }
}
