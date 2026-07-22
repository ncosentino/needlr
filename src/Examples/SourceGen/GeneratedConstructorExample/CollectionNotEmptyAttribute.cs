using System;

using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 8 -- an application-defined alias attribute. Decorating this attribute
/// type with <c>[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]</c>
/// lets fields use the domain-specific <c>[CollectionNotEmpty]</c> spelling
/// directly, instead of the more verbose
/// <c>[ConstructorGuard(typeof(CollectionNotEmptyGuard))]</c>. See
/// <see cref="AliasOrderService"/> for the field that uses it.
/// </summary>
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}
