using System;

using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 10 -- a parameterized application-defined alias attribute. Unlike
/// <see cref="CollectionNotEmptyAttribute"/> (which forwards zero arguments),
/// <c>[MinCount(3)]</c> forwards its own positional constructor argument (the
/// <c>3</c>) onto the resolved <see cref="MinCountGuard.Validate{T}"/> call, in
/// declared order, between the guarded value and the trailing <c>nameof</c>
/// parameter name. See <see cref="BulkOrderRequest"/> for the field that uses it.
/// </summary>
[ConstructorGuardDefinition(typeof(MinCountGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class MinCountAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MinCountAttribute"/> class.</summary>
    /// <param name="minimum">The minimum number of elements the guarded collection must contain.</param>
    public MinCountAttribute(int minimum)
    {
        Minimum = minimum;
    }

    /// <summary>The minimum number of elements the guarded collection must contain.</summary>
    public int Minimum { get; }
}
