using System;
using System.Collections.Generic;

namespace GeneratedConstructorExample;

/// <summary>
/// A custom constructor guard type with a forwarded positional argument. The
/// generator resolves and calls <see cref="Validate{T}"/> directly at compile
/// time as an ordinary static method call, passing the <c>minimum</c> value
/// forwarded from the <see cref="MinCountAttribute"/> alias usage's own
/// constructor argument -- never through reflection.
/// </summary>
public static class MinCountGuard
{
    /// <summary>Throws when <paramref name="value"/> is <see langword="null"/> or contains fewer than <paramref name="minimum"/> elements.</summary>
    /// <param name="value">The collection to validate.</param>
    /// <param name="minimum">The minimum number of elements <paramref name="value"/> must contain.</param>
    /// <param name="parameterName">The constructor parameter name to report.</param>
    public static void Validate<T>(IReadOnlyCollection<T>? value, int minimum, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Count < minimum)
        {
            throw new ArgumentException($"Must contain at least {minimum} element(s).", parameterName);
        }
    }
}
