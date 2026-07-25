# Needlr Agent Marketplace

The Needlr repository is the source of truth for installable Needlr agents and
their shared research workflow.

## Install

```powershell
copilot plugin marketplace add ncosentino/needlr
copilot plugin install needlr@ncosentino-needlr
```

Restart or open a new Copilot CLI session, then use `/agent` to browse:

| Agent | Use for |
|---|---|
| `needlr:application` | Public DI APIs, application architecture, lifetimes, registrations, and consumer troubleshooting |
| `needlr:source-generation` | Needlr generators, analyzers, Build integration, AOT, service catalogs, and dependency graphs |
| `needlr:integrations` | ASP.NET Core, Carter, SignalR, hosting, logging, validation, Avalonia, and MAUI |

All three agents use the bundled `needlr-research` skill. It selects evidence
from the local Needlr checkout, a consumer's effective package version, or
current official web sources when adoption is still being planned.

## Local development

From the Needlr repository root:

```powershell
$marketplacePath = (Get-Location).Path
copilot plugin marketplace add $marketplacePath
copilot plugin install needlr@ncosentino-needlr
copilot plugin list
```

Copilot CLI caches installed plugins. Reinstall the plugin after changing an
agent, skill, or manifest. Remove the development registration when finished:

```powershell
copilot plugin uninstall needlr
copilot plugin marketplace remove ncosentino-needlr
```

## Evaluation status

The marketplace MVP does not include behavioral evaluations. Agent behavior is
not considered calibrated or gate-ready until a dedicated agent-only eval suite
is added.
