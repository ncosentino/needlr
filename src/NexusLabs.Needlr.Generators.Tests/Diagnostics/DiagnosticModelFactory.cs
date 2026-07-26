using System;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// Creates discovery models for diagnostic artifact tests without running a full compilation.
/// </summary>
internal static class DiagnosticModelFactory
{
    public static DiscoveredType Type(string typeName)
    {
        return Type(typeName, GeneratorLifetime.Singleton, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
    }

    public static DiscoveredType Type(string typeName, string[] interfaceNames)
    {
        return Type(typeName, GeneratorLifetime.Singleton, interfaceNames, Array.Empty<string>(), Array.Empty<string>());
    }

    public static DiscoveredType Type(
        string typeName,
        GeneratorLifetime lifetime,
        string[] interfaceNames,
        string[] dependencies,
        string[] serviceKeys)
    {
        var parameters = new TypeDiscoveryHelper.ConstructorParameterInfo[dependencies.Length];
        for (var i = 0; i < dependencies.Length; i++)
        {
            parameters[i] = new TypeDiscoveryHelper.ConstructorParameterInfo(dependencies[i]);
        }

        return new DiscoveredType(
            typeName,
            interfaceNames,
            "TestAssembly",
            lifetime,
            parameters,
            serviceKeys);
    }

    public static DiscoveredDecorator Decorator(string decoratorTypeName, string serviceTypeName)
    {
        return Decorator(decoratorTypeName, serviceTypeName, 0);
    }

    public static DiscoveredDecorator Decorator(string decoratorTypeName, string serviceTypeName, int order)
    {
        return new DiscoveredDecorator(decoratorTypeName, serviceTypeName, order, "TestAssembly");
    }

    public static DiscoveredFactory Factory(string typeName)
    {
        return Factory(typeName, null);
    }

    public static DiscoveredFactory Factory(string typeName, string? returnTypeName)
    {
        return new DiscoveredFactory(
            typeName,
            Array.Empty<string>(),
            "TestAssembly",
            3,
            Array.Empty<FactoryDiscoveryHelper.FactoryConstructorInfo>(),
            returnTypeName);
    }

    public static DiscoveredPlugin Plugin(string typeName)
    {
        return Plugin(typeName, Array.Empty<string>(), 0);
    }

    public static DiscoveredPlugin Plugin(string typeName, string[] interfaceNames, int order)
    {
        return new DiscoveredPlugin(
            typeName,
            interfaceNames,
            "TestAssembly",
            Array.Empty<string>(),
            null,
            order);
    }

    public static DiscoveredInterceptedService Intercepted(string typeName, string[] interceptorTypeNames)
    {
        return Intercepted(typeName, Array.Empty<string>(), interceptorTypeNames);
    }

    public static DiscoveredInterceptedService Intercepted(
        string typeName,
        string[] interfaceNames,
        string[] interceptorTypeNames)
    {
        return new DiscoveredInterceptedService(
            typeName,
            interfaceNames,
            "TestAssembly",
            GeneratorLifetime.Singleton,
            Array.Empty<InterceptorDiscoveryHelper.InterceptedMethodInfo>(),
            interceptorTypeNames);
    }

    public static DiscoveredHostedService HostedService(string typeName)
    {
        return new DiscoveredHostedService(
            typeName,
            "TestAssembly",
            GeneratorLifetime.Singleton,
            Array.Empty<TypeDiscoveryHelper.ConstructorParameterInfo>());
    }

    public static DiscoveredOptions OptionsType(string typeName, string sectionName)
    {
        return new DiscoveredOptions(typeName, sectionName, null, false, "TestAssembly");
    }

    public static DiscoveredOptions NamedOptions(string typeName, string sectionName, string name)
    {
        return new DiscoveredOptions(typeName, sectionName, name, false, "TestAssembly");
    }

    public static DiscoveredOptions OptionsWithExternalValidator(
        string typeName,
        string sectionName,
        string validatorTypeName,
        bool validateOnStart)
    {
        return new DiscoveredOptions(
            typeName,
            sectionName,
            null,
            validateOnStart,
            "TestAssembly",
            sourceFilePath: null,
            validatorMethod: null,
            validateMethodOverride: null,
            validatorTypeName: validatorTypeName);
    }

    public static DiscoveredOptions OptionsWithValidatorMethod(
        string typeName,
        string sectionName,
        string methodName,
        bool isStatic)
    {
        return new DiscoveredOptions(
            typeName,
            sectionName,
            null,
            true,
            "TestAssembly",
            sourceFilePath: null,
            validatorMethod: new OptionsValidatorInfo(methodName, isStatic, false));
    }

    public static DiscoveredOptions OptionsWithMissingValidatorMethod(
        string typeName,
        string sectionName,
        string methodName)
    {
        return new DiscoveredOptions(
            typeName,
            sectionName,
            null,
            true,
            "TestAssembly",
            sourceFilePath: null,
            validatorMethod: null,
            validateMethodOverride: methodName);
    }

    public static DiagnosticTypeInfo PluginType(string fullName, string shortName)
    {
        return PluginType(fullName, shortName, Array.Empty<string>(), false, false, false);
    }

    public static DiagnosticTypeInfo PluginType(
        string fullName,
        string shortName,
        string[] interfaces,
        bool isDecorator,
        bool hasFactory,
        bool hasInterceptorProxy)
    {
        return new DiagnosticTypeInfo(
            fullName,
            shortName,
            GeneratorLifetime.Singleton,
            interfaces,
            Array.Empty<string>(),
            isDecorator,
            false,
            hasFactory,
            null,
            false,
            hasInterceptorProxy);
    }
}
