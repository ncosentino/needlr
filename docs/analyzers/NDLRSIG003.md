# NDLRSIG003: IHubRegistrationPlugin implementation cannot use generated-constructor generation

## Cause

A class implementing `IHubRegistrationPlugin` is eligible for Needlr's generated-constructor generation -- it carries `[GenerateConstructor]`, or a field has a positive constructor guard trigger.

## Rule Description

Needlr's SignalR hub-registration generator activates every `IHubRegistrationPlugin` implementation with a **parameterless constructor**. A type eligible for generated-constructor generation has its implicit parameterless constructor replaced by a generated constructor that requires arguments, so the hub-registration generator deliberately excludes such a type from registration -- it is never activated, and its hub is never registered, with no other build-time signal that anything went wrong.

This analyzer closes that silent gap: it flags any `IHubRegistrationPlugin` implementation that is eligible for generated-constructor generation, so the mismatch is caught at compile time instead of surfacing as a hub that silently never registers at runtime.

## How to Fix

Remove `[GenerateConstructor]` and every field-level constructor guard trigger from the plugin, or add a hand-written parameterless constructor and perform any required setup another way (for example, resolving dependencies inside the hub registration method itself, or from a field default):

```csharp
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.SignalR;

// WRONG - NDLRSIG003: eligible for a generated constructor requiring IRepository
[GenerateConstructor]
public partial class ChatHubRegistrationPlugin : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public void Configure(HubRegistrationOptions options)
    {
        // ...
    }
}

// CORRECT - no generated-constructor trigger; parameterless activation still works
public sealed class ChatHubRegistrationPlugin : IHubRegistrationPlugin
{
    public void Configure(HubRegistrationOptions options)
    {
        // ...
    }
}
```

## See Also

- [NDLRGEN039](NDLRGEN039.md) - Generated-constructor type must be partial
- [NDLRGEN041](NDLRGEN041.md) - Generated-constructor conflicts with an explicit constructor
- [Generated Constructors](../generated-constructors.md)
