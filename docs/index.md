# Needlr

![Needlr](assets/needlr.webp){ width="200" }

**Opinionated fluent dependency injection for .NET with source generation.**

[![CI](https://github.com/ncosentino/needlr/actions/workflows/ci.yml/badge.svg)](https://github.com/ncosentino/needlr/actions/workflows/ci.yml)
[![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/ncosentino/needlr/main/.github/badges/coverage.json)](https://ncosentino.github.io/needlr/coverage/)
[![NuGet](https://img.shields.io/nuget/v/NexusLabs.Needlr.svg)](https://www.nuget.org/packages/NexusLabs.Needlr)

## What is Needlr?

Needlr is a source-generation-first dependency injection library for .NET that provides automatic service registration through a simple, discoverable API. It's designed to minimize boilerplate code by automatically registering types from scanned assemblies.

!!! tip "Source Generation First"
    Needlr prioritizes compile-time source generation for AOT compatibility and optimal performance. Both source-gen (`.UsingSourceGen()`) and reflection (`.UsingReflection()`) require explicit opt-in—source-gen is recommended for most scenarios.

## Features

- **Source Generation First** - Compile-time type discovery for AOT/trimming compatibility
- **Automatic Service Discovery** - Automatically registers services from assemblies using conventions
- **Fluent API** - Chain-able configuration methods for clean, readable setup
- **ASP.NET Core Integration** - Seamless web application creation and configuration
- **Plugin System** - Extensible architecture for modular applications
- **Decorator Pattern Support** - Automatic decorator wiring with `[DecoratorFor<T>]` attribute
- **Analyzers & Diagnostics** - Catch DI issues at compile-time, not runtime

## Quick Start

### Installation

See the [Getting Started](getting-started.md) guide for full package requirements. Quick overview:

=== "Source Generation (Recommended)"

    ```xml
    <PackageReference Include="NexusLabs.Needlr.Injection" />
    <PackageReference Include="NexusLabs.Needlr.Injection.SourceGen" />
    <PackageReference Include="NexusLabs.Needlr.Generators" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <PackageReference Include="NexusLabs.Needlr.Generators.Attributes" />
    <!-- For ASP.NET Core: NexusLabs.Needlr.AspNet -->
    ```

=== "Reflection"

    ```xml
    <PackageReference Include="NexusLabs.Needlr.Injection" />
    <PackageReference Include="NexusLabs.Needlr.Injection.Reflection" />
    <!-- For ASP.NET Core: NexusLabs.Needlr.AspNet -->
    ```

### Source Generation (Recommended)

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

var app = new Syringe()
    .UsingSourceGen()
    .CreateWebApplication(args);

app.Run();
```

### Reflection

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

var app = new Syringe()
    .UsingReflection()
    .CreateWebApplication(args);

app.Run();
```

## Next Steps

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } **Getting Started**

    ---

    Step-by-step guide to set up Needlr in your project

    [:octicons-arrow-right-24: Getting Started](getting-started.md)

-   :material-book-open-variant:{ .lg .middle } **Core Concepts**

    ---

    Understand the architecture and design principles

    [:octicons-arrow-right-24: Core Concepts](core-concepts.md)

-   :material-cog:{ .lg .middle } **Features**

    ---

    Explore hosted services, keyed services, options, and more

    [:octicons-arrow-right-24: Hosted Services](hosted-services.md)

-   :material-alert-circle:{ .lg .middle } **Analyzers**

    ---

    Compile-time diagnostics to catch DI issues early

    [:octicons-arrow-right-24: Analyzers](analyzers/README.md)

</div>
