namespace NexusLabs.Needlr.AgentFramework.Generators.Models
{
    /// <summary>
    /// Describes a single property on the value type that the generated
    /// <c>[AsyncLocalScoped]</c> accessor should proxy through to
    /// <c>Current?.PropertyName</c>.
    /// </summary>
    internal readonly struct AsyncLocalScopedPropertyInfo
    {
        public AsyncLocalScopedPropertyInfo(
            string name,
            string typeFullName,
            bool hasSetter,
            bool isNonNullableValueType)
        {
            Name = name;
            TypeFullName = typeFullName;
            HasSetter = hasSetter;
            IsNonNullableValueType = isNonNullableValueType;
        }

        /// <summary>The property name (e.g., "Title").</summary>
        public string Name { get; }

        /// <summary>
        /// The fully-qualified type name of the property, using
        /// <c>global::</c> prefix (e.g., "global::System.String?").
        /// </summary>
        public string TypeFullName { get; }

        /// <summary>
        /// Whether the property has a setter on the value type interface,
        /// enabling a write-through proxy.
        /// </summary>
        public bool HasSetter { get; }

        /// <summary>
        /// Whether the property type is a non-nullable value type (e.g., <c>int</c>,
        /// <c>bool</c>). When <see langword="true"/>, the generated getter appends
        /// <c>?? default</c> to the null-conditional expression to coerce the
        /// <c>Nullable&lt;T&gt;</c> back to <c>T</c>.
        /// </summary>
        public bool IsNonNullableValueType { get; }
    }
}
