<#
.SYNOPSIS
    Validates Needlr CI scope classification.
#>

$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'get-ci-scope.ps1'
$previousGitHubOutput = $env:GITHUB_OUTPUT

function Assert-Scope {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string[]]$ChangedPaths,
        [Parameter(Mandatory = $true)][bool]$ExpectedSource,
        [Parameter(Mandatory = $true)][bool]$ExpectedMarketplace,
        [switch]$ForceAll
    )

    $arguments = @{
        ChangedPaths = $ChangedPaths
        ForceAll = $ForceAll
        NoCiOutput = $true
    }
    $result = (& $scriptPath @arguments | ConvertFrom-Json)

    if ($result.source_required -ne $ExpectedSource -or
        $result.marketplace_required -ne $ExpectedMarketplace) {
        throw "$Name failed. Expected source=$ExpectedSource marketplace=$ExpectedMarketplace; actual source=$($result.source_required) marketplace=$($result.marketplace_required)."
    }

    Write-Host "PASS: $Name"
}

try {
    Remove-Item Env:GITHUB_OUTPUT -ErrorAction SilentlyContinue

    Assert-Scope `
        -Name 'Agent profile only' `
        -ChangedPaths @('agents/application.agent.md') `
        -ExpectedSource $false `
        -ExpectedMarketplace $true

    Assert-Scope `
        -Name 'Plugin manifests and shared skill' `
        -ChangedPaths @(
            '.claude-plugin/marketplace.json',
            'plugin.json',
            'skills/needlr-research/SKILL.md') `
        -ExpectedSource $false `
        -ExpectedMarketplace $true

    Assert-Scope `
        -Name 'Future agent eval only' `
        -ChangedPaths @('evals/agents/routing.eval.ts') `
        -ExpectedSource $false `
        -ExpectedMarketplace $true

    Assert-Scope `
        -Name 'Source only' `
        -ChangedPaths @('src/NexusLabs.Needlr/Syringe.cs') `
        -ExpectedSource $true `
        -ExpectedMarketplace $false

    Assert-Scope `
        -Name 'CI infrastructure fails into source validation' `
        -ChangedPaths @('.github/workflows/ci.yml', 'scripts/get-ci-scope.ps1') `
        -ExpectedSource $true `
        -ExpectedMarketplace $false

    Assert-Scope `
        -Name 'Mixed source and marketplace change' `
        -ChangedPaths @(
            'agents/source-generation.agent.md',
            'src/NexusLabs.Needlr.Generators/TypeRegistryGenerator.cs') `
        -ExpectedSource $true `
        -ExpectedMarketplace $true

    Assert-Scope `
        -Name 'Manual full validation' `
        -ChangedPaths @() `
        -ExpectedSource $true `
        -ExpectedMarketplace $true `
        -ForceAll

    Write-Host 'CI scope validation passed.' -ForegroundColor Green
} finally {
    if ($null -eq $previousGitHubOutput) {
        Remove-Item Env:GITHUB_OUTPUT -ErrorAction SilentlyContinue
    } else {
        $env:GITHUB_OUTPUT = $previousGitHubOutput
    }
}
