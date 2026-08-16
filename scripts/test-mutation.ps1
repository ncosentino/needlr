<#
.SYNOPSIS
    Validates Needlr's advisory mutation-testing contract.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolManifestPath = Join-Path $repoRoot '.config\dotnet-tools.json'
$runtimeConfigPath = Join-Path $PSScriptRoot 'mutation\stryker-runtime.json'
$generatorConfigPath = Join-Path $PSScriptRoot 'mutation\stryker-generators.json'
$runnerPath = Join-Path $PSScriptRoot 'run-mutation-tests.ps1'
$classifierPath = Join-Path $PSScriptRoot 'get-mutation-scope.ps1'
$workflowPath = Join-Path $repoRoot '.github\workflows\mutation-testing.yml'
$deliveryPath = Join-Path $repoRoot '.github\genesis-delivery.json'

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-PowerShellSyntax {
    param([Parameter(Mandatory)][string]$Path)

    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$parseErrors)
    Assert-Condition `
        -Condition ($parseErrors.Count -eq 0) `
        -Message "$Path has PowerShell parse errors: $($parseErrors.Message -join '; ')"
}

foreach ($path in @(
        $toolManifestPath,
        $runtimeConfigPath,
        $generatorConfigPath,
        $runnerPath,
        $classifierPath,
        $workflowPath,
        $deliveryPath)) {
    Assert-Condition `
        -Condition (Test-Path -LiteralPath $path -PathType Leaf) `
        -Message "Mutation-testing surface is missing: $path"
}

$toolManifest = Get-Content -LiteralPath $toolManifestPath -Raw | ConvertFrom-Json
$strykerTool = $toolManifest.tools.'dotnet-stryker'
Assert-Condition `
    -Condition ([string]$strykerTool.version -ceq '4.16.0') `
    -Message 'dotnet-stryker must be pinned to 4.16.0.'
Assert-Condition `
    -Condition ((@($strykerTool.commands) -join ',') -ceq 'dotnet-stryker') `
    -Message 'The local tool manifest must expose the dotnet-stryker command.'

foreach ($entry in @(
        [PSCustomObject]@{
            Name = 'runtime'
            Path = $runtimeConfigPath
            Project = 'NexusLabs.Needlr.csproj'
        },
        [PSCustomObject]@{
            Name = 'generators'
            Path = $generatorConfigPath
            Project = 'NexusLabs.Needlr.Generators.csproj'
        })) {
    $config = (
        Get-Content -LiteralPath $entry.Path -Raw |
            ConvertFrom-Json).'stryker-config'
    Assert-Condition `
        -Condition ([string]$config.project -ceq $entry.Project) `
        -Message "The $($entry.Name) scope targets the wrong project."
    Assert-Condition `
        -Condition ([string]$config.'test-runner' -ceq 'vstest') `
        -Message "The $($entry.Name) scope must use the stable VSTest runner."
    Assert-Condition `
        -Condition ([string]$config.'mutation-level' -ceq 'Standard') `
        -Message "The $($entry.Name) scope must use Standard mutation level."
    Assert-Condition `
        -Condition ([int]$config.thresholds.break -eq 0) `
        -Message "The $($entry.Name) scope must remain score-nonblocking."
    Assert-Condition `
        -Condition (
            @($config.reporters) -contains 'json' -and
            @($config.reporters) -contains 'markdown' -and
            @($config.reporters) -notcontains 'dashboard') `
        -Message "The $($entry.Name) scope must report locally without the dashboard."
    Assert-Condition `
        -Condition (@($config.mutate).Count -gt 0) `
        -Message "The $($entry.Name) scope must remain explicitly bounded."
}

Assert-PowerShellSyntax -Path $runnerPath
Assert-PowerShellSyntax -Path $classifierPath

$workflow = Get-Content -LiteralPath $workflowPath -Raw
Assert-Condition `
    -Condition (
        $workflow -match '(?m)^  pull_request:\r?$' -and
        $workflow -match '(?m)^  workflow_dispatch:\r?$' -and
        $workflow -match '(?m)^  schedule:\r?$') `
    -Message 'Mutation testing must support ready PR, manual, and scheduled execution.'
Assert-Condition `
    -Condition ($workflow -notmatch 'actions/upload-artifact|dashboard') `
    -Message 'Mutation testing must not upload artifacts or use the Stryker dashboard.'
Assert-Condition `
    -Condition ($workflow -notmatch 'continue-on-error') `
    -Message 'Mutation tool, build, and initial-test failures must remain visible.'
Assert-Condition `
    -Condition ($workflow -match 'github\.event\.pull_request\.draft == false') `
    -Message 'Mutation testing must not occupy runners for draft pull requests.'
Assert-Condition `
    -Condition ($workflow -match 'head\.repo\.full_name != github\.repository') `
    -Message 'External fork mutation runs must use GitHub-hosted infrastructure.'

$runtimeOnly = (
    & $classifierPath `
        -ChangedPaths @('src/NexusLabs.Needlr/TypeExtensions.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        $runtimeOnly.runtime_required -and
        -not $runtimeOnly.generators_required) `
    -Message 'Runtime changes must select only runtime mutation testing.'

$generatorsOnly = (
    & $classifierPath `
        -ChangedPaths @('src/NexusLabs.Needlr.Generators/GeneratorHelpers.cs') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition (
        -not $generatorsOnly.runtime_required -and
        $generatorsOnly.generators_required) `
    -Message 'Generator changes must select only generator mutation testing.'

$shared = (
    & $classifierPath `
        -ChangedPaths @('scripts/mutation/stryker-runtime.json') `
        -NoCiOutput |
        ConvertFrom-Json)
Assert-Condition `
    -Condition ($shared.runtime_required -and $shared.generators_required) `
    -Message 'Shared mutation tooling changes must select both scopes.'

$delivery = Get-Content -LiteralPath $deliveryPath -Raw | ConvertFrom-Json
$component = @($delivery.componentWorkflows) |
    Where-Object path -eq '.github/workflows/mutation-testing.yml' |
    Select-Object -First 1
Assert-Condition `
    -Condition ($null -ne $component) `
    -Message 'The delivery contract must declare the mutation workflow.'
Assert-Condition `
    -Condition (@($component.requiredChecks).Count -eq 0) `
    -Message 'Mutation testing must remain outside required branch checks.'

Write-Host 'Mutation testing contract validation passed.' -ForegroundColor Green
