# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2-alpha.1] - 2026-01-19

### ⚠️ Breaking Changes
- Removed `IAssemblyProviderBuilder` interface

### Added
- Source-generation type providers (`GeneratedTypeProvider`, `GeneratedPluginProvider`)
- Module initializer bootstrap for zero-reflection startup
- Reflection-based type providers (`ReflectionTypeProvider`, `ReflectionPluginProvider`)
- Bundle package with auto-configuration (source-gen first, reflection fallback)
- Roslyn analyzers for common Needlr mistakes
- Source-generation support for SignalR hub registration
- Source-generation support for Semantic Kernel plugin discovery
- AOT/Trimming example applications
- Bundle auto-configuration example
- Parallel CI/CD with AOT publish validation
- Changelog generator agent skill

### Changed
- Source generation is now the default pattern (reflection is opt-in)
- ASP.NET package decoupled from Bundle (explicit strategy choice required)
- Simplified Syringe API with provider-based architecture

_(249 files changed, +13,355/-545 lines)_

## [0.0.1-alpha.19] - 2026-01-18

Initial alpha releases with reflection-based architecture.
