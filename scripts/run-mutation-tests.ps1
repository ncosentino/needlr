<#
.SYNOPSIS
    Runs one or both bounded Needlr mutation-testing scopes.

.PARAMETER Scope
    The runtime, generators, or all mutation scope.

.PARAMETER OutputPath
    Report root. Defaults to artifacts/mutation under the repository.
#>
param(
    [ValidateSet('runtime', 'generators', 'all')]
    [string]$Scope = 'all',

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'artifacts' 'mutation'
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)

$definitions = @(
    [PSCustomObject]@{
        Name = 'runtime'
        WorkingDirectory = Join-Path $repoRoot 'src' 'NexusLabs.Needlr.Tests'
        ConfigPath = Join-Path $PSScriptRoot 'mutation' 'stryker-runtime.json'
    },
    [PSCustomObject]@{
        Name = 'generators'
        WorkingDirectory = Join-Path $repoRoot 'src' 'NexusLabs.Needlr.Generators.Tests'
        ConfigPath = Join-Path $PSScriptRoot 'mutation' 'stryker-generators.json'
    }
)
if ($Scope -ne 'all') {
    $definitions = @($definitions | Where-Object Name -eq $Scope)
}

Push-Location $repoRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

$summaries = foreach ($definition in $definitions) {
    $scopeOutput = Join-Path $OutputPath $definition.Name
    if (Test-Path -LiteralPath $scopeOutput) {
        throw (
            "Mutation output already exists at '$scopeOutput'. " +
            'Choose a new OutputPath or remove that scope directory explicitly.')
    }

    Write-Host "Running Stryker.NET scope '$($definition.Name)'..." -ForegroundColor Cyan
    $relativeConfigPath = [IO.Path]::GetRelativePath(
        $definition.WorkingDirectory,
        $definition.ConfigPath)
    Push-Location $definition.WorkingDirectory
    try {
        & dotnet stryker `
            --config-file $relativeConfigPath `
            --output $scopeOutput `
            --skip-version-check
        if ($LASTEXITCODE -ne 0) {
            throw "Stryker.NET scope '$($definition.Name)' failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }

    $reportDirectory = Join-Path $scopeOutput 'reports'
    $jsonPath = Join-Path $reportDirectory 'mutation-report.json'
    $markdownPath = Join-Path $reportDirectory 'mutation-report.md'
    if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf)) {
        throw "Stryker.NET scope '$($definition.Name)' did not produce $jsonPath."
    }
    if (-not (Test-Path -LiteralPath $markdownPath -PathType Leaf)) {
        throw "Stryker.NET scope '$($definition.Name)' did not produce $markdownPath."
    }

    $report = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
    $mutants = @(
        $report.files.PSObject.Properties |
            ForEach-Object { @($_.Value.mutants) })
    if ($mutants.Count -eq 0) {
        throw "Stryker.NET scope '$($definition.Name)' produced no mutants."
    }

    $counts = [ordered]@{
        Killed = 0
        Survived = 0
        NoCoverage = 0
        Timeout = 0
        CompileError = 0
        RuntimeError = 0
        Ignored = 0
    }
    foreach ($group in ($mutants | Group-Object status | Sort-Object Name)) {
        $counts[$group.Name] = $group.Count
    }

    $detected = $counts.Killed + $counts.Timeout
    $undetected = $counts.Survived + $counts.NoCoverage
    $score = if ($detected + $undetected -eq 0) {
        $null
    } else {
        [Math]::Round(($detected * 100.0) / ($detected + $undetected), 2)
    }

    $summary = [ordered]@{
        scope = $definition.Name
        totalMutants = $mutants.Count
        mutationScore = $score
        counts = $counts
        reportDirectory = $reportDirectory
    }
    $summaryPath = Join-Path $scopeOutput 'mutation-summary.json'
    [IO.File]::WriteAllText(
        $summaryPath,
        (ConvertTo-Json $summary -Depth 5) + "`n",
        [Text.UTF8Encoding]::new($false))

    Write-Host (
        "Scope '$($definition.Name)': score=$score; " +
        "killed=$($counts.Killed); survived=$($counts.Survived); " +
        "no-coverage=$($counts.NoCoverage); compile-errors=$($counts.CompileError).")

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value (
            "## Mutation testing: $($definition.Name)`n")
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value (
            "_Advisory score: `thresholds.break` is `0`; mutation score does not gate this workflow._`n")
        Get-Content -LiteralPath $markdownPath |
            Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "`n"
    }

    [PSCustomObject]$summary
}

$summaries
