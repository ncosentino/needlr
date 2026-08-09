---
applyTo: "**/*.csproj"
---

# Project structure

- Production feature projects are included by the bootstrap/composition project and
  appear in the solution's feature folder. Test projects remain sibling projects and
  are not bootstrapped into production.
- Feature projects do not reference sibling features; route cross-feature contracts
  through the SDK layer. A specialized implementation may reference its parent feature.
- Split a third-party dependency behind an abstraction when it is interchangeable,
  must be mocked without booting it, has licensing/availability risk, or carries
  native/external runtime baggage.
- Name contracts `<Owner>.Abstractions` and implementations
  `<Root>.Adapters.<Vendor>`. Do not insert a capability segment after `Adapters`.
- Only applications/bootstrap aggregators, tests, and benchmarks reference adapters.
  Owners and adapters reference the abstraction, never each other.
- `Missing*` fallbacks live in the owner and throw a concrete setup error naming the
  adapter/configuration required.
- Testable feature projects declare `InternalsVisibleTo` with project
  `AssemblyAttribute` items for `.Tests`, `.Tests.Unit`, `.Tests.Functional`, and Moq's
  signed `DynamicProxyGenAssembly2`.
- `Directory.Packages.props` owns versions. Project `PackageReference` items do not use
  inline `Version`.
