using System;

namespace NexusLabs.Needlr.AgentFramework.Generators.Models
{
    /// <summary>
    /// Discovered metadata for an interface decorated with [AsyncLocalScoped].
    /// </summary>
    internal readonly struct AsyncLocalScopedInfo
    {
        public AsyncLocalScopedInfo(
            string interfaceFullName,
            string interfaceName,
            string namespaceName,
            string valueTypeFullName,
            string scopeMethodName,
            bool hasScopeParameter,
            string scopeParameterTypeFullName,
            bool isMutable)
        {
            InterfaceFullName = interfaceFullName;
            InterfaceName = interfaceName;
            NamespaceName = namespaceName;
            ValueTypeFullName = valueTypeFullName;
            ScopeMethodName = scopeMethodName;
            HasScopeParameter = hasScopeParameter;
            ScopeParameterTypeFullName = scopeParameterTypeFullName;
            IsMutable = isMutable;
        }

        public string InterfaceFullName { get; }
        public string InterfaceName { get; }
        public string NamespaceName { get; }
        public string ValueTypeFullName { get; }
        public string ScopeMethodName { get; }
        public bool HasScopeParameter { get; }
        public string ScopeParameterTypeFullName { get; }
        public bool IsMutable { get; }

        public string GeneratedClassName
        {
            get
            {
                return InterfaceName.StartsWith("I", StringComparison.Ordinal) && InterfaceName.Length > 1
                    ? InterfaceName.Substring(1)
                    : InterfaceName + "Impl";
            }
        }
    }
}
