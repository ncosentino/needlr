# Blazor extensibility

Needlr does not currently ship a Blazor-specific integration package. Blazor
applications can use the normal Needlr ASP.NET Core, hosting, and service-registration
packages, but component rendering and component discovery remain Blazor concerns.

Services consumed by components are ordinary DI services and can participate in
Needlr source generation. Needlr must not treat Razor components themselves as
automatic services merely because they are concrete classes.

A future Blazor integration requires an explicit package boundary, trimming and
render-mode coverage, documentation, and a clear distinction between service
registration and component composition.
