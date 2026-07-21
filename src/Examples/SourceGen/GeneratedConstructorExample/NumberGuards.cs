using System;

namespace GeneratedConstructorExample;

/// <summary>
/// A custom guard type whose validation method is selected by an explicit
/// method name via <see langword="nameof"/> rather than the conventional
/// <c>Validate</c> name. Used by <see cref="RetryPolicy"/>.
/// </summary>
public static class NumberGuards
{
    /// <summary>Throws when <paramref name="value"/> is not a positive number.</summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The constructor parameter name to report.</param>
    public static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be positive.");
        }
    }
}
