# Changelog

All notable changes to Needlr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2-alpha.1] - 2026-01-19

### Added
- AOT publish validation job to CI workflow for console and web apps
- Scrutor package tests with 10 tests for type registrar and extension methods

### Changed
- Changelog display in release script
- Stripping "Additional" Assemblies from Source Gen
- Lazy loading in GeneratedPluginFactory
- AspNet: No longer depends on Bundle
- Project reorganization: Explicit Reflection OR Source-Gen
- Source-Gen is now the default
- NeedlrSourceGenBootstrap: Migrating default behavior
- Removing some reflection fallback
- Trimmed AoT example
- Plugin Factory: Source Generation
- Injectable Lifetime from Generator
- Initial Source Generation: TypeRegistryGenerator

### Removed
- PluginFactory: Replaced with ReflectionPluginFactory (removed duplicate code)

### Fixed
- AddDecorator missing attribute

## [0.0.1-alpha.19] - 2026-01-18

Initial alpha releases with reflection-based architecture.
